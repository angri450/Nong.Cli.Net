using Microsoft.ML.OnnxRuntime.Tensors;

namespace MultiModalCore;

/// <summary>
/// Postprocessing for PP-OCRv6 ONNX inference.
/// Det: DB binary → connected components → bounding boxes.
/// Rec: CTC greedy decode.
/// </summary>
public static class OcrOnnxPostprocess
{
    /// <summary>
    /// DB detection postprocess.
    /// threshold: probability threshold for pixel classification.
    /// boxThreshold: minimum ratio of positive pixels in a box to keep it.
    /// detH, detW: size of the detection model output.
    /// Returns list of axis-aligned bounding boxes in original image coordinates.
    /// </summary>
    public static List<DetBox> DetPostprocess(
        DenseTensor<float> output,
        int detH, int detW,
        float scaleX, float scaleY,
        int padLeft, int padTop,
        int origW, int origH,
        float threshold, float boxThreshold)
    {
        // output shape [1, 1, H, W]
        var boxes = new List<DetBox>();
        int h = detH, w = detW;

        // Build binary map and collect positive pixels
        var binMask = new bool[h * w];
        int posCount = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float prob = output[0, 0, y, x];
                // Apply sigmoid
                float sig = 1.0f / (1.0f + MathF.Exp(-prob));
                if (sig > threshold)
                {
                    binMask[y * w + x] = true;
                    posCount++;
                }
            }
        }

        if (posCount == 0) return boxes;

        // Shrink binary map to separate connected components
        int shrink = Math.Max(1, (int)(MathF.Sqrt(posCount / (float)(h * w)) * 2));
        var shrunk = ShrinkBinaryMap(binMask, w, h, shrink);
        if (shrunk == null) return boxes;

        // Find connected components via BFS
        var visited = new bool[h * w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (!shrunk[idx] || visited[idx]) continue;

                // BFS to find this component
                var queue = new Queue<(int, int)>();
                queue.Enqueue((x, y));
                visited[idx] = true;
                int minX = x, maxX = x, minY = y, maxY = y;
                int count = 0;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    count++;
                    if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            int nidx = ny * w + nx;
                            if (shrunk[nidx] && !visited[nidx])
                            {
                                visited[nidx] = true;
                                queue.Enqueue((nx, ny));
                            }
                        }
                    }
                }

                // Filter tiny components (noise)
                if (count < 10) continue;

                // Box score: fraction of positive pixels in this bbox in the ORIGINAL binMask
                // (not the shrunk one)
                int boxPixels = (maxX - minX + 1) * (maxY - minY + 1);
                int boxPos = 0;
                for (int by = minY; by <= maxY; by++)
                    for (int bx = minX; bx <= maxX; bx++)
                        if (binMask[by * w + bx]) boxPos++;

                float score = (float)boxPos / boxPixels;
                if (score < 0.15f) continue;

                // Expand bbox slightly (unclip effect)
                int ux = shrink, uy = shrink;
                minX = Math.Max(0, minX - ux);
                minY = Math.Max(0, minY - uy);
                maxX = Math.Min(w - 1, maxX + ux);
                maxY = Math.Min(h - 1, maxY + uy);

                // Map from detection coordinates back to original image
                float ox1 = (minX - padLeft) * scaleX;
                float oy1 = (minY - padTop) * scaleY;
                float ox2 = (maxX - padLeft) * scaleX;
                float oy2 = (maxY - padTop) * scaleY;

                // Clamp to image bounds
                ox1 = Math.Clamp(ox1, 0, origW - 1);
                oy1 = Math.Clamp(oy1, 0, origH - 1);
                ox2 = Math.Clamp(ox2, 0, origW - 1);
                oy2 = Math.Clamp(oy2, 0, origH - 1);

                if (ox2 - ox1 < 2 || oy2 - oy1 < 2) continue;

                boxes.Add(new DetBox { X1 = ox1, Y1 = oy1, X2 = ox2, Y2 = oy2 });
            }
        }

        return boxes;
    }

    /// <summary>Shrink binary map by N pixels in each direction.</summary>
    static bool[]? ShrinkBinaryMap(bool[] mask, int w, int h, int n)
    {
        var result = new bool[w * h];
        bool any = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[y * w + x]) continue;

                bool allNeighborsPositive = true;
                for (int dy = -n; dy <= n && allNeighborsPositive; dy++)
                {
                    for (int dx = -n; dx <= n && allNeighborsPositive; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                        {
                            allNeighborsPositive = false;
                            break;
                        }
                        if (!mask[ny * w + nx]) allNeighborsPositive = false;
                    }
                }
                if (allNeighborsPositive)
                {
                    result[y * w + x] = true;
                    any = true;
                }
            }
        }
        return any ? result : null;
    }

    /// <summary>
    /// CTC greedy decode.
    /// output shape: [1, seq_len, num_classes].
    /// dict: index→character, with index 0 reserved for blank.
    /// </summary>
    public static (string text, double confidence) CtcDecode(
        DenseTensor<float> output, string[] dict)
    {
        int seqLen = output.Dimensions[1];
        int numClasses = output.Dimensions[2];

        var chars = new List<(int idx, float prob)>();
        int prevIdx = -1;
        double totalProb = 0;
        int charCount = 0;

        for (int t = 0; t < seqLen; t++)
        {
            // Find argmax
            int bestIdx = 0;
            float bestProb = output[0, t, 0];
            for (int c = 1; c < numClasses; c++)
            {
                float p = output[0, t, c];
                if (p > bestProb) { bestProb = p; bestIdx = c; }
            }

            if (bestIdx > 0 && bestIdx <= dict.Length && bestIdx != prevIdx)
            {
                chars.Add((bestIdx, bestProb));
                totalProb += bestProb;
                charCount++;
            }
            prevIdx = bestIdx;
        }

        if (chars.Count == 0)
            return ("", 0);

        var text = new System.Text.StringBuilder();
        foreach (var (idx, _) in chars)
        {
            if (idx > 0 && idx <= dict.Length)
                text.Append(dict[idx - 1]);
        }

        double avgConf = charCount > 0 ? totalProb / charCount : 0;
        return (text.ToString(), avgConf);
    }
}

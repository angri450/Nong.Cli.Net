using SkiaSharp;

namespace MultiModalCore;

/// <summary>图像内容区域类型</summary>
public enum RegionType { Background, Text, Graphic, Edge }

/// <summary>检测到的内容区域</summary>
public class ContentRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public RegionType Type { get; set; }
    public int PixelCount { get; set; }
}

/// <summary>图像结构分析结果</summary>
public class ImageLayout
{
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int SampleWidth { get; set; }
    public int SampleHeight { get; set; }
    /// <summary>像素→字符文本地图，可直接打印</summary>
    public string AsciiMap { get; set; } = "";
    /// <summary>白色空白比例</summary>
    public double WhitespaceRatio { get; set; }
    public int BlackPixelCount { get; set; }
    public int GraphicPixelCount { get; set; }
    public int EdgePixelCount { get; set; }
    /// <summary>内容区域在原图中的像素坐标</summary>
    public int ContentMinX { get; set; }
    public int ContentMinY { get; set; }
    public int ContentWidth { get; set; }
    public int ContentHeight { get; set; }
    /// <summary>连通内容区块列表</summary>
    public List<ContentRegion> Regions { get; set; } = new();
}

/// <summary>
/// 纯 .NET 图像结构分析器。
/// 加载 PNG/JPEG → 降采样 → 像素分类 → 输出 ASCII 地图和内容区域。
/// 基于 SkiaSharp（已合并到 ThirdParty），无需 Python 或额外原生 DLL。
/// </summary>
public class ImageAnalyzer
{
    private const int DefaultTargetWidth = 60;
    private const byte WhiteThreshold = 240;

    /// <summary>从文件分析</summary>
    public ImageLayout Analyze(string path, int targetWidth = DefaultTargetWidth)
    {
        using var bitmap = SKBitmap.Decode(path)
            ?? throw new FileNotFoundException($"Cannot decode image: {path}");
        return Analyze(bitmap, targetWidth);
    }

    /// <summary>从字节数组分析</summary>
    public ImageLayout Analyze(byte[] bytes, int targetWidth = DefaultTargetWidth)
    {
        using var bitmap = SKBitmap.Decode(bytes)
            ?? throw new ArgumentException("Cannot decode image from bytes");
        return Analyze(bitmap, targetWidth);
    }

    /// <summary>从 SKBitmap 分析</summary>
    public ImageLayout Analyze(SKBitmap bitmap, int targetWidth = DefaultTargetWidth)
    {
        int origW = bitmap.Width;
        int origH = bitmap.Height;
        double scale = Math.Max(1.0, (double)origW / targetWidth);
        int w = (int)(origW / scale);
        int h = (int)(origH / scale);

        var cells = new PixelClass[w, h];
        int white = 0, black = 0, graphic = 0, edge = 0;
        int minX = w, minY = h, maxX = 0, maxY = 0;

        for (int y = 0; y < h; y++)
        {
            int srcY = (int)(y * scale);
            int sh = Math.Min((int)scale, origH - srcY);
            for (int x = 0; x < w; x++)
            {
                int srcX = (int)(x * scale);
                int sw = Math.Min((int)scale, origW - srcX);

                // 采样区块内像素（隔点采样提速）
                int samples = 0;
                float rs = 0, gs = 0, bs = 0;
                for (int dy = 0; dy < sh; dy += Math.Max(1, sh / 4))
                {
                    for (int dx = 0; dx < sw; dx += Math.Max(1, sw / 4))
                    {
                        var p = bitmap.GetPixel(srcX + dx, srcY + dy);
                        rs += p.Red; gs += p.Green; bs += p.Blue;
                        samples++;
                    }
                }
                if (samples == 0) samples = 1;
                byte r = (byte)(rs / samples);
                byte g = (byte)(gs / samples);
                byte b = (byte)(bs / samples);
                float brightness = (r + g + b) / 3f;

                var cls = ClassifyPixel(r, g, b, brightness);
                cells[x, y] = cls;

                if (cls != PixelClass.White)
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }

                switch (cls)
                {
                    case PixelClass.White: white++; break;
                    case PixelClass.Black: black++; break;
                    case PixelClass.Graphic: graphic++; break;
                    case PixelClass.Edge: edge++; break;
                }
            }
        }

        // ASCII 地图
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{origW}x{origH} → {w}x{h}  white={white * 100 / Math.Max(1, w * h)}%  blk={black} gfx={graphic} edge={edge}");
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                sb.Append(ToChar(cells[x, y]));
            sb.AppendLine();
        }

        // 连通区域检测
        var regions = FindRegions(cells, w, h);

        return new ImageLayout
        {
            OriginalWidth = origW, OriginalHeight = origH,
            SampleWidth = w, SampleHeight = h,
            AsciiMap = sb.ToString(),
            WhitespaceRatio = (w * h) > 0 ? (double)white / (w * h) : 0,
            BlackPixelCount = black,
            GraphicPixelCount = graphic,
            EdgePixelCount = edge,
            ContentMinX = minX < w ? (int)(minX * scale) : 0,
            ContentMinY = minY < h ? (int)(minY * scale) : 0,
            ContentWidth = maxX >= minX ? (int)((maxX - minX + 1) * scale) : origW,
            ContentHeight = maxY >= minY ? (int)((maxY - minY + 1) * scale) : origH,
            Regions = regions,
        };
    }

    private static PixelClass ClassifyPixel(byte r, byte g, byte b, float brightness)
    {
        if (brightness > WhiteThreshold) return PixelClass.White;
        if (brightness < 40) return PixelClass.Black;
        int maxC = Math.Max(r, Math.Max(g, b));
        int minC = Math.Min(r, Math.Min(g, b));
        if (maxC - minC > 30) return PixelClass.Graphic;
        if (brightness < 100) return PixelClass.Edge;
        return PixelClass.Graphic;
    }

    private static List<ContentRegion> FindRegions(PixelClass[,] cells, int w, int h)
    {
        var regions = new List<ContentRegion>();
        var visited = new bool[w, h];
        (int dx, int dy)[] dirs = { (1,0), (-1,0), (0,1), (0,-1) };

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (visited[x, y] || cells[x, y] == PixelClass.White) continue;

                int rx = x, ry = y, rw = x, rh = y;
                int cnt = 0, blk = 0, gfx = 0, edg = 0;
                var queue = new Queue<(int, int)>();
                queue.Enqueue((x, y));
                visited[x, y] = true;

                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue(); cnt++;
                    rx = Math.Min(rx, cx); ry = Math.Min(ry, cy);
                    rw = Math.Max(rw, cx); rh = Math.Max(rh, cy);
                    switch (cells[cx, cy])
                    {
                        case PixelClass.Black: blk++; break;
                        case PixelClass.Graphic: gfx++; break;
                        case PixelClass.Edge: edg++; break;
                    }
                    foreach (var (dx, dy) in dirs)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h
                            && !visited[nx, ny] && cells[nx, ny] != PixelClass.White)
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }

                if (cnt >= 4)
                {
                    var type = gfx > blk && gfx > edg ? RegionType.Graphic
                             : edg > blk && edg > gfx ? RegionType.Edge
                             : blk > 0 ? RegionType.Text : RegionType.Graphic;
                    regions.Add(new ContentRegion
                    {
                        X = rx, Y = ry, Width = rw - rx + 1, Height = rh - ry + 1,
                        Type = type, PixelCount = cnt,
                    });
                }
            }
        }
        return regions;
    }

    private static char ToChar(PixelClass cls) => cls switch
    {
        PixelClass.White => ' ',
        PixelClass.Black => '#',
        PixelClass.Graphic => 'O',
        PixelClass.Edge => '+',
        _ => '?',
    };

    private enum PixelClass { White, Black, Graphic, Edge }
}

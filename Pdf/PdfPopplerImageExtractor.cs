using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PdfCore;

/// <summary>
/// Poppler pdfimages wrapper for embedded image extraction.
/// Replaces PdfImageExtractor (PdfPig-based).
/// </summary>
public static class PdfPopplerImageExtractor
{
    public static PdfImageExtractResult Extract(string pdfPath, string outputDir)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        Directory.CreateDirectory(outputDir);

        var toolPath = PdfNativeRuntime.ResolvePopplerTool("pdfimages")
            ?? throw new PdfProcessingException(PdfErrorKind.DependencyMissing,
                "Poppler pdfimages not found. Ensure Poppler runtime is bundled in Pdf/runtimes/<rid>/native/ or installed on PATH.");

        var fullPath = Path.GetFullPath(pdfPath);
        var result = new PdfImageExtractResult
        {
            OutputDir = Path.GetFullPath(outputDir),
        };

        // Phase 1: list images
        var imageList = RunPdfImagesList(toolPath, fullPath);

        // Phase 2: extract each image
        var index = 0;
        foreach (var img in imageList)
        {
            index++;
            var id = $"img{index:D4}";
            var outPath = Path.Combine(outputDir, $"{id}.png");

            try
            {
                ExtractSingleImage(toolPath, fullPath, img.Number, outPath);
            }
            catch
            {
                // Fallback: try raw extraction
                try
                {
                    ExtractSingleImageRaw(toolPath, fullPath, img.Number, outputDir, id, out outPath);
                }
                catch
                {
                    // Skip failed extractions
                    continue;
                }
            }

            var asset = new PdfAssetEntry
            {
                Id = id,
                Path = Path.GetRelativePath(outputDir, outPath).Replace('\\', '/'),
                Size = new FileInfo(outPath).Length,
                Page = img.Page,
                Bbox = new[] { img.Left, img.Bottom, img.Right, img.Top },
                ExtractionMethod = "pdfimages",
                ContentType = "image/png",
            };

            if (!File.Exists(outPath) || new FileInfo(outPath).Length == 0)
                asset.Warnings.Add("Extracted image is empty.");

            result.Items.Add(asset);
        }

        result.PageCount = imageList.Select(i => i.Page).DefaultIfEmpty(0).Max();
        result.ImageCount = result.Items.Count;
        return result;
    }

    // ── pdfimages -list parsing ──

    sealed record ImageEntry
    {
        public int Number;
        public int Page;
        public double Left, Bottom, Right, Top;
    }

    static List<ImageEntry> RunPdfImagesList(string toolPath, string pdfPath)
    {
        var psi = new ProcessStartInfo(toolPath)
        {
            ArgumentList = { "-list", pdfPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new PdfProcessingException(PdfErrorKind.InternalError, "Failed to start pdfimages.");
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(TimeSpan.FromSeconds(30));

        return ParsePdfImagesList(stdout);
    }

    static List<ImageEntry> ParsePdfImagesList(string output)
    {
        var entries = new List<ImageEntry>();
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);

        // pdfimages -list format (variable spacing):
        // page   num  type   width height color comp bpc  enc    interp  object ID x-ppi y-ppi size ratio
        // -------------------------------------------------------------------------------------------
        //    1     0 image    100   200  rgb     3   8  jpeg   no        12  0   150   150  1.0K 0.5%
        //
        // We need: page number, image number.
        // Bbox info is NOT in -list output; use -bbox or infer.

        foreach (var line in lines.Skip(2)) // skip header + separator
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            if (int.TryParse(parts[0], out var page) && int.TryParse(parts[1], out var num))
            {
                entries.Add(new ImageEntry
                {
                    Page = page,
                    Number = num,
                    // Bbox will be 0 — pdfimages -list doesn't provide bbox.
                    // For bbox we need pdfimages -bbox output which requires Poppler ≥ 24.0.
                });
            }
        }

        return entries;
    }

    // ── extraction ──

    static void ExtractSingleImage(string toolPath, string pdfPath, int imageNum, string outPath)
    {
        // pdfimages -f page -l page -png pdfPath prefix
        // This extracts ALL images on the page. To extract a specific image by number,
        // we use -png and rely on pdfimages naming: prefix-000.png etc.
        // For now, extract all and pick the right one.
        var dir = Path.GetDirectoryName(outPath)!;
        var tmpPrefix = Path.Combine(dir, "_tmp");

        var psi = new ProcessStartInfo(toolPath)
        {
            ArgumentList = { "-png", pdfPath, tmpPrefix },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(TimeSpan.FromSeconds(60));

        // Find the extracted file matching our image number
        var expected = $"{tmpPrefix}-{imageNum:D3}.png";
        if (File.Exists(expected))
            File.Move(expected, outPath, overwrite: true);

        // Also try with different padding
        foreach (var pad in new[] { "D2", "D4", "D5" })
        {
            var alt = $"{tmpPrefix}-{imageNum.ToString(pad)}.png";
            if (File.Exists(alt))
            {
                File.Move(alt, outPath, overwrite: true);
                break;
            }
        }

        // Clean up any remaining tmp files
        foreach (var f in Directory.GetFiles(dir, "_tmp-*"))
            File.Delete(f);
    }

    static void ExtractSingleImageRaw(string toolPath, string pdfPath, int imageNum, string dir, string id, out string outPath)
    {
        // pdfimages -all extracts all images in original format
        var tmpPrefix = Path.Combine(dir, "_raw");
        var psi = new ProcessStartInfo(toolPath)
        {
            ArgumentList = { "-all", pdfPath, tmpPrefix },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        proc.WaitForExit(TimeSpan.FromSeconds(60));

        // Find the matching file
        var candidates = Directory.GetFiles(dir, $"_raw-{imageNum:D3}.*")
            .Concat(Directory.GetFiles(dir, $"_raw-{imageNum:D4}.*"))
            .Concat(Directory.GetFiles(dir, $"_raw-{imageNum:D5}.*"))
            .ToList();

        if (candidates.Count > 0)
        {
            var src = candidates[0];
            var ext = Path.GetExtension(src);
            outPath = Path.Combine(dir, id + ext);
            File.Move(src, outPath, overwrite: true);
        }
        else
        {
            outPath = Path.Combine(dir, id + ".bin");
        }

        // Cleanup
        foreach (var f in Directory.GetFiles(dir, "_raw-*"))
            File.Delete(f);
        foreach (var f in Directory.GetFiles(dir, "_tmp-*"))
            File.Delete(f);
    }
}

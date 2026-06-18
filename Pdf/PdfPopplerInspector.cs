using System.Diagnostics;
using System.Globalization;

namespace PdfCore;

/// <summary>
/// Poppler pdfinfo + pdftotext wrapper for PDF document inspection.
/// Replaces PdfDocumentInspector (PdfPig-based).
/// </summary>
public static class PdfPopplerInspector
{
    public static PdfCheckResult Check(string pdfPath)
    {
        PdfUtilities.ValidatePdfPath(pdfPath);
        var fullPath = Path.GetFullPath(pdfPath);

        var info = RunPdfInfo(fullPath);
        var result = new PdfCheckResult
        {
            Input = Path.GetFileName(pdfPath),
            FullPath = fullPath,
            FileSize = new FileInfo(fullPath).Length,
            Sha256 = PdfUtilities.Sha256(fullPath),
            PageCount = info.PageCount,
        };

        var textChars = 0;
        var imageCount = 0;

        for (var p = 1; p <= info.PageCount; p++)
        {
            var pageInfo = info.Pages.FirstOrDefault(pi => pi.Number == p);
            var w = pageInfo?.Width ?? 612;
            var h = pageInfo?.Height ?? 792;

            // Text char count: use pdftotext -f p -l p to get text for this page
            var pageText = RunPdftotextPageText(fullPath, p);
            var charCount = CountMeaningfulChars(pageText);

            var pageCheck = new PdfPageCheck
            {
                Page = p,
                Width = w,
                Height = h,
                TextCharCount = charCount,
                ImageCount = 0, // pdfimages -list can fill this if needed
                ImageCoverageRatio = 0,
                SuspiciousTextRatio = 0,
            };
            result.Pages.Add(pageCheck);
            textChars += charCount;
        }

        result.TextCharCount = textChars;
        result.TextCharsPerPage = info.PageCount == 0 ? 0 : (double)textChars / info.PageCount;
        result.ImageCount = imageCount;
        result.HasTextLayer = textChars > 0;

        Classify(result);
        return result;
    }

    // ── pdfinfo parse ──

    sealed record PdfInfoResult
    {
        public int PageCount;
        public readonly List<PdfPageSize> Pages = new();
    }

    sealed record PdfPageSize
    {
        public int Number;
        public double Width;
        public double Height;
    }

    static PdfInfoResult RunPdfInfo(string pdfPath)
    {
        var infoPath = PdfNativeRuntime.ResolvePopplerTool("pdfinfo")
            ?? throw new PdfProcessingException(PdfErrorKind.DependencyMissing,
                "Poppler pdfinfo not found. Ensure Poppler runtime is bundled in Pdf/runtimes/<rid>/native/ or installed on PATH.");

        var psi = new ProcessStartInfo(infoPath)
        {
            ArgumentList = { pdfPath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new PdfProcessingException(PdfErrorKind.InternalError, "Failed to start pdfinfo.");
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(TimeSpan.FromSeconds(30));

        if (proc.ExitCode != 0)
            throw new PdfProcessingException(PdfErrorKind.ReadFailed, $"pdfinfo exited with code {proc.ExitCode}.");

        return ParsePdfInfo(stdout);
    }

    static PdfInfoResult ParsePdfInfo(string output)
    {
        var result = new PdfInfoResult();
        double? pageW = null, pageH = null;

        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("Pages:", StringComparison.OrdinalIgnoreCase))
            {
                var val = line.Split(':', 2)[1].Trim();
                if (int.TryParse(val, out var pages))
                    result.PageCount = pages;
            }
            else if (line.StartsWith("Page size:", StringComparison.OrdinalIgnoreCase))
            {
                // "Page size: 612 x 792 pts (letter)"
                var val = line.Split(':', 2)[1].Trim();
                var xIdx = val.IndexOf('x', StringComparison.OrdinalIgnoreCase);
                if (xIdx > 0)
                {
                    var wPart = val[..xIdx].Trim();
                    var hPart = val[xIdx..].TrimStart('x').Trim();
                    // hPart may contain " pts (letter)" — extract numbers
                    hPart = new string(hPart.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                    if (double.TryParse(wPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                        && double.TryParse(hPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                    {
                        pageW = w;
                        pageH = h;
                    }
                }
            }
        }

        // If we got a single page size from pdfinfo, apply to all pages.
        if (pageW.HasValue && pageH.HasValue && result.PageCount > 0)
        {
            for (var i = 1; i <= result.PageCount; i++)
                result.Pages.Add(new PdfPageSize { Number = i, Width = pageW.Value, Height = pageH.Value });
        }

        return result;
    }

    // ── pdftotext page text ──

    static string RunPdftotextPageText(string pdfPath, int pageNumber)
    {
        var toolPath = PdfNativeRuntime.ResolvePopplerTool("pdftotext")
            ?? throw new PdfProcessingException(PdfErrorKind.DependencyMissing, "Poppler pdftotext not found.");

        var psi = new ProcessStartInfo(toolPath)
        {
            ArgumentList = { "-f", pageNumber.ToString(), "-l", pageNumber.ToString(), "-enc", "UTF-8", pdfPath, "-" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(TimeSpan.FromSeconds(30));

        return stdout;
    }

    internal static int CountMeaningfulChars(string? text) =>
        string.IsNullOrEmpty(text)
            ? 0
            : text.Count(c => !char.IsWhiteSpace(c) && !char.IsControl(c));

    // ── classification ──

    static void Classify(PdfCheckResult result)
    {
        if (result.PageCount == 0)
        {
            result.Classification = "unknown";
            result.RecommendedMode = "auto";
            result.Warnings.Add("PDF has 0 pages or could not be parsed.");
            return;
        }

        var textPerPage = result.TextCharsPerPage;
        var pageCount = result.PageCount;

        if (textPerPage >= 60)
        {
            result.Classification = "text";
            result.RecommendedMode = "text";
        }
        else if (textPerPage >= 15)
        {
            result.Classification = "hybrid";
            result.RecommendedMode = "hybrid";
        }
        else
        {
            result.Classification = "scan";
            result.RecommendedMode = "ocr";
            result.RenderRequired = true;
        }

        if (textPerPage < 10 && pageCount > 0)
            result.Warnings.Add("PDF has very little extractable text. Use --mode ocr for OCR-based extraction.");

        // Bug 16: when text extraction returns 0 but PDF has substantial size,
        // likely CID fonts without ToUnicode CMap are embedded. Signal this.
        if (textPerPage < 1 && pageCount > 0 && result.FileSize > 10240)
        {
            result.Warnings.Add(
                "PDF text extraction returned 0 characters despite having content. " +
                "This PDF likely uses CID-keyed fonts without a ToUnicode CMap, " +
                "which prevents local pdftotext from extracting readable text. " +
                "Cloud OCR (or pypdfium2) can still extract the text. " +
                "Recommended route: use --mode ocr with cloud OCR, or 'nong ocr cloud'.");
        }
    }
}

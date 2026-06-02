using ShapeCrawler;

namespace PptxCore;

public static class SlideValidator
{
    public static List<string> Validate(string path)
    {
        var issues = new List<string>();

        if (!File.Exists(path))
        {
            issues.Add($"File not found: {path}");
            return issues;
        }

        var info = new FileInfo(path);
        if (info.Length < 1024)
            issues.Add($"File too small: {info.Length} bytes (suspicious, may be corrupted)");

        try
        {
            using var pres = new Presentation(path);

            if (pres.Slides.Count == 0)
                issues.Add("Presentation has 0 slides");

            if (pres.SlideWidth <= 0 || pres.SlideHeight <= 0)
                issues.Add("Invalid slide dimensions");

            for (int i = 0; i < pres.Slides.Count; i++)
            {
                var slide = pres.Slides[i];
                int n = i + 1;

                bool hasContent = false;
                foreach (var shape in slide.Shapes)
                {
                    if (!string.IsNullOrWhiteSpace(shape.TextBox?.Text)) hasContent = true;
                    if (shape.Table != null) hasContent = true;
                    if (shape.PieChart != null || shape.BarChart != null || shape.ColumnChart != null) hasContent = true;
                }

                if (!hasContent)
                    issues.Add($"Slide {n}: no visible content");
            }
        }
        catch (Exception ex)
        {
            issues.Add($"Failed to open as valid PPTX: {ex.Message}");
        }

        return issues;
    }

    public static bool ValidateAndReport(string path)
    {
        var issues = Validate(path);
        if (issues.Count == 0)
        {
            Console.WriteLine($"PASS: {path}");
            return true;
        }

        Console.WriteLine($"FAIL: {path} — {issues.Count} issue(s):");
        foreach (var issue in issues)
            Console.WriteLine($"  - {issue}");
        return false;
    }
}

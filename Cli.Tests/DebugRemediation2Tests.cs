using System.Text.Json;
using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

/// <summary>
/// Tests for v5.0.2 debug remediation 2 — 6 issues from english coursework report.
/// Tests are numbered by debug report 2 issue ID (#1–#7).
/// </summary>
public class DebugRemediation2Tests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, string stderr, int exitCode) Run(params string[] args) =>
        CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null, args);

    // =========================================================================
    // #1 — word merge preserves OOXML validity per ECMA-376 element order
    // =========================================================================

    [Fact]
    public void WordMerge_OutputValidates_NoUnexpectedChildError()
    {
        var coverPath = CreateSimpleDocx("Cover Title", "Cover Subtitle");
        var bodyPath = CreateSimpleDocx("Introduction", "Body paragraph text goes here.");
        var mergedPath = Path.Combine(Path.GetTempPath(), "merged-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            // Merge via direct API
            var result = DocxAnalysis.MergeDocx(new[] { coverPath, bodyPath }, mergedPath);
            Assert.Equal(2, result.SourceFiles);

            // Validate merged document with OpenXML SDK validator
            using var doc = WordprocessingDocument.Open(mergedPath, false);
            var validator = new OpenXmlValidator();
            var errors = validator.Validate(doc).ToList();

            // No unexpected child errors
            Assert.DoesNotContain(errors, e => e.Description.Contains("unexpected child", StringComparison.OrdinalIgnoreCase));
            // No errors about element order
            Assert.DoesNotContain(errors, e => e.Description.Contains("misplaced", StringComparison.OrdinalIgnoreCase)
                || e.Description.Contains("order", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(coverPath); } catch { }
            try { File.Delete(bodyPath); } catch { }
            try { File.Delete(mergedPath); } catch { }
        }
    }

    [Fact]
    public void WordMerge_SectPrRemainsLastChild()
    {
        var coverPath = CreateSimpleDocx("Title Page");
        var bodyPath = CreateSimpleDocx("Chapter 1");
        var mergedPath = Path.Combine(Path.GetTempPath(), "merged-sectpr-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            DocxAnalysis.MergeDocx(new[] { coverPath, bodyPath }, mergedPath);

            using var doc = WordprocessingDocument.Open(mergedPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var children = body.Elements().ToList();

            // The last child must be a SectionProperties (ECMA-376 requirement)
            Assert.NotEmpty(children);
            Assert.IsType<SectionProperties>(children[children.Count - 1]);

            // No element after the last SectionProperties
            var lastSectPrIndex = children.FindLastIndex(e => e is SectionProperties);
            Assert.Equal(children.Count - 1, lastSectPrIndex);
        }
        finally
        {
            try { File.Delete(coverPath); } catch { }
            try { File.Delete(bodyPath); } catch { }
            try { File.Delete(mergedPath); } catch { }
        }
    }

    [Fact]
    public void WordMerge_ThreeWayMerge_Validates()
    {
        var paths = new[]
        {
            CreateSimpleDocx("Cover", "University Name"),
            CreateSimpleDocx("Abstract", "This paper discusses..."),
            CreateSimpleDocx("Introduction", "Background and motivation..."),
        };
        var mergedPath = Path.Combine(Path.GetTempPath(), "merged-3way-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            var result = DocxAnalysis.MergeDocx(paths, mergedPath);
            Assert.Equal(3, result.SourceFiles);

            using var doc = WordprocessingDocument.Open(mergedPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var children = body.Elements().ToList();

            // Should have at least 3 paragraphs + section breaks + sectPr
            Assert.True(children.Count >= 5);
            // Last child must be SectionProperties
            Assert.IsType<SectionProperties>(children[children.Count - 1]);
        }
        finally
        {
            foreach (var p in paths) try { File.Delete(p); } catch { }
            try { File.Delete(mergedPath); } catch { }
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    static string CreateSimpleDocx(params string[] paragraphTexts)
    {
        var path = Path.Combine(Path.GetTempPath(), "test-docx-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart();

        var body = new Body();
        foreach (var text in paragraphTexts)
        {
            body.Append(new Paragraph(new Run(new Text(text))));
        }

        // Add a dummy sectPr (required for valid docx)
        body.Append(new SectionProperties(new PageSize { Width = 11906, Height = 16838 }));

        doc.MainDocumentPart!.Document = new Document(body);
        return path;
    }
}

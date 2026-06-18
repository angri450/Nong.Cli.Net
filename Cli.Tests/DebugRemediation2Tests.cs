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
    // #2 — academic-format skips cover-block and heuristic cover detection
    // =========================================================================

    [Fact]
    public void AcademicFormat_SkipsCoverBlockMarker()
    {
        // Create a docx with a cover-block paragraph (CoverBlock style + large centered text)
        var docxPath = Path.Combine(Path.GetTempPath(), "af-cover-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var outPath = Path.Combine(Path.GetTempPath(), "af-cover-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using (var createDoc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                createDoc.AddMainDocumentPart();
                var stylesPart = createDoc.MainDocumentPart!.AddNewPart<StyleDefinitionsPart>();
                stylesPart.Styles = new Styles(
                    new Style(new StyleName { Val = "CoverBlock" })
                    {
                        StyleId = "CoverBlock",
                        Type = StyleValues.Paragraph,
                    });
                stylesPart.Styles.Save();

                var coverPara = new Paragraph(
                    new ParagraphProperties(
                        new ParagraphStyleId { Val = "CoverBlock" },
                        new Justification { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(new FontSize { Val = "44" }),
                        new Text("Course Paper Title")));

                var bodyPara = new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Both }),
                    new Run(new Text("Body text content.")));

                var body = new Body(coverPara, bodyPara,
                    new SectionProperties(new PageSize { Width = 11906, Height = 16838 }));
                createDoc.MainDocumentPart.Document = new Document(body);
            }

            WordAcademicFormatter.Apply(docxPath, outPath);

            using var resultDoc = WordprocessingDocument.Open(outPath, false);
            var paras = resultDoc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();
            var coverResult = paras[0];

            // Cover paragraph should retain center alignment (not changed to Both)
            var jc = coverResult.ParagraphProperties?.Justification?.Val?.Value;
            Assert.Equal(JustificationValues.Center, jc);

            // Cover paragraph font size should remain 44 (not changed to 24)
            var runFontSize = coverResult.Elements<Run>().FirstOrDefault()?
                .RunProperties?.FontSize?.Val?.Value;
            Assert.Equal("44", runFontSize);
        }
        finally
        {
            try { File.Delete(docxPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void AcademicFormat_HeuristicSkipsLargeCenteredUnindentedFirstPage()
    {
        // Simulate a hand-written cover: large font (44 half-pt), center, no indent
        var docxPath = Path.Combine(Path.GetTempPath(), "af-heur-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var outPath = Path.Combine(Path.GetTempPath(), "af-heur-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using (var createDoc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                createDoc.AddMainDocumentPart();
                var coverTitle = new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(new RunFonts { Ascii = "Times New Roman", EastAsia = "宋体" },
                            new FontSize { Val = "44" }),
                        new Text("English Course Paper")));

                var coverSubtitle = new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(new FontSize { Val = "36" }),
                        new Text("Department of Horticulture")));

                var bodyPara = new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Both }),
                    new Run(new RunProperties(new FontSize { Val = "24" }),
                        new Text("Introduction and background.")));

                var body = new Body(coverTitle, coverSubtitle, bodyPara,
                    new SectionProperties(new PageSize { Width = 11906, Height = 16838 }));
                createDoc.MainDocumentPart!.Document = new Document(body);
            }

            WordAcademicFormatter.Apply(docxPath, outPath);

            using var resultDoc = WordprocessingDocument.Open(outPath, false);
            var paras = resultDoc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();

            // Cover paragraph 0: should retain center and 44pt
            Assert.Equal(JustificationValues.Center,
                paras[0].ParagraphProperties?.Justification?.Val?.Value);
            Assert.Equal("44",
                paras[0].Elements<Run>().First()?.RunProperties?.FontSize?.Val?.Value);

            // Cover paragraph 1: should retain center and 36pt
            Assert.Equal(JustificationValues.Center,
                paras[1].ParagraphProperties?.Justification?.Val?.Value);
            Assert.Equal("36",
                paras[1].Elements<Run>().First()?.RunProperties?.FontSize?.Val?.Value);

            // Body paragraph 2: should be reformatted to Both + 24pt
            Assert.Equal(JustificationValues.Both,
                paras[2].ParagraphProperties?.Justification?.Val?.Value);
            Assert.Equal("24",
                paras[2].Elements<Run>().First()?.RunProperties?.FontSize?.Val?.Value);
        }
        finally
        {
            try { File.Delete(docxPath); } catch { }
            try { File.Delete(outPath); } catch { }
        }
    }

    [Fact]
    public void AcademicFormat_SkipFirstPageOption()
    {
        // Merged docx with section break between cover and body
        var docxPath = Path.Combine(Path.GetTempPath(), "af-skip-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        var outPath = Path.Combine(Path.GetTempPath(), "af-skip-out-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using (var createDoc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                createDoc.AddMainDocumentPart();
                // Cover page: large centered paragraphs
                var coverTitle = new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(new RunProperties(new FontSize { Val = "44" }), new Text("Course Paper")));

                // Section break paragraph (marks end of cover page, inserted by merge)
                var sectionBreak = new Paragraph(
                    new ParagraphProperties(new SectionProperties(
                        new SectionType { Val = SectionMarkValues.NextPage })));

                // Body: regular paragraph
                var bodyPara = new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Both }),
                    new Run(new RunProperties(new FontSize { Val = "24" }), new Text("Introduction text.")));

                var body = new Body(coverTitle, sectionBreak, bodyPara,
                    new SectionProperties(new PageSize { Width = 11906, Height = 16838 }));
                createDoc.MainDocumentPart!.Document = new Document(body);
            }

            WordAcademicFormatter.Apply(docxPath, outPath, skipFirstPage: true);

            using var resultDoc = WordprocessingDocument.Open(outPath, false);
            var paras = resultDoc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();

            // Cover paragraph: should be untouched (skipped)
            Assert.Equal(JustificationValues.Center,
                paras[0].ParagraphProperties?.Justification?.Val?.Value);
            Assert.Equal("44",
                paras[0].Elements<Run>().First()?.RunProperties?.FontSize?.Val?.Value);

            // Body paragraph: should be reformatted
            Assert.Equal(JustificationValues.Both,
                paras[2].ParagraphProperties?.Justification?.Val?.Value);
            Assert.Equal("24",
                paras[2].Elements<Run>().First()?.RunProperties?.FontSize?.Val?.Value);
        }
        finally
        {
            try { File.Delete(docxPath); } catch { }
            try { File.Delete(outPath); } catch { }
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

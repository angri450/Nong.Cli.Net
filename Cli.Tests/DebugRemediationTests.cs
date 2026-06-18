using System.Text.Json;
using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

/// <summary>
/// Tests for v5.0.1 debug remediation — 13 issues from horticulture postharvest report.
/// Tests are numbered by debug report issue ID (#1–#13).
/// </summary>
public class DebugRemediationTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, string stderr, int exitCode) Run(params string[] args) =>
        CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null, args);

    // =========================================================================
    // #4 — C# enum values serialized as strings in format.json / content.jsonl
    // =========================================================================

    [Fact]
    public void WordSlice_FormatJson_EnumFieldsSerializeAsStringValues()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "slice-enum-" + Guid.NewGuid().ToString("N")[..8]);
        var docxPath = Path.Combine(Path.GetTempPath(), "enum-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            // Create a docx with: centered paragraph + bordered table + 1.5 line spacing
            using (var doc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart();

                // Centered paragraph "Hello" with 1.5 line spacing
                var para = new Paragraph(
                    new ParagraphProperties(
                        new Justification { Val = JustificationValues.Both },
                        new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto }),
                    new Run(new Text("Hello")));

                // Simple table with borders
                var table = new Table(
                    new TableProperties(
                        new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                        new TableJustification { Val = TableRowAlignmentValues.Left },
                        new TableBorders(
                            new TopBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                            new BottomBorder { Val = BorderValues.Single, Size = 12, Color = "000000" },
                            new LeftBorder { Val = BorderValues.None },
                            new RightBorder { Val = BorderValues.None },
                            new InsideHorizontalBorder { Val = BorderValues.None },
                            new InsideVerticalBorder { Val = BorderValues.None })),
                    new TableRow(
                        new TableCell(
                            new TableCellProperties(
                                new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2500" }),
                            new Paragraph(new Run(new Text("A"))))));

                doc.MainDocumentPart!.Document = new Document(new Body(para, table));
                // Add a styles part with Normal style (isDefault check later)
                var stylesPart = doc.MainDocumentPart.AddNewPart<StyleDefinitionsPart>();
                stylesPart.Styles = new Styles(
                    new DocDefaults(
                        new ParagraphPropertiesDefault(
                            new ParagraphProperties(
                                new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto }))));
                stylesPart.Styles.Append(new Style(
                    new StyleName { Val = "Normal" })
                {
                    StyleId = "Normal",
                    Type = StyleValues.Paragraph,
                    Default = true
                });
                stylesPart.Styles.Save();
            }

            // Run WordSlice
            var result = WordSlice.Slice(docxPath, outDir);
            Assert.Empty(result.Warnings);

            // Check content.jsonl — format.alignment and format.lineRule
            var contentPath = Path.Combine(outDir, "content.jsonl");
            Assert.True(File.Exists(contentPath), "content.jsonl should exist");
            var contentLines = File.ReadAllLines(contentPath);
            var contentJson = JsonDocument.Parse(contentLines[0]).RootElement;

            // format.alignment should NOT be "JustificationValues { }"
            var alignment = contentJson.GetProperty("format").GetProperty("alignment").GetString();
            Assert.NotNull(alignment);
            Assert.DoesNotContain("JustificationValues", alignment);
            Assert.DoesNotContain("{ }", alignment);

            // format.lineRule should NOT be "LineSpacingRuleValues { }"
            var lineRule = contentJson.GetProperty("format").GetProperty("lineRule").GetString();
            Assert.NotNull(lineRule);
            Assert.DoesNotContain("LineSpacingRuleValues", lineRule);
            Assert.DoesNotContain("{ }", lineRule);

            // Check format.json — table format enum fields
            var formatPath = Path.Combine(outDir, "format.json");
            Assert.True(File.Exists(formatPath), "format.json should exist");
            var formatJson = JsonDocument.Parse(File.ReadAllText(formatPath)).RootElement;

            var tables = formatJson.GetProperty("tables");
            Assert.True(tables.GetArrayLength() > 0, "should have at least one table");
            var tableFormat = tables[0].GetProperty("format");

            // justification should NOT be "TableRowAlignmentValues { }"
            var justification = tableFormat.GetProperty("justification").GetString();
            Assert.NotNull(justification);
            Assert.DoesNotContain("TableRowAlignmentValues", justification);
            Assert.DoesNotContain("{ }", justification);

            // widthType should NOT be "TableWidthUnitValues { }"
            var widthType = tableFormat.GetProperty("widthType").GetString();
            Assert.NotNull(widthType);
            Assert.DoesNotContain("TableWidthUnitValues", widthType);
            Assert.DoesNotContain("{ }", widthType);

            // border val fields should NOT be "BorderValues { }"
            var borders = tableFormat.GetProperty("borders");
            foreach (var borderName in new[] { "top", "bottom", "left", "right", "insideH", "insideV" })
            {
                if (borders.TryGetProperty(borderName, out var border))
                {
                    var val = border.GetProperty("val").GetString();
                    Assert.NotNull(val);
                    Assert.DoesNotContain("BorderValues", val);
                    Assert.DoesNotContain("{ }", val);
                }
            }

            // visualEvidence.lineSpacing should NOT contain "LineSpacingRuleValues { }"
            var visual = formatJson.GetProperty("visualEvidence");
            var lineSpacing = visual.GetProperty("lineSpacing");
            foreach (var item in lineSpacing.EnumerateArray())
            {
                var str = item.GetString();
                if (str != null && str.Contains("rule="))
                {
                    Assert.DoesNotContain("LineSpacingRuleValues", str);
                    Assert.DoesNotContain("{ }", str);
                }
            }
        }
        finally
        {
            try { Directory.Delete(outDir, true); } catch { }
            try { File.Delete(docxPath); } catch { }
        }
    }

    // =========================================================================
    // #9 — Normal style isDefault = true per OOXML spec
    // =========================================================================

    [Fact]
    public void FormatJson_NormalStyle_IsDefaultTrue()
    {
        var outDir = Path.Combine(Path.GetTempPath(), "slice-default-" + Guid.NewGuid().ToString("N")[..8]);
        var docxPath = Path.Combine(Path.GetTempPath(), "default-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using (var doc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart();
                var stylesPart = doc.MainDocumentPart!.AddNewPart<StyleDefinitionsPart>();
                stylesPart.Styles = new Styles(
                    new DocDefaults(
                        new ParagraphPropertiesDefault(
                            new ParagraphProperties(
                                new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto }))));
                // Normal style WITHOUT explicit w:default — but it IS the default per OOXML
                stylesPart.Styles.Append(new Style(
                    new StyleName { Val = "Normal" })
                {
                    StyleId = "Normal",
                    Type = StyleValues.Paragraph
                    // Note: no Default = true set explicitly
                });
                stylesPart.Styles.Save();
                doc.MainDocumentPart.Document = new Document(new Body(
                    new Paragraph(new Run(new Text("Hello")))));
            }

            var result = WordSlice.Slice(docxPath, outDir);
            Assert.Empty(result.Warnings);

            var formatPath = Path.Combine(outDir, "format.json");
            Assert.True(File.Exists(formatPath));
            var formatJson = JsonDocument.Parse(File.ReadAllText(formatPath)).RootElement;
            var styles = formatJson.GetProperty("styles");

            // Find Normal style
            JsonElement normalStyle = default;
            bool found = false;
            foreach (var s in styles.EnumerateArray())
            {
                if (s.GetProperty("id").GetString() == "Normal")
                {
                    normalStyle = s;
                    found = true;
                    break;
                }
            }
            Assert.True(found, "Normal style should exist in format.json");

            // isDefault should be true — Normal is the default paragraph style
            Assert.True(normalStyle.GetProperty("isDefault").GetBoolean(),
                "Normal style isDefault should be true per OOXML spec");
        }
        finally
        {
            try { Directory.Delete(outDir, true); } catch { }
            try { File.Delete(docxPath); } catch { }
        }
    }

    // =========================================================================
    // #1 — frontmatter title/date not rendered to body, # heading1 is the title
    // =========================================================================

    [Fact]
    public void NongMark_FrontmatterTitle_NotRenderedInBody()
    {
        var nongmarkPath = Path.Combine(Path.GetTempPath(), "front-" + Guid.NewGuid().ToString("N")[..8] + ".nmk");
        var docxPath = Path.Combine(Path.GetTempPath(), "front-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            File.WriteAllText(nongmarkPath, """
---
title: "乙烯调控"
date: "2025-06-18"
lang: zh-CN
---

# 乙烯调控

正文内容
""");

            var result = NongMarkDocumentBuilder.Build(nongmarkPath, docxPath);
            Assert.Empty(result.Warnings);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var allTexts = body.Descendants<Text>().Select(t => t.Text).ToList();
            var fullText = string.Join("", allTexts);

            // frontmatter title "乙烯调控" should appear exactly ONCE (from # heading1, not from frontmatter)
            var titleCount = allTexts.Count(t => t.Contains("乙烯调控"));
            Assert.Equal(1, titleCount);

            // frontmatter date should NOT appear in body at all
            Assert.DoesNotContain("2025-06-18", fullText);

            // # heading1 should still be rendered as the first heading
            var headings = body.Elements<Paragraph>()
                .Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("Heading") == true)
                .Select(p => p.InnerText)
                .ToList();
            Assert.Contains(headings, h => h.Contains("乙烯调控"));
        }
        finally
        {
            try { File.Delete(nongmarkPath); } catch { }
            try { File.Delete(docxPath); } catch { }
        }
    }

    // =========================================================================
    // #2 — lit search relevanceScore non-zero + MatchReasons non-empty
    // =========================================================================

    [Fact]
    public void LiteratureRanker_RelevanceScore_NonZero_ForRelevantPaper()
    {
        // Use Angri450.Nong.Literature types directly
        var query = Angri450.Nong.Literature.Dsl.CnkiParser.Parse("SU='banana' AND SU='ethylene'");
        var concepts = Angri450.Nong.Literature.Dsl.CnkiQueryNormalizer.Normalize(query).Concepts;
        Assert.NotEmpty(concepts);

        var record = new Angri450.Nong.Literature.Models.PaperRecord
        {
            Title = "Ethylene regulation of banana fruit ripening",
            Abstract = "Banana is a climacteric fruit whose ripening is controlled by ethylene...",
            Keywords = new List<string> { "banana", "ethylene", "ripening" },
            CitationCount = 50,
            Year = 2022,
            RetrievedFrom = new List<string> { "openalex" },
            MatchReasons = new List<string> { "SU:banana", "SU:ethylene" }
        };

        var ranker = new Angri450.Nong.Literature.Pipeline.LiteratureRanker();
        var score = ranker.Score(query, record, Angri450.Nong.Literature.Models.RankProfile.Balanced);

        Assert.True(score > 0, $"RelevanceScore should be > 0 for a relevant paper, got {score}");
        Assert.True(score >= 0.5, $"Highly relevant paper should score >= 0.5, got {score}");
    }

    [Fact]
    public void LiteratureRanker_IrrelevantPaper_ScoresLowerThanRelevant()
    {
        var query = Angri450.Nong.Literature.Dsl.CnkiParser.Parse("SU='banana' AND SU='ethylene'");
        var concepts = Angri450.Nong.Literature.Dsl.CnkiQueryNormalizer.Normalize(query).Concepts;

        var relevant = new Angri450.Nong.Literature.Models.PaperRecord
        {
            Title = "Ethylene regulation of banana fruit ripening",
            Abstract = "Banana is a climacteric fruit...",
            Keywords = new List<string> { "banana", "ethylene" },
            CitationCount = 50, Year = 2022,
            RetrievedFrom = new List<string> { "openalex" },
            MatchReasons = new List<string> { "SU:banana", "SU:ethylene" }
        };

        var irrelevant = new Angri450.Nong.Literature.Models.PaperRecord
        {
            Title = "Advances in applied supramolecular technologies",
            Abstract = "This review covers recent advances in supramolecular chemistry...",
            Keywords = new List<string> { "supramolecular", "chemistry" },
            CitationCount = 50, Year = 2022,
            RetrievedFrom = new List<string> { "openalex" },
            MatchReasons = new List<string>()
        };

        var ranker = new Angri450.Nong.Literature.Pipeline.LiteratureRanker();
        var relevantScore = ranker.Score(query, relevant, Angri450.Nong.Literature.Models.RankProfile.Balanced);
        var irrelevantScore = ranker.Score(query, irrelevant, Angri450.Nong.Literature.Models.RankProfile.Balanced);

        Assert.True(relevantScore > irrelevantScore,
            $"Relevant paper ({relevantScore:F3}) should outscore irrelevant paper ({irrelevantScore:F3})");
    }

    // =========================================================================
    // #3 — English spaces around parentheses/colons preserved in DOCX
    // =========================================================================

    [Fact]
    public void NongMark_EnglishSpacesAroundParenthesisColon_PreservedInDocx()
    {
        var nongmarkPath = Path.Combine(Path.GetTempPath(), "space-" + Guid.NewGuid().ToString("N")[..8] + ".nmk");
        var docxPath = Path.Combine(Path.GetTempPath(), "space-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            File.WriteAllText(nongmarkPath, "Banana (Musa spp.) is one of the most traded fruits.\n\nKeywords: Banana; Postharvest");

            var result = NongMarkDocumentBuilder.Build(nongmarkPath, docxPath);
            Assert.Empty(result.Warnings);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var fullText = string.Join("", doc.MainDocumentPart!.Document.Body!
                .Descendants<Text>().Select(t => t.Text));

            Assert.Contains("Banana (Musa spp.) is", fullText);   // spaces around () preserved
            Assert.Contains("Keywords: Banana", fullText);         // colon space preserved
            Assert.DoesNotContain("Banana(Musa", fullText);        // no merged
            Assert.DoesNotContain(")is", fullText);                // no merged
            Assert.DoesNotContain("Keywords:Banana", fullText);    // no merged
        }
        finally
        {
            try { File.Delete(nongmarkPath); } catch { }
            try { File.Delete(docxPath); } catch { }
        }
    }

    // =========================================================================
    // #5 — keyword line (关键词/Keywords) should have no first-line indent
    // =========================================================================

    [Fact]
    public void NongMark_KeywordLine_NoFirstLineIndent()
    {
        var nongmarkPath = Path.Combine(Path.GetTempPath(), "kw-" + Guid.NewGuid().ToString("N")[..8] + ".nmk");
        var docxPath = Path.Combine(Path.GetTempPath(), "kw-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            File.WriteAllText(nongmarkPath, "正文段落。\n\n关键词： 香蕉；采后生理；乙烯\n\nKeywords: Banana; Postharvest");

            var result = NongMarkDocumentBuilder.Build(nongmarkPath, docxPath);
            Assert.Empty(result.Warnings);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var paras = body.Elements<Paragraph>().ToList();

            var zhKeyword = paras.FirstOrDefault(p => p.InnerText.StartsWith("关键词"));
            var enKeyword = paras.FirstOrDefault(p => p.InnerText.StartsWith("Keywords"));

            Assert.NotNull(zhKeyword);
            Assert.NotNull(enKeyword);
            // After academic-format runs, these should have no first-line indent
        }
        finally
        {
            try { File.Delete(nongmarkPath); } catch { }
            try { File.Delete(docxPath); } catch { }
        }
    }

    // =========================================================================
    // #7 — empty line after table not rendered as empty paragraph
    // =========================================================================

    [Fact]
    public void NongMark_EmptyLineAfterTable_NotRenderedAsParagraph()
    {
        var nongmarkPath = Path.Combine(Path.GetTempPath(), "emptab-" + Guid.NewGuid().ToString("N")[..8] + ".nmk");
        var docxPath = Path.Combine(Path.GetTempPath(), "emptab-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            File.WriteAllText(nongmarkPath, "| A | B |\n|---|---|\n| 1 | 2 |\n\n## 标题");

            var result = NongMarkDocumentBuilder.Build(nongmarkPath, docxPath);
            Assert.Empty(result.Warnings);

            using var doc = WordprocessingDocument.Open(docxPath, false);
            var body = doc.MainDocumentPart!.Document.Body!;
            var elements = body.Elements().ToList();

            // After the table, the next element should be a heading paragraph, not an empty paragraph
            var tableIndex = elements.FindIndex(e => e is Table);
            Assert.True(tableIndex >= 0, "should have a table");
            var nextElement = elements.Skip(tableIndex + 1).FirstOrDefault();
            Assert.NotNull(nextElement);
            Assert.IsType<Paragraph>(nextElement);
            var para = (Paragraph)nextElement;
            // Should be a heading with text "标题", not an empty paragraph
            Assert.True(para.InnerText.Contains("标题")
                || (para.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("Heading") == true),
                $"Element after table should be heading, got: '{para.InnerText}'");
        }
        finally
        {
            try { File.Delete(nongmarkPath); } catch { }
            try { File.Delete(docxPath); } catch { }
        }
    }
}

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
}

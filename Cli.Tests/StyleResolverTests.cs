using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class StyleResolverTests
{
    [Fact]
    public void StyleResolver_ResolveParagraph_MergesDefaultsStyleDirect()
    {
        var path = Path.Combine(Path.GetTempPath(), "style-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            doc.AddMainDocumentPart();

            // Set up docDefaults with line spacing 1.5
            var stylesPart = doc.MainDocumentPart!.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new DocDefaults(
                    new ParagraphPropertiesDefault(
                        new ParagraphProperties(
                            new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto })))); // 360/240 = 1.5

            // Add a paragraph style with line spacing 2.0
            stylesPart.Styles.Append(new Style(
                new StyleName { Val = "TestStyle" },
                new StyleParagraphProperties(
                    new SpacingBetweenLines { Line = "480", LineRule = LineSpacingRuleValues.Auto })) // 480/240 = 2.0
            {
                StyleId = "TestStyle",
                Type = StyleValues.Paragraph
            });
            stylesPart.Styles.Save();

            // Create paragraph with direct line spacing 1.0 and style TestStyle
            var para = new Paragraph(
                new ParagraphProperties(
                    new ParagraphStyleId { Val = "TestStyle" },
                    new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto }), // 240/240 = 1.0
                new Run(new Text("Test")));

            doc.MainDocumentPart.Document = new Document(new Body(para));

            var resolver = new StyleResolver(doc);
            var resolved = resolver.ResolveParagraph(para);

            // Direct formatting should win (highest priority)
            Assert.Equal(1.0, resolved.LineSpacingCm);
            Assert.True(resolved.IsFromDirect);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

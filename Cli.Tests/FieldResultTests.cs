using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class FieldResultTests
{
    [Fact]
    public void WordSlice_FieldBlock_ExtractsCachedResult()
    {
        var docxPath = Path.Combine(Path.GetTempPath(), "field-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            // Create a docx with a heading and a TOC field that has a cached result
            using (var doc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart();
                var headingPara = new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                    new Run(new Text("Test Document")));
                var tocPara = new Paragraph(
                    new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                    new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
                    new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
                    new Run(new Text("42")),
                    new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
                doc.MainDocumentPart!.Document = new Document(new Body(headingPara, tocPara));
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "field-slice-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var slice = WordSlice.Slice(docxPath, tempDir);
                Assert.Equal(0, slice.Warnings.Count);

                // Read JSONL output and find field block
                var jsonlPath = Path.Combine(tempDir, "content.jsonl");
                Assert.True(File.Exists(jsonlPath), $"content.jsonl not found in {tempDir}");
                var lines = File.ReadAllLines(jsonlPath);
                Assert.NotEmpty(lines);

                var fieldLine = lines.FirstOrDefault(l => l.Contains("\"kind\": \"field\"") || l.Contains("\"fieldCode\""));
                Assert.NotNull(fieldLine);
                Assert.Contains("PAGE", fieldLine);
                Assert.Contains("cachedResult", fieldLine);
                Assert.Contains("42", fieldLine);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
        finally
        {
            try { File.Delete(docxPath); } catch { }
        }
    }
}

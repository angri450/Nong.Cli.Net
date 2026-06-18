using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class FieldResultTests
{
    static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    static string NongDll => Path.Combine(RepoRoot, "Cli", "bin", "Release", "net8.0", "nong.dll");

    (string json, string stderr, int exitCode) Run(params string[] args) =>
        CliTestToolPath.RunDotnetCli(RepoRoot, NongDll, 60000, true, null, args);

    [Fact]
    public void WordSlice_FieldBlock_ExtractsCachedResult()
    {
        // ... (existing library test) ...
    }

    [Fact]
    public void WordFieldsCommand_ListsFieldsWithCachedResults()
    {
        var docxPath = Path.Combine(Path.GetTempPath(), "field-cli-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            // Create docx with PAGE field
            using (var doc = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart();
                var para = new Paragraph(
                    new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
                    new Run(new Text("Test")));
                var fieldPara = new Paragraph(
                    new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
                    new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
                    new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
                    new Run(new Text("42")),
                    new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
                doc.MainDocumentPart!.Document = new Document(new Body(para, fieldPara));
            }

            var (output, _, code) = Run("word", "fields", "--input", docxPath, "--json");
            Assert.Equal(0, code);
            Assert.Contains("\"fieldKind\": \"PAGE\"", output);
            Assert.Contains("\"cachedResult\": \"42\"", output);
        }
        finally
        {
            try { File.Delete(docxPath); } catch { }
        }
    }
}

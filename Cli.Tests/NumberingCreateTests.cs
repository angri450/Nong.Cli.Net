using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class NumberingCreateTests
{
    [Fact]
    public void DocxNumbering_CreateList_ReturnsNumId_AndListShowsIt()
    {
        var path = Path.Combine(Path.GetTempPath(), "num-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            doc.AddMainDocumentPart().Document = new Document(new Body());
            doc.MainDocumentPart!.Document.Save();

            var numbering = new DocxNumbering(doc);

            var numId = numbering.CreateList(new NumberingSpec(NumberingKind.Decimal, Levels: 3));
            Assert.True(numId > 0);

            var list = numbering.List();
            Assert.Contains(list, n => n.NumId == numId);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

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

    [Fact]
    public void DocxNumbering_NewNumId_AtLeastMaxPlus100_AvoidTemplateConflict()
    {
        var path = Path.Combine(Path.GetTempPath(), "num-comp-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            // Create a docx with some pre-existing numbering at low IDs
            using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart().Document = new Document(new Body());
                doc.MainDocumentPart!.Document.Save();

                var numbering = new DocxNumbering(doc);
                // Create a first list
                var firstNumId = numbering.CreateList(new NumberingSpec(NumberingKind.Bullet));
                Assert.True(firstNumId >= 100);

                // Create a second list — should get a higher numId
                var secondNumId = numbering.CreateList(new NumberingSpec(NumberingKind.Decimal));
                var list = numbering.List();
                var maxNumId = list.Max(n => n.NumId);
                Assert.True(secondNumId >= firstNumId,
                    $"second numId {secondNumId} should be >= first {firstNumId}");
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

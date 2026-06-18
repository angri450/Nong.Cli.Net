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
            using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart().Document = new Document(new Body());
                doc.MainDocumentPart!.Document.Save();

                var numbering = new DocxNumbering(doc);
                var firstNumId = numbering.CreateList(new NumberingSpec(NumberingKind.Bullet));
                Assert.True(firstNumId >= 100);

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

    [Fact]
    public void DocumentWriter_OrderedList_CreatesNumberingAndAppliesToParagraphs()
    {
        var path = Path.Combine(Path.GetTempPath(), "list-test-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            doc.AddMainDocumentPart().Document = new Document(new Body());
            doc.MainDocumentPart!.Document.Save();

            var writer = new DocumentWriter(doc.MainDocumentPart.Document.Body!, doc);
            writer.OrderedList(new NumberingSpec(NumberingKind.Decimal, Levels: 2), "第一项", "第二项");

            var paragraphs = doc.MainDocumentPart.Document.Body!.Elements<Paragraph>().ToList();
            Assert.Equal(2, paragraphs.Count);
            foreach (var p in paragraphs)
            {
                var numPr = p.ParagraphProperties?.NumberingProperties;
                Assert.NotNull(numPr);
                Assert.NotNull(numPr!.NumberingId);
                Assert.True(numPr.NumberingId!.Val!.Value > 0);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

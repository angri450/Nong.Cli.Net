using DocxCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class NumberingCompatTests
{
    [Fact]
    public void ExistingNumbering_NotBroken_AfterCreatingNewLists()
    {
        var path = Path.Combine(Path.GetTempPath(), "num-compat-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            // Create a docx with pre-existing numbering (simulating a template)
            using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                doc.AddMainDocumentPart().Document = new Document(new Body());
                doc.MainDocumentPart!.Document.Save();

                var numbering = new DocxNumbering(doc);

                // Create some "existing template" numbering
                var existingNumId1 = numbering.CreateList(new NumberingSpec(NumberingKind.Decimal, Levels: 3));
                var existingNumId2 = numbering.CreateList(new NumberingSpec(NumberingKind.Bullet, Levels: 1));

                // Record existing state
                var listBefore = numbering.List();
                Assert.Equal(2, listBefore.Count);

                // Now create a NEW list (simulating V5 adding numbering)
                var newNumId = numbering.CreateList(new NumberingSpec(NumberingKind.LowerLetter, Levels: 2));

                // Verify: existing numbering is still there
                var listAfter = numbering.List();
                Assert.Equal(3, listAfter.Count);
                Assert.Contains(listAfter, n => n.NumId == existingNumId1);
                Assert.Contains(listAfter, n => n.NumId == existingNumId2);
                Assert.Contains(listAfter, n => n.NumId == newNumId);

                // Verify: existing numbering properties unchanged
                var existing1 = listAfter.First(n => n.NumId == existingNumId1);
                Assert.Equal(NumberingKind.Decimal, existing1.Kind);
                Assert.Equal(3, existing1.Levels);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void NewNumId_AvoidsLowRange_CompatibleWithTemplates()
    {
        var path = Path.Combine(Path.GetTempPath(), "num-compat2-" + Guid.NewGuid().ToString("N")[..8] + ".docx");
        try
        {
            using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            doc.AddMainDocumentPart().Document = new Document(new Body());
            doc.MainDocumentPart!.Document.Save();

            // Simulate template with low numIds (like real templates use 1-10)
            var numbering = new DocxNumbering(doc);
            var firstNumId = numbering.CreateList(new NumberingSpec(NumberingKind.Decimal));
            Assert.True(firstNumId >= 100, $"First numId {firstNumId} should be >= 100 to avoid template conflict");

            // All subsequent IDs should also be >= 100
            for (int i = 0; i < 5; i++)
            {
                var id = numbering.CreateList(new NumberingSpec(NumberingKind.Bullet));
                Assert.True(id >= 100, $"numId {id} should be >= 100");
            }
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

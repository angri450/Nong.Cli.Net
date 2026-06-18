using DocxCore;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nong.Cli.Tests;

public class TableMergeTests
{
    [Fact]
    public void TableBuilder_MergeHorizontal_WritesGridSpan()
    {
        var table = new TableBuilder()
            .HeaderRow("A", "B", "C")
            .DataRow("1", "2", "3")
            .MergeHorizontal(0, 0, 2)  // first row, columns 0-2 merged
            .Build();

        var firstRow = table.Elements<TableRow>().First();
        var firstCell = firstRow.Elements<TableCell>().First();
        var gridSpan = firstCell.TableCellProperties?.GridSpan;
        Assert.NotNull(gridSpan);
        Assert.Equal(3, gridSpan!.Val?.Value);
        // After merge, the row should have fewer cells
        var cellCount = firstRow.Elements<TableCell>().Count();
        Assert.True(cellCount <= 1, $"Expected ≤1 cell after 3-col merge, got {cellCount}");
    }
}

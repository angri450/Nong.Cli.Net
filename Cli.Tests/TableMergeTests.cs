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
        var cellCount = firstRow.Elements<TableCell>().Count();
        Assert.True(cellCount <= 1, $"Expected ≤1 cell after 3-col merge, got {cellCount}");
    }

    [Fact]
    public void TableBuilder_MergeVertical_WritesVMergeRestartAndContinue()
    {
        var table = new TableBuilder()
            .HeaderRow("A", "B")
            .DataRow("1", "2")
            .DataRow("3", "4")
            .DataRow("5", "6")
            .MergeVertical(0, 0, 2)  // column 0, rows 0-2 merged
            .Build();

        var rows = table.Elements<TableRow>().ToList();
        var firstCell = rows[0].Elements<TableCell>().First();
        var vMergeRestart = firstCell.TableCellProperties?.VerticalMerge;
        Assert.NotNull(vMergeRestart);
        Assert.Equal(MergedCellValues.Restart, vMergeRestart!.Val?.Value);

        var secondCell = rows[1].Elements<TableCell>().First();
        var vMergeContinue = secondCell.TableCellProperties?.VerticalMerge;
        Assert.NotNull(vMergeContinue);
        // Continue can be expressed as no val (our convention) or val=continue
        Assert.True(vMergeContinue!.Val?.Value == null || vMergeContinue.Val?.Value == MergedCellValues.Continue);
    }

    [Fact]
    public void TableBuilder_MergeRange_RectangularMerge()
    {
        var table = new TableBuilder()
            .Headers("A", "B", "C")
            .Row("1", "2", "3")
            .Row("4", "5", "6")
            .MergeRange(0, 0, 1, 1)  // 2x2 rectangle merge
            .Build();

        // Verify (0,0) cell has gridSpan=2 and vMerge restart
        var rows = table.Elements<TableRow>().ToList();
        var cell00 = rows[0].Elements<TableCell>().First();
        Assert.NotNull(cell00.TableCellProperties?.GridSpan);
        Assert.Equal(2, cell00.TableCellProperties!.GridSpan!.Val!.Value);
        Assert.NotNull(cell00.TableCellProperties.VerticalMerge);
        Assert.Equal(MergedCellValues.Restart, cell00.TableCellProperties.VerticalMerge!.Val!.Value);

        // (1,0) cell has vMerge continue
        var cell10 = rows[1].Elements<TableCell>().First();
        Assert.NotNull(cell10.TableCellProperties?.VerticalMerge);
        Assert.True(cell10.TableCellProperties!.VerticalMerge!.Val?.Value == null ||
                    cell10.TableCellProperties.VerticalMerge.Val?.Value == MergedCellValues.Continue);
    }

    [Fact]
    public void TableBuilder_SplitAt_RemovesMerge()
    {
        var table = new TableBuilder()
            .Headers("A", "B", "C")
            .Row("1", "2", "3")
            .MergeHorizontal(0, 0, 2)
            .Build();
        var builder = TableBuilder.FromExisting(table);
        builder.SplitAt(0, 0);  // split back to 3 cells
        var rebuilt = builder.Build();
        var firstRow = rebuilt.Elements<TableRow>().First();
        Assert.Equal(3, firstRow.Elements<TableCell>().Count());
    }
}

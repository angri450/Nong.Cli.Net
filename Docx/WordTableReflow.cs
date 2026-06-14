using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Explicit table layout/reflow rules for long, wide, and continuation tables.
/// This command-level engine is intentionally separate from academic-format so
/// ordinary formatting never changes table structure without an explicit request.
/// </summary>
public static class WordTableReflow
{
    const uint ThreeLineFrameBorderSize = 12; // 1.5 pt
    const uint ThreeLineHeaderBorderSize = 6; // 0.75 pt
    const string TableFontSize = "21";
    const string ChineseBodyFont = "宋体";
    const string LatinFont = "Times New Roman";

    public sealed record TableReflowOptions(
        int MaxRows,
        int MaxColumns,
        int RepeatLeftColumns,
        string ContinuationLabel);

    public sealed record TableReflowResult(
        string Input,
        string Output,
        int TablesVisited,
        int TablesReflowed,
        int LongTablesSplit,
        int WideTablesSplit,
        int OutputTables,
        int ContinuationLabelsInserted,
        List<string> Warnings);

    sealed record ColumnGroup(int Index, IReadOnlyList<int> Columns);
    sealed record RowChunk(int Index, IReadOnlyList<W.TableRow> Rows);
    sealed record TablePart(ColumnGroup ColumnGroup, RowChunk RowChunk, bool HasNextRowChunk);

    public static TableReflowResult Apply(string inputPath, string outputPath, TableReflowOptions options)
    {
        GuardDifferentPaths(inputPath, outputPath);
        if (options.MaxRows <= 0 && options.MaxColumns <= 0)
            throw new ArgumentException("At least one of --max-rows or --max-cols must be greater than zero.");
        if (options.MaxColumns > 0 && options.RepeatLeftColumns >= options.MaxColumns)
            throw new ArgumentException("--repeat-left-cols must be smaller than --max-cols.");

        File.Copy(inputPath, outputPath, true);

        var warnings = new List<string>();
        var tablesVisited = 0;
        var tablesReflowed = 0;
        var longTablesSplit = 0;
        var wideTablesSplit = 0;
        var outputTables = 0;
        var continuationLabels = 0;

        using var doc = WordprocessingDocument.Open(outputPath, true);
        var body = doc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("Document body is missing.");

        var tables = body.Elements<W.Table>().ToList();
        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            var table = tables[tableIndex];
            tablesVisited++;

            var rows = table.Elements<W.TableRow>().ToList();
            if (rows.Count == 0)
                continue;

            var colCount = rows.Select(r => r.Elements<W.TableCell>().Count()).DefaultIfEmpty(0).Max();
            if (colCount == 0)
                continue;

            if (options.MaxColumns > 0 && colCount > options.MaxColumns && HasHorizontalMerges(table))
            {
                warnings.Add($"Table {tableIndex + 1} has horizontally merged cells; column reflow was skipped.");
                continue;
            }

            if (options.MaxRows > 0 && rows.Skip(1).Count() > options.MaxRows && HasVerticalMerges(table))
            {
                warnings.Add($"Table {tableIndex + 1} has vertical merges; row continuation reflow was skipped.");
                continue;
            }

            var columnGroups = BuildColumnGroups(colCount, options);
            var dataRows = rows.Skip(1).ToList();
            var rowChunks = BuildRowChunks(dataRows, options);
            var needsWideReflow = columnGroups.Count > 1;
            var needsLongReflow = rowChunks.Count > 1;
            if (!needsWideReflow && !needsLongReflow)
                continue;

            var parts = new List<TablePart>();
            foreach (var columnGroup in columnGroups)
            {
                for (var rowChunkIndex = 0; rowChunkIndex < rowChunks.Count; rowChunkIndex++)
                {
                    parts.Add(new TablePart(
                        columnGroup,
                        rowChunks[rowChunkIndex],
                        HasNextRowChunk: rowChunkIndex < rowChunks.Count - 1));
                }
            }

            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                if (partIndex > 0)
                {
                    table.InsertBeforeSelf(CreateContinuationParagraph(
                        options.ContinuationLabel,
                        tableIndex + 1,
                        partIndex + 1,
                        parts[partIndex].ColumnGroup.Index + 1,
                        parts[partIndex].RowChunk.Index + 1));
                    continuationLabels++;
                }

                var nextTable = CreatePartTable(rows[0], parts[partIndex]);
                table.InsertBeforeSelf(nextTable);
                outputTables++;
            }

            table.Remove();
            tablesReflowed++;
            if (needsLongReflow)
                longTablesSplit++;
            if (needsWideReflow)
                wideTablesSplit++;
        }

        doc.MainDocumentPart?.Document?.Save();

        return new TableReflowResult(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputPath),
            tablesVisited,
            tablesReflowed,
            longTablesSplit,
            wideTablesSplit,
            outputTables,
            continuationLabels,
            warnings);
    }

    static List<ColumnGroup> BuildColumnGroups(int columnCount, TableReflowOptions options)
    {
        if (options.MaxColumns <= 0 || columnCount <= options.MaxColumns)
            return [new ColumnGroup(0, Enumerable.Range(0, columnCount).ToArray())];

        var groups = new List<ColumnGroup>();
        groups.Add(new ColumnGroup(0, Enumerable.Range(0, options.MaxColumns).ToArray()));

        var repeated = Math.Clamp(options.RepeatLeftColumns, 0, Math.Max(0, options.MaxColumns - 1));
        var payloadWidth = options.MaxColumns - repeated;
        var cursor = options.MaxColumns;
        var groupIndex = 1;
        while (cursor < columnCount)
        {
            var columns = new List<int>();
            columns.AddRange(Enumerable.Range(0, repeated));
            columns.AddRange(Enumerable.Range(cursor, Math.Min(payloadWidth, columnCount - cursor)));
            groups.Add(new ColumnGroup(groupIndex++, columns));
            cursor += payloadWidth;
        }

        return groups;
    }

    static List<RowChunk> BuildRowChunks(IReadOnlyList<W.TableRow> dataRows, TableReflowOptions options)
    {
        if (dataRows.Count == 0)
            return [new RowChunk(0, Array.Empty<W.TableRow>())];
        if (options.MaxRows <= 0 || dataRows.Count <= options.MaxRows)
            return [new RowChunk(0, dataRows)];

        var result = new List<RowChunk>();
        var index = 0;
        for (var cursor = 0; cursor < dataRows.Count; cursor += options.MaxRows)
        {
            result.Add(new RowChunk(index++, dataRows.Skip(cursor).Take(options.MaxRows).ToArray()));
        }

        return result;
    }

    static W.Table CreatePartTable(W.TableRow headerRow, TablePart part)
    {
        var table = new W.Table();
        table.Append(CreateTableProperties(bottomBorderSize: part.HasNextRowChunk ? ThreeLineHeaderBorderSize : ThreeLineFrameBorderSize));
        table.Append(CreateTableGrid(part.ColumnGroup.Columns.Count));

        var header = SelectColumns(headerRow, part.ColumnGroup.Columns, isHeader: true);
        table.Append(header);

        foreach (var row in part.RowChunk.Rows)
            table.Append(SelectColumns(row, part.ColumnGroup.Columns, isHeader: false));

        return table;
    }

    static W.TableProperties CreateTableProperties(uint bottomBorderSize) =>
        new(
            new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
            new W.TableJustification { Val = W.TableRowAlignmentValues.Center },
            new W.TableLayout { Type = W.TableLayoutValues.Fixed },
            new W.TableCellMarginDefault(
                new W.TopMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
                new W.TableCellLeftMargin { Width = 120, Type = W.TableWidthValues.Dxa },
                new W.BottomMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
                new W.TableCellRightMargin { Width = 120, Type = W.TableWidthValues.Dxa }),
            new W.TableBorders(
                new W.TopBorder { Val = W.BorderValues.Single, Size = ThreeLineFrameBorderSize, Color = "000000" },
                new W.LeftBorder { Val = W.BorderValues.Nil },
                new W.BottomBorder { Val = W.BorderValues.Single, Size = bottomBorderSize, Color = "000000" },
                new W.RightBorder { Val = W.BorderValues.Nil },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Nil },
                new W.InsideVerticalBorder { Val = W.BorderValues.Nil }));

    static W.TableGrid CreateTableGrid(int columnCount)
    {
        var grid = new W.TableGrid();
        var width = Math.Max(1, 9000 / Math.Max(1, columnCount)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < columnCount; i++)
            grid.Append(new W.GridColumn { Width = width });
        return grid;
    }

    static W.TableRow SelectColumns(W.TableRow source, IReadOnlyList<int> columnIndexes, bool isHeader)
    {
        var sourceCells = source.Elements<W.TableCell>().ToList();
        var row = new W.TableRow();
        if (isHeader)
            row.Append(new W.TableRowProperties(new W.TableHeader()));

        foreach (var index in columnIndexes)
        {
            var cell = index < sourceCells.Count
                ? (W.TableCell)sourceCells[index].CloneNode(true)
                : CreateEmptyCell();
            FormatCell(cell, isHeader);
            row.Append(cell);
        }

        return row;
    }

    static W.TableCell CreateEmptyCell() =>
        new(new W.Paragraph(new W.Run(new W.Text(""))));

    static void FormatCell(W.TableCell cell, bool isHeader)
    {
        cell.TableCellProperties ??= new W.TableCellProperties();
        cell.TableCellProperties.RemoveAllChildren<W.Shading>();
        SetOrReplace(cell.TableCellProperties, new W.TableCellVerticalAlignment { Val = W.TableVerticalAlignmentValues.Center });
        SetOrReplace(cell.TableCellProperties, new W.TableCellMargin(
            new W.TopMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
            new W.LeftMargin { Width = "120", Type = W.TableWidthUnitValues.Dxa },
            new W.BottomMargin { Width = "80", Type = W.TableWidthUnitValues.Dxa },
            new W.RightMargin { Width = "120", Type = W.TableWidthUnitValues.Dxa }));

        if (isHeader)
        {
            SetOrReplace(cell.TableCellProperties, new W.TableCellBorders(
                new W.BottomBorder { Val = W.BorderValues.Single, Size = ThreeLineHeaderBorderSize, Color = "000000" }));
        }
        else
        {
            cell.TableCellProperties.RemoveAllChildren<W.TableCellBorders>();
        }

        foreach (var paragraph in cell.Elements<W.Paragraph>())
        {
            paragraph.ParagraphProperties ??= new W.ParagraphProperties();
            paragraph.ParagraphProperties.RemoveAllChildren<W.Shading>();
            SetOrReplace(paragraph.ParagraphProperties, new W.Justification
            {
                Val = ShouldLeftAlignTableText(paragraph.InnerText.Trim()) ? W.JustificationValues.Left : W.JustificationValues.Center,
            });
            SetOrReplace(paragraph.ParagraphProperties, new W.Indentation { FirstLine = "0" });
            SetOrReplace(paragraph.ParagraphProperties, new W.SpacingBetweenLines
            {
                Before = "60",
                After = "60",
                Line = "360",
                LineRule = W.LineSpacingRuleValues.AtLeast,
            });

            foreach (var run in paragraph.Elements<W.Run>())
            {
                run.RunProperties ??= new W.RunProperties();
                run.RunProperties.RemoveAllChildren<W.Shading>();
                SetOrReplace(run.RunProperties, new W.RunFonts
                {
                    Ascii = LatinFont,
                    HighAnsi = LatinFont,
                    EastAsia = ChineseBodyFont,
                });
                SetOrReplace(run.RunProperties, new W.FontSize { Val = TableFontSize });
                if (isHeader)
                    SetOrReplace(run.RunProperties, new W.Bold());
            }
        }
    }

    static W.Paragraph CreateContinuationParagraph(string label, int tableIndex, int partIndex, int columnGroup, int rowChunk)
    {
        var text = $"{label} {tableIndex}-{partIndex}";
        if (columnGroup > 1)
            text += $"（列组 {columnGroup}）";
        if (rowChunk > 1)
            text += $"（第 {rowChunk} 段）";

        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.Justification { Val = W.JustificationValues.Center },
                new W.SpacingBetweenLines { Before = "120", After = "60", Line = "360", LineRule = W.LineSpacingRuleValues.AtLeast }),
            new W.Run(
                new W.RunProperties(
                    new W.RunFonts { Ascii = LatinFont, HighAnsi = LatinFont, EastAsia = ChineseBodyFont },
                    new W.FontSize { Val = TableFontSize }),
                new W.Text(text)));
    }

    static bool HasHorizontalMerges(W.Table table) =>
        table.Descendants<W.TableCellProperties>()
            .Any(tcPr => tcPr.GridSpan?.Val?.Value > 1);

    static bool HasVerticalMerges(W.Table table) =>
        table.Descendants<W.TableCellProperties>()
            .Any(tcPr => tcPr.VerticalMerge != null);

    static bool ShouldLeftAlignTableText(string text) =>
        text.Length >= 12
        || text.Contains('，')
        || text.Contains('。')
        || text.Contains(';')
        || text.Contains('；')
        || text.Contains('：');

    static void SetOrReplace<TContainer, TChild>(TContainer container, TChild child)
        where TContainer : OpenXmlCompositeElement
        where TChild : OpenXmlElement
    {
        container.RemoveAllChildren<TChild>();
        container.Append(child);
    }

    static void GuardDifferentPaths(string inputPath, string outputPath)
    {
        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Input and output paths must be different.");
    }
}

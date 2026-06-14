using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using PandocCore;

namespace ExcelCore;

public static class ExcelSlice
{
    public static ExcelSliceResult Slice(string xlsxPath, string outputDir)
    {
        if (string.IsNullOrWhiteSpace(xlsxPath))
            throw new ArgumentException("Excel path is required.", nameof(xlsxPath));
        if (!File.Exists(xlsxPath))
            throw new FileNotFoundException($"File not found: {xlsxPath}", xlsxPath);

        var ext = Path.GetExtension(xlsxPath).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xlsm")
            throw new InvalidDataException($"Expected .xlsx or .xlsm file, got: {ext}");

        using var workbook = new XLWorkbook(xlsxPath);
        var source = Path.GetFileName(xlsxPath);
        var sheets = new List<ExcelSheetSlice>();
        var blocks = new List<ExcelContentBlock>();
        var contentLines = new List<string>();
        var warnings = new List<string>();
        var blockIndex = 0;

        foreach (var worksheet in workbook.Worksheets)
        {
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var sheet = new ExcelSheetSlice
            {
                Name = worksheet.Name,
                Position = worksheet.Position,
                RowCount = lastRow,
                ColumnCount = lastColumn,
                UsedRange = lastRow > 0 && lastColumn > 0
                    ? RangeEvidence(worksheet.Range(1, 1, lastRow, lastColumn).RangeAddress)
                    : null,
            };

            foreach (var mergedRange in worksheet.MergedRanges)
                sheet.MergedRanges.Add(RangeEvidence(mergedRange.RangeAddress));

            foreach (var table in worksheet.Tables)
            {
                sheet.Tables.Add(new ExcelTableRegion
                {
                    Name = table.Name,
                    Address = RangeEvidence(table.RangeAddress),
                    DataRange = table.DataRange != null ? RangeEvidence(table.DataRange.RangeAddress) : null,
                    Headers = table.Fields.Select(f => f.Name).ToList(),
                    ShowHeaderRow = table.ShowHeaderRow,
                    ShowTotalsRow = table.ShowTotalsRow,
                    ShowAutoFilter = table.ShowAutoFilter,
                });
            }

            if (lastRow == 0 || lastColumn == 0)
            {
                warnings.Add($"Sheet '{worksheet.Name}' is empty.");
                sheets.Add(sheet);
                continue;
            }

            for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
            {
                var values = new List<string>();
                for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
                {
                    var cell = worksheet.Cell(rowNumber, columnNumber);
                    var value = cell.HasFormula ? $"={cell.FormulaA1}" : cell.GetString();
                    values.Add(value);

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        var cellRef = cell.Address.ToStringRelative();
                        var mergedRange = worksheet.MergedRanges
                            .FirstOrDefault(r => r.RangeAddress.Contains(cell.Address));
                        var containingTable = worksheet.Tables
                            .FirstOrDefault(t => t.RangeAddress.Contains(cell.Address));
                        var blockId = $"cell{++blockIndex:D4}";
                        if (cell.HasFormula)
                        {
                            sheet.Formulas.Add(new ExcelFormulaEvidence
                            {
                                Address = cellRef,
                                FormulaA1 = cell.FormulaA1,
                                ValuePreview = Preview(cell.GetString()),
                                BlockId = blockId,
                            });
                        }

                        blocks.Add(new ExcelContentBlock
                        {
                            Id = blockId,
                            BlockId = blockId,
                            Index = blockIndex - 1,
                            Kind = "cell",
                            Sheet = worksheet.Name,
                            Row = rowNumber,
                            Column = columnNumber,
                            Address = cellRef,
                            Text = value,
                            HasFormula = cell.HasFormula,
                            FormulaA1 = cell.HasFormula ? cell.FormulaA1 : null,
                            MergedRange = mergedRange?.RangeAddress.ToStringRelative(),
                            TableName = containingTable?.Name,
                            Style = new ExcelCellStyleInfo
                            {
                                Font = cell.Style.Font.FontName,
                                FontSize = cell.Style.Font.FontSize,
                                Bold = cell.Style.Font.Bold,
                                Italic = cell.Style.Font.Italic,
                                NumberFormat = cell.Style.NumberFormat.Format,
                            },
                        });
                    }
                }

                sheet.Rows.Add(values);
            }

            sheets.Add(sheet);
        }

        var document = new ExcelSliceDocument
        {
            Source = new ExcelSourceInfo
            {
                Path = source,
                Sha256 = Sha256(xlsxPath),
                SheetCount = sheets.Count,
            },
            Sheets = sheets,
            Blocks = blocks,
            Warnings = warnings,
        };
        var metrics = new NongPandocMetrics
        {
            Blocks = blocks.Count,
            Paragraphs = 0,
            Headings = sheets.Count,
            Tables = sheets.Sum(s => s.Tables.Count > 0 ? s.Tables.Count : (s.RowCount > 0 && s.ColumnCount > 0 ? 1 : 0)),
            Figures = 0,
            Images = 0,
            References = 0,
            Warnings = warnings.Count,
        };
        var manifest = new NongPandocSliceManifest
        {
            Source = new NongPandocSourceInfo
            {
                Path = source,
                Format = "xlsx",
                Sha256 = document.Source.Sha256,
                SheetCount = sheets.Count,
            },
            CreatedAt = DateTime.UtcNow,
            Metrics = metrics,
            Warnings = warnings,
        };
        var structure = BuildStructure(document);
        var format = BuildFormat(document);
        foreach (var block in blocks)
            contentLines.Add(System.Text.Json.JsonSerializer.Serialize(block, block.GetType(), NongPandocSlicePackageWriter.DefaultJsonlOptions));

        var writeResult = NongPandocSlicePackageWriter.Write(new NongPandocSliceWritePayload
        {
            OutputDirectory = outputDir,
            Manifest = manifest,
            Document = document,
            ContentJsonlLines = contentLines,
            NongMarkText = BuildNongMark(document),
            Structure = structure,
            Format = format,
            Diagnostics = new ExcelSliceDiagnostics { Source = source, Warnings = warnings },
            AssetsManifest = BuildAssetsManifest(document),
            TextPreview = ExcelPreview.Preview(workbook).Text,
        }, new NongPandocSliceWriteOptions
        {
            RequiredArtifacts = NongPandocSlicePackageWriter.DefaultRequiredArtifacts
                .Concat(new[] { NongPandocArtifactNames.TextPreview })
                .ToArray(),
        });

        return new ExcelSliceResult
        {
            OutputDir = writeResult.OutputDirectory,
            ManifestPath = writeResult.ManifestPath,
            BlockCount = blocks.Count,
            SheetCount = sheets.Count,
            Warnings = warnings,
        };
    }

    private static ExcelSliceStructure BuildStructure(ExcelSliceDocument document)
    {
        var structure = new ExcelSliceStructure { Source = document.Source.Path };
        foreach (var sheet in document.Sheets)
        {
            structure.Sheets.Add(new ExcelSheetRef
            {
                Name = sheet.Name,
                Position = sheet.Position,
                RowCount = sheet.RowCount,
                ColumnCount = sheet.ColumnCount,
                UsedRange = sheet.UsedRange,
                MergedRanges = sheet.MergedRanges,
                Formulas = sheet.Formulas,
                Tables = sheet.Tables,
            });
        }

        foreach (var block in document.Blocks)
        {
            structure.BlockIndex[block.BlockId] = new ExcelBlockIndexEntry
            {
                Kind = block.Kind,
                Order = block.Index,
                Sheet = block.Sheet,
                Address = block.Address,
                HasFormula = block.HasFormula,
                FormulaA1 = block.FormulaA1,
                MergedRange = block.MergedRange,
                TableName = block.TableName,
                TextPreview = Preview(block.Text),
                Provenance = new NongPandocBlockProvenance
                {
                    Format = "xlsx",
                    Source = "cell",
                    Sheet = block.Sheet,
                    Position = block.Index,
                    Address = block.Address,
                    Confidence = "high",
                    Notes = BuildCellEvidenceNotes(block),
                },
            };
        }

        return structure;
    }

    private static List<string>? BuildCellEvidenceNotes(ExcelContentBlock block)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(block.FormulaA1))
            notes.Add($"formula:{block.FormulaA1}");
        if (!string.IsNullOrWhiteSpace(block.MergedRange))
            notes.Add($"mergedRange:{block.MergedRange}");
        if (!string.IsNullOrWhiteSpace(block.TableName))
            notes.Add($"table:{block.TableName}");
        return notes.Count > 0 ? notes : null;
    }

    private static ExcelSliceFormat BuildFormat(ExcelSliceDocument document)
    {
        var format = new ExcelSliceFormat { Source = document.Source.Path };
        foreach (var sheet in document.Sheets)
        {
            format.Sheets.Add(new ExcelSheetFormat
            {
                Name = sheet.Name,
                RowCount = sheet.RowCount,
                ColumnCount = sheet.ColumnCount,
                UsedRange = sheet.UsedRange,
                MergedRangeCount = sheet.MergedRanges.Count,
                FormulaCount = sheet.Formulas.Count,
                TableCount = sheet.Tables.Count,
            });
        }

        format.Fonts = document.Blocks
            .Select(b => b.Style?.Font)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f)
            .Cast<string>()
            .ToList();
        format.NumberFormats = document.Blocks
            .Select(b => b.Style?.NumberFormat)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f)
            .Cast<string>()
            .ToList();
        format.VisualEvidence = new NongPandocVisualEvidence
        {
            Format = "xlsx",
            Source = document.Source.Path,
            Fonts = format.Fonts,
            Tables = document.Sheets
                .SelectMany(s => s.Tables.Select(t => $"{s.Name}:{t.Name}:{t.Address.Address}"))
                .ToList(),
            Layout = document.Sheets
                .Select(s => $"{s.Name}:usedRange={s.UsedRange?.Address ?? ""};rows={s.RowCount};cols={s.ColumnCount};merged={s.MergedRanges.Count}")
                .ToList(),
            Assets = document.Sheets
                .SelectMany(s => s.Formulas.Select(f => $"{s.Name}:{f.Address}:={f.FormulaA1}"))
                .Concat(document.Sheets.SelectMany(s => s.MergedRanges.Select(r => $"{s.Name}:merged:{r.Address}")))
                .ToList(),
            Warnings = document.Warnings,
        };
        return format;
    }

    private static ExcelAssetManifest BuildAssetsManifest(ExcelSliceDocument document)
    {
        var manifest = new ExcelAssetManifest { Source = document.Source.Path };
        foreach (var sheet in document.Sheets)
        {
            foreach (var table in sheet.Tables)
            {
                manifest.Items.Add(new ExcelAssetItem
                {
                    Kind = "tableRange",
                    Sheet = sheet.Name,
                    Name = table.Name,
                    Address = table.Address.Address,
                });
            }

            foreach (var mergedRange in sheet.MergedRanges)
            {
                manifest.Items.Add(new ExcelAssetItem
                {
                    Kind = "mergedRange",
                    Sheet = sheet.Name,
                    Address = mergedRange.Address,
                });
            }

            foreach (var formula in sheet.Formulas)
            {
                manifest.Items.Add(new ExcelAssetItem
                {
                    Kind = "formula",
                    Sheet = sheet.Name,
                    Name = formula.BlockId,
                    Address = formula.Address,
                });
            }
        }

        return manifest;
    }

    private static string BuildNongMark(ExcelSliceDocument document)
    {
        var sb = new StringBuilder();
        foreach (var sheet in document.Sheets)
        {
            sb.AppendLine($"# {EscapeText(sheet.Name)} {{#sheet-{sheet.Position:D3} kind=sheet rows={sheet.RowCount} cols={sheet.ColumnCount}}}");
            sb.AppendLine();
            if (sheet.Rows.Count == 0)
            {
                sb.AppendLine("::: sheet-empty");
                sb.AppendLine(":::");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"::: table {{#table-{sheet.Position:D3} kind=sheet-table sheet=\"{EscapeAttr(sheet.Name)}\"}}");
            foreach (var row in sheet.Rows)
                sb.AppendLine("| " + string.Join(" | ", row.Select(EscapeCell)) + " |");
            sb.AppendLine(":::");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    private static string Preview(string? value, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= max ? text : text[..max] + "...";
    }

    private static ExcelRangeEvidence RangeEvidence(IXLRangeAddress address) => new()
    {
        Address = address.ToStringRelative(),
        FirstCell = address.FirstAddress.ToStringRelative(),
        LastCell = address.LastAddress.ToStringRelative(),
        FirstRow = address.FirstAddress.RowNumber,
        FirstColumn = address.FirstAddress.ColumnNumber,
        LastRow = address.LastAddress.RowNumber,
        LastColumn = address.LastAddress.ColumnNumber,
        RowCount = address.RowSpan,
        ColumnCount = address.ColumnSpan,
    };

    private static string EscapeText(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string EscapeCell(string value) => EscapeText(value).Replace("|", "\\|");

    private static string EscapeAttr(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public sealed record ExcelSliceResult
{
    public string OutputDir { get; init; } = "";
    public string ManifestPath { get; init; } = "";
    public int BlockCount { get; init; }
    public int SheetCount { get; init; }
    public List<string> Warnings { get; init; } = new();
}

public sealed record ExcelSliceDocument
{
    public string SchemaVersion { get; init; } = "nongexcel/v1";
    public ExcelSourceInfo Source { get; init; } = new();
    public List<ExcelSheetSlice> Sheets { get; init; } = new();
    public List<ExcelContentBlock> Blocks { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed record ExcelSourceInfo
{
    public string Path { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public int SheetCount { get; init; }
}

public sealed record ExcelSheetSlice
{
    public string Name { get; init; } = "";
    public int Position { get; init; }
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public ExcelRangeEvidence? UsedRange { get; init; }
    public List<ExcelRangeEvidence> MergedRanges { get; init; } = new();
    public List<ExcelFormulaEvidence> Formulas { get; init; } = new();
    public List<ExcelTableRegion> Tables { get; init; } = new();
    public List<List<string>> Rows { get; init; } = new();
}

public sealed record ExcelContentBlock
{
    public string Id { get; init; } = "";
    public string BlockId { get; init; } = "";
    public int Index { get; init; }
    public string Kind { get; init; } = "cell";
    public string Sheet { get; init; } = "";
    public int Row { get; init; }
    public int Column { get; init; }
    public string Address { get; init; } = "";
    public string Text { get; init; } = "";
    public bool HasFormula { get; init; }
    public string? FormulaA1 { get; init; }
    public string? MergedRange { get; init; }
    public string? TableName { get; init; }
    public ExcelCellStyleInfo? Style { get; init; }
}

public sealed record ExcelCellStyleInfo
{
    public string Font { get; init; } = "";
    public double FontSize { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public string NumberFormat { get; init; } = "";
}

public sealed record ExcelSliceStructure
{
    public string SchemaVersion { get; init; } = "nongexcel/structure/v1";
    public string Source { get; init; } = "";
    public List<ExcelSheetRef> Sheets { get; init; } = new();
    public Dictionary<string, ExcelBlockIndexEntry> BlockIndex { get; init; } = new();
}

public sealed record ExcelSheetRef
{
    public string Name { get; init; } = "";
    public int Position { get; init; }
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public ExcelRangeEvidence? UsedRange { get; init; }
    public List<ExcelRangeEvidence> MergedRanges { get; init; } = new();
    public List<ExcelFormulaEvidence> Formulas { get; init; } = new();
    public List<ExcelTableRegion> Tables { get; init; } = new();
}

public sealed record ExcelBlockIndexEntry
{
    public string Kind { get; init; } = "";
    public int Order { get; init; }
    public string Sheet { get; init; } = "";
    public string Address { get; init; } = "";
    public bool HasFormula { get; init; }
    public string? FormulaA1 { get; init; }
    public string? MergedRange { get; init; }
    public string? TableName { get; init; }
    public string TextPreview { get; init; } = "";
    public NongPandocBlockProvenance? Provenance { get; init; }
}

public sealed record ExcelSliceFormat
{
    public string SchemaVersion { get; init; } = "nongexcel/format/v1";
    public string Source { get; init; } = "";
    public List<string> Fonts { get; set; } = new();
    public List<string> NumberFormats { get; set; } = new();
    public List<ExcelSheetFormat> Sheets { get; init; } = new();
    public NongPandocVisualEvidence VisualEvidence { get; set; } = new();
}

public sealed record ExcelSheetFormat
{
    public string Name { get; init; } = "";
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public ExcelRangeEvidence? UsedRange { get; init; }
    public int MergedRangeCount { get; init; }
    public int FormulaCount { get; init; }
    public int TableCount { get; init; }
}

public sealed record ExcelSliceDiagnostics
{
    public string SchemaVersion { get; init; } = "nongexcel/diagnostics/v1";
    public string Source { get; init; } = "";
    public List<string> Warnings { get; init; } = new();
}

public sealed record ExcelAssetManifest
{
    public string SchemaVersion { get; init; } = "nongexcel/assets/v1";
    public string Source { get; init; } = "";
    public List<ExcelAssetItem> Items { get; init; } = new();
}

public sealed record ExcelRangeEvidence
{
    public string Address { get; init; } = "";
    public string FirstCell { get; init; } = "";
    public string LastCell { get; init; } = "";
    public int FirstRow { get; init; }
    public int FirstColumn { get; init; }
    public int LastRow { get; init; }
    public int LastColumn { get; init; }
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
}

public sealed record ExcelFormulaEvidence
{
    public string Address { get; init; } = "";
    public string FormulaA1 { get; init; } = "";
    public string ValuePreview { get; init; } = "";
    public string BlockId { get; init; } = "";
}

public sealed record ExcelTableRegion
{
    public string Name { get; init; } = "";
    public ExcelRangeEvidence Address { get; init; } = new();
    public ExcelRangeEvidence? DataRange { get; init; }
    public List<string> Headers { get; init; } = new();
    public bool ShowHeaderRow { get; init; }
    public bool ShowTotalsRow { get; init; }
    public bool ShowAutoFilter { get; init; }
}

public sealed record ExcelAssetItem
{
    public string Kind { get; init; } = "";
    public string Sheet { get; init; } = "";
    public string? Name { get; init; }
    public string Address { get; init; } = "";
}

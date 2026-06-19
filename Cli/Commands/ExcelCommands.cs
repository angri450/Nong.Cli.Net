using System.Globalization;
using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
using Angri450.Nong.Data;
using ClosedXML.Excel;
using ExcelCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// Excel command group.
/// </summary>
public static class ExcelCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("excel", "Excel spreadsheet operations");

        cmd.AddCommand(CreateSheets(jsonOpt));
        cmd.AddCommand(CreateRead(jsonOpt));
        cmd.AddCommand(CreateToGroups(jsonOpt));
        cmd.AddCommand(CreateRestructure(jsonOpt));
        cmd.AddCommand(CreateCreateXlsx(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateStyle(jsonOpt));
        cmd.AddCommand(CreateFormula(jsonOpt));
        cmd.AddCommand(CreateEvaluate(jsonOpt));
        cmd.AddCommand(CreateChart(jsonOpt));
        cmd.AddCommand(CreatePivot(jsonOpt));
        cmd.AddCommand(CreateDbImport(jsonOpt));
        cmd.AddCommand(CreateDbList(jsonOpt));
        cmd.AddCommand(CreateDbBlocks(jsonOpt));
        cmd.AddCommand(CreateDbImages(jsonOpt));

        return cmd;
    }

    // ===== excel sheets =====

    static Command CreateSheets(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var cmd = new Command("sheets", "List worksheets") { fileArg };

        cmd.SetHandler((string file, bool json) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel sheets", err, json); return; }

            var (result, elapsed) = CliHelpers.Time(() =>
            {
                using var wb = new XLWorkbook(file);
                return wb.Worksheets.Select(ws => new
                {
                    name = ws.Name,
                    position = ws.Position,
                    rows = ws.LastRowUsed()?.RowNumber() ?? 0,
                    columns = ws.LastColumnUsed()?.ColumnNumber() ?? 0
                }).ToList();
            });

            if (json)
            {
                var output = JsonOutput.Ok("excel sheets", $"{result.Count} sheet(s)", new { sheets = result });
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine($"{"Name",-20} {"Pos",3} {"Rows",6} {"Cols",6}");
                foreach (var s in result)
                    Console.WriteLine($"{s.name,-20} {s.position,3} {s.rows,6} {s.columns,6}");
            }


        }, fileArg, jsonOpt);

        return cmd;
    }

    // ===== excel read =====

    static Command CreateRead(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var sheetOpt = new Option<string>("--sheet", () => "", "Sheet name (default: first sheet)");
        var rangeOpt = new Option<string>("--range", () => "", "Cell range (e.g. A1:D20)");
        var formulaOpt = new Option<bool>("--formula", () => false,
            "Include formula string as second column of each cell (off by default). With --json each cell becomes {value,formula}; text mode only prints value.");
        var cmd = new Command("read", "Read xlsx content") { fileArg, sheetOpt, rangeOpt };
        cmd.AddOption(formulaOpt);

        cmd.SetHandler((string file, string sheet, string range, bool json, bool formula) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel read", err, json); return; }

            try
            {
            var (result, elapsed) = CliHelpers.Time(() =>
            {
                using var wb = new XLWorkbook(file);
                var ws = string.IsNullOrEmpty(sheet) ? wb.Worksheet(1) : wb.Worksheet(sheet);

                int startRow = 1, endRow, startCol = 1, endCol;
                if (!string.IsNullOrEmpty(range))
                {
                    var rng = ws.Range(range);
                    startRow = rng.FirstRow().RowNumber();
                    endRow = rng.LastRow().RowNumber();
                    startCol = rng.FirstColumn().ColumnNumber();
                    endCol = rng.LastColumn().ColumnNumber();
                }
                else
                {
                    endRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                    endCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                }

                var rows = new List<List<object>>();
                for (int r = startRow; r <= endRow; r++)
                {
                    var row = new List<object>();
                    for (int c = startCol; c <= endCol; c++)
                    {
                        var cell = ws.Cell(r, c);
                        if (formula)
                            row.Add(new { value = cell.GetString(), formula = cell.HasFormula ? cell.FormulaA1 : null });
                        else
                            row.Add(cell.GetString());
                    }
                    rows.Add(row);
                }

                return new { sheet = ws.Name, range = $"{ColToRef(startCol)}{startRow}:{ColToRef(endCol)}{endRow}", rows };
            });

            if (json)
            {
                var output = JsonOutput.Ok("excel read",
                    $"Sheet '{result.sheet}', {result.rows.Count} rows × {(result.rows.Count > 0 ? result.rows[0].Count : 0)} cols",
                    result);
                output.Metrics["rows"] = result.rows.Count;
                output.Metrics["columns"] = result.rows.Count > 0 ? result.rows[0].Count : 0;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                foreach (var row in result.rows)
                {
                    if (formula)
                    {
                        var cells = row.Select(o => o is string s ? s : ((dynamic)o).value?.ToString() ?? "");
                        Console.WriteLine(string.Join("\t", cells));
                    }
                    else
                    {
                        Console.WriteLine(string.Join("\t", row));
                    }
                }
            }

            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("excel read",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, sheetOpt, rangeOpt, jsonOpt, formulaOpt);

        return cmd;
    }

    // ===== excel to-groups =====

    static Command CreateToGroups(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var sheetOpt = new Option<string>("--sheet", () => "", "Sheet name (default: first)");
        var groupOpt = new Option<string>("--group", "Group column (letter or name)") { IsRequired = true };
        var valueOpt = new Option<string>("--value", "Value column (letter or name)") { IsRequired = true };
        var cmd = new Command("to-groups", "Convert Excel columns to grouped data") { fileArg, sheetOpt, groupOpt, valueOpt };
        var rawOpt = new Option<bool>("--raw", () => false, "Output bare JSON (for piping to chart commands)");
        cmd.AddOption(rawOpt);

        cmd.SetHandler((string file, string sheet, string group, string value, bool json, bool raw) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel to-groups", err, json); return; }

            // Pre-validate columns before data load
            int groupCol, valueCol;
            try
            {
                using var wbInit = new XLWorkbook(file);
                var wsInit = string.IsNullOrEmpty(sheet) ? wbInit.Worksheet(1) : wbInit.Worksheet(sheet);
                groupCol = ResolveColumn(wsInit, group);
                valueCol = ResolveColumn(wsInit, value);
            }
            catch (KeyNotFoundException)
            {
                CliHelpers.WriteError("excel to-groups",
                    ErrorCodes.ValidationFailed with { Message = $"Sheet not found: {sheet}" }, json);
                return;
            }
            if (groupCol < 1 || valueCol < 1)
            {
                CliHelpers.WriteError("excel to-groups",
                    ErrorCodes.ValidationFailed with { Message = $"Column not found: {(groupCol < 1 ? group : value)}" }, json);
                return;
            }

            var (result, elapsed) = CliHelpers.Time(() =>
            {
                using var wb = new XLWorkbook(file);
                var ws = string.IsNullOrEmpty(sheet) ? wb.Worksheet(1) : wb.Worksheet(sheet);

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                var groups = new Dictionary<string, List<double>>();

                for (int r = 2; r <= lastRow; r++) // skip header row
                {
                    var g = ws.Cell(r, groupCol).GetString().Trim();
                    if (string.IsNullOrEmpty(g)) continue;
                    if (double.TryParse(ws.Cell(r, valueCol).GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    {
                        if (!groups.ContainsKey(g)) groups[g] = new List<double>();
                        groups[g].Add(v);
                    }
                }
                return groups;
            });

            if (raw)
            {
                Console.WriteLine(JsonSerializer.Serialize(result));
            }
            else if (json)
            {
                int obs = result.Values.Sum(v => v.Count);
                var output = JsonOutput.Ok("excel to-groups",
                    $"{result.Count} groups, {obs} observations",
                    result);
                output.Metrics["groups"] = result.Count;
                output.Metrics["observations"] = obs;
                output.Meta.DurationMs = elapsed;
                Console.WriteLine(JsonSerializer.Serialize(output, CliHelpers.JsonOpts));
            }
            else
            {
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }


        }, fileArg, sheetOpt, groupOpt, valueOpt, jsonOpt, rawOpt);

        return cmd;
    }

    // ===== excel restructure =====

    static Command CreateRestructure(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("spec", "Path to restructure spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path (required)") { IsRequired = true };
        var cmd = new Command("restructure", "Restructure experiment Excel sources into normalized data + descriptive statistics workbook. Required: -o <output.xlsx>.") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("excel restructure", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<ExcelRestructureSpec>(jsonText, CliHelpers.JsonOpts);
                var validationMessage = ValidateRestructureSpec(spec);
                if (!string.IsNullOrEmpty(validationMessage))
                {
                    CliHelpers.WriteError("excel restructure",
                        ErrorCodes.ValidationFailed with { Message = validationMessage }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                var (result, elapsed) = CliHelpers.Time(() => RestructureWorkbook(spec!, output));

                var aerr = CliHelpers.CheckArtifact(output, "XLSX");
                if (aerr != null) { CliHelpers.WriteError("excel restructure", aerr, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok("excel restructure",
                        $"Excel restructured: {output}",
                        new
                        {
                            records = result.RecordCount,
                            statsRows = result.StatsRowCount,
                            summaryRows = result.SummaryRowCount,
                            sheets = result.SheetCount,
                            treatments = result.TreatmentCount,
                            weeks = result.WeekCount,
                        });
                    outputJson.Artifacts["xlsx"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Excel restructured: {Path.GetFullPath(output)}");
                    Console.WriteLine($"records={result.RecordCount}, statsRows={result.StatsRowCount}, summaryRows={result.SummaryRowCount}, sheets={result.SheetCount}");
                }
            }
            catch (JsonException jex)
            {
                CliHelpers.WriteError("excel restructure",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {jex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("excel restructure",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== helpers =====

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for NongPandoc slice") { IsRequired = true };
        var ingestOpt = new Option<bool>("--ingest", () => false, "Auto-import dissect output into NongDb for semantic search");
        var cmd = new Command("dissect", "Slice xlsx into a NongPandoc package") { fileArg, outOpt, ingestOpt };

        cmd.SetHandler((string file, string output, bool ingest, bool json) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel dissect", err, json); return; }

            try
            {
                CliHelpers.EnsureParentDir(Path.Combine(output, ".keep"));
                var (result, elapsed) = CliHelpers.Time(() => ExcelSlice.Slice(file, output));
                if (json)
                {
                    var o = JsonOutput.Ok("excel dissect",
                        $"Sliced: {result.SheetCount} sheets, {result.BlockCount} blocks",
                        new { outputDir = result.OutputDir, sheetCount = result.SheetCount, blockCount = result.BlockCount, warnings = result.Warnings });
                    o.Artifacts["dir"] = Path.GetFullPath(output);
                    o.Metrics["sheets"] = result.SheetCount;
                    o.Metrics["blocks"] = result.BlockCount;
                    o.Metrics["warnings"] = result.Warnings.Count;
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Sliced to {Path.GetFullPath(output)}: {result.SheetCount} sheets, {result.BlockCount} blocks");
                    foreach (var warning in result.Warnings)
                        Console.Error.WriteLine($"[WARN] {warning}");
                }
                if (ingest)
                {
                    try
                    {
                        using var ctx = new IngestionContext();
                        var ir = ctx.IngestSlice(file, output, "excel", "dissect");
                        if (!json) Console.Error.WriteLine($"[ingest] {ir.Blocks} blocks imported to nong.db");
                    }
                    catch (Exception ex) { if (!json) Console.Error.WriteLine($"[ingest] warning: {ex.Message}"); }
                }
            }
            catch (FileNotFoundException ex)
            {
                CliHelpers.WriteError("excel dissect", ErrorCodes.FileNotFound with { Message = ex.Message }, json);
            }
            catch (InvalidDataException ex)
            {
                CliHelpers.WriteError("excel dissect", ErrorCodes.UnsupportedFormat with { Message = ex.Message }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("excel dissect", ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, ingestOpt, jsonOpt);

        return cmd;
    }

    // ===== excel style =====

    static Command CreateStyle(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file to modify");
        var specArg = new Argument<string>("spec", "Path to style spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path (required)") { IsRequired = true };
        var cmd = new Command("style", "Apply cell styles from a JSON spec. Required: -o <output.xlsx>.") { fileArg, specArg, outOpt };

        cmd.SetHandler((string file, string spec, string output, bool json) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel style", err, json); return; }
            var serr = CliHelpers.ValidateTextFile(spec);
            if (serr != null) { CliHelpers.WriteError("excel style", serr, json); return; }

            try
            {
                var jsonText = File.ReadAllText(spec);
                var styleSpec = JsonSerializer.Deserialize<ExcelStyleSpec>(jsonText, CliHelpers.JsonOpts);
                if (styleSpec?.Entries == null || styleSpec.Entries.Count == 0)
                {
                    CliHelpers.WriteError("excel style",
                        ErrorCodes.ValidationFailed with { Message = "entries array must be non-empty." }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                File.Copy(file, output, true);
                var (entryCount, elapsed) = CliHelpers.Time<int>(() =>
                {
                    using var wb = new XLWorkbook(output);
                    var ws = string.IsNullOrEmpty(styleSpec.Sheet) ? wb.Worksheet(1) : wb.Worksheet(styleSpec.Sheet);

                    foreach (var e in styleSpec.Entries)
                    {
                        if (!string.IsNullOrEmpty(e.Preset))
                        {
                            if (string.Equals(e.Preset, "Academic", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(e.Preset, "Mono", StringComparison.OrdinalIgnoreCase))
                            {
                                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                                if (lastRow > 0) StylePresets.MonoHeader(ws.Row(1), 1, lastCol);
                                if (lastRow > 1) StylePresets.AlternatingRows(ws, 1, lastRow, 1, lastCol, "#F5F5F5");
                            }
                            else if (string.Equals(e.Preset, "Finance", StringComparison.OrdinalIgnoreCase))
                            {
                                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
                                if (lastRow > 0) StylePresets.FinanceHeader(ws.Row(1), 1, lastCol);
                                if (lastRow > 1) StylePresets.AlternatingRows(ws, 1, lastRow, 1, lastCol, "#FFF3E0");
                            }
                            continue;
                        }

                        var range = !string.IsNullOrEmpty(e.Range) ? ws.Range(e.Range) : null;
                        if (range == null && !string.IsNullOrEmpty(e.Range))
                            continue;

                        if (range != null)
                        {
                            if (!string.IsNullOrEmpty(e.Font)) range.Style.Font.FontName = e.Font;
                            if (e.FontSize.HasValue) range.Style.Font.FontSize = e.FontSize.Value;
                            if (e.Bold.HasValue) range.Style.Font.Bold = e.Bold.Value;
                            if (!string.IsNullOrEmpty(e.FillColor)) range.Style.Fill.BackgroundColor = XLColor.FromHtml(e.FillColor);
                            if (!string.IsNullOrEmpty(e.FontColor)) range.Style.Font.FontColor = XLColor.FromHtml(e.FontColor);
                            if (!string.IsNullOrEmpty(e.NumberFormat)) range.Style.NumberFormat.Format = e.NumberFormat;
                            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        }
                    }

                    wb.Save();
                    return styleSpec.Entries.Count;
                });

                var aerr = CliHelpers.CheckArtifact(output, "XLSX");
                if (aerr != null) { CliHelpers.WriteError("excel style", aerr, json); return; }

                if (json)
                {
                    var o = JsonOutput.Ok("excel style",
                        $"Applied {entryCount} style entries", new { entries = entryCount });
                    o.Artifacts["xlsx"] = Path.GetFullPath(output);
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"Styled: {Path.GetFullPath(output)} ({entryCount} entries)"); }
            }
            catch (JsonException jex) { CliHelpers.WriteError("excel style", ErrorCodes.ValidationFailed with { Message = $"Invalid JSON: {jex.Message}" }, json); }
            catch (Exception ex) { CliHelpers.WriteError("excel style", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, specArg, outOpt, jsonOpt);
        return cmd;
    }

    // ===== excel formula =====

    static Command CreateFormula(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file to modify");
        var specArg = new Argument<string>("spec", "Path to formula spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path (required)") { IsRequired = true };
        var cmd = new Command("formula", "Write formulas from a JSON spec. Required: -o <output.xlsx>.") { fileArg, specArg, outOpt };

        cmd.SetHandler((string file, string spec, string output, bool json) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel formula", err, json); return; }
            var serr = CliHelpers.ValidateTextFile(spec);
            if (serr != null) { CliHelpers.WriteError("excel formula", serr, json); return; }

            try
            {
                var jsonText = File.ReadAllText(spec);
                var fSpec = JsonSerializer.Deserialize<ExcelFormulaSpec>(jsonText, CliHelpers.JsonOpts);
                if (fSpec?.Entries == null || fSpec.Entries.Count == 0)
                {
                    CliHelpers.WriteError("excel formula",
                        ErrorCodes.ValidationFailed with { Message = "entries array must be non-empty." }, json);
                    return;
                }

                CliHelpers.EnsureParentDir(output);
                File.Copy(file, output, true);
                var (entryCount, elapsed) = CliHelpers.Time<int>(() =>
                {
                    using var wb = new XLWorkbook(output);
                    var ws = string.IsNullOrEmpty(fSpec.Sheet) ? wb.Worksheet(1) : wb.Worksheet(fSpec.Sheet);

                    foreach (var e in fSpec.Entries)
                    {
                        if (string.IsNullOrEmpty(e.Formula)) continue;
                        if (!string.IsNullOrEmpty(e.Cell))
                            ws.Cell(e.Cell).FormulaA1 = e.Formula;
                        else if (!string.IsNullOrEmpty(e.Range))
                            ws.Range(e.Range).FormulaA1 = e.Formula;
                    }

                    wb.Save();
                    return fSpec.Entries.Count;
                });

                if (json)
                {
                    var o = JsonOutput.Ok("excel formula",
                        $"Wrote {entryCount} formula entries", new { entries = entryCount });
                    o.Artifacts["xlsx"] = Path.GetFullPath(output);
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"Formulas written: {Path.GetFullPath(output)} ({entryCount} entries)"); }
            }
            catch (JsonException jex) { CliHelpers.WriteError("excel formula", ErrorCodes.ValidationFailed with { Message = $"Invalid JSON: {jex.Message}" }, json); }
            catch (Exception ex) { CliHelpers.WriteError("excel formula", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, specArg, outOpt, jsonOpt);
        return cmd;
    }

    // ===== excel evaluate =====

    static Command CreateEvaluate(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var outOpt = new Option<string>("-o", "Output .xlsx path (overwrites input if omitted)");
        var cmd = new Command("evaluate", "Compute all formulas and cache their values") { fileArg, outOpt };

        cmd.SetHandler((string file, string? output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("excel evaluate", err, json); return; }

            try
            {
                string outPath = output ?? file;
                var beforeBytes = new FileInfo(file).Length;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                using var wb = new ClosedXML.Excel.XLWorkbook(file);
                // ClosedXML recalculates on save by default
                wb.SaveAs(outPath);

                sw.Stop();
                var afterBytes = new FileInfo(outPath).Length;

                if (json)
                {
                    var o = JsonOutput.Ok("excel evaluate", $"Formulas evaluated ({sw.ElapsedMilliseconds}ms)",
                        new { output = Path.GetFullPath(outPath), beforeBytes, afterBytes, durationMs = sw.ElapsedMilliseconds });
                    o.Artifacts["xlsx"] = Path.GetFullPath(outPath);
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"Formulas evaluated ({sw.ElapsedMilliseconds}ms) → {outPath}"); }
            }
            catch (Exception ex) { CliHelpers.WriteError("excel evaluate", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, outOpt, jsonOpt);
        return cmd;
    }

    // ===== excel chart =====

    static Command CreateChart(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var specArg = new Argument<string>("spec", "Chart spec JSON: {sheet, dataRange, chartType}");
        var outOpt = new Option<string>("-o", "Output .xlsx path") { IsRequired = true };
        var cmd = new Command("chart", "Create chart in worksheet (V9)") { fileArg, specArg, outOpt };

        cmd.SetHandler((string file, string specPath, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("excel chart", err, json); return; }
            try
            {
                CliHelpers.EnsureParentDir(output);
                File.Copy(file, output, true);
                using var wb = new ClosedXML.Excel.XLWorkbook(output);
                var spec = JsonSerializer.Deserialize<ChartSpec>(File.ReadAllText(specPath), CliHelpers.JsonOpts);
                if (spec == null) { CliHelpers.WriteError("excel chart", ErrorCodes.ValidationFailed with { Message = "Invalid chart spec" }, json); return; }

                var ws = string.IsNullOrWhiteSpace(spec.Sheet) ? wb.Worksheet(1) : wb.Worksheet(spec.Sheet);

                // V12.1: ClosedXML 0.104.1 — XLWorksheet+XLChart internal, use reflection
                var chartType = spec.ChartType?.ToLowerInvariant() switch
                {
                    "bar" or "column" => ClosedXML.Excel.XLChartType.ColumnClustered,
                    "line" => ClosedXML.Excel.XLChartType.Line,
                    "pie" => ClosedXML.Excel.XLChartType.Pie,
                    "area" => ClosedXML.Excel.XLChartType.Area,
                    _ => ClosedXML.Excel.XLChartType.ColumnClustered
                };
                var chartObj = typeof(ClosedXML.Excel.IXLChart).Assembly.GetType("ClosedXML.Excel.XLChart");
                // XLWorksheet is internal → get Charts via reflection
                var chartsProp = ws.GetType().GetProperty("Charts",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (chartObj != null && chartsProp?.GetValue(ws) is ClosedXML.Excel.IXLCharts charts)
                {
                    var chart = (ClosedXML.Excel.IXLChart)Activator.CreateInstance(chartObj)!;
                    chart.SetChartType(chartType);
                    charts.Add(chart);
                }
                wb.SaveAs(output);

                if (json)
                {
                    var o = JsonOutput.Ok("excel chart", $"Chart ({spec.ChartType}) rendered",
                        new { output = Path.GetFullPath(output), type = spec.ChartType });
                    o.Artifacts["xlsx"] = output;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else Console.WriteLine($"Chart ({spec.ChartType}) -> {output}");
            }
            catch (Exception ex) { CliHelpers.WriteError("excel chart", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, specArg, outOpt, jsonOpt);
        return cmd;
    }

    sealed class ChartSpec
    {
        public string? Sheet { get; set; }
        public string DataRange { get; set; } = "A1:B10";
        public string? ChartType { get; set; }
        public string? Title { get; set; }
        public string? Legend { get; set; }
    }

    // ===== excel pivot =====

    static Command CreatePivot(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file with source data");
        var specArg = new Argument<string>("spec", "Path to pivot spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path (required)") { IsRequired = true };
        var cmd = new Command("pivot", "Create a pivot table from a JSON spec. Required: -o <output.xlsx>.") { fileArg, specArg, outOpt };

        cmd.SetHandler((string file, string spec, string output, bool json) =>
        {
            var err = ValidateXlsx(file);
            if (err != null) { CliHelpers.WriteError("excel pivot", err, json); return; }
            var serr = CliHelpers.ValidateTextFile(spec);
            if (serr != null) { CliHelpers.WriteError("excel pivot", serr, json); return; }

            try
            {
                var jsonText = File.ReadAllText(spec);
                var pSpec = JsonSerializer.Deserialize<ExcelPivotSpec>(jsonText, CliHelpers.JsonOpts);
                if (pSpec == null || string.IsNullOrEmpty(pSpec.Sheet) || string.IsNullOrEmpty(pSpec.Range))
                { CliHelpers.WriteError("excel pivot", ErrorCodes.ValidationFailed with { Message = "sheet and range are required." }, json); return; }

                CliHelpers.EnsureParentDir(output);
                File.Copy(file, output, true);
                var (_, elapsed) = CliHelpers.Time<int>(() =>
                {
                    using var wb = new XLWorkbook(output);
                    var ws = wb.Worksheet(pSpec.Sheet);
                    var range = ws.Range(pSpec.Range);
                    var pivotSheet = !string.IsNullOrEmpty(pSpec.PivotSheet) ? wb.Worksheets.Add(pSpec.PivotSheet) : wb.Worksheets.Add("Pivot");
                    var builder = pivotSheet.CreatePivotTable(pSpec.PivotSheet ?? "PivotTable", pivotSheet.Cell("A1"), range);

                    if (pSpec.RowLabels != null)
                        foreach (var r in pSpec.RowLabels) builder.RowLabel(r);
                    if (pSpec.ColumnLabels != null)
                        foreach (var c in pSpec.ColumnLabels) builder.ColumnLabel(c);
                    if (pSpec.Values != null)
                        foreach (var v in pSpec.Values) builder.Value(v.Field ?? "", ParseSummary(v.Summary));
                    if (pSpec.ShowGrandTotals != null)
                        builder.ShowGrandTotals(pSpec.ShowGrandTotals.Value);

                    wb.Save();
                    return 1;
                });

                if (json)
                {
                    var o = JsonOutput.Ok("excel pivot", $"Pivot table created on sheet '{pSpec.PivotSheet ?? "Pivot"}'");
                    o.Artifacts["xlsx"] = Path.GetFullPath(output);
                    o.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
                }
                else { Console.WriteLine($"Pivot created: {Path.GetFullPath(output)}"); }
            }
            catch (JsonException jex) { CliHelpers.WriteError("excel pivot", ErrorCodes.ValidationFailed with { Message = $"Invalid JSON: {jex.Message}" }, json); }
            catch (Exception ex) { CliHelpers.WriteError("excel pivot", ErrorCodes.InternalError with { Message = ex.Message }, json); }
        }, fileArg, specArg, outOpt, jsonOpt);
        return cmd;
    }

    static XLPivotSummary ParseSummary(string? summary) => (summary ?? "sum").ToLowerInvariant() switch
    {
        "count" => XLPivotSummary.Count, "average" or "avg" => XLPivotSummary.Average,
        "min" => XLPivotSummary.Minimum, "max" => XLPivotSummary.Maximum,
        _ => XLPivotSummary.Sum
    };

    static string? ValidateRestructureSpec(ExcelRestructureSpec? spec)
    {
        if (spec == null) return "Spec is required.";
        if ((spec.WeeklySources == null || spec.WeeklySources.Count == 0) &&
            (spec.LegacySources == null || spec.LegacySources.Count == 0))
            return "At least one weeklySources or legacySources entry is required.";
        if (spec.Metrics == null || spec.Metrics.Count == 0)
            return "metrics array must be non-empty.";
        if (spec.Blocks == null || spec.Blocks.Count == 0)
            return "blocks array must be non-empty.";

        var metricKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in spec.Metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.Key))
                return "Each metric must define key.";
            if (string.IsNullOrWhiteSpace(metric.Title))
                return $"Metric '{metric.Key}' must define title.";
            if (!metricKeys.Add(metric.Key))
                return $"Duplicate metric key: {metric.Key}";
        }

        foreach (var block in spec.Blocks)
        {
            if (block.HeaderRow < 1)
                return "Each block.headerRow must be >= 1.";
            if (block.MetricRows == null || block.MetricRows.Count == 0)
                return $"Block headerRow={block.HeaderRow} must define metricRows.";

            foreach (var metric in spec.Metrics)
            {
                if (!block.MetricRows.Keys.Any(k => string.Equals(k, metric.Key, StringComparison.OrdinalIgnoreCase)))
                    return $"Block headerRow={block.HeaderRow} is missing metricRows entry for '{metric.Key}'.";
            }
        }

        foreach (var source in spec.WeeklySources ?? new List<ExcelWeeklySourceSpec>())
        {
            var err = ValidateXlsx(source.File);
            if (err != null) return err.Message;
        }

        foreach (var source in spec.LegacySources ?? new List<ExcelLegacySourceSpec>())
        {
            var err = ValidateXlsx(source.File);
            if (err != null) return err.Message;
            if (string.IsNullOrWhiteSpace(source.Treatment))
                return $"Legacy source '{source.File}' must define treatment.";
            if (string.IsNullOrWhiteSpace(source.ReplicateColumn))
                return $"Legacy source '{source.File}' must define replicateColumn.";
        }

        return null;
    }

    static ExcelRestructureResult RestructureWorkbook(ExcelRestructureSpec spec, string output)
    {
        var metrics = spec.Metrics!;
        var records = new List<ExcelRestructureRecord>();

        foreach (var legacy in spec.LegacySources ?? new List<ExcelLegacySourceSpec>())
            records.AddRange(ReadLegacySource(spec, legacy, metrics));

        foreach (var weekly in spec.WeeklySources ?? new List<ExcelWeeklySourceSpec>())
            records.AddRange(ReadWeeklySource(spec, weekly, metrics));

        if (records.Count == 0)
            throw new InvalidDataException("No records were parsed from the provided sources.");

        var treatmentOrder = BuildTreatmentOrderMap(spec, records.Select(r => r.Treatment).Distinct(StringComparer.OrdinalIgnoreCase));
        foreach (var record in records)
            record.TreatmentOrder = treatmentOrder[record.Treatment];

        var orderedRecords = records
            .OrderBy(r => r.Week)
            .ThenBy(r => r.TreatmentOrder)
            .ThenBy(r => r.Replicate)
            .ToList();

        var outputSpec = spec.Output ?? new ExcelRestructureOutputSpec();

        using var wb = new XLWorkbook();
        WriteAllDataSheet(wb, outputSpec.AllDataSheet ?? "全部数据", orderedRecords, metrics);
        var statsRows = WriteStatsSheet(wb, outputSpec.StatsSheet ?? "统计分析", orderedRecords, metrics, treatmentOrder);
        var summaryRows = WriteSummarySheet(wb, outputSpec.SummarySheet ?? "统计分析 (2)", orderedRecords, metrics, treatmentOrder);
        wb.SaveAs(output);

        return new ExcelRestructureResult
        {
            RecordCount = orderedRecords.Count,
            StatsRowCount = statsRows,
            SummaryRowCount = summaryRows,
            SheetCount = wb.Worksheets.Count,
            TreatmentCount = treatmentOrder.Count,
            WeekCount = orderedRecords.Select(r => r.Week).Distinct().Count(),
        };
    }

    static List<ExcelRestructureRecord> ReadLegacySource(
        ExcelRestructureSpec spec,
        ExcelLegacySourceSpec source,
        IReadOnlyList<ExcelRestructureMetricSpec> metrics)
    {
        using var wb = new XLWorkbook(source.File!);
        var ws = ResolveWorksheet(wb, source.Sheet, spec.Sheet);
        var replicateColumn = ResolveColumn(ws, source.ReplicateColumn!);
        var startRow = source.DataStartRow > 0 ? source.DataStartRow : 2;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var rows = new List<ExcelRestructureRecord>();

        for (int rowNumber = startRow; rowNumber <= lastRow; rowNumber++)
        {
            var replicateText = ws.Cell(rowNumber, replicateColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(replicateText) || !TryParseFlexibleNumber(replicateText, out var replicateNumber))
                continue;

            var record = new ExcelRestructureRecord
            {
                Week = source.Week,
                SourceFile = Path.GetFileName(source.File!),
                Treatment = source.Treatment!,
                Replicate = (int)Math.Round(replicateNumber, MidpointRounding.AwayFromZero),
                Note = source.Note ?? string.Empty,
            };

            foreach (var metric in metrics)
            {
                if (source.MetricColumns != null &&
                    TryGetDictionaryValue(source.MetricColumns, metric.Key!, out var metricColumn))
                {
                    var columnNumber = ResolveColumn(ws, metricColumn);
                    record.Values[metric.Key!] = ParseNullableNumber(ws.Cell(rowNumber, columnNumber).GetString());
                }
                else
                {
                    record.Values[metric.Key!] = null;
                }
            }

            rows.Add(record);
        }

        return rows;
    }

    static List<ExcelRestructureRecord> ReadWeeklySource(
        ExcelRestructureSpec spec,
        ExcelWeeklySourceSpec source,
        IReadOnlyList<ExcelRestructureMetricSpec> metrics)
    {
        using var wb = new XLWorkbook(source.File!);
        var ws = ResolveWorksheet(wb, source.Sheet, spec.Sheet);
        var lastColumn = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var rows = new List<ExcelRestructureRecord>();

        foreach (var block in spec.Blocks!)
        {
            for (int columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
            {
                var label = ws.Cell(block.HeaderRow, columnNumber).GetString().Trim();
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var treatmentInfo = ParseTreatmentLabel(label, spec);
                var record = new ExcelRestructureRecord
                {
                    Week = source.Week,
                    SourceFile = Path.GetFileName(source.File!),
                    Treatment = treatmentInfo.Treatment,
                    Replicate = treatmentInfo.Replicate,
                    Note = source.Note ?? string.Empty,
                };

                foreach (var metric in metrics)
                {
                    if (!TryGetDictionaryValue(block.MetricRows!, metric.Key!, out var metricRow))
                        throw new InvalidDataException($"Block headerRow={block.HeaderRow} does not define metric row for '{metric.Key}'.");

                    record.Values[metric.Key!] = ParseNullableNumber(ws.Cell(metricRow, columnNumber).GetString());
                }

                rows.Add(record);
            }
        }

        return rows;
    }

    static IXLWorksheet ResolveWorksheet(XLWorkbook workbook, string? primarySheet, string? fallbackSheet)
    {
        var sheetName = !string.IsNullOrWhiteSpace(primarySheet) ? primarySheet : fallbackSheet;
        if (string.IsNullOrWhiteSpace(sheetName))
            return workbook.Worksheet(1);

        var sheet = workbook.Worksheets.FirstOrDefault(ws => string.Equals(ws.Name, sheetName, StringComparison.OrdinalIgnoreCase));
        if (sheet == null)
            throw new InvalidDataException($"Worksheet not found: {sheetName}");
        return sheet;
    }

    static ExcelTreatmentInfo ParseTreatmentLabel(string label, ExcelRestructureSpec spec)
    {
        var pattern = string.IsNullOrWhiteSpace(spec.TreatmentPattern)
            ? @"^(?<code>[A-Za-z]+)(?<rep>\d+)$"
            : spec.TreatmentPattern!;

        var match = Regex.Match(label, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
            throw new InvalidDataException($"Treatment label '{label}' does not match pattern '{pattern}'.");

        var rawCode = match.Groups["code"].Value;
        var mappedCode = TryGetDictionaryValue(spec.TreatmentMap, rawCode, out var mapped)
            ? mapped
            : rawCode.ToUpperInvariant();

        if (!int.TryParse(match.Groups["rep"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var replicate))
            throw new InvalidDataException($"Treatment label '{label}' has invalid replicate suffix.");

        return new ExcelTreatmentInfo(mappedCode, replicate);
    }

    static Dictionary<string, int> BuildTreatmentOrderMap(ExcelRestructureSpec spec, IEnumerable<string> discoveredTreatments)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        foreach (var treatment in spec.TreatmentOrder ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(treatment) || map.ContainsKey(treatment))
                continue;
            map[treatment] = index++;
        }

        foreach (var treatment in discoveredTreatments)
        {
            if (string.IsNullOrWhiteSpace(treatment) || map.ContainsKey(treatment))
                continue;
            map[treatment] = index++;
        }

        return map;
    }

    static void WriteAllDataSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<ExcelRestructureRecord> records,
        IReadOnlyList<ExcelRestructureMetricSpec> metrics)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        var headers = new List<string> { "周次", "来源文件", "处理", "处理序号", "重复" };
        headers.AddRange(metrics.Select(m => m.Title!));
        headers.Add("备注");

        for (int column = 0; column < headers.Count; column++)
            ws.Cell(1, column + 1).Value = headers[column];

        int rowNumber = 2;
        foreach (var record in records)
        {
            int column = 1;
            ws.Cell(rowNumber, column++).Value = record.Week;
            ws.Cell(rowNumber, column++).Value = record.SourceFile;
            ws.Cell(rowNumber, column++).Value = record.Treatment;
            ws.Cell(rowNumber, column++).Value = record.TreatmentOrder;
            ws.Cell(rowNumber, column++).Value = record.Replicate;

            foreach (var metric in metrics)
            {
                var value = record.Values.TryGetValue(metric.Key!, out var metricValue) ? metricValue : null;
                var cell = ws.Cell(rowNumber, column++);
                if (value.HasValue)
                    cell.Value = value.Value;
            }

            ws.Cell(rowNumber, column).Value = record.Note;
            rowNumber++;
        }

        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 20;
        ws.Column(3).Width = 10;
        ws.Column(4).Width = 10;
        ws.Column(5).Width = 8;
        for (int i = 0; i < metrics.Count; i++)
        {
            var metricColumn = 6 + i;
            ws.Column(metricColumn).Width = 12;
            ws.Column(metricColumn).Style.NumberFormat.Format = BuildNumberFormat(metrics[i].Decimals);
        }
        ws.Column(headers.Count).Width = 28;

        StylePresets.MonoHeader(ws.Row(1), 1, headers.Count);
        if (rowNumber > 2)
            StylePresets.AlternatingRows(ws, 1, rowNumber - 1, 1, headers.Count, "#F5F5F5");
    }

    static int WriteStatsSheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<ExcelRestructureRecord> records,
        IReadOnlyList<ExcelRestructureMetricSpec> metrics,
        IReadOnlyDictionary<string, int> treatmentOrder)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        var headers = new[]
        {
            "周次", "指标", "处理", "处理序号", "个案数", "平均值", "标准差",
            "标准误", "95%CI下限", "95%CI上限", "最小值", "最大值",
        };

        for (int column = 0; column < headers.Length; column++)
            ws.Cell(1, column + 1).Value = headers[column];

        int rowNumber = 2;
        foreach (var week in records.Select(r => r.Week).Distinct().OrderBy(w => w))
        {
            foreach (var metric in metrics)
            {
                foreach (var treatment in treatmentOrder.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key))
                {
                    var values = records
                        .Where(r => r.Week == week && string.Equals(r.Treatment, treatment, StringComparison.OrdinalIgnoreCase))
                        .Select(r => r.Values.TryGetValue(metric.Key!, out var value) ? value : null)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToList();

                    if (values.Count == 0)
                        continue;

                    var stats = ComputeStats(values);
                    ws.Cell(rowNumber, 1).Value = week;
                    ws.Cell(rowNumber, 2).Value = metric.Title;
                    ws.Cell(rowNumber, 3).Value = treatment;
                    ws.Cell(rowNumber, 4).Value = treatmentOrder[treatment];
                    ws.Cell(rowNumber, 5).Value = stats.Count;
                    ws.Cell(rowNumber, 6).Value = stats.Mean;
                    ws.Cell(rowNumber, 7).Value = stats.Sd;
                    ws.Cell(rowNumber, 8).Value = stats.Se;
                    ws.Cell(rowNumber, 9).Value = stats.Lower;
                    ws.Cell(rowNumber, 10).Value = stats.Upper;
                    ws.Cell(rowNumber, 11).Value = stats.Min;
                    ws.Cell(rowNumber, 12).Value = stats.Max;
                    rowNumber++;
                }
            }
        }

        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Columns(1, 12).AdjustToContents();
        ws.Range($"F2:L{Math.Max(rowNumber - 1, 2)}").Style.NumberFormat.Format = "0.000000";
        StylePresets.MonoHeader(ws.Row(1), 1, headers.Length);
        if (rowNumber > 2)
            StylePresets.AlternatingRows(ws, 1, rowNumber - 1, 1, headers.Length, "#F5F5F5");

        return rowNumber - 2;
    }

    static int WriteSummarySheet(
        XLWorkbook workbook,
        string sheetName,
        IReadOnlyList<ExcelRestructureRecord> records,
        IReadOnlyList<ExcelRestructureMetricSpec> metrics,
        IReadOnlyDictionary<string, int> treatmentOrder)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        var headers = new List<string> { "周次", "处理", "处理序号", "个案数" };
        headers.AddRange(metrics.Select(m => $"{m.Title}(均值+/-SD)"));

        for (int column = 0; column < headers.Count; column++)
            ws.Cell(1, column + 1).Value = headers[column];

        int rowNumber = 2;
        foreach (var week in records.Select(r => r.Week).Distinct().OrderBy(w => w))
        {
            foreach (var treatment in treatmentOrder.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key))
            {
                var group = records
                    .Where(r => r.Week == week && string.Equals(r.Treatment, treatment, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (group.Count == 0)
                    continue;

                ws.Cell(rowNumber, 1).Value = week;
                ws.Cell(rowNumber, 2).Value = treatment;
                ws.Cell(rowNumber, 3).Value = treatmentOrder[treatment];
                ws.Cell(rowNumber, 4).Value = group.Count;

                for (int i = 0; i < metrics.Count; i++)
                {
                    var metric = metrics[i];
                    var values = group
                        .Select(r => r.Values.TryGetValue(metric.Key!, out var value) ? value : null)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToList();

                    ws.Cell(rowNumber, 5 + i).Value = values.Count == 0
                        ? string.Empty
                        : FormatMeanSd(ComputeStats(values), metric.Decimals);
                }

                rowNumber++;
            }
        }

        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Columns(1, headers.Count).AdjustToContents();
        StylePresets.FinanceHeader(ws.Row(1), 1, headers.Count);
        if (rowNumber > 2)
            StylePresets.AlternatingRows(ws, 1, rowNumber - 1, 1, headers.Count, "#FFF3E0");

        return rowNumber - 2;
    }

    static ExcelDescriptiveStats ComputeStats(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        var min = values.Min();
        var max = values.Max();
        var sd = 0.0;
        var se = 0.0;
        var lower = mean;
        var upper = mean;

        if (values.Count > 1)
        {
            var sumSquares = values.Sum(value => Math.Pow(value - mean, 2));
            sd = Math.Sqrt(sumSquares / (values.Count - 1));
            se = sd / Math.Sqrt(values.Count);
            var halfWidth = GetTCritical(values.Count) * se;
            lower = mean - halfWidth;
            upper = mean + halfWidth;
        }

        return new ExcelDescriptiveStats(values.Count, mean, sd, se, lower, upper, min, max);
    }

    static double GetTCritical(int count) => count switch
    {
        2 => 12.7062047364,
        3 => 4.3026527299,
        4 => 3.1824463053,
        5 => 2.7764451052,
        6 => 2.5705818366,
        7 => 2.4469118511,
        8 => 2.3646242510,
        9 => 2.3060041352,
        10 => 2.2621571629,
        _ => 1.96,
    };

    static string FormatMeanSd(ExcelDescriptiveStats stats, int? decimals)
    {
        var precision = Math.Max(decimals ?? 2, 0);
        return string.Format(CultureInfo.InvariantCulture, $"{{0:F{precision}}} +/- {{1:F{precision}}}", stats.Mean, stats.Sd);
    }

    static string BuildNumberFormat(int? decimals)
    {
        var precision = Math.Max(decimals ?? 2, 0);
        return precision == 0 ? "0" : "0." + new string('0', precision);
    }

    static double? ParseNullableNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return TryParseFlexibleNumber(text, out var value) ? value : null;
    }

    static bool TryParseFlexibleNumber(string text, out double value)
    {
        value = 0;
        var normalized = text.Trim()
            .Replace("cm", "", StringComparison.OrdinalIgnoreCase)
            .Replace("mm", "", StringComparison.OrdinalIgnoreCase)
            .Replace("。", ".", StringComparison.Ordinal)
            .Replace("，", ",", StringComparison.Ordinal);

        if (Regex.IsMatch(normalized, @"^\-?\d+,\d+$"))
            normalized = normalized.Replace(",", ".", StringComparison.Ordinal);
        else
            normalized = normalized.Replace(",", "", StringComparison.Ordinal);

        normalized = Regex.Replace(normalized, @"[^\d\.\-]", "");
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    static bool TryGetDictionaryValue<TValue>(IDictionary<string, TValue>? dictionary, string key, out TValue value)
    {
        if (dictionary != null)
        {
            foreach (var item in dictionary)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }
        }

        value = default!;
        return false;
    }

    static ErrorEntry? ValidateXlsx(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return ErrorCodes.MissingArgument with { Message = "File path is required." };
        if (!File.Exists(path)) return ErrorCodes.FileNotFound with { Message = $"File not found: {path}" };
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".xlsm") return ErrorCodes.UnsupportedFormat with { Message = $"Expected .xlsx file, got: {ext}" };
        return null;
    }

    static int ResolveColumn(IXLWorksheet ws, string col)
    {
        if (int.TryParse(col, out var n) && n > 0) return n;
        if (col.All(char.IsLetter))
        {
            int result = 0;
            foreach (var c in col)
                result = result * 26 + (char.ToUpper(c) - 'A' + 1);
            return result;
        }
        // Try header row match
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (int c = 1; c <= lastCol; c++)
        {
            if (string.Equals(ws.Cell(1, c).GetString().Trim(), col, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return -1; // not found — caller must validate
    }

    static string ColToRef(int col)
    {
        if (col < 1) return "?";
        var sb = new System.Text.StringBuilder();
        while (col > 0)
        {
            col--;
            sb.Insert(0, (char)('A' + col % 26));
            col /= 26;
        }
        return sb.ToString();
    }

    // ===== excel create =====

    static Command CreateCreateXlsx(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path (required)") { IsRequired = true };
        var cmd = new Command("create", "Create xlsx from JSON spec. Required: -o <output.xlsx>.") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
        {
            var err = CliHelpers.ValidateTextFile(file);
            if (err != null) { CliHelpers.WriteError("excel create", err, json); return; }

            try
            {
                var jsonText = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<ExcelCreateSpec>(jsonText, CliHelpers.JsonOpts);
                if (spec?.Sheets == null || spec.Sheets.Count == 0)
                {
                    CliHelpers.WriteError("excel create",
                        ErrorCodes.ValidationFailed with { Message = "sheets array must be non-empty." }, json);
                    return;
                }

                // Validate each sheet
                foreach (var sheet in spec.Sheets)
                {
                    if (string.IsNullOrWhiteSpace(sheet.Name))
                    {
                        CliHelpers.WriteError("excel create",
                            ErrorCodes.ValidationFailed with { Message = "Each sheet must have a name." }, json);
                        return;
                    }
                    if (sheet.Name!.Length > 31)
                    {
                        CliHelpers.WriteError("excel create",
                            ErrorCodes.ValidationFailed with { Message = $"Sheet name '{sheet.Name}' exceeds 31 characters." }, json);
                        return;
                    }
                    if (sheet.Headers == null)
                    {
                        CliHelpers.WriteError("excel create",
                            ErrorCodes.ValidationFailed with { Message = $"Sheet '{sheet.Name}': headers is required." }, json);
                        return;
                    }
                    if (sheet.Rows == null)
                    {
                        CliHelpers.WriteError("excel create",
                            ErrorCodes.ValidationFailed with { Message = $"Sheet '{sheet.Name}': rows is required." }, json);
                        return;
                    }
                }

                CliHelpers.EnsureParentDir(output);
                int sheetCount = spec.Sheets.Count;
                var (totalRows, elapsed) = CliHelpers.Time(() =>
                {
                    using var wb = new XLWorkbook();
                    int rowCount = 0;

                    foreach (var sheet in spec.Sheets)
                    {
                        var ws = wb.Worksheets.Add(sheet.Name!);

                        // Write headers
                        for (int c = 0; c < sheet.Headers!.Count; c++)
                        {
                            ws.Cell(1, c + 1).Value = sheet.Headers[c] ?? "";
                        }

                        // Write data rows
                        for (int r = 0; r < sheet.Rows!.Count; r++)
                        {
                            var row = sheet.Rows[r];
                            for (int c = 0; c < row.Count && c < sheet.Headers.Count; c++)
                            {
                                var cell = ws.Cell(r + 2, c + 1);
                                var val = row[c];
                                if (val is JsonElement je)
                                {
                                    switch (je.ValueKind)
                                    {
                                        case JsonValueKind.Number:
                                            cell.Value = je.GetDouble();
                                            break;
                                        case JsonValueKind.True:
                                            cell.Value = true;
                                            break;
                                        case JsonValueKind.False:
                                            cell.Value = false;
                                            break;
                                        case JsonValueKind.Null:
                                            cell.Value = "";
                                            break;
                                        default:
                                            cell.Value = je.ToString();
                                            break;
                                    }
                                }
                                else if (val is string s)
                                {
                                    cell.Value = s;
                                }
                                else if (val != null)
                                {
                                    cell.Value = val.ToString();
                                }
                                else
                                {
                                    cell.Value = "";
                                }
                            }
                            rowCount++;
                        }

                        // Apply column widths
                        if (sheet.ColumnWidths != null)
                        {
                            for (int c = 0; c < sheet.ColumnWidths.Count && c < sheet.Headers.Count; c++)
                            {
                                if (sheet.ColumnWidths[c] > 0)
                                    ws.Column(c + 1).Width = sheet.ColumnWidths[c];
                            }
                        }

                        // Apply freeze panes
                        if (sheet.FreezeRow.HasValue || sheet.FreezeCol.HasValue)
                        {
                            ws.SheetView.FreezeRows(sheet.FreezeRow ?? 0);
                            ws.SheetView.FreezeColumns(sheet.FreezeCol ?? 0);
                        }

                        // Apply data validation
                        if (sheet.Validations != null)
                        {
                            foreach (var v in sheet.Validations)
                            {
                                if (string.IsNullOrWhiteSpace(v.Range)) continue;
                                var range = ws.Range(v.Range);
                                var dv = range.CreateDataValidation();
                                dv.IgnoreBlanks = true;
                                dv.InCellDropdown = true;
                                if (v.List != null && v.List.Count > 0)
                                {
                                    dv.List(string.Join(",", v.List));
                                    dv.ErrorStyle = XLErrorStyle.Warning;
                                }
                                else if (v.Type == "whole" && v.Min.HasValue && v.Max.HasValue)
                                {
                                    dv.MinValue = v.Min.Value.ToString();
                                    dv.MaxValue = v.Max.Value.ToString();
                                    dv.AllowedValues = XLAllowedValues.WholeNumber;
                                }
                                else if (v.Type == "decimal" && v.Min.HasValue && v.Max.HasValue)
                                {
                                    dv.MinValue = v.Min.Value.ToString();
                                    dv.MaxValue = v.Max.Value.ToString();
                                    dv.AllowedValues = XLAllowedValues.Decimal;
                                }
                            }
                        }
                    }

                    wb.SaveAs(output);
                    return rowCount;
                });

                var aerr = CliHelpers.CheckArtifact(output, "XLSX");
                if (aerr != null) { CliHelpers.WriteError("excel create", aerr, json); return; }

                if (json)
                {
                    var outputJson = JsonOutput.Ok("excel create",
                        $"Excel created: {output}",
                        new { sheets = sheetCount, rows = totalRows });
                    outputJson.Artifacts["xlsx"] = Path.GetFullPath(output);
                    outputJson.Meta.DurationMs = elapsed;
                    Console.WriteLine(JsonSerializer.Serialize(outputJson, CliHelpers.JsonOpts));
                }
                else
                {
                    Console.WriteLine($"Excel created: {Path.GetFullPath(output)}");
                }
            }
            catch (JsonException jex)
            {
                CliHelpers.WriteError("excel create",
                    ErrorCodes.ValidationFailed with { Message = $"Invalid JSON spec: {jex.Message}" }, json);
            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("excel create",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ════════════════════════════════════════════════════════════
    // excel db — unified ingestion via IngestionContext
    // ════════════════════════════════════════════════════════════

    static Command CreateDbImport(Option<bool> jsonOpt)
    {
        var sliceArg = new Argument<string>("slice-dir", "Directory from excel dissect");
        var xlsxArg = new Argument<string>("xlsx", "Original .xlsx file");
        var cmd = new Command("db-import", "Import excel dissect output into NongDb (unified ingestion)") { sliceArg, xlsxArg };
        cmd.SetHandler((string dir, string xlsx, bool json) =>
        {
            if (!Directory.Exists(dir)) { CliHelpers.WriteError("excel db-import", ErrorCodes.FileNotFound with { Message = $"Directory not found: {dir}" }, json); return; }
            if (!File.Exists(xlsx)) { CliHelpers.WriteError("excel db-import", ErrorCodes.FileNotFound with { Message = $"File not found: {xlsx}" }, json); return; }

            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var result = ctx.IngestSlice(xlsx, dir, "excel", "db-import");

            var shaShort = result.Sha256[..12];
            var dbPath = Path.Combine(Angri450.Nong.NongWorkplace.Cache, "nong.db");

            var o = JsonOutput.Ok("excel db-import", $"Imported: {result.Blocks} blocks, {result.Images} images", new
            {
                documentId = result.DocumentId, result.FileName, result.Format, sha = shaShort,
                result.Blocks, result.Images,
                result.HasFormat,
                dbFile = dbPath,
                runId = result.RunId
            });
            o.Metrics["blocks"] = result.Blocks; o.Metrics["images"] = result.Images;
            if (json) Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
            else Console.WriteLine($"Imported {result.FileName}: {result.Blocks} blocks, {result.Images} images -> nong.db");
        }, sliceArg, xlsxArg, jsonOpt);
        return cmd;
    }

    static Command CreateDbList(Option<bool> jsonOpt)
    {
        var cmd = new Command("db-list", "List documents in NongDb");
        cmd.SetHandler((bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var docs = ctx.QueryDocuments();
            var o = JsonOutput.Ok("excel db-list", $"{docs.Count} documents", new
            {
                count = docs.Count,
                items = docs.Select(d => new { id = d.Id.ToString(), d.FileName, d.Format, d.FileSize, sha = d.Sha256.Length >= 12 ? d.Sha256[..12] : d.Sha256, d.RegisteredAt })
            });
            o.Metrics["documents"] = docs.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, jsonOpt);
        return cmd;
    }

    static Command CreateDbBlocks(Option<bool> jsonOpt)
    {
        var idArg = new Argument<string>("document-id", "Document ID from db-list");
        var typeArg = new Option<string?>("--type", "Block type filter: paragraph, heading, table, image");
        var limitArg = new Option<int>("--limit", () => 50);
        var cmd = new Command("db-blocks", "List blocks for a document") { idArg, typeArg, limitArg };
        cmd.SetHandler((string id, string? type, int limit, bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var blocks = ctx.QueryBlocks(id, type, limit);

            var o = JsonOutput.Ok("excel db-blocks", $"{blocks.Count} blocks", new
            {
                count = blocks.Count,
                items = blocks.Select(b => new { id = b.Id.ToString(), b.BlockId, b.BlockType, text = b.Text?.Length > 200 ? b.Text[..197] + "..." : b.Text, b.Index })
            });
            o.Metrics["blocks"] = blocks.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, idArg, typeArg, limitArg, jsonOpt);
        return cmd;
    }

    static Command CreateDbImages(Option<bool> jsonOpt)
    {
        var idArg = new Argument<string>("document-id", "Document ID from db-list");
        var cmd = new Command("db-images", "List extracted images for a document") { idArg };
        cmd.SetHandler((string id, bool json) =>
        {
            using var ctx = new Angri450.Nong.Data.IngestionContext();
            var images = ctx.QueryAssets(id, "image/");

            var o = JsonOutput.Ok("excel db-images", $"{images.Count} images", new
            {
                count = images.Count,
                items = images.Select(i => new { id = i.Id.ToString(), i.FileName, i.MimeType, i.Width, i.Height, i.Usage, dataSize = i.Data?.Length ?? 0 })
            });
            o.Metrics["images"] = images.Count;
            Console.WriteLine(JsonSerializer.Serialize(o, CliHelpers.JsonOpts));
        }, idArg, jsonOpt);
        return cmd;
    }
}

// === JSON spec model for excel restructure ===

internal sealed class ExcelRestructureSpec
{
    public string? Sheet { get; set; }
    public string? TreatmentPattern { get; set; }
    public Dictionary<string, string>? TreatmentMap { get; set; } = new();
    public List<string>? TreatmentOrder { get; set; } = new();
    public List<ExcelRestructureMetricSpec>? Metrics { get; set; } = new();
    public List<ExcelRestructureBlockSpec>? Blocks { get; set; } = new();
    public List<ExcelWeeklySourceSpec>? WeeklySources { get; set; } = new();
    public List<ExcelLegacySourceSpec>? LegacySources { get; set; } = new();
    public ExcelRestructureOutputSpec? Output { get; set; } = new();
}

internal sealed class ExcelRestructureMetricSpec
{
    public string? Key { get; set; }
    public string? Title { get; set; }
    public int? Decimals { get; set; }
}

internal sealed class ExcelRestructureBlockSpec
{
    public int HeaderRow { get; set; }
    public Dictionary<string, int>? MetricRows { get; set; } = new();
}

internal sealed class ExcelWeeklySourceSpec
{
    public string? File { get; set; }
    public string? Sheet { get; set; }
    public int Week { get; set; }
    public string? Note { get; set; }
}

internal sealed class ExcelLegacySourceSpec
{
    public string? File { get; set; }
    public string? Sheet { get; set; }
    public int Week { get; set; }
    public string? Treatment { get; set; }
    public string? ReplicateColumn { get; set; }
    public Dictionary<string, string>? MetricColumns { get; set; } = new();
    public int DataStartRow { get; set; } = 2;
    public string? Note { get; set; }
}

internal sealed class ExcelRestructureOutputSpec
{
    public string? AllDataSheet { get; set; } = "全部数据";
    public string? StatsSheet { get; set; } = "统计分析";
    public string? SummarySheet { get; set; } = "统计分析 (2)";
}

internal sealed class ExcelRestructureResult
{
    public int RecordCount { get; set; }
    public int StatsRowCount { get; set; }
    public int SummaryRowCount { get; set; }
    public int SheetCount { get; set; }
    public int TreatmentCount { get; set; }
    public int WeekCount { get; set; }
}

internal sealed class ExcelRestructureRecord
{
    public int Week { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public int TreatmentOrder { get; set; }
    public int Replicate { get; set; }
    public Dictionary<string, double?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Note { get; set; } = string.Empty;
}

internal sealed record ExcelTreatmentInfo(string Treatment, int Replicate);

internal sealed record ExcelDescriptiveStats(
    int Count,
    double Mean,
    double Sd,
    double Se,
    double Lower,
    double Upper,
    double Min,
    double Max);

// === JSON spec model for excel create ===

public class ExcelCreateSpec
{
    public List<ExcelSheetEntry> Sheets { get; set; } = new();
}

public class ExcelSheetEntry
{
    public string? Name { get; set; }
    public List<string?> Headers { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public List<double>? ColumnWidths { get; set; }
    public int? FreezeRow { get; set; }
    public int? FreezeCol { get; set; }
    public List<ExcelValidationRule>? Validations { get; set; }
}

public class ExcelValidationRule
{
    [System.Text.Json.Serialization.JsonPropertyName("range")]
    public string? Range { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("list")]
    public List<string> List { get; set; } = new();
    [System.Text.Json.Serialization.JsonPropertyName("min")]
    public double? Min { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("max")]
    public double? Max { get; set; }
}

// === JSON spec model for excel style ===

internal class ExcelStyleSpec
{
    public string? Sheet { get; set; }
    public List<ExcelStyleEntry> Entries { get; set; } = new();
}

internal class ExcelStyleEntry
{
    public string? Range { get; set; }
    public string? Font { get; set; }
    public double? FontSize { get; set; }
    public bool? Bold { get; set; }
    public string? FillColor { get; set; }
    public string? FontColor { get; set; }
    public string? NumberFormat { get; set; }
    public string? Preset { get; set; } // "Academic" or "Finance"
}

// === JSON spec model for excel formula ===

internal class ExcelFormulaSpec
{
    public string? Sheet { get; set; }
    public List<ExcelFormulaEntry> Entries { get; set; } = new();
}

internal class ExcelFormulaEntry
{
    public string? Cell { get; set; }
    public string? Range { get; set; }
    public string? Formula { get; set; }
}

internal class ExcelPivotSpec
{
    public string? Sheet { get; set; }
    public string? PivotSheet { get; set; }
    public string? Range { get; set; }
    public List<string>? RowLabels { get; set; }
    public List<string>? ColumnLabels { get; set; }
    public List<ExcelPivotValue>? Values { get; set; }
    public bool? ShowGrandTotals { get; set; }
}

internal class ExcelPivotValue
{
    public string? Field { get; set; }
    public string? Summary { get; set; }
}

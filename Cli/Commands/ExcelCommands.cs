using System.Globalization;
using System.CommandLine;
using System.Text.Json;
using ClosedXML.Excel;
using ExcelCore;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>
/// Excel command group: sheets, read, to-groups (phase 5).
/// </summary>
public static class ExcelCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("excel", "Excel spreadsheet operations");

        cmd.AddCommand(CreateSheets(jsonOpt));
        cmd.AddCommand(CreateRead(jsonOpt));
        cmd.AddCommand(CreateToGroups(jsonOpt));
        cmd.AddCommand(CreateCreateXlsx(jsonOpt));
        cmd.AddCommand(CreateDissect(jsonOpt));
        cmd.AddCommand(CreateStyle(jsonOpt));
        cmd.AddCommand(CreateFormula(jsonOpt));
        cmd.AddCommand(CreatePivot(jsonOpt));

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
        var cmd = new Command("read", "Read xlsx content") { fileArg, sheetOpt, rangeOpt };

        cmd.SetHandler((string file, string sheet, string range, bool json) =>
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

                var rows = new List<List<string>>();
                for (int r = startRow; r <= endRow; r++)
                {
                    var row = new List<string>();
                    for (int c = startCol; c <= endCol; c++)
                        row.Add(ws.Cell(r, c).GetString());
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
                    Console.WriteLine(string.Join("\t", row));
            }

            }
            catch (Exception ex)
            {
                CliHelpers.WriteError("excel read",
                    ErrorCodes.InternalError with { Message = ex.Message }, json);
            }
        }, fileArg, sheetOpt, rangeOpt, jsonOpt);

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

    // ===== helpers =====

    static Command CreateDissect(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file");
        var outOpt = new Option<string>(new[] { "-o", "--output" }, "Output directory for NongPandoc slice") { IsRequired = true };
        var cmd = new Command("dissect", "Slice xlsx into a NongPandoc package") { fileArg, outOpt };

        cmd.SetHandler((string file, string output, bool json) =>
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
        }, fileArg, outOpt, jsonOpt);

        return cmd;
    }

    // ===== excel style =====

    static Command CreateStyle(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file to modify");
        var specArg = new Argument<string>("spec", "Path to style spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path") { IsRequired = true };
        var cmd = new Command("style", "Apply cell styles from a JSON spec") { fileArg, specArg, outOpt };

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
        var outOpt = new Option<string>("-o", "Output xlsx path") { IsRequired = true };
        var cmd = new Command("formula", "Write formulas from a JSON spec") { fileArg, specArg, outOpt };

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

    // ===== excel pivot =====

    static Command CreatePivot(Option<bool> jsonOpt)
    {
        var fileArg = new Argument<string>("file", "Path to .xlsx file with source data");
        var specArg = new Argument<string>("spec", "Path to pivot spec JSON");
        var outOpt = new Option<string>("-o", "Output xlsx path") { IsRequired = true };
        var cmd = new Command("pivot", "Create a pivot table from a JSON spec") { fileArg, specArg, outOpt };

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
        var outOpt = new Option<string>("-o", "Output xlsx path") { IsRequired = true };
        var cmd = new Command("create", "Create xlsx from JSON spec") { fileArg, outOpt };

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
}

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

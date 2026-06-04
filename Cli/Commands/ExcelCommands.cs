using System.Globalization;
using System.CommandLine;
using System.Text.Json;
using ClosedXML.Excel;
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
        if (col <= 26) return ((char)('A' + col - 1)).ToString();
        return ((char)('A' + (col - 1) / 26 - 1)).ToString() + ((char)('A' + (col - 1) % 26)).ToString();
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

internal class ExcelCreateSpec
{
    public List<ExcelSheetEntry> Sheets { get; set; } = new();
}

internal class ExcelSheetEntry
{
    public string? Name { get; set; }
    public List<string?> Headers { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
}

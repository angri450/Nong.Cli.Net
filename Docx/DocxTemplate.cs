using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocxCore;

/// <summary>
/// DOCX 模板填充引擎。用数据字典替换模板中的 {{tag}} 占位符。
/// 支持 @if/@endif 条件块、@foreach/@endforeach 循环块、表格数据绑定。
/// 移植自 MiniWord 核心逻辑，适配 DocxCore API。
/// </summary>
public static class DocxTemplate
{
    /// <summary>用数据填充模板并保存到文件。</summary>
    public static void Fill(string templatePath, string outputPath, object data)
    {
        File.Copy(templatePath, outputPath, true);
        var dict = ToDictionary(data);
        using var doc = WordprocessingDocument.Open(outputPath, true);
        FillParts(doc, dict);
        doc.Save();
    }

    /// <summary>用数据填充模板字节数组，返回结果字节数组。</summary>
    public static byte[] Fill(byte[] templateBytes, object data)
    {
        var dict = ToDictionary(data);
        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        using var doc = WordprocessingDocument.Open(ms, true);
        FillParts(doc, dict);
        doc.Save();
        return ms.ToArray();
    }

    /// <summary>用数据填充模板流，写入输出流。</summary>
    public static void Fill(Stream templateStream, Stream outputStream, object data)
    {
        var dict = ToDictionary(data);
        templateStream.CopyTo(outputStream);
        outputStream.Position = 0;
        using var doc = WordprocessingDocument.Open(outputStream, true);
        FillParts(doc, dict);
        doc.Save();
    }

    /// <summary>异步版本。</summary>
    public static async Task FillAsync(string templatePath, string outputPath, object data, CancellationToken ct = default)
    {
        await Task.Run(() => Fill(templatePath, outputPath, data), ct);
    }

    // === core ===

    static void FillParts(WordprocessingDocument doc, Dictionary<string, object?> data)
    {
        var normalized = (Dictionary<string, object?>)NormalizeJsonValue(data)!;

        var main = doc.MainDocumentPart!;
        if (main.Document.Body != null)
        {
            // 0. cellReplace — key=template cell text, value=replacement
            //    If the matched cell has a cell to its RIGHT, fills the right cell (label-value tables).
            //    Otherwise fills THIS cell (description tables, single-cell tables).
            if (normalized.TryGetValue("cellReplace", out var cellRepVal)
                && cellRepVal is Dictionary<string, object?> cellRep)
            {
                ReplaceCellsByText(main, cellRep);
                normalized.Remove("cellReplace");
            }

            // 1. tableRows — add data rows to tables with headers.
            //    JSON: { "tableRows": { "match": [ ["col0","col1"], ... ] } }
            if (normalized.TryGetValue("tableRows", out var tblVal)
                && tblVal is Dictionary<string, object?> tblRows)
            {
                AppendTableDataRows(main, tblRows);
                normalized.Remove("tableRows");
            }

            ProcessElement(main.Document.Body, normalized, main);
        }

        foreach (var hp in main.HeaderParts)
            if (hp.Header != null)
                ProcessElement(hp.Header, normalized, main);

        foreach (var fp in main.FooterParts)
            if (fp.Footer != null)
                ProcessElement(fp.Footer, normalized, main);
    }

    /// <summary>
    /// Walk every table cell, match its trimmed text against the keys in
    /// cellReplace, and replace content. If the matched cell has a cell to its
    /// right, the RIGHT cell is filled (label-value tables). Otherwise THIS cell
    /// is filled (single-cell description tables).
    /// Also removes fixed row heights so text reflow doesn't clip.
    /// </summary>
    static void ReplaceCellsByText(MainDocumentPart main, Dictionary<string, object?> cellRep)
    {
        foreach (var table in main.Document.Body!.Elements<Table>())
        {
            // Remove fixed row heights so content can reflow
            foreach (var tr in table.Elements<TableRow>())
            {
                var trPr = tr.GetFirstChild<TableRowProperties>();
                var trHeight = trPr?.GetFirstChild<TableRowHeight>();
                if (trHeight != null)
                    trHeight.Remove();
            }

            var rows = table.Elements<TableRow>().ToList();
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var cells = rows[ri].Elements<TableCell>().ToList();
                for (int ci = 0; ci < cells.Count; ci++)
                {
                    var cellText = cells[ci].InnerText.Trim();
                    if (cellText.Length == 0) continue;

                    foreach (var kv in cellRep)
                    {
                        if (cellText.StartsWith(kv.Key, StringComparison.Ordinal))
                        {
                            var newText = kv.Value?.ToString() ?? "";
                            if (newText.Length == 0) break;

                            // Label-value table: cell to the right exists → fill that
                            if (ci + 1 < cells.Count)
                                FillCellContent(cells[ci + 1], newText);
                            else
                                FillCellContent(cells[ci], newText);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Fill a table cell with text, preserving original font and size.</summary>
    static void FillCellContent(TableCell cell, string text)
    {
        // Extract font/size from first run BEFORE removing paragraphs
        var allParas = cell.Elements<Paragraph>().ToList();
        string? asciiFont = null, eaFont = null, sizeVal = null;
        var firstRun = allParas.SelectMany(p => p.Elements<Run>()).FirstOrDefault();
        if (firstRun?.RunProperties?.RunFonts is {} rf)
        {
            asciiFont = rf.Ascii?.Value ?? rf.HighAnsi?.Value;
            eaFont = rf.EastAsia?.Value;
        }
        sizeVal = firstRun?.RunProperties?.FontSize?.Val?.Value;

        // Remove all existing paragraphs
        foreach (var p in allParas)
            p.Remove();

        // Build replacement paragraph(s) — split on \n for Word line breaks
        var lines = text.Split('\n');
        var para = new Paragraph();
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                para.Append(new Run(new Break()));

            if (lines[i].Length > 0)
            {
                var rp = new RunProperties();
                if (asciiFont != null || eaFont != null)
                    rp.Append(new RunFonts { Ascii = asciiFont, HighAnsi = asciiFont, EastAsia = eaFont });
                if (sizeVal != null)
                    rp.Append(new FontSize { Val = sizeVal });
                para.Append(new Run(rp, new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve }));
            }
        }
        cell.Append(para);
    }

    /// <summary>
    /// Append data rows to tables identified by header text.
    /// JSON: { "tableRows": { "授权（登记": [ ["type","name","patent","date"] ] } }
    /// Matches first table whose InnerText contains the key. Preserves header row.
    /// </summary>
    static void AppendTableDataRows(MainDocumentPart main, Dictionary<string, object?> spec)
    {
        foreach (var table in main.Document.Body!.Elements<Table>())
        {
            string tableText = table.InnerText;
            foreach (var kv in spec)
            {
                if (!tableText.Contains(kv.Key, StringComparison.Ordinal)) continue;
                if (kv.Value is not System.Collections.IEnumerable rowList) continue;

                var rows = table.Elements<TableRow>().ToList();
                if (rows.Count < 2) continue; // need at least header + 1 data row

                var headerRow = rows[0];
                var headerCells = headerRow.Elements<TableCell>().ToList();

                // Remove existing data rows
                for (int i = rows.Count - 1; i >= 1; i--)
                    rows[i].Remove();

                foreach (var rowData in rowList)
                {
                    var cells = (rowData as System.Collections.IEnumerable)?.Cast<object>().ToList()
                                ?? new List<object>();
                    var newRow = new TableRow();
                    if (headerRow.GetFirstChild<TableRowProperties>() is {} hPr)
                        newRow.Append((TableRowProperties)hPr.CloneNode(true));

                    for (int c = 0; c < headerCells.Count; c++)
                    {
                        var newCell = (TableCell)headerCells[c].CloneNode(true);
                        var text = c < cells.Count ? cells[c]?.ToString() ?? "" : "";
                        foreach (var p in newCell.Elements<Paragraph>().ToList())
                            p.Remove();
                        var para = new Paragraph();
                        if (text.Length > 0)
                        {
                            var hRun = headerCells[c].Elements<Run>().FirstOrDefault();
                            var rp = hRun?.RunProperties?.CloneNode(true) as RunProperties ?? new RunProperties();
                            para.Append(new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
                        }
                        newCell.Append(para);
                        newRow.Append(newCell);
                    }
                    table.Append(newRow);
                }
                return; // one match = done
            }
        }
    }

    static void ProcessElement(OpenXmlElement root, Dictionary<string, object?> data, MainDocumentPart main)
    {
        // 0. Reassemble split tags
        AvoidSplitTagText(root);

        // 1. Process @foreach blocks (outermost first)
        ReplaceForeachBlocks(root, data, main);

        // 2. Process table row data binding
        GenerateTableRows(root, data, main);

        // 3. Process @if blocks
        ReplaceIfBlocks(root, data);

        // 4. Replace text tags in paragraphs
        foreach (var p in root.Descendants<Paragraph>())
            ReplaceTextInParagraph(p, data, main);
    }

    // === split tag reassembly ===

    static void AvoidSplitTagText(OpenXmlElement root)
    {
        var allTexts = root.Descendants<Text>().ToList();
        var merged = new List<(int start, int end, string full)>();

        for (int i = 0; i < allTexts.Count; i++)
        {
            var t = allTexts[i];
            if (string.IsNullOrEmpty(t.Text)) continue;
            var idx = t.Text.IndexOf("{{", StringComparison.Ordinal);
            if (idx < 0) continue;

            var sb = new System.Text.StringBuilder(t.Text);
            int j = i + 1;
            while (j < allTexts.Count && sb.Length <= 1000)
            {
                if (sb.ToString().Contains("}}")) break;
                sb.Append(allTexts[j].Text);
                j++;
            }
            if (sb.ToString().Contains("}}"))
            {
                merged.Add((i, j - 1, sb.ToString()));
                i = j - 1;
            }
        }

        foreach (var (start, end, full) in merged)
        {
            allTexts[start].Text = full;
            for (int k = start + 1; k <= end; k++)
                allTexts[k].Text = "";
        }
    }

    // === foreach blocks ===

    static readonly Regex ForeachRegex = new(@"@foreach\s*\(\s*(\w+(?:\.\w+)*)\s*\)", RegexOptions.Compiled);
    static readonly Regex EndForeachRegex = new(@"@endforeach", RegexOptions.Compiled);

    static void ReplaceForeachBlocks(OpenXmlElement root, Dictionary<string, object?> data, MainDocumentPart main)
    {
        var paragraphs = root.Descendants<Paragraph>().ToList();
        for (int i = 0; i < paragraphs.Count; i++)
        {
            var text = paragraphs[i].InnerText;
            var fm = ForeachRegex.Match(text);
            if (!fm.Success) continue;

            var key = fm.Groups[1].Value;
            int endIdx = -1;
            for (int j = i + 1; j < paragraphs.Count; j++)
                if (EndForeachRegex.IsMatch(paragraphs[j].InnerText))
                { endIdx = j; break; }

            if (endIdx < 0) continue;
            if (!TryGetValue(data, key, out var list) || list is not System.Collections.IEnumerable enumerable) continue;

            var templateParas = paragraphs.Skip(i + 1).Take(endIdx - i - 1).ToList();
            var insertAfter = paragraphs[endIdx];
            foreach (var item in enumerable)
            {
                var itemDict = ToDictionary(item ?? new());
                var clones = templateParas.Select(p => (Paragraph)p.CloneNode(true)).ToList();
                foreach (var clone in clones)
                {
                    ReplaceTextInParagraph(clone, itemDict, main);
                    insertAfter = insertAfter.InsertAfterSelf(clone);
                }
            }

            // Remove template block
            for (int k = i; k <= endIdx; k++)
                paragraphs[k].Remove();
            i = endIdx;
        }
    }

    // === table row binding ===

    static void GenerateTableRows(OpenXmlElement root, Dictionary<string, object?> data, MainDocumentPart main)
    {
        foreach (var table in root.Descendants<Table>().ToList())
        {
            var rows = table.Elements<TableRow>().ToList();
            foreach (var row in rows)
            {
                var cellText = string.Join("", row.Descendants<Text>().Select(t => t.Text));
                if (!cellText.Contains("{{")) continue;

                var tags = Regex.Matches(cellText, @"\{\{(\w+(?:\.\w+)*)\}\}");
                if (tags.Count == 0) continue;

                var firstTag = tags[0].Groups[1].Value;
                var collectionKey = firstTag.Split('.')[0];
                if (!TryGetValue(data, collectionKey, out var list) || list is not System.Collections.IEnumerable enumerable) continue;

                var newRows = new List<TableRow>();
                foreach (var item in enumerable)
                {
                    var itemDict = ToDictionary(item ?? new());
                    var clone = (TableRow)row.CloneNode(true);
                    ReplaceTextInTableRow(clone, itemDict, collectionKey, main);
                    newRows.Add(clone);
                }

                row.Remove();
                foreach (var nr in newRows)
                    table.Append(nr);
            }
        }
    }

    static void ReplaceTextInTableRow(TableRow row, Dictionary<string, object?> data, string prefix, MainDocumentPart main)
    {
        foreach (var cell in row.Descendants<TableCell>())
        foreach (var p in cell.Descendants<Paragraph>())
        {
            var dict = new Dictionary<string, object?>();
            foreach (var kv in data)
                dict[$"{prefix}.{kv.Key}"] = kv.Value;
            ReplaceTextInParagraph(p, dict, main);
        }
    }

    // === if blocks ===

    static readonly Regex IfRegex = new(@"@if\s*\(\s*(\S+)\s*(==|!=|>=|<=|>|<)\s*(\S+)\s*\)", RegexOptions.Compiled);
    static readonly Regex EndIfRegex = new(@"@endif", RegexOptions.Compiled);

    static void ReplaceIfBlocks(OpenXmlElement root, Dictionary<string, object?> data)
    {
        var paragraphs = root.Descendants<Paragraph>().ToList();
        for (int i = 0; i < paragraphs.Count; i++)
        {
            var text = paragraphs[i].InnerText;
            var im = IfRegex.Match(text);
            if (!im.Success) continue;

            var left = im.Groups[1].Value;
            var op = im.Groups[2].Value;
            var right = im.Groups[3].Value;

            int endIdx = -1;
            for (int j = i + 1; j < paragraphs.Count; j++)
                if (EndIfRegex.IsMatch(paragraphs[j].InnerText))
                { endIdx = j; break; }

            if (endIdx < 0) continue;

            var result = EvaluateCondition(data, left, op, right);

            if (result)
            {
                // Keep block, remove markers
                paragraphs[i].Remove();
                paragraphs[endIdx].Remove();
            }
            else
            {
                // Remove entire block
                for (int k = i; k <= endIdx; k++)
                    paragraphs[k].Remove();
            }
            i = endIdx;
        }
    }

    static bool EvaluateCondition(Dictionary<string, object?> data, string left, string op, string right)
    {
        var leftVal = ResolveValue(data, left);
        var rightVal = ResolveValue(data, right);

        if (leftVal == null && rightVal == null) return op == "==";
        if (leftVal == null || rightVal == null) return op == "!=";

        // Try numeric comparison
        if (double.TryParse(leftVal, out var ln) && double.TryParse(rightVal, out var rn))
            return op switch { "==" => ln == rn, "!=" => ln != rn, ">" => ln > rn, "<" => ln < rn, ">=" => ln >= rn, "<=" => ln <= rn, _ => false };

        // Try DateTime
        if (DateTime.TryParse(leftVal, out var ld) && DateTime.TryParse(rightVal, out var rd))
            return op switch { "==" => ld == rd, "!=" => ld != rd, ">" => ld > rd, "<" => ld < rd, ">=" => ld >= rd, "<=" => ld <= rd, _ => false };

        // String comparison
        return op switch { "==" => leftVal == rightVal, "!=" => leftVal != rightVal, _ => false };
    }

    static string? ResolveValue(Dictionary<string, object?> data, string key)
    {
        if (TryGetValue(data, key, out var v))
            return v?.ToString();
        // Could be a literal
        return key.Trim('\'', '"');
    }

    // === text replacement ===

    static readonly Regex TagRegex = new(@"\{\{(\w+(?:\.\w+)*)\}\}", RegexOptions.Compiled);

    static void ReplaceTextInParagraph(Paragraph p, Dictionary<string, object?> data, MainDocumentPart main)
    {
        foreach (var text in p.Descendants<Text>().ToList())
        {
            if (string.IsNullOrEmpty(text.Text)) continue;
            var matches = TagRegex.Matches(text.Text);
            if (matches.Count == 0) continue;

            var result = text.Text;
            foreach (Match m in matches)
            {
                var key = m.Groups[1].Value;
                if (!TryGetValue(data, key, out var value)) continue;
                result = result.Replace(m.Value, FormatValue(value));
            }

            // Handle image/hyperlink/color special types
            if (matches.Count == 1 && TryGetValue(data, matches[0].Groups[1].Value, out var special))
            {
                if (special is MiniWordPicture pic)
                {
                    text.Text = "";
                    InsertPicture(p, pic, main);
                    continue;
                }
                if (special is MiniWordHyperLink link)
                {
                    text.Text = "";
                    InsertHyperlink(p, link, main);
                    continue;
                }
                if (special is MiniWordColorText ct)
                {
                    ReplaceWithColorText(p, text, ct);
                    continue;
                }
            }

            // Handle IEnumerable<string> as line breaks
            if (TryGetValue(data, matches[0].Groups[1].Value, out var listVal) && listVal is System.Collections.IEnumerable e && listVal is not string)
            {
                var items = e.Cast<object>().Select(o => o?.ToString() ?? "").ToList();
                if (items.Count > 0)
                {
                    var first = text;
                    first.Text = items[0];
                    // Add breaks + text for remaining items
                    OpenXmlElement insertAfter = first;
                    for (int i = 1; i < items.Count; i++)
                    {
                        var br = new Run(new Break());
                        var rt = new Run(new Text(items[i]));
                        // Insert after the parent run
                        var parent = insertAfter.Parent;
                        if (parent != null)
                        {
                            parent.Append(br);
                            parent.Append(rt);
                        }
                        insertAfter = rt;
                    }
                    continue;
                }
            }

            // Handle newlines in string values
            if (result.Contains('\n'))
            {
                var parts = result.Split('\n');
                text.Text = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var br = new Run(new Break());
                    p.Append(br);
                    if (!string.IsNullOrEmpty(parts[i]))
                        p.Append(new Run(new Text(parts[i])));
                }
            }
            else
            {
                text.Text = result;
            }
        }
    }

    static void InsertPicture(Paragraph p, MiniWordPicture pic, MainDocumentPart main)
    {
        var ext = Path.GetExtension(pic.Path ?? "").ToLowerInvariant();
        var ct = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".bmp" => "image/bmp", _ => "image/png" };
        var ip = main.AddImagePart(ct);
        if (pic.Bytes != null)
            using (var ms = new MemoryStream(pic.Bytes))
                ip.FeedData(ms);
        else if (pic.Path != null)
            using (var fs = File.OpenRead(pic.Path))
                ip.FeedData(fs);

        var rId = main.GetIdOfPart(ip);
        int w = pic.Width > 0 ? pic.Width : 200;
        int h = pic.Height > 0 ? pic.Height : 150;
        long cx = w * 9525L, cy = h * 9525L;

        var drawing = ImageEmbedder.ImageRun(rId, "img", cx, cy);
        p.Append(drawing);
    }

    static void InsertHyperlink(Paragraph p, MiniWordHyperLink link, MainDocumentPart main)
    {
        var extRel = main.AddHyperlinkRelationship(new Uri(link.Url), true);
        var hl = new Hyperlink(
            new Run(new RunProperties(
                new RunStyle { Val = "Hyperlink" },
                link.Underline != null ? new Underline { Val = link.Underline.Value } : new Underline { Val = UnderlineValues.Single },
                new Color { Val = "0563C1" }),
                new Text(link.Text)))
        { Id = extRel.Id };
        p.Append(hl);
    }

    static void ReplaceWithColorText(Paragraph p, Text original, MiniWordColorText ct)
    {
        var rpr = new RunProperties();
        if (!string.IsNullOrEmpty(ct.FontColor))
            rpr.Append(new Color { Val = ct.FontColor });
        if (!string.IsNullOrEmpty(ct.HighlightColor))
            rpr.Append(new Highlight { Val = ParseHighlight(ct.HighlightColor) });
        original.Text = "";
        p.Append(new Run(rpr, new Text(ct.Text)));
    }

    static HighlightColorValues ParseHighlight(string color) => color.ToLowerInvariant() switch
    {
        "yellow" => HighlightColorValues.Yellow, "green" => HighlightColorValues.Green,
        "cyan" => HighlightColorValues.Cyan, "magenta" => HighlightColorValues.Magenta,
        "blue" => HighlightColorValues.Blue, "red" => HighlightColorValues.Red,
        "darkblue" => HighlightColorValues.DarkBlue, "darkcyan" => HighlightColorValues.DarkCyan,
        "darkgreen" => HighlightColorValues.DarkGreen, "darkmagenta" => HighlightColorValues.DarkMagenta,
        "darkred" => HighlightColorValues.DarkRed, "darkyellow" => HighlightColorValues.DarkYellow,
        "darkgray" => HighlightColorValues.DarkGray, "lightgray" => HighlightColorValues.LightGray,
        "black" => HighlightColorValues.Black, "white" => HighlightColorValues.White,
        _ => HighlightColorValues.Yellow,
    };

    static string FormatValue(object? value) => value switch
    {
        null => "",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
        string s => s,
        _ => value.ToString() ?? "",
    };

    // === dictionary helpers ===

    static bool TryGetValue(Dictionary<string, object?> data, string key, out object? value)
    {
        value = null;
        var parts = key.Split('.');
        object? current = data;
        foreach (var part in parts)
        {
            if (current is Dictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(part, out current)) return false;
            }
            else return false;
        }
        value = current;
        return true;
    }

    static Dictionary<string, object?> ToDictionary(object data)
    {
        if (data is Dictionary<string, object?> d) return d;
        var result = new Dictionary<string, object?>();
        foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(data))
            result[prop.Name] = prop.GetValue(data);
        return result;
    }

    // === JsonElement normalization ===
    // System.Text.Json deserializes nested objects/arrays/numbers as JsonElement,
    // which doesn't implement IEnumerable and breaks TypeDescriptor-based ToDictionary.
    // NormalizeJsonValue recursively converts the JsonElement tree to native .NET types.

    static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Object => NormalizeJsonObject(je),
                JsonValueKind.Array  => NormalizeJsonArray(je),
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => NormalizeJsonNumber(je),
                JsonValueKind.True   => true,
                JsonValueKind.False  => false,
                JsonValueKind.Null   => null,
                _ => null,
            };
        }

        if (value is Dictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kv in dict)
                result[kv.Key] = NormalizeJsonValue(kv.Value);
            return result;
        }

        if (value is System.Collections.IList list)
        {
            var result = new List<object?>();
            foreach (var item in list)
                result.Add(NormalizeJsonValue(item));
            return result;
        }

        return value;  // string, bool, int, etc. already native — pass through
    }

    static Dictionary<string, object?> NormalizeJsonObject(JsonElement je)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in je.EnumerateObject())
            result[prop.Name] = NormalizeJsonValue(prop.Value);
        return result;
    }

    static List<object?> NormalizeJsonArray(JsonElement je)
    {
        var result = new List<object?>();
        foreach (var item in je.EnumerateArray())
            result.Add(NormalizeJsonValue(item));
        return result;
    }

    static object NormalizeJsonNumber(JsonElement je)
    {
        if (je.TryGetInt32(out var i)) return i;
        if (je.TryGetInt64(out var l)) return l;
        return je.GetDouble();
    }
}

// === template value types ===

/// <summary>模板图片标签。</summary>
public class MiniWordPicture
{
    public string? Path { get; set; }
    public byte[]? Bytes { get; set; }
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 150;
}

/// <summary>模板超链接标签。</summary>
public class MiniWordHyperLink
{
    public string Url { get; set; } = "";
    public string Text { get; set; } = "";
    public UnderlineValues? Underline { get; set; }
}

/// <summary>模板彩色文本标签。</summary>
public class MiniWordColorText
{
    public string Text { get; set; } = "";
    public string? FontColor { get; set; }
    public string? HighlightColor { get; set; }
}

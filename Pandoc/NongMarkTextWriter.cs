using System.Text;

namespace PandocCore;

public static class NongMarkTextWriter
{
    public static string Write(NongPandocDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        WriteMetadata(sb, document.Metadata);

        foreach (var block in document.Blocks)
        {
            WriteBlock(sb, block);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    static void WriteMetadata(StringBuilder sb, IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count == 0) return;

        sb.AppendLine("---");
        foreach (var (key, value) in metadata)
        {
            sb.Append(key).Append(": ").AppendLine(EscapeMetadataValue(value));
        }
        sb.AppendLine("---");
        sb.AppendLine();
    }

    static void WriteBlock(StringBuilder sb, NongPandocBlock block)
    {
        switch (block)
        {
            case NongHeadingBlock heading:
                sb.Append(new string('#', Math.Clamp(heading.Level, 1, 6)))
                    .Append(' ')
                    .Append(RenderInlines(heading.Inlines));
                if (!string.IsNullOrWhiteSpace(heading.Id))
                    sb.Append(" {#").Append(heading.Id).Append('}');
                sb.AppendLine();
                break;

            case NongParagraphBlock paragraph:
                sb.AppendLine(RenderInlines(paragraph.Inlines));
                break;

            case NongBlockQuoteBlock quote:
                var rendered = WriteNested(quote.Blocks).TrimEnd().Split('\n');
                foreach (var line in rendered)
                    sb.Append("> ").AppendLine(line.TrimEnd('\r'));
                break;

            case NongBulletListBlock list:
                foreach (var item in list.Items)
                    WriteListItem(sb, "-", item);
                break;

            case NongOrderedListBlock list:
                var number = list.Start;
                foreach (var item in list.Items)
                    WriteListItem(sb, number++ + ".", item);
                break;

            case NongTableBlock table:
                WriteTable(sb, table);
                break;

            case NongFigureBlock figure:
                WriteFigure(sb, figure);
                break;

            case NongReferencesBlock references:
                sb.AppendLine("::: references");
                foreach (var entry in references.Entries)
                    sb.AppendLine(entry);
                sb.AppendLine(":::");
                break;

            case NongRawBlock raw:
                sb.Append("::: raw {format=\"").Append(EscapeAttribute(raw.Format)).AppendLine("\"}");
                sb.AppendLine(raw.Text);
                sb.AppendLine(":::");
                break;

            default:
                throw new NotSupportedException($"Unsupported block type: {block.GetType().Name}");
        }
    }

    static void WriteListItem(StringBuilder sb, string marker, List<NongPandocBlock> item)
    {
        var nested = WriteNested(item).TrimEnd().Split('\n');
        if (nested.Length == 0)
        {
            sb.Append(marker).AppendLine();
            return;
        }

        sb.Append(marker).Append(' ').AppendLine(nested[0].TrimEnd('\r'));
        for (var i = 1; i < nested.Length; i++)
            sb.Append("  ").AppendLine(nested[i].TrimEnd('\r'));
    }

    static string WriteNested(IReadOnlyList<NongPandocBlock> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
            WriteBlock(sb, block);
        return sb.ToString();
    }

    static void WriteTable(StringBuilder sb, NongTableBlock table)
    {
        sb.Append("::: table");
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(table.Caption)) attrs["caption"] = table.Caption!;
        if (!string.IsNullOrWhiteSpace(table.Style)) attrs["style"] = table.Style!;
        foreach (var (key, value) in table.Attributes)
            attrs[key] = value;
        if (attrs.Count > 0)
            sb.Append(' ').Append(RenderAttributes(table.Id, attrs));
        else if (!string.IsNullOrWhiteSpace(table.Id))
            sb.Append(" {#").Append(table.Id).Append('}');
        sb.AppendLine();

        var columnCount = Math.Max(table.Headers.Count, table.Rows.Count == 0 ? 0 : table.Rows.Max(r => r.Count));
        var headers = Pad(table.Headers, columnCount);
        sb.Append("| ").AppendJoin(" | ", headers.Select(EscapeTableCell)).AppendLine(" |");
        sb.Append("| ").AppendJoin(" | ", Enumerable.Repeat("---", columnCount)).AppendLine(" |");

        foreach (var row in table.Rows)
        {
            sb.Append("| ").AppendJoin(" | ", Pad(row, columnCount).Select(EscapeTableCell)).AppendLine(" |");
        }

        sb.AppendLine(":::");
    }

    static void WriteFigure(StringBuilder sb, NongFigureBlock figure)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["src"] = figure.Source
        };
        if (!string.IsNullOrWhiteSpace(figure.Caption)) attrs["caption"] = figure.Caption!;
        if (!string.IsNullOrWhiteSpace(figure.AltText)) attrs["alt"] = figure.AltText!;
        foreach (var (key, value) in figure.Attributes)
            attrs[key] = value;

        sb.Append("::: figure ").AppendLine(RenderAttributes(figure.Id, attrs));
        sb.AppendLine(":::");
    }

    static string RenderInlines(IReadOnlyList<NongPandocInline> inlines)
    {
        var sb = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case NongTextInline text:
                    sb.Append(text.Text);
                    break;
                case NongEmphasisInline emphasis:
                    sb.Append('*').Append(RenderInlines(emphasis.Inlines)).Append('*');
                    break;
                case NongStrongInline strong:
                    sb.Append("**").Append(RenderInlines(strong.Inlines)).Append("**");
                    break;
                case NongCodeInline code:
                    sb.Append('`').Append(code.Code.Replace("`", "\\`")).Append('`');
                    break;
                case NongSuperscriptInline sup:
                    sb.Append('^').Append(RenderInlines(sup.Inlines)).Append('^');
                    break;
                case NongSubscriptInline sub:
                    sb.Append('~').Append(RenderInlines(sub.Inlines)).Append('~');
                    break;
                case NongLinkInline link:
                    sb.Append('[').Append(RenderInlines(link.Inlines)).Append("](").Append(link.Url).Append(')');
                    break;
                case NongRawInline raw:
                    sb.Append(raw.Text);
                    break;
            }
        }

        return sb.ToString();
    }

    static string RenderAttributes(string? id, IReadOnlyDictionary<string, string> attributes)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(id)) parts.Add("#" + id);
        parts.AddRange(attributes.Select(kv => $"{kv.Key}=\"{EscapeAttribute(kv.Value)}\""));
        return "{" + string.Join(' ', parts) + "}";
    }

    static string EscapeAttribute(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string EscapeMetadataValue(string value)
    {
        return value.Any(c => c is ':' or '#' or '"' or '\'' || char.IsWhiteSpace(c) && value.Length == 1)
            ? "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            : value;
    }

    static string EscapeTableCell(string value) => value.Replace("|", "\\|").ReplaceLineEndings("<br>");

    static IReadOnlyList<string> Pad(IReadOnlyList<string> values, int count)
    {
        if (values.Count >= count) return values;
        var result = new List<string>(values);
        while (result.Count < count) result.Add("");
        return result;
    }
}

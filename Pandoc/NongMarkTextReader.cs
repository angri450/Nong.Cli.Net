using System.Text.RegularExpressions;

namespace PandocCore;

public static partial class NongMarkTextReader
{
    public static NongPandocDocument Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var document = new NongPandocDocument();
        var index = ReadMetadata(lines, document.Metadata);

        while (index < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            if (TryReadFencedBlock(lines, ref index, out var fenced))
            {
                document.Blocks.Add(fenced);
                continue;
            }

            if (TryReadHeading(lines[index], out var heading))
            {
                document.Blocks.Add(heading);
                index++;
                continue;
            }

            if (TryReadPipeTable(lines, ref index, out var table))
            {
                document.Blocks.Add(table);
                continue;
            }

            if (TryReadBulletList(lines, ref index, out var bulletList))
            {
                document.Blocks.Add(bulletList);
                continue;
            }

            if (TryReadOrderedList(lines, ref index, out var orderedList))
            {
                document.Blocks.Add(orderedList);
                continue;
            }

            if (TryReadBlockQuote(lines, ref index, out var quote))
            {
                document.Blocks.Add(quote);
                continue;
            }

            document.Blocks.Add(ReadParagraph(lines, ref index));
        }

        return document;
    }

    static int ReadMetadata(IReadOnlyList<string> lines, Dictionary<string, string> metadata)
    {
        if (lines.Count == 0 || lines[0].Trim() != "---") return 0;

        var index = 1;
        while (index < lines.Count && lines[index].Trim() != "---")
        {
            var line = lines[index];
            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var key = line[..colon].Trim();
                var value = Unquote(line[(colon + 1)..].Trim());
                if (key.Length > 0) metadata[key] = value;
            }
            index++;
        }

        return index < lines.Count ? index + 1 : index;
    }

    static bool TryReadHeading(string line, out NongHeadingBlock heading)
    {
        var match = HeadingRegex().Match(line);
        if (!match.Success)
        {
            heading = new NongHeadingBlock();
            return false;
        }

        var level = match.Groups["marks"].Value.Length;
        var text = match.Groups["text"].Value.Trim();
        var id = match.Groups["id"].Success ? match.Groups["id"].Value : null;
        heading = NongHeadingBlock.FromText(level, text, id);
        return true;
    }

    static bool TryReadFencedBlock(IReadOnlyList<string> lines, ref int index, out NongPandocBlock block)
    {
        var start = lines[index].Trim();
        if (!start.StartsWith(":::", StringComparison.Ordinal))
        {
            block = NongParagraphBlock.FromText("");
            return false;
        }

        var directive = start[3..].Trim();
        var kind = directive.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var attrs = ParseAttributes(directive);
        var body = new List<string>();
        index++;
        while (index < lines.Count && lines[index].Trim() != ":::")
        {
            body.Add(lines[index]);
            index++;
        }
        if (index < lines.Count) index++;

        block = kind switch
        {
            "table" => ReadFencedTable(body, attrs),
            "figure" => new NongFigureBlock
            {
                Id = attrs.Id,
                Source = attrs.Values.GetValueOrDefault("src", ""),
                Caption = attrs.Values.GetValueOrDefault("caption"),
                AltText = attrs.Values.GetValueOrDefault("alt"),
                Attributes = WithoutKnown(attrs.Values, "src", "caption", "alt")
            },
            "references" => new NongReferencesBlock
            {
                Entries = body.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToList()
            },
            _ => new NongRawBlock
            {
                Format = kind,
                Text = string.Join(Environment.NewLine, body),
                Id = attrs.Id,
                Attributes = attrs.Values
            }
        };
        return true;
    }

    static NongTableBlock ReadFencedTable(IReadOnlyList<string> body, ParsedAttributes attrs)
    {
        var rows = body.Where(IsPipeRow).Select(ParsePipeRow).ToList();
        var headers = rows.Count > 0 ? rows[0] : new List<string>();
        var dataStart = rows.Count > 1 && IsSeparatorRow(rows[1]) ? 2 : 1;
        return new NongTableBlock
        {
            Id = attrs.Id,
            Caption = attrs.Values.GetValueOrDefault("caption"),
            Style = attrs.Values.GetValueOrDefault("style"),
            Attributes = WithoutKnown(attrs.Values, "caption", "style"),
            Headers = headers,
            Rows = rows.Skip(dataStart).ToList()
        };
    }

    static bool TryReadPipeTable(IReadOnlyList<string> lines, ref int index, out NongTableBlock table)
    {
        if (index + 1 >= lines.Count || !IsPipeRow(lines[index]) || !IsPipeRow(lines[index + 1]))
        {
            table = new NongTableBlock();
            return false;
        }

        var header = ParsePipeRow(lines[index]);
        var separator = ParsePipeRow(lines[index + 1]);
        if (!IsSeparatorRow(separator))
        {
            table = new NongTableBlock();
            return false;
        }

        index += 2;
        var rows = new List<List<string>>();
        while (index < lines.Count && IsPipeRow(lines[index]))
        {
            rows.Add(ParsePipeRow(lines[index]));
            index++;
        }

        table = new NongTableBlock { Headers = header, Rows = rows };
        return true;
    }

    static bool TryReadBulletList(IReadOnlyList<string> lines, ref int index, out NongBulletListBlock list)
    {
        var items = new List<List<NongPandocBlock>>();
        while (index < lines.Count)
        {
            var match = BulletRegex().Match(lines[index]);
            if (!match.Success) break;
            items.Add(new List<NongPandocBlock> { NongParagraphBlock.FromText(match.Groups["text"].Value.Trim()) });
            index++;
        }

        list = new NongBulletListBlock { Items = items };
        return items.Count > 0;
    }

    static bool TryReadOrderedList(IReadOnlyList<string> lines, ref int index, out NongOrderedListBlock list)
    {
        var items = new List<List<NongPandocBlock>>();
        int? start = null;
        while (index < lines.Count)
        {
            var match = OrderedRegex().Match(lines[index]);
            if (!match.Success) break;
            start ??= int.Parse(match.Groups["number"].Value);
            items.Add(new List<NongPandocBlock> { NongParagraphBlock.FromText(match.Groups["text"].Value.Trim()) });
            index++;
        }

        list = new NongOrderedListBlock { Start = start ?? 1, Items = items };
        return items.Count > 0;
    }

    static bool TryReadBlockQuote(IReadOnlyList<string> lines, ref int index, out NongBlockQuoteBlock quote)
    {
        var quoted = new List<string>();
        while (index < lines.Count && lines[index].StartsWith("> ", StringComparison.Ordinal))
        {
            quoted.Add(lines[index][2..]);
            index++;
        }

        quote = new NongBlockQuoteBlock
        {
            Blocks = quoted.Count == 0
                ? new List<NongPandocBlock>()
                : new List<NongPandocBlock> { NongParagraphBlock.FromText(string.Join(" ", quoted).Trim()) }
        };
        return quoted.Count > 0;
    }

    static NongParagraphBlock ReadParagraph(IReadOnlyList<string> lines, ref int index)
    {
        var parts = new List<string>();
        while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]) && !StartsBlock(lines, index))
        {
            parts.Add(lines[index].Trim());
            index++;
        }

        if (parts.Count == 0)
        {
            if (index >= lines.Count)
                return NongParagraphBlock.FromText("");
            parts.Add(lines[index].Trim());
            index++;
        }

        return NongParagraphBlock.FromText(string.Join(" ", parts));
    }

    static bool StartsBlock(IReadOnlyList<string> lines, int index)
    {
        var line = lines[index];
        return line.TrimStart().StartsWith(":::", StringComparison.Ordinal)
            || HeadingRegex().IsMatch(line)
            || BulletRegex().IsMatch(line)
            || OrderedRegex().IsMatch(line)
            || line.StartsWith("> ", StringComparison.Ordinal)
            || (index + 1 < lines.Count && IsPipeRow(line) && IsSeparatorRow(ParsePipeRow(lines[index + 1])));
    }

    static bool IsPipeRow(string line) => line.TrimStart().StartsWith('|') && line.TrimEnd().EndsWith('|');

    static List<string> ParsePipeRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(cell => cell.Trim().Replace("\\|", "|").Replace("<br>", Environment.NewLine)).ToList();
    }

    static bool IsSeparatorRow(IReadOnlyList<string> cells)
    {
        return cells.Count > 0 && cells.All(cell => cell.Trim().Trim(':').All(ch => ch == '-'));
    }

    static ParsedAttributes ParseAttributes(string directive)
    {
        var match = AttributeBlockRegex().Match(directive);
        if (!match.Success) return new ParsedAttributes(null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? id = null;
        foreach (Match token in AttributeTokenRegex().Matches(match.Groups["attrs"].Value))
        {
            if (token.Groups["id"].Success)
            {
                id = token.Groups["id"].Value;
                continue;
            }

            if (token.Groups["key"].Success)
                values[token.Groups["key"].Value] = Unquote(token.Groups["value"].Value);
        }

        return new ParsedAttributes(id, values);
    }

    static Dictionary<string, string> WithoutKnown(Dictionary<string, string> attrs, params string[] known)
    {
        var result = new Dictionary<string, string>(attrs, StringComparer.OrdinalIgnoreCase);
        foreach (var key in known) result.Remove(key);
        return result;
    }

    static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return value;
    }

    sealed record ParsedAttributes(string? Id, Dictionary<string, string> Values);

    [GeneratedRegex("^(?<marks>#{1,6})\\s+(?<text>.*?)(?:\\s+\\{#(?<id>[^}]+)\\})?$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("^\\s*[-*+]\\s+(?<text>.+)$")]
    private static partial Regex BulletRegex();

    [GeneratedRegex("^\\s*(?<number>\\d+)\\.\\s+(?<text>.+)$")]
    private static partial Regex OrderedRegex();

    [GeneratedRegex("\\{(?<attrs>.*)\\}\\s*$")]
    private static partial Regex AttributeBlockRegex();

    [GeneratedRegex("#(?<id>[A-Za-z0-9_.:-]+)|(?<key>[A-Za-z0-9_.:-]+)=(?<value>\"(?:\\\\.|[^\"])*\"|[^\\s}]+)")]
    private static partial Regex AttributeTokenRegex();
}

using System.Text;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Pipeline;

public static class CitationFormatter
{
    public static string FormatMarkdown(PaperRecord record, int index) => ToGbt7714Like(record, index);

    public static string ToGbt7714Like(PaperRecord record, int index)
    {
        var authors = record.Authors.Count > 0 ? string.Join(", ", record.Authors.Take(3)) : "Unknown";
        if (record.Authors.Count > 3)
            authors += ", et al";

        var title = string.IsNullOrWhiteSpace(record.Title) ? "Untitled" : record.Title;
        var venue = string.IsNullOrWhiteSpace(record.Venue) ? record.Journal : record.Venue;
        var builder = new StringBuilder();
        builder.Append(index).Append(". ").Append(authors).Append(". ").Append(title).Append("[J]. ");
        if (!string.IsNullOrWhiteSpace(venue))
            builder.Append(venue).Append(", ");
        if (record.Year.HasValue)
            builder.Append(record.Year.Value);
        if (!string.IsNullOrWhiteSpace(record.Volume))
            builder.Append(", ").Append(record.Volume);
        if (!string.IsNullOrWhiteSpace(record.Issue))
            builder.Append("(").Append(record.Issue).Append(")");
        if (!string.IsNullOrWhiteSpace(record.Pages))
            builder.Append(": ").Append(record.Pages);
        if (!string.IsNullOrWhiteSpace(record.Doi))
            builder.Append(". DOI: ").Append(record.Doi);
        return builder.ToString().Trim();
    }

    public static string BibTeXKey(PaperRecord record, int index)
    {
        var author = record.FirstAuthor ?? record.Authors.FirstOrDefault() ?? "ref";
        author = new string(author.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (author.Length > 16)
            author = author[..16];
        var year = record.Year?.ToString() ?? "nd";
        return string.IsNullOrWhiteSpace(author) ? $"ref{index}" : $"{author}{year}_{index}";
    }

    public static string ToBibTeX(PaperRecord record, int index)
    {
        var venue = string.IsNullOrWhiteSpace(record.Venue) ? record.Journal : record.Venue;
        var type = !string.IsNullOrWhiteSpace(venue) && record.Year.HasValue ? "article" : "misc";
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = string.IsNullOrWhiteSpace(record.Title) ? "Untitled" : record.Title!
        };

        if (record.Authors.Count > 0)
            fields["author"] = string.Join(" and ", record.Authors);
        if (!string.IsNullOrWhiteSpace(venue))
            fields[type == "article" ? "journal" : "howpublished"] = venue!;
        if (record.Year.HasValue)
            fields["year"] = record.Year.Value.ToString();
        if (!string.IsNullOrWhiteSpace(record.Volume))
            fields["volume"] = record.Volume!;
        if (!string.IsNullOrWhiteSpace(record.Issue))
            fields["number"] = record.Issue!;
        if (!string.IsNullOrWhiteSpace(record.Pages))
            fields["pages"] = record.Pages!;
        var normalizedDoi = PaperRecordMerger.NormalizeDoi(record.Doi);
        if (!string.IsNullOrWhiteSpace(normalizedDoi))
            fields["doi"] = normalizedDoi;
        if (!string.IsNullOrWhiteSpace(record.LandingPageUrl))
            fields["url"] = record.LandingPageUrl!;
        else if (!string.IsNullOrWhiteSpace(record.PdfUrl))
            fields["url"] = record.PdfUrl!;
        if (!string.IsNullOrWhiteSpace(record.Publisher))
            fields["publisher"] = record.Publisher!;
        if (!string.IsNullOrWhiteSpace(record.Abstract))
            fields["abstract"] = record.Abstract!;

        var builder = new StringBuilder();
        builder.Append('@').Append(type).Append('{').Append(BibTeXKey(record, index));
        foreach (var field in fields)
        {
            builder.AppendLine(",");
            builder.Append("  ").Append(field.Key).Append(" = {").Append(EscapeBibTeX(field.Value)).Append('}');
        }

        builder.AppendLine();
        builder.Append('}');
        return builder.ToString();
    }

    public static string FormatBibTeX(PaperRecord record, int index = 1) => ToBibTeX(record, index);

    public static string EscapeBibTeX(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("{", "\\{", StringComparison.Ordinal)
            .Replace("}", "\\}", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

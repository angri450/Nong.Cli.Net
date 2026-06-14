using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Pipeline;

public sealed class PaperRecordMerger
{
    public IReadOnlyList<PaperRecord> Merge(IEnumerable<PaperRecord> records)
    {
        var merged = new List<PaperRecord>();
        foreach (var record in records)
        {
            var existing = merged.FirstOrDefault(candidate => IsSame(candidate, record));
            if (existing is null)
            {
                merged.Add(record);
                continue;
            }

            MergeInto(existing, record);
        }

        return merged;
    }

    static bool IsSame(PaperRecord a, PaperRecord b)
    {
        var doiA = NormalizeDoi(a.Doi);
        var doiB = NormalizeDoi(b.Doi);
        if (!string.IsNullOrWhiteSpace(doiA) && !string.IsNullOrWhiteSpace(doiB))
            return string.Equals(doiA, doiB, StringComparison.OrdinalIgnoreCase);

        if (!a.Year.HasValue || !b.Year.HasValue || a.Year.Value != b.Year.Value)
            return false;

        var titleA = NormalizeTitle(a.Title);
        var titleB = NormalizeTitle(b.Title);
        if (string.IsNullOrWhiteSpace(titleA) || string.IsNullOrWhiteSpace(titleB))
            return false;

        if (LooksChinese(a.Title) != LooksChinese(b.Title))
            return false;

        var authorA = NormalizeTitle(a.FirstAuthor ?? a.Authors.FirstOrDefault());
        var authorB = NormalizeTitle(b.FirstAuthor ?? b.Authors.FirstOrDefault());
        if (string.IsNullOrWhiteSpace(authorA) || string.IsNullOrWhiteSpace(authorB))
            return false;

        return string.Equals(titleA, titleB, StringComparison.OrdinalIgnoreCase)
            && string.Equals(authorA, authorB, StringComparison.OrdinalIgnoreCase);
    }

    static void MergeInto(PaperRecord target, PaperRecord source)
    {
        target.Doi = Prefer(target.Doi, source.Doi);
        target.Title = Prefer(target.Title, source.Title);
        target.Abstract = PreferLonger(target.Abstract, source.Abstract);
        target.Venue = Prefer(target.Venue, source.Venue);
        target.Journal = Prefer(target.Journal, source.Journal);
        target.Publisher = Prefer(target.Publisher, source.Publisher);
        target.Year ??= source.Year;
        target.FirstAuthor = Prefer(target.FirstAuthor, source.FirstAuthor);
        target.CitationCount = Math.Max(target.CitationCount.GetValueOrDefault(), source.CitationCount.GetValueOrDefault());
        target.IsOpenAccess = source.IsOpenAccess ?? target.IsOpenAccess;
        target.OpenAccessStatus = Prefer(target.OpenAccessStatus, source.OpenAccessStatus);
        target.License = Prefer(target.License, source.License);
        target.PdfUrl = Prefer(target.PdfUrl, source.PdfUrl);
        target.LandingPageUrl = Prefer(target.LandingPageUrl, source.LandingPageUrl);
        target.Volume = Prefer(target.Volume, source.Volume);
        target.Issue = Prefer(target.Issue, source.Issue);
        target.Pages = Prefer(target.Pages, source.Pages);

        AddRange(target.Authors, source.Authors);
        AddRange(target.Keywords, source.Keywords);
        AddRange(target.Concepts, source.Concepts);
        AddRange(target.Topics, source.Topics);
        AddRange(target.Affiliations, source.Affiliations);
        AddRange(target.Funders, source.Funders);
        AddRange(target.References, source.References);
        AddRange(target.RetrievedFrom, source.RetrievedFrom);
        AddRange(target.MatchReasons, source.MatchReasons);

        foreach (var pair in source.SourceIds)
            target.SourceIds.TryAdd(pair.Key, pair.Value);
    }

    static void AddRange(List<string> target, IEnumerable<string> source)
    {
        foreach (var item in source.Where(i => !string.IsNullOrWhiteSpace(i)))
        {
            if (!target.Contains(item, StringComparer.OrdinalIgnoreCase))
                target.Add(item);
        }
    }

    static string? Prefer(string? current, string? candidate) => string.IsNullOrWhiteSpace(current) ? candidate : current;

    static string? PreferLonger(string? current, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(current))
            return candidate;
        if (string.IsNullOrWhiteSpace(candidate))
            return current;
        return candidate.Length > current.Length ? candidate : current;
    }

    static string NormalizeTitle(string? value) => CnkiQueryNormalizer.NormalizeText(value);

    public static string? NormalizeDoi(string? doi)
    {
        if (string.IsNullOrWhiteSpace(doi))
            return null;
        var normalized = doi.Trim();
        if (normalized.StartsWith("https://doi.org/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["https://doi.org/".Length..];
        if (normalized.StartsWith("http://doi.org/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["http://doi.org/".Length..];
        if (normalized.StartsWith("https://dx.doi.org/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["https://dx.doi.org/".Length..];
        if (normalized.StartsWith("http://dx.doi.org/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["http://dx.doi.org/".Length..];
        if (normalized.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];
        return normalized.Trim().TrimEnd('.', ',', ';').ToLowerInvariant();
    }

    static bool LooksChinese(string? value) => !string.IsNullOrEmpty(value) && value.Any(ch => ch >= 0x4e00 && ch <= 0x9fff);
}

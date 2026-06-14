using Angri450.Nong.Literature.Dsl;
using Angri450.Nong.Literature.Models;

namespace Angri450.Nong.Literature.Pipeline;

public sealed class LiteratureRanker
{
    static int ReferenceYear => DateTime.UtcNow.Year;

    public double Score(string queryText, PaperRecord record, RankProfile profile)
    {
        return Score(record, CnkiQueryNormalizer.Normalize(CnkiParser.Parse(queryText)).Concepts, profile);
    }

    public double Score(CnkiQuery query, PaperRecord record, RankProfile profile)
    {
        return Score(record, CnkiQueryNormalizer.Normalize(query).Concepts, profile);
    }

    public IReadOnlyList<PaperRecord> Rank(string queryText, IEnumerable<PaperRecord> records, RankProfile profile)
    {
        return Rank(records, CnkiParser.Parse(queryText), profile);
    }

    public IReadOnlyList<PaperRecord> Rank(IEnumerable<PaperRecord> records, CnkiQuery query, RankProfile profile)
    {
        var concepts = CnkiQueryNormalizer.Normalize(query).Concepts;
        var ranked = records
            .Select(record =>
            {
                record.RelevanceScore = Score(record, concepts, profile);
                return record;
            })
            .OrderByDescending(r => r.RelevanceScore)
            .ThenByDescending(r => r.CitationCount.GetValueOrDefault())
            .ThenByDescending(r => r.Year.GetValueOrDefault())
            .ThenBy(r => r.Title ?? "", StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return ranked;
    }

    static double Score(PaperRecord record, IReadOnlyList<string> concepts, RankProfile profile)
    {
        var conceptCoverage = ConceptCoverage(record, concepts);
        var fieldMatch = Clamp(record.MatchReasons.Count / 5.0);
        var citationScore = Clamp(Math.Log10(record.CitationCount.GetValueOrDefault() + 1) / 3.0);
        var recencyScore = RecencyScore(record.Year);
        var sourceQuality = Clamp(record.RetrievedFrom.Count / 3.0);

        var score = profile switch
        {
            RankProfile.Classic => 0.35 * conceptCoverage + 0.20 * fieldMatch + 0.35 * citationScore + 0.05 * recencyScore + 0.05 * sourceQuality,
            RankProfile.Recent => 0.45 * conceptCoverage + 0.20 * fieldMatch + 0.05 * citationScore + 0.25 * recencyScore + 0.05 * sourceQuality,
            _ => 0.45 * conceptCoverage + 0.20 * fieldMatch + 0.15 * citationScore + 0.15 * recencyScore + 0.05 * sourceQuality
        };

        return double.IsFinite(score) ? Clamp(score) : 0;
    }

    static double ConceptCoverage(PaperRecord record, IReadOnlyList<string> concepts)
    {
        if (concepts.Count == 0)
            return 1;

        var text = CnkiQueryNormalizer.NormalizeText(string.Join(' ', new[]
        {
            record.Title,
            record.Abstract,
            string.Join(' ', record.Keywords),
            string.Join(' ', record.Concepts),
            string.Join(' ', record.Topics)
        }));

        var matched = concepts.Count(concept => text.Contains(concept, StringComparison.OrdinalIgnoreCase));
        return Clamp((double)matched / concepts.Count);
    }

    static double RecencyScore(int? year)
    {
        if (!year.HasValue)
            return 0;
        return Clamp(1.0 - Math.Max(0, ReferenceYear - year.Value) / 25.0);
    }

    static double Clamp(double value) => Math.Min(1, Math.Max(0, value));
}

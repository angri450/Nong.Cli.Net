using System.Text.Json.Serialization;

namespace Angri450.Nong.Literature.Models;

public sealed class PaperRecord
{
    public string? Id { get; set; }
    public string? Doi { get; set; }
    public string? Title { get; set; }
    public List<string> Authors { get; set; } = new();
    public int? Year { get; set; }
    public string? Venue { get; set; }
    public string? Publisher { get; set; }
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Concepts { get; set; } = new();
    public List<string> Topics { get; set; } = new();
    public List<string> Affiliations { get; set; } = new();
    public List<string> Funders { get; set; } = new();
    public List<string> References { get; set; } = new();
    public string? FirstAuthor { get; set; }
    public string? Journal { get; set; }
    public string? Issn { get; set; }
    public string? Cn { get; set; }
    public string? Isbn { get; set; }
    public string? Clc { get; set; }
    public string? Volume { get; set; }
    public string? Issue { get; set; }
    public string? Pages { get; set; }
    public int? CitationCount { get; set; }
    public bool? IsOpenAccess { get; set; }
    public string? OpenAccessStatus { get; set; }
    public string? License { get; set; }
    public string? PdfUrl { get; set; }
    public string? LandingPageUrl { get; set; }
    public string? FullText { get; set; }
    public List<string> RetrievedFrom { get; set; } = new();
    public Dictionary<string, string> SourceIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> MatchReasons { get; set; } = new();
    public double RelevanceScore { get; set; }

    [JsonIgnore]
    public bool HasFullText => !string.IsNullOrWhiteSpace(FullText);
}

using Angri450.Nong.Literature.Dsl;

namespace Angri450.Nong.Literature.Pipeline;

public static class CnkiQueryText
{
    public static string Extract(CnkiQuery query) => ExtractObject(query);

    internal static string ExtractObject(object? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        if (query is string text)
        {
            return text;
        }

        var direct = LiteraturePipelineModelAccess.String(
            query,
            "Text",
            "Query",
            "RawQuery",
            "RawText",
            "Source",
            "SourceText",
            "OriginalText",
            "Expression",
            "Dsl");

        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var rendered = query.ToString();
        return string.Equals(rendered, query.GetType().FullName, StringComparison.Ordinal)
            ? string.Empty
            : rendered ?? string.Empty;
    }
}

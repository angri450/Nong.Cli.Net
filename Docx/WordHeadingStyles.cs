using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Shared heading detection for Word documents where style IDs, style names,
/// and outline levels can disagree across Word, WPS, COM, and generated files.
/// </summary>
public static class WordHeadingStyles
{
    static readonly Regex HeadingStyleRegex = new(@"heading\s*([1-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ChineseHeadingStyleRegex = new(@"标题\s*([1-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex ChineseSectionHeadingRegex = new(@"^[一二三四五六七八九十百]+[、.．]\S+", RegexOptions.Compiled);
    static readonly Regex NumberedHeadingRegex = new(@"^\d+(\.\d+){0,2}\s+\S+", RegexOptions.Compiled);

    public static int? GetHeadingLevel(string? styleId, string? styleName, int? outlineLevel, string? text = null)
    {
        if (outlineLevel is >= 0 and <= 8)
            return outlineLevel.Value + 1;

        var byStyleId = GetHeadingLevelFromStyleToken(styleId);
        if (byStyleId.HasValue)
            return byStyleId.Value;

        var byStyleName = GetHeadingLevelFromStyleToken(styleName);
        if (byStyleName.HasValue)
            return byStyleName.Value;

        return null;
    }

    public static int? GetHeadingLevelFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();
        if (ChineseSectionHeadingRegex.IsMatch(trimmed))
            return 1;

        if (NumberedHeadingRegex.IsMatch(trimmed))
            return trimmed.Count(c => c == '.') >= 2 ? 3 : 2;

        return null;
    }

    public static string? GetStyleName(MainDocumentPart? mainPart, string? styleId)
    {
        if (mainPart == null || string.IsNullOrWhiteSpace(styleId))
            return null;

        var styles = mainPart.StyleDefinitionsPart?.Styles;
        if (styles == null)
            return null;

        var style = styles.Elements<W.Style>()
            .FirstOrDefault(s => string.Equals(s.StyleId?.Value, styleId, StringComparison.Ordinal));
        return style?.StyleName?.Val?.Value;
    }

    static int? GetHeadingLevelFromStyleToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var token = value.Trim();

        if (token is "21")
            return 2;
        if (token is "31")
            return 3;

        if (token.Length == 1 && token[0] is >= '1' and <= '9')
            return token[0] - '0';

        var headingMatch = HeadingStyleRegex.Match(token);
        if (headingMatch.Success && int.TryParse(headingMatch.Groups[1].Value, out var headingLevel))
            return headingLevel;

        var chineseMatch = ChineseHeadingStyleRegex.Match(token);
        if (chineseMatch.Success && int.TryParse(chineseMatch.Groups[1].Value, out var chineseLevel))
            return chineseLevel;

        var normalized = new string(token
            .Where(ch => !char.IsWhiteSpace(ch) && ch is not '_' and not '-')
            .ToArray())
            .ToLowerInvariant();

        if (normalized.StartsWith("heading", StringComparison.Ordinal)
            && normalized.Length > "heading".Length
            && char.IsDigit(normalized["heading".Length]))
        {
            var level = normalized["heading".Length] - '0';
            if (level is >= 1 and <= 9)
                return level;
        }

        if (normalized.StartsWith("标题", StringComparison.Ordinal)
            && normalized.Length > 2
            && char.IsDigit(normalized[2]))
        {
            var level = normalized[2] - '0';
            if (level is >= 1 and <= 9)
                return level;
        }

        return null;
    }
}

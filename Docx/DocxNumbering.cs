using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

public enum NumberingKind { Bullet, Decimal, LowerLetter, UpperLetter, LowerRoman, UpperRoman, ChineseCounting }

public sealed record NumberingSpec(NumberingKind Kind, int Levels = 1, string NumberSeparator = ".", double IndentStepCm = 0.74);

public sealed record NumberingInfo(int NumId, int AbstractNumId, NumberingKind Kind, int Levels);

public sealed class DocxNumbering
{
    private readonly WordprocessingDocument _doc;

    public DocxNumbering(WordprocessingDocument doc)
    {
        _doc = doc;
        EnsureNumberingPart();
    }

    public int CreateList(NumberingSpec spec)
    {
        var numberingPart = EnsureNumberingPart();
        var numbering = numberingPart.Numbering;

        // Find next available IDs
        var existingNums = numbering.Elements<NumberingInstance>().ToList();
        var existingAbstracts = numbering.Elements<AbstractNum>().ToList();
        int maxNumId = existingNums.Any() ? existingNums.Max(n => n.NumberID?.Value ?? 0) : 0;
        int maxAbstractNumId = existingAbstracts.Any() ? existingAbstracts.Max(a => a.AbstractNumberId?.Value ?? 0) : 0;

        int numId = Math.Max(maxNumId + 100, 100);
        int abstractNumId = maxAbstractNumId + 1;

        // Create abstractNum
        var abstractNum = new AbstractNum(
            new MultiLevelType { Val = MultiLevelValues.HybridMultilevel })
        {
            AbstractNumberId = abstractNumId
        };

        for (int level = 0; level < Math.Max(spec.Levels, 1); level++)
        {
            abstractNum.Append(CreateLevel(spec, level));
        }

        numbering.Append(abstractNum);

        // Create num instance
        var num = new NumberingInstance(
            new AbstractNumId { Val = abstractNumId })
        {
            NumberID = numId
        };
        numbering.Append(num);

        numberingPart.Numbering.Save();
        return numId;
    }

    public IReadOnlyList<NumberingInfo> List()
    {
        var numberingPart = _doc.MainDocumentPart?.NumberingDefinitionsPart;
        if (numberingPart?.Numbering == null) return Array.Empty<NumberingInfo>();

        var numbering = numberingPart.Numbering;
        var abstractNums = numbering.Elements<AbstractNum>()
            .ToDictionary(a => a.AbstractNumberId?.Value ?? 0);

        return numbering.Elements<NumberingInstance>()
            .Select(n =>
            {
                int absId = n.AbstractNumId?.Val?.Value ?? 0;
                abstractNums.TryGetValue(absId, out var abs);
                var kind = NumberingKind.Decimal;
                int levels = 0;
                if (abs != null)
                {
                    var lvls = abs.Elements<Level>().ToList();
                    levels = lvls.Count;
                    var fmt = lvls.FirstOrDefault()?.NumberingFormat?.Val?.Value;
                    kind = MapNumberFormatToKind(fmt);
                }
                return new NumberingInfo(n.NumberID?.Value ?? 0, absId, kind, levels);
            })
            .ToList();
    }

    NumberingDefinitionsPart EnsureNumberingPart()
    {
        var mainPart = _doc.MainDocumentPart!;
        var np = mainPart.NumberingDefinitionsPart;
        if (np == null)
        {
            np = mainPart.AddNewPart<NumberingDefinitionsPart>();
            np.Numbering = new Numbering();
            np.Numbering.Save();
        }
        return np;
    }

    static Level CreateLevel(NumberingSpec spec, int level)
    {
        NumberFormatValues numFmt = MapNumberKind(spec.Kind);

        // Build level text like "%1." or "%1.%2.%3."
        var lvlText = string.Join(spec.NumberSeparator,
            Enumerable.Range(1, level + 1).Select(i => $"%{i}")) + ".";

        int indentTwips = (int)(spec.IndentStepCm * 567 * (level + 1));

        return new Level(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = numFmt },
            new LevelText { Val = lvlText },
            new PreviousParagraphProperties(
                new Indentation { Left = indentTwips.ToString(), Hanging = "420" })
        )
        {
            LevelIndex = level
        };
    }

    static NumberFormatValues MapNumberKind(NumberingKind kind)
    {
        if (kind == NumberingKind.Bullet) return NumberFormatValues.Bullet;
        if (kind == NumberingKind.LowerLetter) return NumberFormatValues.LowerLetter;
        if (kind == NumberingKind.UpperLetter) return NumberFormatValues.UpperLetter;
        if (kind == NumberingKind.LowerRoman) return NumberFormatValues.LowerRoman;
        if (kind == NumberingKind.UpperRoman) return NumberFormatValues.UpperRoman;
        if (kind == NumberingKind.ChineseCounting) return NumberFormatValues.ChineseCounting;
        return NumberFormatValues.Decimal;
    }

    static NumberingKind MapNumberFormatToKind(NumberFormatValues? fmt)
    {
        if (fmt == NumberFormatValues.Bullet) return NumberingKind.Bullet;
        if (fmt == NumberFormatValues.LowerLetter) return NumberingKind.LowerLetter;
        if (fmt == NumberFormatValues.UpperLetter) return NumberingKind.UpperLetter;
        if (fmt == NumberFormatValues.LowerRoman) return NumberingKind.LowerRoman;
        if (fmt == NumberFormatValues.UpperRoman) return NumberingKind.UpperRoman;
        if (fmt == NumberFormatValues.ChineseCounting) return NumberingKind.ChineseCounting;
        return NumberingKind.Decimal;
    }
}

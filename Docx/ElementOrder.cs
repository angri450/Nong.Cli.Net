using DocumentFormat.OpenXml;

namespace DocxCore;

/// <summary>
/// ECMA-376 元素顺位校正器。
/// 确保 OpenXML 元素的子元素按 ISO 29500 规范顺序排列。
/// 参照 Minimax 的 conformance.py / ecma376_parser.py 的设计。
/// </summary>
public static class ElementOrder
{
    private const string WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string MathNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private const string Word2010Namespace = "http://schemas.microsoft.com/office/word/2010/wordml";
    private const string Word2012Namespace = "http://schemas.microsoft.com/office/word/2012/wordml";
    private const string SchemaLibraryNamespace = "http://schemas.openxmlformats.org/schemaLibrary/2006/main";

    private static readonly HashSet<string> LegacyTableLookAttributes = new(StringComparer.Ordinal)
    {
        "firstRow",
        "lastRow",
        "firstColumn",
        "lastColumn",
        "noHBand",
        "noVBand",
    };

    // ECMA-376 规范子元素顺位表（按 ISO/IEC 29500 定义）
    private static readonly Dictionary<string, string[]> Sequences = new()
    {
        // 7.1.2.1.5 — Run Properties：28 个子元素，必须严格按此顺序
        ["rPr"] = new[] { "rStyle", "rFonts", "b", "bCs", "i", "iCs", "caps", "smallCaps",
            "strike", "dstrike", "outline", "shadow", "emboss", "imprint", "noProof",
            "snapToGrid", "vanish", "webHidden", "color", "spacing", "w", "kern",
            "position", "sz", "szCs", "highlight", "u", "effect", "bdr", "shd",
            "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish", "oMath" },

        // 7.1.2.1.2 — Paragraph Properties：26 个子元素
        ["pPr"] = new[] { "pStyle", "keepNext", "keepLines", "pageBreakBefore",
            "framePr", "widowControl", "numPr", "suppressLineNumbers", "pBdr", "shd",
            "tabs", "suppressAutoHyphens", "kinsoku", "wordWrap", "overflowPunct",
            "topLinePunct", "autoSpaceDE", "autoSpaceDN", "bidi", "adjustRightInd",
            "snapToGrid", "spacing", "ind", "contextualSpacing", "mirrorIndents",
            "suppressOverlap", "jc", "textDirection", "textAlignment", "textboxTightWrap",
            "outlineLvl", "divId", "cnfStyle", "rPr", "sectPr", "pPrChange" },

        // 7.1.2.1.10 — Section Properties：21 个子元素
        ["sectPr"] = new[] { "headerReference", "footerReference", "footnotePr",
            "endnotePr", "type", "pgSz", "pgMar", "paperSrc", "pgBorders",
            "lnNumType", "pgNumType", "cols", "formProt", "vAlign", "noEndnote",
            "titlePg", "textDirection", "bidi", "rtlGutter", "docGrid", "printerSettings", "sectPrChange" },

        // 7.1.2.1.12 — Table Cell Properties：14 个子元素
        ["tcPr"] = new[] { "cnfStyle", "tcW", "gridSpan", "hMerge", "vMerge",
            "tcBorders", "shd", "noWrap", "tcMar", "textDirection", "tcFitText",
            "vAlign", "hideMark", "cellInsertion", "cellDeletion", "cellMerge", "tcPrChange" },

        // 7.1.2.1.9 — Table Properties：14 个子元素
        ["tblPr"] = new[] { "tblStyle", "tblpPr", "tblOverlap", "bidiVisual",
            "tblStyleRowBandSize", "tblStyleColBandSize", "tblW", "jc",
            "tblCellSpacing", "tblInd", "tblBorders", "shd", "tblLayout",
            "tblCellMar", "tblLook", "tblCaption", "tblDescription", "tblPrChange" },

        // 7.1.2.1.7 — Table Borders：8 个子元素
        ["tblBorders"] = new[] { "top", "left", "start", "bottom", "right",
            "end", "insideH", "insideV" },

        // 7.1.2.1.24 — Numbering Level：12 个子元素
        ["lvl"] = new[] { "start", "numFmt", "lvlRestart", "pStyle", "isLgl",
            "suff", "lvlText", "lvlPicBullId", "legacy", "lvlJc", "pPr", "rPr" },

        // Paragraph Border
        ["pBdr"] = new[] { "top", "left", "start", "bottom", "right", "end",
            "between", "bar" },

        // Table Cell Borders
        ["tcBorders"] = new[] { "top", "left", "start", "bottom", "right",
            "end", "insideH", "insideV", "tl2br", "tr2bl" },

        // Table Cell Margin
        ["tcMar"] = new[] { "top", "start", "left", "bottom", "end", "right" },
        ["tblCellMar"] = new[] { "top", "start", "left", "bottom", "end", "right" },

        // Numbering
        ["numbering"] = new[] { "numPicBullet", "abstractNum", "num", "numIdMacAtCleanup" },

        // Table Row
        ["tr"] = new[] { "tblPrEx", "trPr", "tc", "customXml", "sdt",
            "proofErr", "permStart", "permEnd", "bookmarkStart", "bookmarkEnd",
            "moveFromRangeStart", "moveFromRangeEnd", "moveToRangeStart",
            "moveToRangeEnd", "commentRangeStart", "commentRangeEnd",
            "customXmlInsRangeStart", "customXmlInsRangeEnd",
            "customXmlDelRangeStart", "customXmlDelRangeEnd",
            "customXmlMoveFromRangeStart", "customXmlMoveFromRangeEnd",
            "customXmlMoveToRangeStart", "customXmlMoveToRangeEnd",
            "ins", "del", "moveFrom", "moveTo" },

        // Style definition
        ["style"] = new[] { "name", "aliases", "basedOn", "next",
            "link", "autoRedefine", "hidden", "uiPriority", "semiHidden",
            "unhideWhenUsed", "qFormat", "locked", "personal", "personalCompose",
            "personalReply", "rsid", "pPr", "rPr", "tblPr", "tblStylePr" },

        // Table
        ["tbl"] = new[] { "tblPr", "tblGrid", "tr" },

        // Fonts
        ["rFonts"] = new[] { "ascii", "hAnsi", "eastAsia", "cs", "asciiTheme",
            "hAnsiTheme", "eastAsiaTheme", "cstheme" },

        // Document settings. Keep the common compatibility/update-fields
        // elements in schema order for Word/WPS-generated files.
        ["settings"] = new[] { "writeProtection", "view", "zoom", "removePersonalInformation",
            "removeDateAndTime", "doNotDisplayPageBoundaries", "displayBackgroundShape",
            "printPostScriptOverText", "printFractionalCharacterWidth", "printFormsData",
            "embedTrueTypeFonts", "embedSystemFonts", "saveSubsetFonts", "saveFormsData",
            "mirrorMargins", "alignBordersAndEdges", "bordersDoNotSurroundHeader",
            "bordersDoNotSurroundFooter", "gutterAtTop", "hideSpellingErrors",
            "hideGrammaticalErrors", "activeWritingStyle", "proofState", "formsDesign",
            "attachedTemplate", "linkStyles", "stylePaneFormatFilter",
            "stylePaneSortMethod", "documentType", "mailMerge", "revisionView",
            "trackRevisions", "doNotTrackMoves", "doNotTrackFormatting",
            "documentProtection", "autoFormatOverride", "styleLockTheme",
            "styleLockQFSet", "defaultTabStop", "autoHyphenation",
            "consecutiveHyphenLimit", "hyphenationZone", "doNotHyphenateCaps",
            "showEnvelope", "summaryLength", "clickAndTypeStyle", "defaultTableStyle",
            "evenAndOddHeaders", "bookFoldRevPrinting", "bookFoldPrinting",
            "bookFoldPrintingSheets", "drawingGridHorizontalSpacing",
            "drawingGridVerticalSpacing", "displayHorizontalDrawingGridEvery",
            "displayVerticalDrawingGridEvery", "doNotUseMarginsForDrawingGridOrigin",
            "drawingGridHorizontalOrigin", "drawingGridVerticalOrigin",
            "doNotShadeFormData", "noPunctuationKerning", "characterSpacingControl",
            "printTwoOnOne", "strictFirstAndLastChars", "noLineBreaksAfter",
            "noLineBreaksBefore", "savePreviewPicture", "doNotValidateAgainstSchema",
            "saveInvalidXml", "ignoreMixedContent", "alwaysShowPlaceholderText",
            "doNotDemarcateInvalidXml", "saveXmlDataOnly", "useXSLTWhenSaving",
            "saveThroughXslt", "showXMLTags", "alwaysMergeEmptyNamespace",
            "updateFields", "hdrShapeDefaults", "footnotePr", "endnotePr", "compat",
            "docVars", "rsids", "m:mathPr", "uiCompat97To2003", "attachedSchema", "themeFontLang",
            "clrSchemeMapping", "doNotIncludeSubdocsInStats", "doNotAutoCompressPictures",
            "forceUpgrade", "captions", "readModeInkLockDown", "smartTagType",
            "sl:schemaLibrary", "shapeDefaults", "doNotEmbedSmartTags",
            "decimalSymbol", "listSeparator", "w14:docId", "w14:discardImageEditingData",
            "w14:defaultImageDpi", "w14:conflictMode", "w15:chartTrackingRefBased", "w15:docId" },

        // Run
        ["r"] = new[] { "rPr", "br", "t", "delText", "instrText", "tab",
            "drawing", "object", "fldChar", "ruby", "footnoteReference",
            "endnoteReference", "commentReference", "sym", "ptab", "lastRenderedPageBreak" },
    };

    /// <summary>
    /// 对 OpenXmlElement 的子元素按 ECMA-376 规范顺序重新排列。
    /// 返回是否做了修改。
    /// </summary>
    public static bool Rectify(OpenXmlElement element)
    {
        string tag = element.LocalName;
        if (!Sequences.TryGetValue(tag, out var order)) return false;

        var children = element.ChildElements.ToList();
        if (children.Count <= 1) return false;

        // 建立 "标签名 → 规范位置" 索引
        var rank = new Dictionary<string, int>();
        for (int i = 0; i < order.Length; i++)
            rank[order[i]] = i;

        int Rank(OpenXmlElement child)
        {
            var qualified = GetQualifiedToken(child);
            if (qualified != null && rank.TryGetValue(qualified, out var qualifiedRank))
                return qualifiedRank;

            return rank.TryGetValue(child.LocalName, out var localRank) ? localRank : int.MaxValue;
        }

        // 按规范顺序排序（未知标签放到后面）
        var sorted = children
            .OrderBy(Rank)
            .ThenBy(c => c.LocalName) // 同顺位的按标签名字典序稳定
            .ToList();

        // 检查是否有变化
        bool changed = false;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] != sorted[i]) { changed = true; break; }
        }

        if (!changed) return false;

        element.RemoveAllChildren();
        foreach (var child in sorted)
            element.AppendChild(child.CloneNode(true));

        return true;
    }

    /// <summary>
    /// 递归校正整个文档树。
    /// </summary>
    public static int RectifyTree(OpenXmlElement root)
    {
        int count = 0;
        Walk(root);
        return count;

        void Walk(OpenXmlElement el)
        {
            if (Rectify(el)) count++;
            foreach (var child in el.ChildElements)
                Walk(child);
        }
    }

    /// <summary>
    /// 包裹孤立边框元素到正确的容器中。
    /// 参照 Minimax 的 wrap_orphan_borders()。
    /// </summary>
    public static int FixOrphanBorders(OpenXmlElement root)
    {
        int fixed_ = 0;
        Walk(root);
        return fixed_;

        void Walk(OpenXmlElement el)
        {
            // 检查 ParagraphProperties 中是否有脱离 pBdr 的边框
            if (el.LocalName == "pPr")
            {
                var orphans = el.ChildElements
                    .Where(c => c.LocalName is "top" or "left" or "bottom" or "right"
                        or "start" or "end" or "between" or "bar")
                    .Where(c => c.Parent?.LocalName != "pBdr")
                    .ToList();

                if (orphans.Count > 0)
                {
                    var pBdr = el.Elements().FirstOrDefault(e => e.LocalName == "pBdr");
                    if (pBdr == null)
                    {
                        pBdr = new DocumentFormat.OpenXml.Wordprocessing.ParagraphBorders();
                        el.InsertAt(pBdr, 0);
                    }
                    foreach (var o in orphans)
                    {
                        o.Remove();
                        pBdr.AppendChild(o.CloneNode(true));
                        fixed_++;
                    }
                }
            }
            foreach (var child in el.ChildElements)
                Walk(child);
        }
    }

    /// <summary>
    /// Removes compatibility artifacts produced by legacy Word/WPS conversion
    /// that are known to fail strict OpenXmlValidator checks but do not carry
    /// visible document intent.
    /// </summary>
    public static int SanitizeCompatibilityArtifacts(OpenXmlElement root)
    {
        int fixed_ = 0;

        foreach (var el in root.Descendants().ToList())
        {
            if (el.LocalName == "tcPr" && el.Parent?.LocalName == "style")
            {
                el.Remove();
                fixed_++;
                continue;
            }

            if (el.LocalName == "tblLook")
            {
                foreach (var attr in el.GetAttributes()
                    .Where(a => a.NamespaceUri == WordprocessingNamespace
                        && LegacyTableLookAttributes.Contains(a.LocalName))
                    .ToList())
                {
                    el.RemoveAttribute(attr.LocalName, attr.NamespaceUri);
                    fixed_++;
                }
            }

            if (el.LocalName == "noWrap" && IsFalseOnOff(el))
            {
                el.Remove();
                fixed_++;
                continue;
            }

            if (el.LocalName == "tblStyle"
                && el.Parent?.LocalName == "tblPr"
                && HasAncestor(el.Parent, "style"))
            {
                el.Remove();
                fixed_++;
            }
        }

        fixed_ += NormalizeTableCellProperties(root);

        return fixed_;
    }

    private static int NormalizeTableCellProperties(OpenXmlElement root)
    {
        int fixed_ = 0;

        foreach (var cell in root.Descendants().Where(e => e.LocalName == "tc").ToList())
        {
            var props = cell.ChildElements.Where(e => e.LocalName == "tcPr").ToList();
            if (props.Count == 0)
                continue;

            var primary = props[0];
            foreach (var extra in props.Skip(1).ToList())
            {
                MergeProperties(primary, extra);
                extra.Remove();
                fixed_++;
            }

            if (!ReferenceEquals(cell.FirstChild, primary))
            {
                primary.Remove();
                cell.PrependChild(primary);
                fixed_++;
            }
        }

        return fixed_;
    }

    private static void MergeProperties(OpenXmlElement primary, OpenXmlElement extra)
    {
        foreach (var attr in extra.GetAttributes())
            primary.SetAttribute(attr);

        foreach (var child in extra.ChildElements)
        {
            foreach (var existing in primary.ChildElements
                .Where(e => e.LocalName == child.LocalName && e.NamespaceUri == child.NamespaceUri)
                .ToList())
            {
                existing.Remove();
            }

            primary.AppendChild(child.CloneNode(true));
        }
    }

    private static bool IsFalseOnOff(OpenXmlElement element)
    {
        var attr = element.GetAttribute("val", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
        var value = attr.Value?.Trim();
        return string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAncestor(OpenXmlElement? element, string localName)
    {
        for (var current = element?.Parent; current != null; current = current.Parent)
        {
            if (current.LocalName == localName) return true;
        }

        return false;
    }

    private static string? GetQualifiedToken(OpenXmlElement element)
    {
        var prefix = element.NamespaceUri switch
        {
            WordprocessingNamespace => "w",
            MathNamespace => "m",
            Word2010Namespace => "w14",
            Word2012Namespace => "w15",
            SchemaLibraryNamespace => "sl",
            _ => null,
        };

        return prefix == null ? null : $"{prefix}:{element.LocalName}";
    }
}

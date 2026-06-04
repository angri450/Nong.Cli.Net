using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// 批注、修订追踪、内容控件、字体嵌入、文档合并。
/// </summary>
public static class AdvancedFeatures
{
    // ==================== 批注 (Comments) ====================

    /// <summary>在文档中插入批注。</summary>
    public static void InsertComment(WordprocessingDocument doc, string author, string text, Paragraph targetParagraph)
    {
        var main = doc.MainDocumentPart!;
        var commentsPart = main.WordprocessingCommentsPart ?? main.AddNewPart<WordprocessingCommentsPart>();
        if (commentsPart.Comments == null)
            commentsPart.Comments = new Comments();

        var commentId = (commentsPart.Comments.Elements<Comment>()
            .Select(c => int.TryParse(c.Id?.Value, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max()) + 1;

        commentsPart.Comments.Append(new Comment(
            new Paragraph(new Run(new Text(text))))
        {
            Id = commentId.ToString(),
            Author = author,
            Date = DateTime.Now,
        });

        // Insert CommentRangeStart, CommentRangeEnd, CommentReference around first run of target paragraph
        var firstRun = targetParagraph.Elements<Run>().FirstOrDefault();
        if (firstRun != null)
        {
            targetParagraph.InsertBefore(new CommentRangeStart { Id = commentId.ToString() }, firstRun);
            targetParagraph.InsertAfter(new CommentRangeEnd { Id = commentId.ToString() }, firstRun);
            firstRun.InsertAfter(new Run(new CommentReference { Id = commentId.ToString() }), firstRun);
        }
    }

    // ==================== 修订追踪 (Track Changes) ====================

    /// <summary>插入带修订标记的文本（标记为新增）。</summary>
    public static Run InsertedRun(string text, string author = "DocxCore", DateTime? date = null)
    {
        return new Run(
            new RunProperties(new Inserted { Author = author, Date = date ?? DateTime.Now }),
            new Text(text));
    }

    /// <summary>删除带修订标记的文本（标记为删除）。</summary>
    public static Run DeletedRun(string text, string author = "DocxCore", DateTime? date = null)
    {
        return new Run(
            new RunProperties(new Deleted { Author = author, Date = date ?? DateTime.Now }),
            new Text(text));
    }

    /// <summary>在段落末尾添加修订插入文本。</summary>
    public static void AppendTrackedInsertion(Paragraph p, string text, string author = "DocxCore")
    {
        p.Append(new Run(
            new RunProperties(new Inserted { Author = author, Date = DateTime.Now }),
            new Text(text)));
    }

    /// <summary>在段落末尾添加修订删除文本。</summary>
    public static void AppendTrackedDeletion(Paragraph p, string text, string author = "DocxCore")
    {
        p.Append(new Run(
            new RunProperties(new Deleted { Author = author, Date = DateTime.Now }),
            new Text(text)));
    }

    // ==================== 内容控件 (SDT / Content Controls) ====================

    /// <summary>插入纯文本内容控件。</summary>
    public static SdtBlock InsertPlainTextControl(string tag, string placeholder = "单击此处输入文字")
    {
        return new SdtBlock(
            new SdtProperties(
                new SdtAlias { Val = tag },
                new Tag { Val = tag },
                new SdtContentText()),
            new SdtContentBlock(
                new Paragraph(
                    new Run(new Text(placeholder)))));
    }

    /// <summary>插入下拉列表内容控件。</summary>
    public static SdtBlock InsertDropDownListControl(string tag, string[] items, int defaultIndex = 0)
    {
        var ddList = new SdtContentDropDownList { LastValue = items[defaultIndex] };
        foreach (var item in items)
            ddList.Append(new ListItem { DisplayText = item, Value = item });

        return new SdtBlock(
            new SdtProperties(
                new SdtAlias { Val = tag },
                new Tag { Val = tag },
                ddList),
            new SdtContentBlock(
                new Paragraph(
                    new Run(new Text(items[defaultIndex])))));
    }

    /// <summary>插入日期选择器内容控件。</summary>
    public static SdtBlock InsertDatePickerControl(string tag, string displayFormat = "yyyy-MM-dd")
    {
        return new SdtBlock(
            new SdtProperties(
                new SdtAlias { Val = tag },
                new Tag { Val = tag },
                new SdtContentDate { FullDate = DateTime.Now }),
            new SdtContentBlock(
                new Paragraph(
                    new Run(new Text(DateTime.Now.ToString(displayFormat))))));
    }

    /// <summary>插入复选框内容控件（可用版本简化）。</summary>
    public static SdtBlock InsertCheckBoxControl(string tag, bool defaultChecked = false)
    {
        return new SdtBlock(
            new SdtProperties(
                new SdtAlias { Val = tag },
                new Tag { Val = tag }),
            new SdtContentBlock(
                new Paragraph(new Run(new Text(defaultChecked ? "[X]" : "[ ]")))));
    }

    // ==================== 字体嵌入 ====================

    /// <summary>将 TrueType 字体嵌入文档（OOXML 混淆格式）。</summary>
    public static void EmbedFont(WordprocessingDocument doc, string fontPath, string fontName)
    {
        var main = doc.MainDocumentPart!;

        var fontTablePart = main.FontTablePart ?? main.AddNewPart<FontTablePart>();
        if (fontTablePart.Fonts == null)
            fontTablePart.Fonts = new Fonts();

        var fontKey = Guid.NewGuid();
        var relationshipId = "rIdFont" + fontKey.ToString("N")[..12];
        var fontPart = fontTablePart.AddFontPart(FontPartType.FontOdttf, relationshipId);

        var fontBytes = File.ReadAllBytes(fontPath);
        ObfuscateFont(fontBytes, fontKey);
        using (var ms = new MemoryStream(fontBytes))
        {
            fontPart.FeedData(ms);
        }

        var font = fontTablePart.Fonts.Elements<Font>()
            .FirstOrDefault(f => string.Equals(f.Name?.Value, fontName, StringComparison.OrdinalIgnoreCase));
        if (font == null)
        {
            font = new Font { Name = fontName };
            fontTablePart.Fonts.Append(font);
        }

        font.RemoveAllChildren<EmbedRegularFont>();
        font.Append(new EmbedRegularFont
        {
            Id = relationshipId,
            FontKey = fontKey.ToString("B").ToUpperInvariant(),
        });
        fontTablePart.Fonts.Save();
    }

    static void ObfuscateFont(byte[] fontBytes, Guid fontKey)
    {
        if (fontBytes.Length < 32) return;
        var key = fontKey.ToByteArray();
        var reversed = key.Reverse().ToArray();
        for (int i = 0; i < 32; i++)
            fontBytes[i] ^= reversed[i % 16];
    }

    // ==================== 文档合并 ====================

    /// <summary>将源文档追加到目标文档末尾。</summary>
    public static void AppendDocument(WordprocessingDocument target, string sourcePath, bool sectionBreak = true)
    {
        using var source = WordprocessingDocument.Open(sourcePath, false);
        var sourceBody = source.MainDocumentPart!.Document.Body!;

        if (sectionBreak)
            target.MainDocumentPart!.Document.Body!.Append(new Paragraph(
                new ParagraphProperties(new SectionProperties(
                    new SectionType { Val = SectionMarkValues.NextPage }))));

        // Clone all paragraphs, tables, sections from source
        foreach (var el in sourceBody.Elements())
        {
            if (el is SectionProperties sectionProps)
            {
                // Skip source section properties, we add our own
                continue;
            }
            var clone = el.CloneNode(true);
            target.MainDocumentPart!.Document.Body!.Append(clone);

            // Copy images referenced in this element
            CopyReferencedImages(source, target, clone);
        }

        // Copy styles and numbering if not present
        CopyPart(source.MainDocumentPart.StyleDefinitionsPart, target.MainDocumentPart!);
        CopyPart(source.MainDocumentPart.NumberingDefinitionsPart, target.MainDocumentPart!);
    }

    static void CopyReferencedImages(WordprocessingDocument source, WordprocessingDocument target, OpenXmlElement element)
    {
        foreach (var blip in element.Descendants<DocumentFormat.OpenXml.Drawing.Blip>())
        {
            var embedId = blip.Embed?.Value;
            if (string.IsNullOrEmpty(embedId)) continue;

            var sourcePart = source.MainDocumentPart!.GetPartById(embedId);
            if (sourcePart is not ImagePart sourceImage) continue;

            var targetImage = target.MainDocumentPart!.AddImagePart(sourceImage.ContentType);
            using var sourceStream = sourceImage.GetStream();
            targetImage.FeedData(sourceStream);

            var newId = target.MainDocumentPart.GetIdOfPart(targetImage);
            blip.Embed = newId;
        }
    }

    static void CopyPart<T>(T? sourcePart, MainDocumentPart targetMain) where T : OpenXmlPart, IFixedContentTypePart
    {
        if (sourcePart == null) return;
        var existing = targetMain.GetPartsOfType<T>().FirstOrDefault();
        if (existing != null) return; // Already exists

        var newPart = targetMain.AddNewPart<T>();
        using var stream = sourcePart.GetStream();
        newPart.FeedData(stream);
    }

    // ==================== 文档属性 ====================

    /// <summary>设置文档核心属性。</summary>
    public static void SetDocumentProperties(WordprocessingDocument doc, string? title = null, string? author = null, string? subject = null, string? keywords = null)
    {
        var packageProps = doc.PackageProperties;
        if (title != null) packageProps.Title = title;
        if (author != null) packageProps.Creator = author;
        if (subject != null) packageProps.Subject = subject;
        if (keywords != null) packageProps.Keywords = keywords;
    }

    // ==================== 文档保护 ====================

    /// <summary>设置文档编辑保护。protection 可选: "readOnly", "comments", "trackedChanges", "forms"。默认 "readOnly"。</summary>
    public static void ProtectDocument(WordprocessingDocument doc, string protection = "readOnly", string? password = null)
    {
        var main = doc.MainDocumentPart!;
        var settingsPart = main.DocumentSettingsPart ?? main.AddNewPart<DocumentSettingsPart>();
        if (settingsPart.Settings == null)
            settingsPart.Settings = new Settings();

        var val = protection switch
        {
            "comments" => DocumentProtectionValues.Comments,
            "trackedChanges" => DocumentProtectionValues.TrackedChanges,
            "forms" => DocumentProtectionValues.Forms,
            _ => DocumentProtectionValues.ReadOnly,
        };
        var dp = new DocumentProtection { Edit = val, Enforcement = true };
        if (!string.IsNullOrEmpty(password))
            dp.Hash = ComputePasswordHash(password);

        settingsPart.Settings.Append(dp);
    }

    static string ComputePasswordHash(string password)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(password);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

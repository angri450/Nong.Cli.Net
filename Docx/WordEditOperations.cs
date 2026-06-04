using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxCore;

/// <summary>
/// Word edit operation helpers: fix-order, protect, embed-font, merge.
/// All generation operations follow the pattern: copy input, open output, modify, save.
/// Output and input MUST NOT be the same path (caller guards this).
/// </summary>
public static class WordEditOperations
{
    // ==================== fix-order ====================

    /// <summary>
    /// Result for <see cref="FixOrder"/>.
    /// </summary>
    public sealed record FixOrderResult(int FixedElements, int PartsScanned, int OrphanBordersFixed);

    /// <summary>
    /// Fix OOXML element ordering across all document parts.
    /// Copies input to output, then calls ElementOrder.RectifyTree and
    /// ElementOrder.FixOrphanBorders on every available part:
    /// Document body, styles, numbering, settings, headers, footers,
    /// footnotes, endnotes, and comments.
    /// Caller is responsible for EnsureParentDir and CheckArtifact.
    /// </summary>
    public static FixOrderResult FixOrder(string inputPath, string outputPath)
    {
        GuardDifferentPaths(inputPath, outputPath);
        File.Copy(inputPath, outputPath, true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidOperationException("MainDocumentPart is missing.");

        int fixedElements = 0;
        int orphanBordersFixed = 0;
        int partsScanned = 0;

        void ProcessRoot(DocumentFormat.OpenXml.OpenXmlElement root)
        {
            fixedElements += ElementOrder.SanitizeCompatibilityArtifacts(root);
            fixedElements += ElementOrder.RectifyTree(root);
            orphanBordersFixed += ElementOrder.FixOrphanBorders(root);
            fixedElements += ElementOrder.RectifyTree(root);
            partsScanned++;
        }

        // 1. MainDocumentPart.Document.Body
        var body = mainPart.Document?.Body;
        if (body != null)
        {
            ProcessRoot(body);
        }

        // 2. StyleDefinitionsPart.Styles
        var stylesPart = mainPart.StyleDefinitionsPart;
        if (stylesPart?.Styles != null)
        {
            ProcessRoot(stylesPart.Styles);
        }

        // 3. NumberingDefinitionsPart.Numbering
        var numPart = mainPart.NumberingDefinitionsPart;
        if (numPart?.Numbering != null)
        {
            ProcessRoot(numPart.Numbering);
        }

        // 4. DocumentSettingsPart.Settings
        var settingsPart = mainPart.DocumentSettingsPart;
        if (settingsPart?.Settings != null)
        {
            ProcessRoot(settingsPart.Settings);
        }

        // 5. Header parts
        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header != null)
            {
                ProcessRoot(headerPart.Header);
            }
        }

        // 6. Footer parts
        foreach (var footerPart in mainPart.FooterParts)
        {
            if (footerPart.Footer != null)
            {
                ProcessRoot(footerPart.Footer);
            }
        }

        // 7. FootnotesPart
        var fnPart = mainPart.FootnotesPart;
        if (fnPart?.Footnotes != null)
        {
            ProcessRoot(fnPart.Footnotes);
        }

        // 8. EndnotesPart
        var enPart = mainPart.EndnotesPart;
        if (enPart?.Endnotes != null)
        {
            ProcessRoot(enPart.Endnotes);
        }

        // 9. CommentsPart (WordprocessingCommentsPart)
        var commentsPart = mainPart.WordprocessingCommentsPart;
        if (commentsPart?.Comments != null)
        {
            ProcessRoot(commentsPart.Comments);
        }

        return new FixOrderResult(fixedElements, partsScanned, orphanBordersFixed);
    }

    // ==================== protect ====================

    /// <summary>
    /// Result for <see cref="Protect"/>.
    /// </summary>
    public sealed record ProtectResult(string ProtectionMode, bool HasPassword);

    /// <summary>
    /// Apply document protection to a copy of the input.
    /// Valid modes: readonly, comments, tracked, forms.
    /// Caller is responsible for EnsureParentDir and CheckArtifact.
    /// </summary>
    public static ProtectResult Protect(string inputPath, string outputPath, string mode, string? password = null)
    {
        GuardDifferentPaths(inputPath, outputPath);

        // Validate mode
        var validModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "readonly", "comments", "tracked", "forms" };
        if (!validModes.Contains(mode))
            throw new ArgumentException(
                $"Invalid protection mode: '{mode}'. Valid modes: readonly, comments, tracked, forms.",
                nameof(mode));

        File.Copy(inputPath, outputPath, true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        // Map CLI mode names to the protection values AdvancedFeatures expects
        var protection = mode.ToLowerInvariant() switch
        {
            "readonly" => "readOnly",
            "comments" => "comments",
            "tracked" => "trackedChanges",
            "forms" => "forms",
            _ => "readOnly"
        };
        AdvancedFeatures.ProtectDocument(doc, protection, password);

        return new ProtectResult(mode.ToLowerInvariant(), !string.IsNullOrEmpty(password));
    }

    // ==================== embed-font ====================

    /// <summary>
    /// Result for <see cref="EmbedFont"/>.
    /// </summary>
    public sealed record EmbedFontResult(string FontName, string FontFile, bool Embedded);

    /// <summary>
    /// Embed a font file into a copy of the document.
    /// Font file must exist and have .ttf or .otf extension, otherwise
    /// throws ArgumentException (E002 UnsupportedFormat).
    /// Caller is responsible for EnsureParentDir and CheckArtifact.
    /// </summary>
    public static EmbedFontResult EmbedFont(string inputPath, string outputPath, string fontFilePath, string? fontName = null)
    {
        GuardDifferentPaths(inputPath, outputPath);

        // Validate font file exists
        if (!File.Exists(fontFilePath))
            throw new FileNotFoundException($"Font file not found: {fontFilePath}");

        // Validate font file extension (E002)
        var ext = Path.GetExtension(fontFilePath).ToLowerInvariant();
        if (ext != ".ttf" && ext != ".otf")
            throw new ArgumentException(
                $"Unsupported font format: '{ext}'. Expected .ttf or .otf.",
                nameof(fontFilePath));

        // Determine font name from file name if not provided
        var name = fontName ?? Path.GetFileNameWithoutExtension(fontFilePath);

        File.Copy(inputPath, outputPath, true);

        using var doc = WordprocessingDocument.Open(outputPath, true);
        AdvancedFeatures.EmbedFont(doc, fontFilePath, name);

        return new EmbedFontResult(name, fontFilePath, true);
    }

    // ==================== merge (upgraded) ====================

    /// <summary>
    /// Result for <see cref="MergeDocuments"/>.
    /// </summary>
    public sealed record MergeResult(string OutputPath, int SourceFiles, List<string> Warnings);

    /// <summary>
    /// Merge multiple docx files into one output using deep merge via
    /// <see cref="AdvancedFeatures.AppendDocument"/>.
    ///
    /// Copies the first file as base, then appends each subsequent file
    /// with section breaks, image references, styles, and numbering.
    ///
    /// Known limitations (returned as warnings):
    /// - Headers and footers from source documents are not merged.
    /// - Numbering definitions may conflict between documents.
    /// - Style naming conflicts are resolved by keeping the first-encountered style.
    ///
    /// Throws ArgumentException if any input path equals the output path (E006).
    /// Caller is responsible for EnsureParentDir and CheckArtifact.
    /// </summary>
    public static MergeResult MergeDocuments(string[] inputFiles, string outputPath)
    {
        if (inputFiles == null || inputFiles.Length < 2)
            throw new ArgumentException("At least 2 input files are required.", nameof(inputFiles));

        // Guard: input == output (E006)
        var outputFull = Path.GetFullPath(outputPath);
        foreach (var f in inputFiles)
        {
            if (string.Equals(Path.GetFullPath(f), outputFull, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Input and output paths must be different.",
                    nameof(inputFiles));
        }

        // Validate all input files exist and are .docx
        foreach (var f in inputFiles)
        {
            if (!File.Exists(f))
                throw new FileNotFoundException($"Input file not found: {f}");
            if (!f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Expected .docx file, got: {f}", nameof(inputFiles));
        }

        var warnings = new List<string>
        {
            "Headers and footers from source documents are not merged.",
            "Numbering definitions may conflict between documents (first-encountered wins).",
            "Style naming conflicts are resolved by keeping the first-encountered style."
        };

        // Copy first file as base
        File.Copy(inputFiles[0], outputPath, true);

        using var target = WordprocessingDocument.Open(outputPath, true);

        // Append subsequent files
        for (int i = 1; i < inputFiles.Length; i++)
        {
            AdvancedFeatures.AppendDocument(target, inputFiles[i], sectionBreak: true);
        }

        return new MergeResult(Path.GetFullPath(outputPath), inputFiles.Length, warnings);
    }

    // ==================== guards ====================

    private static void GuardDifferentPaths(string inputPath, string outputPath)
    {
        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath),
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Input and output paths must be different.",
                nameof(outputPath));
    }
}

using System.Text.Json.Serialization;

namespace DocxCore;

// ============================================================================
// NongMark v1 JSON Schema — "One Cut, Three Streams" canonical models
// ============================================================================

// --- Base block -------------------------------------------------------------

/// <summary>Base type for all nongmark/v1 blocks. Every block has an id and kind.</summary>
[JsonDerivedType(typeof(ParagraphBlock), "paragraph")]
[JsonDerivedType(typeof(HeadingBlock), "heading")]
[JsonDerivedType(typeof(RunBlock), "run")]
[JsonDerivedType(typeof(TableBlock), "table")]
[JsonDerivedType(typeof(TableRowBlock), "tableRow")]
[JsonDerivedType(typeof(TableCellBlock), "tableCell")]
[JsonDerivedType(typeof(ImageBlock), "image")]
[JsonDerivedType(typeof(FigureBlock), "figure")]
[JsonDerivedType(typeof(EquationBlock), "equation")]
[JsonDerivedType(typeof(ChemEquationBlock), "chemEquation")]
[JsonDerivedType(typeof(ChemicalStructureBlock), "chemicalStructure")]
[JsonDerivedType(typeof(FootnoteBlock), "footnote")]
[JsonDerivedType(typeof(EndnoteBlock), "endnote")]
[JsonDerivedType(typeof(HyperlinkBlock), "hyperlink")]
[JsonDerivedType(typeof(BookmarkBlock), "bookmark")]
[JsonDerivedType(typeof(CommentBlock), "comment")]
[JsonDerivedType(typeof(RevisionBlock), "revision")]
[JsonDerivedType(typeof(TocBlock), "toc")]
[JsonDerivedType(typeof(FieldBlock), "field")]
[JsonDerivedType(typeof(RawOpenXmlRefBlock), "rawOpenXmlRef")]
public abstract record NongBlock
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";
}

// --- Text blocks ------------------------------------------------------------

/// <summary>Ordinary paragraph block. ID prefix: p0001+</summary>
public sealed record ParagraphBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("styleName")]
    public string? StyleName { get; set; }

    [JsonPropertyName("outlineLevel")]
    public int? OutlineLevel { get; set; }

    [JsonPropertyName("format")]
    public NongParagraphFormat? Format { get; set; }
}

/// <summary>Heading block (detected by style or outlineLvl). ID prefix: h0001+</summary>
public sealed record HeadingBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("styleName")]
    public string? StyleName { get; set; }

    [JsonPropertyName("format")]
    public NongParagraphFormat? Format { get; set; }
}

/// <summary>Text run with formatting. ID prefix: r0001+</summary>
public sealed record RunBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("format")]
    public NongRunFormat? Format { get; set; }
}

// --- Run format -------------------------------------------------------------

/// <summary>Run-level character format.</summary>
public sealed record NongRunFormat
{
    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("fontEastAsia")]
    public string? FontEastAsia { get; set; }

    [JsonPropertyName("fontAscii")]
    public string? FontAscii { get; set; }

    [JsonPropertyName("fontSizePt")]
    public double? FontSizePt { get; set; }

    [JsonPropertyName("bold")]
    public bool? Bold { get; set; }

    [JsonPropertyName("italic")]
    public bool? Italic { get; set; }

    [JsonPropertyName("underline")]
    public string? Underline { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("superscript")]
    public bool? Superscript { get; set; }

    [JsonPropertyName("subscript")]
    public bool? Subscript { get; set; }
}

/// <summary>Paragraph-level layout and spacing format.</summary>
public sealed record NongParagraphFormat
{
    [JsonPropertyName("alignment")]
    public string? Alignment { get; set; }

    [JsonPropertyName("firstLineIndent")]
    public string? FirstLineIndent { get; set; }

    [JsonPropertyName("leftIndent")]
    public string? LeftIndent { get; set; }

    [JsonPropertyName("rightIndent")]
    public string? RightIndent { get; set; }

    [JsonPropertyName("lineSpacing")]
    public string? LineSpacing { get; set; }

    [JsonPropertyName("lineRule")]
    public string? LineRule { get; set; }

    [JsonPropertyName("spaceBefore")]
    public string? SpaceBefore { get; set; }

    [JsonPropertyName("spaceAfter")]
    public string? SpaceAfter { get; set; }

    [JsonPropertyName("keepNext")]
    public bool? KeepNext { get; set; }
}

// --- Table blocks -----------------------------------------------------------

/// <summary>Table block. ID prefix: t0001+</summary>
public sealed record TableBlock : NongBlock
{
    [JsonPropertyName("rows")]
    public List<TableRowBlock> Rows { get; set; } = new();

    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }

    [JsonPropertyName("colCount")]
    public int ColCount { get; set; }

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("styleName")]
    public string? StyleName { get; set; }

    [JsonPropertyName("format")]
    public NongTableFormat? Format { get; set; }
}

/// <summary>A single table row.</summary>
public sealed record TableRowBlock : NongBlock
{
    [JsonPropertyName("cells")]
    public List<TableCellBlock> Cells { get; set; } = new();

    [JsonPropertyName("isHeader")]
    public bool IsHeader { get; set; }
}

/// <summary>A single table cell with its runs.</summary>
public sealed record TableCellBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();

    [JsonPropertyName("gridSpan")]
    public int GridSpan { get; set; } = 1;

    [JsonPropertyName("rowSpan")]
    public int RowSpan { get; set; } = 1;

    [JsonPropertyName("format")]
    public NongTableCellFormat? Format { get; set; }
}

/// <summary>Table-level layout and border format.</summary>
public sealed record NongTableFormat
{
    [JsonPropertyName("justification")]
    public string? Justification { get; set; }

    [JsonPropertyName("width")]
    public string? Width { get; set; }

    [JsonPropertyName("widthType")]
    public string? WidthType { get; set; }

    [JsonPropertyName("borders")]
    public NongTableBorders? Borders { get; set; }
}

/// <summary>Table-cell layout and border format.</summary>
public sealed record NongTableCellFormat
{
    [JsonPropertyName("width")]
    public string? Width { get; set; }

    [JsonPropertyName("widthType")]
    public string? WidthType { get; set; }

    [JsonPropertyName("verticalAlignment")]
    public string? VerticalAlignment { get; set; }

    [JsonPropertyName("shadingFill")]
    public string? ShadingFill { get; set; }

    [JsonPropertyName("borders")]
    public NongTableBorders? Borders { get; set; }
}

/// <summary>Six-edge table border description.</summary>
public sealed record NongTableBorders
{
    [JsonPropertyName("top")]
    public NongBorderInfo? Top { get; set; }

    [JsonPropertyName("bottom")]
    public NongBorderInfo? Bottom { get; set; }

    [JsonPropertyName("left")]
    public NongBorderInfo? Left { get; set; }

    [JsonPropertyName("right")]
    public NongBorderInfo? Right { get; set; }

    [JsonPropertyName("insideH")]
    public NongBorderInfo? InsideH { get; set; }

    [JsonPropertyName("insideV")]
    public NongBorderInfo? InsideV { get; set; }
}

/// <summary>Single border edge description.</summary>
public sealed record NongBorderInfo
{
    [JsonPropertyName("val")]
    public string? Val { get; set; }

    [JsonPropertyName("size")]
    public uint? Size { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("space")]
    public uint? Space { get; set; }
}

// --- Image and figure blocks ------------------------------------------------

/// <summary>Image/media block. ID prefix: img0001+</summary>
public sealed record ImageBlock : NongBlock
{
    [JsonPropertyName("imageId")]
    public string? ImageId { get; set; }

    [JsonPropertyName("assetPath")]
    public string? AssetPath { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("widthEmu")]
    public long? WidthEmu { get; set; }

    [JsonPropertyName("heightEmu")]
    public long? HeightEmu { get; set; }

    [JsonPropertyName("widthPx")]
    public int WidthPx { get; set; }

    [JsonPropertyName("heightPx")]
    public int HeightPx { get; set; }

    [JsonPropertyName("altText")]
    public string? AltText { get; set; }

    [JsonPropertyName("analysis")]
    public ImageAnalysis? Analysis { get; set; }
}

/// <summary>Figure block (image with caption). ID prefix: fig0001+</summary>
public sealed record FigureBlock : NongBlock
{
    [JsonPropertyName("image")]
    public ImageBlock? Image { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();
}

/// <summary>Result of image structure analysis.</summary>
public sealed record ImageAnalysis
{
    [JsonPropertyName("engine")]
    public string? Engine { get; set; }

    [JsonPropertyName("netLocal")]
    public bool NetLocal { get; set; }

    [JsonPropertyName("regions")]
    public List<ImageAnalysisRegion> Regions { get; set; } = new();
}

/// <summary>A detected content region in an image.</summary>
public sealed record ImageAnalysisRegion
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "graphic";
}

/// <summary>OCR result for an image. status defaults to "notRun".</summary>
public sealed record OcrInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "notRun";

    [JsonPropertyName("engine")]
    public string? Engine { get; set; }

    [JsonPropertyName("textBlocks")]
    public List<string> TextBlocks { get; set; } = new();
}

// --- Equation blocks --------------------------------------------------------

/// <summary>OMML math equation block. ID prefix: m0001+</summary>
public sealed record EquationBlock : NongBlock
{
    [JsonPropertyName("latex")]
    public string? Latex { get; set; }

    [JsonPropertyName("display")]
    public bool Display { get; set; }

    [JsonPropertyName("ommlPresent")]
    public bool OmmlPresent { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "omml";

    [JsonPropertyName("textFallback")]
    public string? TextFallback { get; set; }
}

// --- Chemical equation block ------------------------------------------------

/// <summary>Chemical equation extracted from text. ID prefix: ce0001+</summary>
public sealed record ChemEquationBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("normalized")]
    public string? Normalized { get; set; }

    [JsonPropertyName("species")]
    public List<string> Species { get; set; } = new();

    [JsonPropertyName("source")]
    public string Source { get; set; } = "text";

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }
}

// --- Chemical structure block -----------------------------------------------

/// <summary>Chemical structure (from image or embedded). ID prefix: cs0001+</summary>
public sealed record ChemicalStructureBlock : NongBlock
{
    [JsonPropertyName("representation")]
    public ChemRepresentation? Representation { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "image";

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }
}

/// <summary>Schema placeholder for chemical structure representation.</summary>
public sealed record ChemRepresentation
{
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("isPlaceholder")]
    public bool IsPlaceholder { get; set; } = true;
}

// --- Footnote / Endnote blocks ----------------------------------------------

/// <summary>Footnote block. ID prefix: f0001+</summary>
public sealed record FootnoteBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();
}

/// <summary>Endnote block. ID prefix: e0001+</summary>
public sealed record EndnoteBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();
}

// --- Hyperlink block --------------------------------------------------------

/// <summary>Hyperlink block (external or internal).</summary>
public sealed record HyperlinkBlock : NongBlock
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("internalAnchor")]
    public string? InternalAnchor { get; set; }

    [JsonPropertyName("tooltip")]
    public string? Tooltip { get; set; }

    [JsonPropertyName("isInternal")]
    public bool IsInternal { get; set; }

    [JsonPropertyName("runs")]
    public List<RunBlock> Runs { get; set; } = new();
}

// --- Bookmark block ---------------------------------------------------------

/// <summary>Bookmark marker block.</summary>
public sealed record BookmarkBlock : NongBlock
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("bookmarkId")]
    public string? BookmarkId { get; set; }

    [JsonPropertyName("isStart")]
    public bool IsStart { get; set; } = true;
}

// --- Comment block ----------------------------------------------------------

/// <summary>Document comment. ID prefix: c0001+</summary>
public sealed record CommentBlock : NongBlock
{
    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("initials")]
    public string? Initials { get; set; }

    [JsonPropertyName("anchorBlockId")]
    public string? AnchorBlockId { get; set; }

    [JsonPropertyName("anchorText")]
    public string? AnchorText { get; set; }
}

// --- Revision block ---------------------------------------------------------

/// <summary>Track change (revision) block. ID prefix: rev0001+</summary>
public sealed record RevisionBlock : NongBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("id")]
    public string? __refId;
}

// --- TOC block --------------------------------------------------------------

/// <summary>Table of Contents field block.</summary>
public sealed record TocBlock : NongBlock
{
    [JsonPropertyName("instruction")]
    public string? Instruction { get; set; }

    [JsonPropertyName("switches")]
    public List<string> Switches { get; set; } = new();
}

// --- Field block ------------------------------------------------------------

/// <summary>Generic Word field code block (PAGE, DATE, SEQ, etc.).</summary>
public sealed record FieldBlock : NongBlock
{
    [JsonPropertyName("fieldCode")]
    public string? FieldCode { get; set; }

    [JsonPropertyName("fieldResult")]
    public string? FieldResult { get; set; }
}

// --- Raw OpenXML reference block --------------------------------------------

/// <summary>Bridged raw OOXML element reference for unhandled or complex elements.</summary>
public sealed record RawOpenXmlRefBlock : NongBlock
{
    [JsonPropertyName("element")]
    public string? Element { get; set; }

    [JsonPropertyName("outerXml")]
    public string? OuterXml { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

// ============================================================================
// Document and Manifest models
// ============================================================================

/// <summary>Total manifest describing all output streams and metrics.</summary>
public sealed record NongManifest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "nongmark/v1";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("sourceSha256")]
    public string? SourceSha256 { get; set; }

    /// <summary>Backward-compatible alias for SourceSha256. Not serialized.</summary>
    [JsonIgnore]
    public string? Sha256 { get => SourceSha256; set => SourceSha256 = value; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("streams")]
    public NongStreamPaths Streams { get; set; } = new();

    [JsonPropertyName("metrics")]
    public NongMetrics Metrics { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Paths to the three output streams plus structure/format/assets.</summary>
public sealed record NongStreamPaths
{
    [JsonPropertyName("document")]
    public string Document { get; set; } = "document.json";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "content.md";

    /// <summary>Backward-compatible alias for Content. Not serialized.</summary>
    [JsonIgnore]
    public string ContentMd { get => Content; set => Content = value; }

    [JsonPropertyName("contentJsonl")]
    public string ContentJsonl { get; set; } = "content.jsonl";

    [JsonPropertyName("structure")]
    public string Structure { get; set; } = "structure.json";

    [JsonPropertyName("format")]
    public string Format { get; set; } = "format.json";

    [JsonPropertyName("assets")]
    public string Assets { get; set; } = "assets/manifest.json";
}

/// <summary>Slicing metrics (counts per block kind).</summary>
public sealed record NongMetrics
{
    [JsonPropertyName("blocks")]
    public int Blocks { get; set; }

    [JsonPropertyName("paragraphs")]
    public int Paragraphs { get; set; }

    [JsonPropertyName("headings")]
    public int Headings { get; set; }

    [JsonPropertyName("tables")]
    public int Tables { get; set; }

    [JsonPropertyName("images")]
    public int Images { get; set; }

    [JsonPropertyName("equations")]
    public int Equations { get; set; }

    [JsonPropertyName("chemEquations")]
    public int ChemEquations { get; set; }

    [JsonPropertyName("chemStructures")]
    public int ChemStructures { get; set; }

    [JsonPropertyName("footnotes")]
    public int Footnotes { get; set; }

    [JsonPropertyName("endnotes")]
    public int Endnotes { get; set; }

    [JsonPropertyName("comments")]
    public int Comments { get; set; }

    [JsonPropertyName("revisions")]
    public int Revisions { get; set; }

    [JsonPropertyName("hyperlinks")]
    public int Hyperlinks { get; set; }

    [JsonPropertyName("bookmarks")]
    public int Bookmarks { get; set; }

    [JsonPropertyName("figures")]
    public int Figures { get; set; }

    [JsonPropertyName("tocs")]
    public int Tocs { get; set; }

    [JsonPropertyName("fields")]
    public int Fields { get; set; }

    [JsonPropertyName("rawRefs")]
    public int RawRefs { get; set; }
}

/// <summary>Canonical nongmark/v1 document — the master block array.</summary>
public sealed record NongDocument
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "nongmark/v1";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("blocks")]
    public List<NongBlock> Blocks { get; set; } = new();
}

// ============================================================================
// Structure models (structure.json)
// ============================================================================

/// <summary>Document structural map: outline, index, cross-references.</summary>
public sealed record NongStructure
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "nongmark/v1";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("outline")]
    public List<NongOutlineItem> Outline { get; set; } = new();

    [JsonPropertyName("blockIndex")]
    public Dictionary<string, NongBlockEntry> BlockIndex { get; set; } = new();

    [JsonPropertyName("tables")]
    public List<NongTableRef> Tables { get; set; } = new();

    [JsonPropertyName("footnotes")]
    public List<NongNoteRef> Footnotes { get; set; } = new();

    [JsonPropertyName("endnotes")]
    public List<NongNoteRef> Endnotes { get; set; } = new();

    [JsonPropertyName("hyperlinks")]
    public List<NongHyperlinkRef> Hyperlinks { get; set; } = new();

    [JsonPropertyName("bookmarks")]
    public List<NongBookmarkRef> Bookmarks { get; set; } = new();

    [JsonPropertyName("comments")]
    public List<NongCommentRef> Comments { get; set; } = new();

    [JsonPropertyName("revisions")]
    public List<NongRevisionRef> Revisions { get; set; } = new();

    [JsonPropertyName("math")]
    public List<NongMathRef> Math { get; set; } = new();

    [JsonPropertyName("chem")]
    public List<NongChemRef> Chem { get; set; } = new();
}

/// <summary>Heading outline tree node.</summary>
public sealed record NongOutlineItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("children")]
    public List<NongOutlineItem> Children { get; set; } = new();
}

/// <summary>Block index entry: kind, position, text preview, and style.</summary>
public sealed record NongBlockEntry
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("textPreview")]
    public string? TextPreview { get; set; }

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }
}

/// <summary>Table cross-reference.</summary>
public sealed record NongTableRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }

    [JsonPropertyName("colCount")]
    public int ColCount { get; set; }
}

/// <summary>Footnote or endnote reference in body order.</summary>
public sealed record NongNoteRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>Hyperlink cross-reference.</summary>
public sealed record NongHyperlinkRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>Bookmark cross-reference.</summary>
public sealed record NongBookmarkRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>Comment cross-reference.</summary>
public sealed record NongCommentRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>Revision cross-reference.</summary>
public sealed record NongRevisionRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>Math equation cross-reference.</summary>
public sealed record NongMathRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("display")]
    public bool Display { get; set; }

    [JsonPropertyName("hasLatex")]
    public bool HasLatex { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

/// <summary>Chemical content cross-reference.</summary>
public sealed record NongChemRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("position")]
    public int Position { get; set; }
}

// ============================================================================
// Format models (format.json)
// ============================================================================

/// <summary>Document-wide format fingerprint.</summary>
public sealed record NongFormat
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "nongmark/v1";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("styles")]
    public List<NongStyleDef> Styles { get; set; } = new();

    [JsonPropertyName("fonts")]
    public NongFontSummary Fonts { get; set; } = new();

    [JsonPropertyName("numbering")]
    public NongNumberingInfo Numbering { get; set; } = new();

    [JsonPropertyName("sections")]
    public List<NongSectionInfo> Sections { get; set; } = new();

    [JsonPropertyName("tables")]
    public List<NongTableFormatInfo> Tables { get; set; } = new();

    [JsonPropertyName("page")]
    public NongPageInfo Page { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

/// <summary>A style definition (paragraph or character).</summary>
public sealed record NongStyleDef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("basedOn")]
    public string? BasedOn { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("isCustom")]
    public bool IsCustom { get; set; }
}

/// <summary>Summary of fonts used in the document.</summary>
public sealed record NongFontSummary
{
    [JsonPropertyName("families")]
    public List<string> Families { get; set; } = new();

    [JsonPropertyName("eastAsia")]
    public List<string> EastAsia { get; set; } = new();

    [JsonPropertyName("ascii")]
    public List<string> Ascii { get; set; } = new();

    [JsonPropertyName("counts")]
    public Dictionary<string, int> Counts { get; set; } = new();
}

/// <summary>Numbering definitions summary.</summary>
public sealed record NongNumberingInfo
{
    [JsonPropertyName("abstractNums")]
    public int AbstractNums { get; set; }

    [JsonPropertyName("instances")]
    public int Instances { get; set; }

    [JsonPropertyName("types")]
    public List<string> Types { get; set; } = new();
}

/// <summary>Section/page layout information.</summary>
public sealed record NongSectionInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("pageWidth")]
    public int? PageWidth { get; set; }

    [JsonPropertyName("pageHeight")]
    public int? PageHeight { get; set; }

    [JsonPropertyName("orientation")]
    public string? Orientation { get; set; }

    [JsonPropertyName("marginTop")]
    public int? MarginTop { get; set; }

    [JsonPropertyName("marginBottom")]
    public int? MarginBottom { get; set; }

    [JsonPropertyName("marginLeft")]
    public int? MarginLeft { get; set; }

    [JsonPropertyName("marginRight")]
    public int? MarginRight { get; set; }
}

/// <summary>Table-level format info (per-table).</summary>
public sealed record NongTableFormatInfo
{
    [JsonPropertyName("blockId")]
    public string BlockId { get; set; } = "";

    [JsonPropertyName("styleId")]
    public string? StyleId { get; set; }

    [JsonPropertyName("styleName")]
    public string? StyleName { get; set; }

    [JsonPropertyName("format")]
    public NongTableFormat? Format { get; set; }
}

/// <summary>Page-level defaults.</summary>
public sealed record NongPageInfo
{
    [JsonPropertyName("defaultWidth")]
    public int? DefaultWidth { get; set; }

    [JsonPropertyName("defaultHeight")]
    public int? DefaultHeight { get; set; }

    [JsonPropertyName("defaultOrientation")]
    public string? DefaultOrientation { get; set; }
}

// ============================================================================
// Asset models (assets/manifest.json)
// ============================================================================

/// <summary>Asset manifest describing all extracted media files.</summary>
public sealed record NongAssetManifest
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "nongmark/v1";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("items")]
    public List<NongAssetEntry> Items { get; set; } = new();
}

/// <summary>A single asset file entry.</summary>
public sealed record NongAssetEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("usedBy")]
    public List<string> UsedBy { get; set; } = new();

    [JsonPropertyName("internalRelationshipId")]
    public string? InternalRelationshipId { get; set; }

    [JsonPropertyName("analysis")]
    public ImageAnalysis? Analysis { get; set; }

    [JsonPropertyName("ocr")]
    public OcrInfo? Ocr { get; set; }
}

// ============================================================================
// WordSliceResult
// ============================================================================

/// <summary>Result returned by WordSlice.Slice().</summary>
public sealed record WordSliceResult(
    string OutputDir,
    string ManifestPath,
    int BlockCount,
    List<string> Warnings
);

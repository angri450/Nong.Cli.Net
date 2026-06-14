using System.Text.Json.Serialization;
using PandocCore;

namespace PdfCore;

public sealed class PdfProcessingException : Exception
{
    public PdfErrorKind Kind { get; }

    public PdfProcessingException(PdfErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }
}

public enum PdfErrorKind
{
    FileNotFound,
    UnsupportedFormat,
    DependencyMissing,
    ValidationFailed,
    ReadFailed,
    WriteFailed,
    InternalError,
}

public sealed record PdfCheckResult
{
    public string SchemaVersion { get; set; } = "nongpdf/check/v1";
    public string Input { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }
    public int PageCount { get; set; }
    public bool HasTextLayer { get; set; }
    public int TextCharCount { get; set; }
    public double TextCharsPerPage { get; set; }
    public int ImageCount { get; set; }
    public double ImageCoverageRatio { get; set; }
    public double SuspiciousTextRatio { get; set; }
    public List<string> SuspectFonts { get; set; } = new();
    public bool RenderRequired { get; set; }
    public string Classification { get; set; } = "unknown";
    public string RecommendedMode { get; set; } = "auto";
    public List<string> Warnings { get; set; } = new();
    public List<PdfPageCheck> Pages { get; set; } = new();
}

public sealed record PdfPageCheck
{
    public int Page { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int TextCharCount { get; set; }
    public int ImageCount { get; set; }
    public double ImageCoverageRatio { get; set; }
    public double SuspiciousTextRatio { get; set; }
    public List<string> SuspectFonts { get; set; } = new();
}

public sealed record PdfSliceOptions
{
    public string Mode { get; set; } = "auto";
    public int Dpi { get; set; } = 200;
}

public sealed record PdfSliceResult
{
    public string OutputDir { get; set; } = "";
    public string ManifestPath { get; set; } = "";
    public int BlockCount { get; set; }
    public int AssetCount { get; set; }
    public int PageCount { get; set; }
    public string Classification { get; set; } = "unknown";
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfRenderResult
{
    public string OutputDir { get; set; } = "";
    public int PageCount { get; set; }
    public int Dpi { get; set; }
    public List<PdfRenderedPage> Pages { get; set; } = new();
}

public sealed record PdfRenderedPage
{
    public int Page { get; set; }
    public string Path { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed record PdfRenderedCrop
{
    public int Page { get; set; }
    public string Path { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int Dpi { get; set; }
    public double[] SourceBbox { get; set; } = Array.Empty<double>();
    public double[] PixelBbox { get; set; } = Array.Empty<double>();
}

public sealed record PdfImageExtractResult
{
    public string OutputDir { get; set; } = "";
    public int PageCount { get; set; }
    public int ImageCount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<PdfAssetEntry> Items { get; set; } = new();
}

public sealed record PdfManifest
{
    public string SchemaVersion { get; set; } = "nongpdf/v1";
    public PdfSourceInfo Source { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public PdfStreamPaths Streams { get; set; } = new();
    public PdfSliceMetrics Metrics { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfSourceInfo
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public int PageCount { get; set; }
    public string Classification { get; set; } = "unknown";
}

public sealed record PdfStreamPaths
{
    public string DocumentJson { get; set; } = NongPandocArtifactNames.Document;
    public string ContentJsonl { get; set; } = NongPandocArtifactNames.ContentJsonl;
    public string StructureJson { get; set; } = NongPandocArtifactNames.Structure;
    public string FormatJson { get; set; } = NongPandocArtifactNames.Format;
    public string NongmarkText { get; set; } = NongPandocArtifactNames.ContentNongMark;
    public string TextPreview { get; set; } = NongPandocArtifactNames.TextPreview;
    public string AssetsManifest { get; set; } = NongPandocArtifactNames.AssetsManifest;
    public string Diagnostics { get; set; } = NongPandocArtifactNames.Diagnostics;
}

public sealed record PdfSliceMetrics
{
    public int Pages { get; set; }
    public int Blocks { get; set; }
    public int Paragraphs { get; set; }
    public int Headings { get; set; }
    public int Images { get; set; }
    public int OcrTextBlocks { get; set; }
    public int Tables { get; set; }
    public int Warnings { get; set; }
}

public sealed record PdfDocumentModel
{
    public string SchemaVersion { get; set; } = "nongpdf/v1";
    public PdfSourceInfo Source { get; set; } = new();
    public PdfStreamPaths Streams { get; set; } = new();
    public List<PdfPageModel> Pages { get; set; } = new();
    public List<PdfContentBlock> Blocks { get; set; } = new();
    public List<PdfAssetEntry> Assets { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfPageModel
{
    public int Page { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Unit { get; set; } = "pt";
    public int TextCharCount { get; set; }
    public int ImageCount { get; set; }
    public string ReadingOrderMethod { get; set; } = "single-column-y-desc-x-asc";
    public double? ColumnSplitX { get; set; }
}

public sealed record PdfContentBlock
{
    public string Id { get; set; } = "";
    public string BlockId { get; set; } = "";
    public int Index { get; set; }
    public string Kind { get; set; } = "paragraph";
    public int Page { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
    public string Source { get; set; } = "pdfText";
    public string? Text { get; set; }
    public List<PdfRun> Runs { get; set; } = new();
    public PdfBlockFormat? Format { get; set; }
    public string? AssetId { get; set; }
    public string? AssetPath { get; set; }
    public string? CaptionBlockId { get; set; }
    public List<string> OcrBlockIds { get; set; } = new();
    public string? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfRun
{
    public string Text { get; set; } = "";
    public PdfRunFormat? Format { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
}

public sealed record PdfRunFormat
{
    public string? Font { get; set; }
    public double? Size { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
}

public sealed record PdfBlockFormat
{
    public string? Font { get; set; }
    public double? Size { get; set; }
    public string? Align { get; set; }
    public string? Indent { get; set; }
    public string? LineSpacing { get; set; }
}

public sealed record PdfStructure
{
    public string SchemaVersion { get; set; } = "nongpdf/v1";
    public string Source { get; set; } = "";
    public List<PdfOutlineItem> Outline { get; set; } = new();
    public Dictionary<string, PdfBlockIndexEntry> BlockIndex { get; set; } = new();
    public List<PdfPageStructure> Pages { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

public sealed record PdfOutlineItem
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Page { get; set; }
}

public sealed record PdfBlockIndexEntry
{
    public string Kind { get; set; } = "";
    public int Order { get; set; }
    public int Page { get; set; }
    public string? TextPreview { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
    public string Source { get; set; } = "";
    public NongPandocBlockProvenance? Provenance { get; set; }
}

public sealed record PdfPageStructure
{
    public int Page { get; set; }
    public List<string> BlockIds { get; set; } = new();
}

public sealed record PdfFormatDocument
{
    public string SchemaVersion { get; set; } = "nongpdf/v1";
    public string Source { get; set; } = "";
    public List<PdfPageFormat> Pages { get; set; } = new();
    public List<string> Fonts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public NongPandocVisualEvidence VisualEvidence { get; set; } = new();
}

public sealed record PdfPageFormat
{
    public int Page { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Unit { get; set; } = "pt";
    public List<string> Fonts { get; set; } = new();
}

public sealed record PdfAssetManifest
{
    public string SchemaVersion { get; set; } = "nongpdf/v1";
    public string Source { get; set; } = "";
    public List<PdfAssetEntry> Items { get; set; } = new();
}

public sealed record PdfAssetEntry
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public int Page { get; set; }
    public double[] Bbox { get; set; } = Array.Empty<double>();
    public string ExtractionMethod { get; set; } = "embeddedImage";
    public string? CaptionBlockId { get; set; }
    public string? Caption { get; set; }
    public string? AltTextCandidate { get; set; }
    public List<string> OcrBlockIds { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record PdfReadingOrderDiagnostics
{
    public string SchemaVersion { get; set; } = "nongpdf/reading-order/v1";
    public List<PdfReadingOrderPage> Pages { get; set; } = new();
    public List<string> Issues { get; set; } = new();
}

public sealed record PdfReadingOrderPage
{
    public int Page { get; set; }
    public List<string> BlockIds { get; set; } = new();
    public string Method { get; set; } = "single-column-y-desc-x-asc";
}

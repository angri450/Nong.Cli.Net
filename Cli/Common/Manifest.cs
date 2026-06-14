namespace Nong.Cli.Common;

/// <summary>
/// Command manifest. Describes every available command for nong commands --json output.
/// Status: implemented | stub | planned
/// </summary>
public static class Manifest
{
    public sealed record CommandInfo(
        string Name,
        string Description,
        string Group,
        string[] Aliases,
        ParamDef[]? Parameters = null,
        string Status = "implemented"
    );

    /// <summary>Simple CLI parameter descriptor for OpenAI tool schema generation.</summary>
    public sealed record ParamDef(
        string Name,
        string Type,       // "string" | "boolean" | "number" | "integer"
        string Description,
        bool Required = false
    );

    public static List<CommandInfo> All()
    {
        var list = new List<CommandInfo>();

        // === word ===
        list.Add(new("word check", "Preflight .doc/.docx before conversion, slicing, or editing", "word", [],
            Parameters: [new("file", "string", "Path to .doc or .docx file", Required: true)]));
        list.Add(new("word convert", "Convert legacy .doc to .docx as a boundary step", "word", [],
            Parameters: [new("file", "string", "Path to .doc file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word create", "Create DOCX directly from authored NongMark", "word", [],
            Parameters: [new("spec", "string", "Path to JSON spec or NongMark content", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word read", "Extract plain text from a .docx file", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word preview", "7-step document structure diagnostic", "word", ["word diagnose"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word fill", "Template fill from JSON data", "word", [],
            Parameters: [new("template", "string", "Path to template .docx file", Required: true),
                         new("data", "string", "Path to JSON data file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word rebuild", "Clean OOXML style pollution", "word", ["word clean-styles"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word extract", "Extract embedded images", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("word dissect", "Format fingerprint or NongPandoc package slice", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output directory (optional)")]));
        list.Add(new("word stats", "Document statistics", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word fonts", "List all fonts", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word styles", "List all style definitions", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word validate", "OOXML schema validation", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word merge", "Merge two docx files", "word", [],
            Parameters: [new("source", "string", "Path to source .docx file", Required: true),
                         new("target", "string", "Path to target .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word outline", "Extract document outline", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word images", "List, extract, analyze images with content-aware border detection", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output directory (optional)")]));
        list.Add(new("word crop", "Auto-crop blank margins from all images", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word fit-images", "Scale inline multi-image paragraphs side-by-side within page width", "word", ["word compact-images"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word compact-tables", "Compact tables: remove fixed row heights, equalize columns, center on page", "word", ["word tables"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word regroup-images", "Merge orphan images across paragraphs for side-by-side layout", "word", ["word pair-images"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word estimate", "Estimate page breaks and blank space on each page", "word", ["word pages"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word page-setup", "Set page size, orientation, margins, columns, different first page", "word", ["word layout"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true),
                         new("spec", "string", "Path to JSON page-setup spec")]));
        list.Add(new("word indent", "Set paragraph indentation: first-line, hanging, left, right, outline level", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true),
                         new("spec", "string", "Path to JSON indent spec")]));
        list.Add(new("word paragraph-control", "Set pagination controls: keepNext, keepLines, pageBreakBefore, widowControl", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true),
                         new("spec", "string", "Path to JSON paragraph-control spec")]));
        list.Add(new("word image-wrap", "Convert inline images to floating with configurable text wrap modes", "word", ["word wrap"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word cell-format", "Format table cells: borders, shading, alignment, padding", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true),
                         new("spec", "string", "Path to JSON cell-format spec")]));
        list.Add(new("word run-format", "Character-level formatting: underline, strikethrough, color, highlight, spacing", "word", ["word char-format"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true),
                         new("spec", "string", "Path to JSON run-format spec")]));
        list.Add(new("word comments", "Read document comments", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word revisions", "List tracked changes", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word infer-format", "Infer OpenXML format from Chinese description", "word", [],
            Parameters: [new("description", "string", "Chinese format description", Required: true)]));
        list.Add(new("word fix-order", "Internal OOXML/structure repair only", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word academic-format", "Visible academic Word formatting repair for headings, body, tables, fonts, spacing", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word format-gongwen", "Apply Chinese official-document formatting to an existing DOCX", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word format-audit", "Read-only visible Word formatting evidence audit", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("word repair-plan", "Explain which Word repair command to use and how to verify", "word", [],
            Parameters: [new("file", "string", "Path to .docx file (optional)")]));
        list.Add(new("word table-reflow", "Explicitly split long or wide tables into continuation tables", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word protect", "Apply document protection", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word embed-font", "Embed font into document", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("font", "string", "Font file path or name", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add paragraph", "Append paragraph to document", "word", ["word add-paragraph"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("text", "string", "Paragraph text", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add table", "Append table to document", "word", ["word add-table"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("spec", "string", "Path to JSON table spec", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add footnote", "Append footnote to document", "word", ["word add-footnote"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("text", "string", "Footnote text", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add endnote", "Append endnote to document", "word", ["word add-endnote"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("text", "string", "Endnote text", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add image", "Append image to document", "word", ["word add-image"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("image", "string", "Path to image file (PNG/JPG)", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add toc", "Append table of contents to document", "word", ["word add-toc"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add xref", "Append cross-reference to document", "word", ["word add-xref"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("target", "string", "Bookmark or heading to reference", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add link", "Append hyperlink to document", "word", ["word add-link"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("url", "string", "URL to link to", Required: true),
                         new("text", "string", "Link display text", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add bookmark", "Append bookmark to document", "word", ["word add-bookmark"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("name", "string", "Bookmark name", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add comment", "Append comment to document", "word", ["word add-comment"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("text", "string", "Comment text", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word add math", "Append math equation to document", "word", ["word add-math"],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("formula", "string", "LaTeX or OMML formula", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("word compare", "Compare two DOCX files paragraph-by-paragraph", "word", [],
            Parameters: [new("left", "string", "Path to first .docx file", Required: true),
                         new("right", "string", "Path to second .docx file", Required: true)]));
        list.Add(new("word render-preview", "Render DOCX pages as PNGs via LibreOffice headless PDF conversion", "word", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true),
                         new("o", "string", "Output directory for PNGs", Required: true),
                         new("dpi", "integer", "Render DPI (default 150)")]));

        // === inspect (12 commands) ===
        list.Add(new("inspect diagnose", "Full paper quality diagnosis", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect refs", "Reference analysis and risk check", "inspect", ["inspect references"],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect write-paper", "Generate paper DOCX from JSON spec", "inspect", [],
            Parameters: [new("spec", "string", "Path to JSON paper spec", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("inspect write-official", "Generate official-document DOCX from JSON spec", "inspect", [],
            Parameters: [new("spec", "string", "Path to JSON official-document spec", Required: true),
                         new("o", "string", "Output .docx path", Required: true)]));
        list.Add(new("inspect official-check", "Audit official-document DOCX format compliance", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx file", Required: true)]));
        list.Add(new("inspect classify", "Classify paper type (16 types)", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect structure", "Extract paper structure (IMRaD)", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect varplan", "Variable operationalization plan", "inspect", ["inspect variables"],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect evidence", "Evidence chain diagnosis", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect data-req", "Data requirements diagnosis", "inspect", ["inspect data-requirements"],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect gap", "Gap grade assessment", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));
        list.Add(new("inspect semantics", "Semantic diagnosis", "inspect", [],
            Parameters: [new("file", "string", "Path to .docx paper file", Required: true)]));

        // === chart (implemented: 11 commands) ===
        list.Add(new("chart analyze", "Full analysis: ANOVA + Duncan + stats", "chart", [],
            Parameters: [new("file", "string", "Path to CSV/JSON data file", Required: true),
                         new("o", "string", "Output directory for generated files", Required: true)]));
        list.Add(new("chart bar", "Bar chart with error bars and significance letters", "chart", [],
            Parameters: [new("file", "string", "Path to treatment data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path or directory", Required: true)]));
        list.Add(new("chart anova", "One-way ANOVA", "chart", [],
            Parameters: [new("file", "string", "Path to treatment data file", Required: true)]));
        list.Add(new("chart duncan", "Duncan MRT post-hoc test", "chart", [],
            Parameters: [new("file", "string", "Path to treatment data file", Required: true)]));
        list.Add(new("chart line", "Line chart", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("chart scatter", "Scatter plot", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("chart pie", "Pie chart", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("chart boxplot", "Box plot for treatment group distribution comparison", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("chart histogram", "Histogram for data distribution", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("chart heatmap", "Heatmap chart from 2D data", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("chart radar", "Radar/spider chart for multi-index comparison", "chart", [],
            Parameters: [new("file", "string", "Path to data file (CSV/JSON)", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));

        // === excel (implemented: 8 commands) ===
        list.Add(new("excel sheets", "List worksheet names in a workbook", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true)]));
        list.Add(new("excel read", "Read xlsx content as structured data", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true),
                         new("sheet", "string", "Sheet name or index")]));
        list.Add(new("excel to-groups", "Convert Excel columns to treatment/value grouped data for statistics", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true),
                         new("treatment", "string", "Treatment column name or letter", Required: true),
                         new("value", "string", "Value column name or letter", Required: true)]));
        list.Add(new("excel create", "Create xlsx from JSON spec", "excel", [],
            Parameters: [new("spec", "string", "Path to JSON spec file", Required: true),
                         new("o", "string", "Output .xlsx path", Required: true)]));
        list.Add(new("excel dissect", "Slice xlsx into a NongPandoc package", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("excel style", "Apply cell styles from a JSON spec", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true),
                         new("spec", "string", "Path to JSON style spec", Required: true),
                         new("o", "string", "Output .xlsx path", Required: true)]));
        list.Add(new("excel formula", "Write formulas from a JSON spec", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true),
                         new("spec", "string", "Path to JSON formula spec", Required: true),
                         new("o", "string", "Output .xlsx path", Required: true)]));
        list.Add(new("excel pivot", "Create a pivot table from a JSON spec", "excel", [],
            Parameters: [new("file", "string", "Path to .xlsx file", Required: true),
                         new("spec", "string", "Path to JSON pivot spec", Required: true),
                         new("o", "string", "Output .xlsx path", Required: true)]));

        // === diagram (implemented: flowchart, network, tree) ===
        list.Add(new("diagram flowchart", "Flowchart from JSON spec", "diagram", [],
            Parameters: [new("spec", "string", "Path to JSON flowchart spec", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("diagram network", "Network graph from JSON spec", "diagram", [],
            Parameters: [new("spec", "string", "Path to JSON network spec", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));
        list.Add(new("diagram tree", "Phylogenetic tree from Newick", "diagram", [],
            Parameters: [new("spec", "string", "Path to Newick file or inline Newick string", Required: true),
                         new("o", "string", "Output PNG path", Required: true)]));

        // === genre (implemented) ===
        list.Add(new("genre list", "List format templates", "genre", []));
        list.Add(new("genre show", "Show template content", "genre", []));

        // === icons (implemented) ===
        list.Add(new("icons list", "List all Bioicons", "icons", []));
        list.Add(new("icons search", "Search Bioicons", "icons", []));

        // === skill (implemented: validate, scan, inventory, package) ===
        list.Add(new("skill validate", "Validate SKILL.md structure and references", "skill", []));
        list.Add(new("skill scan", "Security scan for skill directories", "skill", []));
        list.Add(new("skill inventory", "List skill directory contents", "skill", []));
        list.Add(new("skill package", "Validate + scan + package skill into .zip", "skill", []));

        // === pptx (implemented: read, slides, dissect, create) ===
        list.Add(new("pptx read", "Extract slide text from a .pptx file", "pptx", [],
            Parameters: [new("file", "string", "Path to .pptx file", Required: true)]));
        list.Add(new("pptx slides", "List slide structure from a .pptx file", "pptx", [],
            Parameters: [new("file", "string", "Path to .pptx file", Required: true)]));
        list.Add(new("pptx dissect", "Slice pptx into a NongPandoc package", "pptx", [],
            Parameters: [new("file", "string", "Path to .pptx file", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("pptx create", "Create pptx from JSON slide spec", "pptx", [],
            Parameters: [new("spec", "string", "Path to JSON slide spec file", Required: true),
                         new("o", "string", "Output .pptx path", Required: true)]));

        // === ocr (implemented: cloud, local, check-env, analyze-image, models, install-model, to-word) ===
        list.Add(new("ocr local", "Local PP-OCRv6 Chinese text recognition through pure .NET runtime (no Python required)", "ocr", [],
            Parameters: [new("image", "string", "Path to image file (PNG/JPG/BMP/TIFF/WebP)", Required: true),
                         new("force", "boolean", "Skip preflight check for QR/code/graphic-heavy images")]));
        list.Add(new("ocr cloud", "Cloud PaddleOCR-VL-1.6 via PADDLEOCR_ACCESS_TOKEN", "ocr", [],
            Parameters: [new("file", "string", "Path to image or PDF file, or URL", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("ocr check-env", "Check OCR environment status", "ocr", []));
        list.Add(new("ocr analyze-image", "Analyze image structure (no OCR, no token required)", "ocr", [],
            Parameters: [new("image", "string", "Path to image file", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("ocr models", "List available OCR models (PP-OCRv5 + PP-OCRv6)", "ocr", []));
        list.Add(new("ocr install-model", "Install PP-OCRv6 model from PaddleOCR CDN", "ocr", [],
            Parameters: [new("model-id", "string", "Model ID: pp-ocrv6-medium, pp-ocrv6-small, pp-ocrv6-tiny", Required: true),
                         new("dry-run", "boolean", "Report deployment plan without installing"),
                         new("source", "string", "NuGet v3 source URL for native runtime packages")]));
        list.Add(new("ocr to-word", "Convert image/PDF to Word document via cloud OCR", "ocr", [],
            Parameters: [new("file", "string", "Path to image or PDF file", Required: true),
                         new("o", "string", "Output .docx path", Required: true),
                         new("pages", "string", "Page range (e.g. \"1-5,10\")")]));
        list.Add(new("ocr batch", "Batch OCR on all images in a directory", "ocr", [],
            Parameters: [new("dir", "string", "Directory containing image files", Required: true),
                         new("pattern", "string", "File pattern (e.g. *.jpg)"),
                         new("recursive", "boolean", "Search subdirectories")]));
        list.Add(new("ocr video", "Extract and OCR text from video frames", "ocr", [],
            Parameters: [new("file", "string", "Path to video file", Required: true),
                         new("o", "string", "Output directory (also writes .srt)", Required: true),
                         new("fps", "number", "Frames per second to sample (default 1)"),
                         new("dedup-threshold", "integer", "dHash dedup threshold 0-64 (default 12)")]));
        list.Add(new("ocr screen", "Capture and OCR a screen region (Windows only)", "ocr", [],
            Parameters: [new("region", "string", "Screen region x,y,w,h (e.g. 100,100,800,600)")]));
        list.Add(new("ocr camera", "Capture and OCR frames from camera (requires opencv_videoio)", "ocr", [],
            Parameters: [new("device", "integer", "Camera device index (default 0)"),
                         new("interval", "integer", "Capture interval ms (default 2000)"),
                         new("count", "integer", "Number of captures, 0=unlimited (default 5)")]));

        // === pdf (implemented: 8 commands) ===
        list.Add(new("pdf check", "Preflight PDF and classify text/hybrid/scan route", "pdf", [],
            Parameters: [new("file", "string", "Path to PDF file", Required: true)]));
        list.Add(new("pdf dissect", "Slice PDF into a NongPandoc package", "pdf", [],
            Parameters: [new("file", "string", "Path to PDF file", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("pdf render", "Render PDF pages to PNG images through local PDFium runtime", "pdf", [],
            Parameters: [new("file", "string", "Path to PDF file", Required: true),
                         new("o", "string", "Output directory", Required: true),
                         new("pages", "string", "Page range (e.g. \"1-5,10\")"),
                         new("dpi", "integer", "Render resolution (default 200)")]));
        list.Add(new("pdf images", "Extract embedded PDF images with page/bbox provenance", "pdf", [],
            Parameters: [new("file", "string", "Path to PDF file", Required: true),
                         new("o", "string", "Output directory", Required: true)]));
        list.Add(new("pdf merge", "Merge multiple PDF files into one", "pdf", [],
            Parameters: [new("files", "string", "Comma-separated PDF file paths", Required: true),
                         new("o", "string", "Output PDF path", Required: true)]));
        list.Add(new("pdf split", "Split PDF pages into a separate document", "pdf", [],
            Parameters: [new("file", "string", "Path to PDF file", Required: true),
                         new("o", "string", "Output directory", Required: true),
                         new("pages", "string", "Page range to extract (e.g. \"1-5\")", Required: true)]));
        list.Add(new("pdf ocr", "Add rendered image layer to scanned PDF pages with optional OCR text layer", "pdf", [],
            Parameters: [new("file", "string", "Path to scanned PDF file", Required: true),
                         new("o", "string", "Output PDF path", Required: true),
                         new("dpi", "integer", "Render DPI (default 200)"),
                         new("with-ocr", "boolean", "Run local PP-OCRv6 on each page (requires nong-ocr installed)")]));
        list.Add(new("pdf compress", "Compress PDF by rebuilding content stream and removing unused objects", "pdf", [],
            Parameters: [new("file", "string", "Path to PDF file", Required: true),
                         new("o", "string", "Output compressed PDF path", Required: true)]));

        // === lit (implemented: parse, validate, plan, search, export) ===
        list.Add(new("lit parse", "Parse CNKI-like literature retrieval DSL", "lit", [],
            Parameters: [new("query", "string", "CNKI-like search expression", Required: true)]));
        list.Add(new("lit validate", "Validate CNKI-like literature retrieval DSL syntax", "lit", [],
            Parameters: [new("query", "string", "CNKI-like search expression", Required: true)]));
        list.Add(new("lit plan", "Plan provider rough queries for literature retrieval", "lit", [],
            Parameters: [new("query", "string", "Search topic or research question", Required: true)]));
        list.Add(new("lit search", "Search literature metadata via DOI/OpenAlex/Crossref/Unpaywall", "lit", [],
            Parameters: [new("query", "string", "Search expression or DOI", Required: true),
                         new("o", "string", "Output file path (.json/.md/.bib)", Required: true)]));
        list.Add(new("lit export", "Export literature results as JSON, Markdown, or BibTeX", "lit", [],
            Parameters: [new("file", "string", "Path to raw results JSON", Required: true),
                         new("format", "string", "Export format: json, markdown, or bibtex", Required: true),
                         new("o", "string", "Output file path", Required: true)]));

        // === slice (implemented: inspect, blocks, block, assets) ===
        list.Add(new("slice inspect", "Inspect a NongPandoc package contract and AI read order", "slice", [],
            Parameters: [new("dir", "string", "Path to NongPandoc package directory", Required: true)]));
        list.Add(new("slice blocks", "List content blocks from a NongPandoc package", "slice", [],
            Parameters: [new("dir", "string", "Path to NongPandoc package directory", Required: true)]));
        list.Add(new("slice block", "Read one block with unified content, structure, format, and diagnostics", "slice", [],
            Parameters: [new("dir", "string", "Path to NongPandoc package directory", Required: true),
                         new("id", "string", "Block ID to read", Required: true)]));
        list.Add(new("slice assets", "List assets from a NongPandoc package", "slice", [],
            Parameters: [new("dir", "string", "Path to NongPandoc package directory", Required: true)]));

        // === progress (implemented: report) ===
        list.Add(new("progress report", "Generate HTML progress reports from log/plans, log/changelog, log/debug, and log/guidance", "progress", []));

        return list;
    }
}

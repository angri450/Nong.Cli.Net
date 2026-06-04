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
        string Status = "implemented"
    );

    public static List<CommandInfo> All()
    {
        var list = new List<CommandInfo>();

        // === word (30 implemented: 29 stage-15 canonical commands + retained legacy extract) ===
        list.Add(new("word read", "Extract plain text from a .docx file", "word", []));
        list.Add(new("word preview", "7-step document structure diagnostic", "word", []));
        list.Add(new("word fill", "Template fill from JSON data", "word", []));
        list.Add(new("word rebuild", "Clean OOXML style pollution", "word", []));
        list.Add(new("word extract", "Extract embedded images", "word", []));
        list.Add(new("word dissect", "Format fingerprint or nongmark/v1 one-cut three-stream slice", "word", []));
        list.Add(new("word stats", "Document statistics", "word", []));
        list.Add(new("word fonts", "List all fonts", "word", []));
        list.Add(new("word styles", "List all style definitions", "word", []));
        list.Add(new("word validate", "OOXML schema validation", "word", []));
        list.Add(new("word merge", "Merge two docx files", "word", []));
        list.Add(new("word outline", "Extract document outline", "word", []));
        list.Add(new("word images", "List and optionally extract images", "word", []));
        list.Add(new("word comments", "Read document comments", "word", []));
        list.Add(new("word revisions", "List tracked changes", "word", []));
        list.Add(new("word infer-format", "Infer OpenXML format from Chinese description", "word", []));
        list.Add(new("word fix-order", "Fix OOXML element ordering", "word", []));
        list.Add(new("word protect", "Apply document protection", "word", []));
        list.Add(new("word embed-font", "Embed font into document", "word", []));
        list.Add(new("word add paragraph", "Append paragraph to document", "word", ["word add-paragraph"]));
        list.Add(new("word add table", "Append table to document", "word", ["word add-table"]));
        list.Add(new("word add footnote", "Append footnote to document", "word", ["word add-footnote"]));
        list.Add(new("word add endnote", "Append endnote to document", "word", ["word add-endnote"]));
        list.Add(new("word add image", "Append image to document", "word", ["word add-image"]));
        list.Add(new("word add toc", "Append table of contents to document", "word", ["word add-toc"]));
        list.Add(new("word add xref", "Append cross-reference to document", "word", ["word add-xref"]));
        list.Add(new("word add link", "Append hyperlink to document", "word", ["word add-link"]));
        list.Add(new("word add bookmark", "Append bookmark to document", "word", ["word add-bookmark"]));
        list.Add(new("word add comment", "Append comment to document", "word", ["word add-comment"]));
        list.Add(new("word add math", "Append math equation to document", "word", ["word add-math"]));

        // === inspect (implemented: diagnose, refs, write-paper) ===
        list.Add(new("inspect diagnose", "Full paper quality diagnosis", "inspect", []));
        list.Add(new("inspect refs", "Reference analysis and risk check", "inspect", []));
        list.Add(new("inspect write-paper", "Generate paper from JSON spec", "inspect", []));

        // === inspect (implemented) ===
        list.Add(new("inspect classify", "Classify paper type (16 types)", "inspect", []));
        list.Add(new("inspect structure", "Extract paper structure", "inspect", []));
        list.Add(new("inspect varplan", "Variable operationalization plan", "inspect", []));
        list.Add(new("inspect evidence", "Evidence chain diagnosis", "inspect", []));
        list.Add(new("inspect data-req", "Data requirements diagnosis", "inspect", []));
        list.Add(new("inspect gap", "Gap grade assessment", "inspect", []));
        list.Add(new("inspect semantics", "Semantic diagnosis", "inspect", []));

        // === chart (implemented: analyze, bar, anova, duncan, line, scatter, pie) ===
        list.Add(new("chart analyze", "Full analysis: ANOVA + Duncan + stats", "chart", []));
        list.Add(new("chart bar", "Bar chart with error bars and significance", "chart", []));
        list.Add(new("chart anova", "One-way ANOVA", "chart", []));
        list.Add(new("chart duncan", "Duncan MRT post-hoc test", "chart", []));
        list.Add(new("chart line", "Line chart", "chart", []));
        list.Add(new("chart scatter", "Scatter plot", "chart", []));
        list.Add(new("chart pie", "Pie chart", "chart", []));

        // === excel (implemented: sheets, read, to-groups, create) ===
        list.Add(new("excel sheets", "List worksheets", "excel", []));
        list.Add(new("excel read", "Read xlsx content", "excel", []));
        list.Add(new("excel to-groups", "Convert Excel columns to grouped data", "excel", []));
        list.Add(new("excel create", "Create xlsx from JSON spec", "excel", []));

        // === diagram (implemented: flowchart, network, tree) ===
        list.Add(new("diagram flowchart", "Flowchart from JSON spec", "diagram", []));
        list.Add(new("diagram network", "Network graph from JSON spec", "diagram", []));
        list.Add(new("diagram tree", "Phylogenetic tree from Newick", "diagram", []));

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

        // === pptx (implemented: read, slides) ===
        list.Add(new("pptx read", "Extract slide text", "pptx", []));
        list.Add(new("pptx slides", "List slide structure", "pptx", []));

        // === ocr (implemented: cloud, local, check-env, analyze-image, models, install-model, to-word) ===
        list.Add(new("ocr local", "Local PP-OCRv5 text recognition (E005 if model missing)", "ocr", []));
        list.Add(new("ocr cloud", "Cloud PaddleOCR-VL-1.6 via PADDLEOCR_ACCESS_TOKEN", "ocr", []));
        list.Add(new("ocr check-env", "Check OCR environment status", "ocr", []));
        list.Add(new("ocr analyze-image", "Analyze image structure (no OCR, no token)", "ocr", []));
        list.Add(new("ocr models", "List available OCR models", "ocr", []));
        list.Add(new("ocr install-model", "Install OCR model (E009 for pp-ocrv5-mobile)", "ocr", []));
        list.Add(new("ocr to-word", "Convert image/PDF to Word document via cloud OCR", "ocr", []));

        return list;
    }
}

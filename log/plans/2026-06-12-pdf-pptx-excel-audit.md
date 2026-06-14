# PDF / PPTX / Excel OOXML & Format Gap Audit

Date: 2026-06-12
Scope: PdfCommands (7), PptxCommands (4), ExcelCommands (8)
Reference: Word deep audit at log/plans/2026-06-12-ooxml-deep-audit.md (51 elements, 39% full coverage)

NOTE: PDF does not use OOXML — it is a final-form format. This audit assesses what format properties are READ and what manipulation commands EXIST. PPTX and Excel ARE OOXML formats and are audited the same way as Word.

---

## 1. PDF Module — 7 Commands

### 1.1 Command Breakdown

| Command | R/W | What it does | Format depth |
|---------|-----|--------------|--------------|
| `pdf check` | Read | Preflight: page count, text chars, image coverage, text/scan/hybrid classification, suspicious fonts | Font names, text char per page, image area ratio |
| `pdf dissect` | Read | Full NongPandoc slice: text, tables, images, OCR blocks, reading order | Bbox, font, fontSize, bold, italic, align per run; column detection; table markdown; page size |
| `pdf render` | Read | Render all pages to PNG at configurable DPI | page dimensions only |
| `pdf images` | Read | Extract embedded images with bbox/page provenance | Bbox, extraction method |
| `pdf merge` | Write | Concatenate PDF byte streams (Docnet) | None — byte-level only |
| `pdf split` | Write | Extract page range to new PDF (Docnet) | None — byte-level only |
| `pdf ocr` | Write | Render pages + re-pack as image-layer PDF | Page dimensions only |

### 1.2 What Format Properties Are READ (from PdfCore text extraction)

| Property | Source | Depth |
|----------|--------|-------|
| Text char count per page | PdfDocumentInspector | Full |
| Image count + coverage ratio | PdfDocumentInspector | Full |
| Font name per word | PdfTextExtractor (PdfPig Word.FontName) | Full — but only name, no embedding status |
| Font size (point size from Letters) | PdfTextExtractor | Full |
| Bold/Italic (heuristic from font name string) | PdfTextExtractor | Partial — "Bold"/"Italic" substring match only; not actual font weight |
| Text alignment (left/center heuristic from bbox vs pageWidth) | PdfTextExtractor | Partial |
| Bounding box per word/line/block | PdfTextExtractor + PdfImageExtractor | Full |
| Column detection (single vs two-column) | PdfTextExtractor | Partial — heuristic, two-column only |
| Table detection (column band alignment) | PdfTextExtractor | Partial — heuristic, 2-12 columns, visual rows only |
| Header/footer detection (repeated text fingerprint) | PdfTextExtractor | Partial — works only when 3+ pages |
| Gibberish detection (suspicious text ratio) | PdfTextQuality | Partial |
| Reading order (single or two-column) | PdfTextExtractor | Partial |

### 1.3 What Is MISSING (Format Read)

| Missing | Why it matters | Priority |
|---------|---------------|----------|
| **Table structure accuracy** | Table detection is pure visual heuristic (column band from text position). Merged cells, spanned rows, multi-line cells, and header rows are not distinguished. Real agricultural tables (field trial data, ANOVA results) are formatted as proper tables — the heuristic loses structure. | P0 |
| **Paragraph indent detection** | The PdfBlockFormat has `Indent` and `LineSpacing` fields but they are never populated. No first-line indent detection from text position analysis. | P1 |
| **Multi-column beyond two** | Only two-column layout is detected. Three-column PDFs (common in Chinese journals) are not handled. | P1 |
| **Font weight detection** | Bold is detected via font name substring only. A font named "Songti" set to bold weight would not be detected. | P1 |
| **Color detection** | Text color, fill color, and stroke color are never read from PDF. | P1 |
| **Heading hierarchy (h1/h2/h3)** | All headings get level=1. No multi-level heading detection from font size tiers. | P2 |
| **Page number extraction** | Page numbers in headers/footers are detected and removed but the raw number is not extracted as structured data. | P2 |
| **PDF metadata** | Title, author, subject, keywords from PDF Info dict are never read. | P2 |
| **Link/annotation extraction** | Hyperlinks, internal links (TOC), and annotations are completely ignored. | P2 |
| **Encryption check** | Encrypted PDFs throw a generic read error. No upfront "this PDF is encrypted" check with actionable message. | P1 |

### 1.4 What Is MISSING (Write/Manipulation)

| Missing command | What it would do | Priority |
|----------------|------------------|----------|
| **pdf rotate** | Rotate pages 90/180/270 degrees. Common for scanned documents. | P1 |
| **pdf compress** | Re-compress images, remove unused objects, linearize for web. Large scan PDFs (50MB+) are a daily reality for farmers uploading field photos. | P1 |
| **pdf watermark** | Add text/image watermark to all or selected pages. Needed for draft/confidential marking. | P2 |
| **pdf extract-text** | Output plain text only, no package. Current `pdf dissect` produces a full slice package; a lightweight text-only path would be faster for LLM ingestion. | P2 |
| **pdf metadata** | Read/write PDF Info dict (title, author, subject, keywords). | P2 |
| **pdf to-word** | Already exists in OCR module (`ocr to-word`), not in PDF module. The routing is confusing — users expect `pdf to-word` to exist. | P1 |

---

## 2. PPTX Module — 4 Commands

### 2.1 Command Breakdown

| Command | R/W | What it does | Format depth |
|---------|-----|--------------|--------------|
| `pptx read` | Read | Extract all text from slides | Text only — no font, size, color, position |
| `pptx slides` | Read | Count shapes, texts, pictures, tables, charts per slide | Shape type counts only |
| `pptx dissect` | Read | NongPandoc slice with shape evidence (id, kind, layout bbox, placeholder type, notes) | Bbox (x,y,cx,cy in EMU), shape kind, placeholder type, relationshipId for assets |
| `pptx create` | Write | Create PPTX from JSON spec (zip-level raw XML) | Title, subtitle, items only. Font sizes hardcoded (3600/1800 hundredths-pt). No color, no layout control. |

### 2.2 PptxCore PresentationBuilder — What It Handles (not exposed via CLI)

The `PresentationBuilder` (via ShapeCrawler) supports these through its fluent API:

| Feature | Coverage | Notes |
|---------|----------|-------|
| **Slide layouts** | Full | Title, content, table, chart slides + TwoColumns, Cards, BigNumber, Quote, HeroTop, Symmetric, Asymmetric, ThreeColumn, PrimarySecondary |
| **Themes** | Full | 6 built-in presets + JSON loader. Accent1-3, Dark1-2, Light1-2, body/head fonts (Latin + CJK) |
| **Font control** | Full | LatinName, EastAsianName, size, bold, color per shape |
| **Table styling** | Partial | Header row + banded rows. No column widths, no border control, no cell merge. |
| **Chart embedding** | Partial | Pie + Bar charts via ShapeCrawler built-in methods. No line, scatter, area charts. No chart styling (colors, labels, legend). |
| **Shape geometry** | Full | Rectangle, RoundedRectangle, Ellipse, Triangle, Diamond, Arrow, Line |
| **Shape fill/outline** | Full | Fill color, outline color, outline weight |
| **Text alignment** | Partial | Left, Center, Right horizontal. No vertical alignment. |
| **Page numbers** | Partial | Simple text box with slide number. Not a real slide number field — hardcoded text, won't update if slides reorder. |
| **Speaker notes** | Full | Add notes to slides |

### 2.3 PPTX OOXML Coverage Gap (compared to Word audit)

| OOXML Area | What it controls | Word audit coverage | PPTX coverage | Notes |
|------------|-----------------|---------------------|---------------|-------|
| **a:rPr (Run Properties)** | Font, size, bold, italic, color, underline, strikethrough | Full (7 of 14) | Full via ShapeCrawler in PresentationBuilder | `pptx create` (raw XML) only does font size + bold |
| **a:pPr (Paragraph Properties)** | Alignment, indent, spacing, bullet | Full via academic-format | Partial via SlideHelper (alignment, bullet, spacing) | No indent or line spacing control in CLI |
| **a:bodyPr** | Text box margins, autofit, wrap | None in Word | Partial (margins set, no autofit) | Wrap/autofit not exposed |
| **a:xfrm (Transform)** | Position (off), size (ext), rotation (rot) | None in Word (images only) | Full bbox in dissect; rotation in PresentationBuilder | |
| **p:spPr (Shape Properties)** | Fill, outline, effects, geometry | N/A | Full via ShapeCrawler | |
| **p:nvSpPr (Non-Visual Shape Props)** | Shape ID, name, placeholder type | N/A | Full in dissect evidence | |
| **a:graphicFrame (Chart/Table)** | Embedded chart and table references | N/A | Read (detected + relationshipId); Write (Pie/Bar only) | Line, scatter, area not available |
| **p:sldSz** | Slide dimensions | N/A | Hardcoded 960x540 (standard 16:9) | No 4:3 or custom size |
| **p:timing** | Animations and transitions | N/A | None | Not read, not written |

### 2.4 PPTX Gap Summary

| Gap | Priority | Rationale |
|-----|----------|-----------|
| **pptx create has no layout control** | P0 | Raw XML builder in PptxCommands only supports title+subtitle+items. PresentationBuilder has rich layouts but is not exposed through CLI. Users must choose: basic text slides via CLI, or use the .NET library directly. |
| **pptx read has no format info** | P0 | Only text is extracted. No font name, size, color, bold status. An LLM reading a slide can't distinguish heading from body without format context. |
| **No slide template fill** | P1 | Word has `word fill` for template fill. PPTX has no equivalent — can't populate a template .pptx with data. Agricultural presentations (field reports, annual summaries) follow templates. |
| **Chart support is partial** | P1 | Only Pie and Bar via ShapeCrawler. No line, scatter, area. The chart module already generates these as PNG — embedding them as images on slides should be straightforward. |
| **No image placement control** | P1 | `pptx create` can't add images to slides. PresentationBuilder can add shapes but not images. Photo-heavy agricultural presentations need this. |
| **No table creation in CLI** | P1 | PresentationBuilder has AddTableSlide, but `pptx create` has no table spec. Agricultural reports need data tables on slides. |
| **No slide reordering** | P2 | Can't read a PPTX and reorder slides, delete slides, or duplicate slides. |
| **Slide size is hardcoded** | P2 | 960x540 only. Standard 4:3 (1024x768) is still used in some Chinese academic settings. |
| **No animation/transition read or write** | P2 | Academic presentations don't need this. Extension talks might. |

---

## 3. Excel Module — 8 Commands

### 3.1 Command Breakdown

| Command | R/W | What it does | Format depth |
|---------|-----|--------------|--------------|
| `excel sheets` | Read | List worksheet names, positions, row/col counts | Sheet-level only |
| `excel read` | Read | Read cell values as text (with --sheet and --range) | Value only — no style, no formula evaluation |
| `excel to-groups` | Read | Convert treatment/value columns to grouped JSON for chart/ANOVA | Value only |
| `excel create` | Write | Create xlsx from JSON spec (headers + rows, typed values) | No style applied |
| `excel dissect` | Read | Full NongPandoc slice with cell style (font, fontSize, bold, italic, numberFormat), formulas, merged ranges, tables | Very deep — captures style, formula, table structure, merged ranges |
| `excel style` | Write | Apply cell styles from JSON spec (font, fontSize, bold, fillColor, fontColor, numberFormat, border, presets) | Good — range-level styling + Academic/Finance presets |
| `excel formula` | Write | Write formulas from JSON spec (A1 notation, cell or range) | Good — cell-level and range-level formulas |
| `excel pivot` | Write | Create pivot table from JSON spec (rowLabels, columnLabels, values with summary function) | Good |

### 3.2 ExcelBuilder + AdvancedBuilder Capabilities Not Exposed via CLI

| Feature | In Builder? | CLI exposed? | Notes |
|---------|-------------|--------------|-------|
| **Column widths** | Yes (ColumnWidths) | No | `excel create` has no column width control |
| **Number format** | Yes (NumberFormat) | Via `excel style` only | `excel create` can't set number formats inline |
| **Header style** | Yes (HeaderStyle) | Via `excel style` preset only | Builder has explicit HeaderStyle method |
| **Alternating rows** | Yes (AlternatingRows) | Via `excel style` preset | |
| **Merge cells** | Yes (MergeHeader) | No | No CLI command for cell merging |
| **Data validation (dropdown)** | Yes (Dropdown) | No | Critical for field data collection sheets |
| **Conditional formatting (data bars, color scale)** | Yes (DataBars, ColorScale) | No | Color scales are common in agricultural data analysis |
| **Excel tables (structured references)** | Yes (Table) | No | Structured tables enable formula auto-fill and sorting |
| **Freeze panes** | Yes (FreezeHeader) | No | Essential for data entry sheets |
| **Print setup** | Yes (PrintFit) | No | Page setup for printing |
| **Charts (via AdvancedBuilder)** | Yes | No | Excel-native charts (not PNG images) |
| **Images (via AdvancedBuilder)** | Yes | No | Embed images in cells |
| **Sparklines** | Yes (AdvancedBuilder) | No | In-cell mini charts |
| **AutoFilter** | Yes (AdvancedBuilder) | No | Filter dropdowns on headers |
| **Hyperlinks** | Yes (AdvancedBuilder) | No | Cell hyperlinks |
| **Comments/Notes** | Yes (AdvancedBuilder) | No | Cell comments |
| **Rich text in cells** | Yes (AdvancedBuilder) | No | Mixed format within one cell |
| **Worksheet protection** | Yes (AdvancedBuilder) | No | Lock cells, protect sheets |
| **Named ranges** | Yes (AdvancedBuilder) | No | Named ranges for formulas |
| **Sort** | Yes (AdvancedBuilder) | No | Sort data ranges |
| **Hide gridlines** | Yes (HideGridlines) | No | Cleaner presentation |

### 3.3 Excel Gap Summary

| Gap | Priority | Rationale |
|-----|----------|-----------|
| **No data validation (dropdown) CLI** | P0 | Field data collection without dropdowns is error-prone. Agricultural field sheets need constrained inputs (treatment codes, plot IDs, variety names). Builder has it; CLI doesn't. |
| **No freeze panes in create** | P0 | Any data sheet with headers should have frozen first row. Currently requires manual step after creation. |
| **No column width in create** | P1 | JSON spec has no width control. All columns get default width. Tables with long headers (Chinese characters) look broken. |
| **No cell merge command** | P1 | Merged cells are READ in dissect but never WRITTEN via CLI. Common for report headers spanning multiple columns. |
| **No conditional formatting CLI** | P1 | Builder has DataBars and ColorScale. Agricultural data (yield comparisons, growth rates) benefits from visual heatmaps. |
| **No table (ListObject) creation CLI** | P1 | Excel structured tables enable formula auto-fill, banded rows, and named references. The builder has it; CLI doesn't. |
| **No Excel-native charts** | P1 | The chart module generates PNG images. Embedding those as Excel chart objects (editable, data-linked) would be powerful for agricultural statistics reporting. |
| **No print/page setup CLI** | P2 | Page orientation, margins, headers/footers for printing. |
| **excel read returns everything as string** | P2 | Numbers, dates, and booleans lose type information at the CLI boundary. `excel dissect` keeps more info but is package-oriented. |
| **No worksheet management** | P2 | Can't rename, move, copy, delete, or hide worksheets via CLI. |
| **No row height control** | P2 | Neither builder nor CLI. |
| **No hyperlink/comments/rich text** | P2 | Advanced features from AdvancedBuilder. |

---

## 4. Cross-Module Coverage Summary

| Module | Commands | Read vs Write | Format control depth | Primary gap pattern |
|--------|----------|--------------|----------------------|---------------------|
| **Word** (baseline) | 44 | 22R / 22W | Deep — paragraph, run, table, section, image properties | 51 elements: 39% full, 35% partial, 25% none |
| **PDF** | 7 | 5R / 2W | Moderate read (text + bbox + font), minimal write | Write is byte-level only; 0 semantic write commands beyond merge/split |
| **PPTX** | 4 | 3R / 1W | Shallow read (text + shape counts + layout bbox), minimal write (title+items only) | Core library (PresentationBuilder) has rich capabilities not exposed to CLI |
| **Excel** | 8 | 4R / 4W | Deep read (dissect), decent write (style + formula + pivot) | Builder (ExcelBuilder + AdvancedBuilder) has ~15 features unmatched by CLI |

---

## 5. Priority Gap List (All Three Modules)

### P0 — Basic Usage Missing

1. **PPTX: `pptx read` returns text only** — No font/size/color/bold context. LLMs can't distinguish heading from body.
2. **PPTX: `pptx create` has no layout control** — Raw XML builder only does title+subtitle+items. PresentationBuilder's rich layouts (TwoColumns, Cards, Hero, Table, Chart) are not CLI-accessible.
3. **Excel: No data validation (dropdown) or freeze panes in CLI** — Builder has it, CLI doesn't. Field sheets need this.
4. **PDF: Table extraction is pure visual heuristic** — No merged cell, header row, or multi-line cell handling. Agricultural tables lose structure fidelity.

### P1 — Professional Quality

5. **Excel: No column width in `excel create`** — Trivial to add to JSON spec, high visual impact.
6. **Excel: No cell merge command** — Read in dissect, can't write. Report headers need it.
7. **Excel: No conditional formatting CLI** — Builder has DataBars + ColorScale. Good for agricultural data.
8. **Excel: No structured table (ListObject) creation CLI** — Builder has it. Tables enable formula auto-fill.
9. **PPTX: No template fill (`pptx fill`)** — Equivalent of Word's `word fill`. Farmers use templates.
10. **PPTX: PresentationBuilder not exposed via CLI** — The library has rich capabilities (themes, charts, tables, shapes) but `pptx create` bypasses it with raw XML.
11. **PPTX: No image placement in slides** — Can't add images via CLI. Photo-heavy agricultural presentations need this.
12. **PDF: No `pdf compress`** — Large scan PDFs (field photos) need this daily.
13. **PDF: No indent/alignment detection in text extraction** — PdfBlockFormat has the fields but they are never populated.
14. **PDF: Multi-column beyond two not handled** — Three-column Chinese journal layouts are common.
15. **PDF: No rotation command** — Scanned pages often need 90/180/270 degree correction.

### P2 — Nice to Have

16. **Excel: Charts, images, sparklines via CLI** — AdvancedBuilder has them.
17. **Excel: `excel read` preserves types (number, date, boolean)** — Currently all strings.
18. **Excel: Print setup, worksheet management, hyperlinks, comments** — Lower priority.
19. **PPTX: Slide reorder/delete/duplicate** — Read-only for structure now.
20. **PPTX: Custom slide size (4:3 support)** — Hardcoded 16:9 only.
21. **PDF: Metadata extraction (Info dict)** — Title, author, keywords never read.
22. **PDF: Encryption check with actionable message** — Current behavior: cryptic read error.
23. **PDF: `pdf to-word` routing** — Exists in OCR module but users expect it in PDF module.

---

## 6. Top 3 High-Value Recommendations

### Recommendation 1: Expose PresentationBuilder layouts through `pptx create`

**Impact**: Currently `pptx create` builds slides with raw XML — only title/subtitle/bullet items. The PptxCore PresentationBuilder already has 11 layout types (TwoColumns, Cards, BigNumber, Quote, HeroTop, Symmetric, Asymmetric, ThreeColumn, PrimarySecondary, Table, Chart) with 6 theme presets. All of this is unused by the CLI.

**What to do**: Extend the `PptxCreateSpec` JSON schema to accept a `"layout"` field per slide, mapping to PresentationBuilder layout methods. Add `"theme"` at the document level. Keep the raw XML path as fallback for ultra-simple slides.

**Estimated work**: ~300 lines C# in PptxCommands.cs (spec model extension + handler dispatch), ~50 lines in PptxCore (expose builder factory). No new classes needed — just CLI-to-builder bridge code.

### Recommendation 2: Add `excel format` command (merge create + style + freeze + column widths + data validation)

**Impact**: Currently creating a usable Excel file requires 3 separate commands: `excel create` (data), `excel style` (visual), then manual freeze panes in Excel. Every agricultural field data sheet needs dropdowns and frozen headers.

**What to do**: Extend `excel create` JSON spec to include optional `columnWidths`, `freezeHeader`, `dataValidation`, `conditionalFormat`, and `tableStyle` fields — all processed in a single pass. The ExcelBuilder and AdvancedBuilder already have the methods; this is purely a CLI exposure task.

**Estimated work**: ~200 lines C# in ExcelCommands.cs (spec model extension), no new Core classes needed.

### Recommendation 3: Add `pdf compress` and improve `pptx read` with format context

**Impact**: `pdf compress` is the single most-requested missing PDF feature. Large scan PDFs (50MB+) from field cameras and scanners are a daily reality. For PPTX, `pptx read` currently returns text-only — adding font/size/bold context would make LLM-based slide understanding dramatically more accurate.

**What to do**:
- `pdf compress`: Use Docnet page-level re-render at lower DPI + JPEG recompression. Re-pack with PdfPig builder. ~150 lines C#.
- `pptx read --json`: Extend output to include per-text-run format info (font name, size, bold, italic, color) alongside text. The data is available from OpenXML SDK — `pptx read` just doesn't extract it. ~100 lines C# in PptxReader.cs.

---

## 7. Work Estimate

### New Commands

| Command | Module | Lines C# | New Core Class? |
|---------|--------|----------|-----------------|
| `pdf compress` | PDF | ~150 | PdfCompressor (new) |
| `pdf rotate` | PDF | ~80 | Extend DocLib wrapper |
| `pdf metadata` | PDF | ~60 | PdfMetadataReader (new) |
| `pptx fill` | PPTX | ~250 | PptxTemplateFiller (new) |
| `pptx create` (layout extension) | PPTX | ~300 | None — bridge to existing PresentationBuilder |
| `excel create` (extended spec) | Excel | ~200 | None — bridge to ExcelBuilder + AdvancedBuilder |
| `excel merge-cells` | Excel | ~80 | ExcelCellMerger (new, wraps ClosedXML) |

### Enhancements to Existing Commands

| Enhancement | Module | Lines C# |
|-------------|--------|----------|
| `pptx read --json` format context | PPTX | ~100 |
| `excel create` +column widths +freeze +dropdown | Excel | ~100 |
| `pdf dissect` indent/alignment detection | PDF | ~80 |
| `pdf check` encryption detection | PDF | ~30 |
| `pdf dissect` multi-column (3+) detection | PDF | ~120 |

### Total Estimate

| Priority Tier | New Commands | Lines C# |
|---------------|-------------|----------|
| P0 (4 items) | 1 new (pdf compress) + 3 enhancements | ~360 |
| P1 (11 items) | 4 new + 3 enhancements | ~850 |
| P2 (8 items) | 2 new + 2 enhancements | ~340 |
| **Total** | **7 new commands + 8 enhancements** | **~1,550** |

Note: PPTX `create` layout extension is the single largest item (~300 lines) because it bridges the CLI to PresentationBuilder's full API surface. All other items are thin CLI wrappers around existing Core library capabilities.

### Key Finding: The Core Library Is Ahead of the CLI

Unlike the Word module where format gaps required new Core classes (DocxPageSetup, DocxIndenter, etc.), the PDF/PPTX/Excel gaps are overwhelmingly CLI-exposure gaps. The underlying libraries (PdfCore, PptxCore, ExcelBuilder, AdvancedBuilder) already have the capabilities — they just need command-line wrappers. This makes the work estimate smaller and less risky than the Word audit's ~1,450 lines for 6 new Core classes.

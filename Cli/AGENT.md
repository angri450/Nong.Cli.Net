# Agent Contract for nong CLI v3.1.x

## Quick Start

```bash
dotnet tool install --global Angri450.Nong.Cli
nong commands --json       # discover all commands (71 implemented)
nong word read file.docx   # extract text
```

## Command Discovery

`nong commands --json` returns 71 implemented commands. `nong commands --all --json` includes all.
Use this first in any session to know what's available.

## Input Formats

| Command | Expected Input |
|---------|---------------|
| `word read/preview/rebuild/stats/fonts/styles/validate` | .docx |
| `word dissect` | .docx; optional `-o <dir>` writes nongmark/v1 one-cut three-stream output |
| `word fill` | template .docx + data .json |
| `word extract` | .docx + output dir (-o) |
| `word merge` | 2+ .docx + output path (-o) |
| `word outline` | .docx |
| `word images` | .docx + optional output dir (-o) |
| `word comments` | .docx |
| `word revisions` | .docx |
| `word infer-format` | Chinese format text |
| `word fix-order` | .docx + output .docx (-o) |
| `word protect` | .docx + output .docx (-o) + optional --mode (readonly/comments/tracked/forms) + optional -p |
| `word embed-font` | .docx + .ttf/.otf font + output .docx (-o) + optional --name |
| `word add paragraph` | .docx + --spec JSON file or inline JSON + output .docx (-o) + optional --after |
| `word add table` | .docx + --spec JSON file or inline JSON + output .docx (-o) + optional --after |
| `word add footnote/endnote` | .docx + --text + output .docx (-o) + optional --after |
| `word add image` | .docx + --src image + optional --caption + output .docx (-o) + optional --after |
| `word add toc` | .docx + output .docx (-o) + optional --title/--after |
| `word add xref` | .docx + --to bookmark + --text display + output .docx (-o) + optional --after |
| `word add link` | .docx + --url + --text display + output .docx (-o) + optional --after |
| `word add bookmark` | .docx + --name + output .docx (-o) + optional --after |
| `word add comment` | .docx + --text + optional --author + output .docx (-o) + optional --after |
| `word add math` | .docx + --latex formula + optional --display + output .docx (-o) + optional --after |
| `inspect diagnose/refs/classify/structure/evidence/data-req/gap/varplan/semantics` | .txt |
| `inspect write-paper` | spec .json + output .docx (-o) |
| `chart analyze/anova/duncan/bar` | groups .json |
| `chart bar/line/scatter/pie` | spec .json + output .png (-o) |
| `excel sheets/read/to-groups` | .xlsx |
| `excel create` | spec .json + output .xlsx (-o) |
| `diagram flowchart/network` | spec .json + output .png (-o) |
| `diagram tree` | Newick .nwk/.txt or .json + output .png (-o) |
| `pptx read/slides` | .pptx |
| `ocr cloud` | image/pdf + output dir (-o); requires PADDLEOCR_ACCESS_TOKEN |
| `ocr local` | image file; returns E005/E009 (PP-OCRv5 model not yet available) |
| `ocr check-env` | N/A; returns imageAnalyzer, cloudToken, localModel, pythonFallback status |
| `ocr analyze-image` | image file + output dir (-o); no token required |
| `ocr models` | N/A; returns available model list |
| `ocr install-model` | model-id; pp-ocrv5-mobile returns E009, invalid IDs return E006 |
| `ocr to-word` | image/pdf + output .docx (-o) + optional --pages; requires PADDLEOCR_ACCESS_TOKEN |
| `genre list/show` | N/A |
| `icons list/search` | N/A |
| `skill validate/scan/inventory/package` | directory path |

**Note:** Use `nong skill` instead of the deprecated `skill-manager` global tool.

## Groups JSON Format

```json
{
  "GroupA": [1.2, 1.3, 1.1],
  "GroupB": [2.0, 2.2, 2.1]
}
```

This is the output of `nong excel to-groups`.

## Chart Spec Formats

Line chart:
```json
{"title":"Growth","xLabel":"Days","yLabel":"Height","series":[{"name":"A","x":[0,7,14],"y":[1,2,3]}]}
```

Scatter plot:
```json
{"title":"Correlation","xLabel":"pH","yLabel":"Yield","points":[{"x":6.1,"y":12.3,"group":"A"}],"trendline":true}
```

Pie chart:
```json
{"title":"Composition","values":[{"label":"A","value":30},{"label":"B","value":70}]}
```

## Excel Create Spec

```json
{"sheets":[{"name":"Data","headers":["A","B"],"rows":[[1,2],[3,4]]}]}
```

## OCR Commands

### ocr cloud

Converts image/PDF to structured text via PaddleOCR-VL-1.6. Requires `PADDLEOCR_ACCESS_TOKEN` (the old `PADDLEOCR_TOKEN` is deprecated).

```bash
nong ocr cloud scan.png -o out/ --json
nong ocr cloud doc.pdf -o out/ --json
```

Response includes per-page block details with labels, content, and bounding boxes.

### ocr check-env

```bash
nong ocr check-env --json
```

Returns:
```json
{
  "data": {
    "imageAnalyzer": "ok",
    "cloudToken": "missing",
    "localModel": { "ppOcrV5Mobile": "missing" },
    "pythonFallback": "unavailable"
  }
}
```

### ocr analyze-image

Analyzes image structure (dimensions, whitespace ratio, content regions, ASCII map). No token required.

```bash
nong ocr analyze-image scan.png -o out/ --json
```

Generates `image-analysis.json` and `image.map.txt` in output directory.

### ocr to-word

Converts image/PDF to .docx via PaddleOCR-VL-1.6 cloud API. Requires `PADDLEOCR_ACCESS_TOKEN`.

```bash
nong ocr to-word scan.png -o out.docx --json
nong ocr to-word doc.pdf -o out.docx --pages "1-5" --json
```

### ocr models

Lists available OCR models for local installation.

```bash
nong ocr models --json
```

Returns `data.models` as an array. Currently empty (PP-OCRv5 ONNX not yet available).

### ocr install-model

Installs a local OCR model. `pp-ocrv5-mobile` returns E009 (not yet available). Invalid model IDs return E006.

```bash
nong ocr install-model pp-ocrv5-mobile --json
nong ocr install-model invalid-id --json
```

## Diagram Tree Input

Newick text:
```
((A:0.1,B:0.2):0.3,C:0.4);
```

Or JSON:
```json
{"newick":"((A:0.1,B:0.2):0.3,C:0.4);","title":"Tree"}
```

## JSON Output Schema

Every command with `--json` returns:

```json
{
  "status": "ok" | "error",
  "command": "word read",
  "summary": "Extracted 29 paragraphs",
  "data": {},
  "issues": [],
  "artifacts": { "docx": "out.docx" },
  "metrics": { "paragraphs": 29 },
  "errors": [],
  "meta": { "durationMs": 42, "version": "3.1.0" }
}
```

## Error Codes

| Code | Name | Meaning |
|------|------|---------|
| E001 | file_not_found | File does not exist |
| E002 | unsupported_format | Wrong file extension |
| E003 | missing_argument | Required argument missing |
| E004 | internal_error | Unexpected error |
| E005 | dependency_missing | Required tool/token not installed |
| E006 | validation_failed | Content check failed |
| E007 | read_failed | Could not read document |
| E008 | write_failed | Could not write output |
| E009 | not_implemented | Command is not yet implemented |

## Common Workflows

### Read a docx
```
nong word read paper.docx
→ pure text to stdout
```

### Diagnose a paper (full or stepwise)
```
nong inspect diagnose paper.txt --json
nong inspect classify paper.txt --json     # paper type only
nong inspect gap paper.txt --json          # gap grade only
→ Read: data.paperType, data.gapGrade, data.evidence[*].adequate
```

### Excel → Statistics → Chart
```
nong excel to-groups data.xlsx --group A --value B --raw > groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
→ Read: artifacts.png for the generated chart
```

### Generate charts from specs
```
nong chart line line-spec.json -o line.png --json
nong chart scatter scatter-spec.json -o scatter.png --json
nong chart pie pie-spec.json -o pie.png --json
```

### Generate a paper from spec
```
nong inspect write-paper spec.json -o paper.docx --json
→ Read: artifacts.docx
```

### Audit a document
```
nong word stats paper.docx --json
nong word fonts paper.docx --json
nong word validate paper.docx --json
nong word dissect paper.docx -o paper.slice --json
```

### Extract PPTX content
```
nong pptx read slides.pptx --json
nong pptx slides slides.pptx --json
```

### Skill lifecycle
```
nong skill validate ./word --json
nong skill scan ./plugin --json
nong skill package ./plugin --json
```

### OCR pipeline
```
nong ocr check-env --json
nong ocr cloud scan.png -o out/ --json
nong ocr to-word scan.png -o out.docx --json
```

### Image analysis
```
nong ocr analyze-image scan.png -o analysis/ --json
```

### Document editing (add series)
```
nong word add paragraph doc.docx --spec paragraph.json -o out.docx
nong word add table doc.docx --spec table.json -o out.docx
nong word add image doc.docx --src chart.png -o out.docx
nong word add math doc.docx --latex "E=mc^2" --display -o out.docx
```

## Failure Handling

1. Check `status` field — "error" means the command failed.
2. Read `errors[0].code` for the error code.
3. Read `errors[0].message` for human-readable description.
4. Common fixes:
   - E001: Check file path, use absolute paths.
   - E002: Ensure file extension matches expected format.
   - E005: Install missing tool or set required token (e.g. PADDLEOCR_ACCESS_TOKEN).
   - E009: Command is not yet implemented.

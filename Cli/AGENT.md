# Agent Contract for nong CLI v4.1.x

## Quick Start

```bash
dotnet tool install --global Angri450.Nong.Cli
nong commands --json                 # discover 126 implemented commands
nong commands --format openai-tools  # emit 126 OpenAI tool schemas
nong word read file.docx             # extract DOCX text
```

Use `nong commands --json` first in any session. It is the canonical command and parameter contract.

## Architecture

The main `nong` tool is a light router plus pure .NET built-ins. Heavy native groups are external dotnet tools, but user command names stay stable.

| User command | Tool command | PackageId |
|--------------|--------------|-----------|
| `nong chart ...` | `nong-chart` | `Angri450.Nong.Tool.Chart` |
| `nong diagram ...` | `nong-diagram` | `Angri450.Nong.Tool.Diagram` |
| `nong pdf ...` | `nong-pdf` | `Angri450.Nong.Tool.Pdf` |
| `nong pptx ...` | `nong-pptx` | `Angri450.Nong.Tool.Pptx` |
| `nong ocr ...` | `nong-ocr` | `Angri450.Nong.Tool.Ocr` |
| `nong word images ...` / `nong word crop ...` | `nong-imaging` | `Angri450.Nong.Tool.Imaging` |

Built-in groups stay in the main CLI: `word`, `excel`, `inspect`, `lit`, `genre`, `icons`, `slice`, `skill`, and `progress`.

## Command Discovery

Current local discovery returns:

- `126 commands available`
- 126 OpenAI tool schemas
- `meta.version = "4.1.2"`

Current group counts:

| Group | Count |
|-------|------:|
| `word` | 51 |
| `inspect` | 12 |
| `excel` | 8 |
| `genre` | 2 |
| `icons` | 2 |
| `skill` | 4 |
| `lit` | 5 |
| `slice` | 4 |
| `progress` | 1 |
| `chart` | 11 |
| `diagram` | 3 |
| `ocr` | 11 |
| `pdf` | 8 |
| `pptx` | 4 |

Do not hard-code command counts in agent logic. Parse `nong commands --json` when exact availability matters.

## Input Formats

| Command family | Expected input |
|----------------|----------------|
| `word check/convert` | `.doc` or `.docx`; `.doc` uses LibreOffice or Word COM only as a boundary converter |
| `word create` | `.nongmark`, `.nmk`, or JSON document spec |
| `word read/preview/rebuild/stats/fonts/styles/validate/dissect` | `.docx` |
| `word add ...`, `word page-setup`, `word cell-format`, `word run-format` | `.docx` plus JSON/text options and an output `.docx` |
| `inspect ...` | `.docx` or text input as reported by `nong commands --json` |
| `excel ...` | `.xlsx` or JSON spec |
| `chart ...` | CSV/JSON data; output is usually PNG or analysis JSON |
| `diagram ...` | JSON diagram specs or Newick for `diagram tree` |
| `pptx ...` | `.pptx` or JSON slide spec |
| `pdf ...` | `.pdf`; `pdf ocr --with-ocr` routes through local PP-OCRv6 |
| `ocr cloud/to-word` | image/PDF/URL plus `PADDLEOCR_ACCESS_TOKEN` |
| `ocr local` | image file; local PP-OCRv6 through pure .NET runtime, no Python |
| `ocr install-model` | `pp-ocrv6`, `pp-ocrv6-medium`, `pp-ocrv6-small`, `pp-ocrv6-tiny`, or legacy `pp-ocrv5-mobile` |
| `lit ...` | CNKI-like query string via `--query`, or result JSON for export |
| `slice ...` | `nong-pandoc/package/v1` directory |
| `skill ...` | skill or plugin directory |

## OCR Contract

Local OCR is PP-OCRv6-first.

Use:

```bash
nong ocr models --json
nong ocr install-model pp-ocrv6-medium --json
nong ocr local scan.png --json
```

Supported v6 install IDs are `pp-ocrv6`, `pp-ocrv6-medium`, `pp-ocrv6-small`, and `pp-ocrv6-tiny`. `pp-ocrv5-mobile` remains a legacy compatibility path for the first-party native runtime cache.

The native OCR runtime is maintained in the sibling `Nong.OcrRuntime` repository under the `Angri450.Nong.OcrRuntime.*` package prefix. `Cli/Common/OcrRuntimeVersion.cs` is intentionally independent from `Cli/Common/CliVersion.cs`; do not bump it unless the sibling runtime repo published a new validated native runtime.

Never tell users to install Python, pip, `paddleocr`, or a local OCR executable for local OCR core functionality.

## JSON Output Schema

Every command with `--json` returns:

```json
{
  "status": "ok",
  "command": "word read",
  "summary": "Extracted 29 paragraphs",
  "data": {},
  "issues": [],
  "artifacts": { "docx": "out.docx" },
  "metrics": {},
  "errors": [],
  "meta": { "durationMs": 42, "version": "4.1.2" }
}
```

Read `status` first. On failure, read `errors[0].code` and `errors[0].message`. Error codes are `E001` through `E009`: file not found, unsupported format, missing argument, internal error, dependency missing, validation failed, read failed, write failed, and not implemented.

## Common Workflows

### Word Repair

```bash
nong word repair-plan --json
nong word fix-order input.docx -o input.ooxml-fixed.docx --json
nong word academic-format input.docx -o input.academic-fixed.docx --json
nong word format-audit input.academic-fixed.docx --fail-on-warning --min-score 95 --json
```

`word fix-order` repairs internal OOXML ordering. It is not proof that visible formatting is fixed. For visible formatting requests, use `word academic-format`, then verify with `word format-audit`.

### Slice Inspection

```bash
nong word dissect paper.docx -o paper.slice --json
nong slice inspect paper.slice --strict --json
nong slice blocks paper.slice --json
nong slice block paper.slice p0001 --json
```

Read `content.nongmark` and `slice inspect` evidence before handing a slice to an AI. `preview/content.txt` is a lossy preview, not canonical content.

### Excel to Chart

```bash
nong excel to-groups data.xlsx --group A --value B --raw > groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
```

### PDF

```bash
nong pdf check guide.pdf --json
nong pdf dissect guide.pdf -o guide.slice --json
nong pdf render guide.pdf -o pages --dpi 200 --json
nong pdf ocr scan.pdf -o searchable.pdf --with-ocr --json
```

### Literature

```bash
nong lit parse --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --json
nong lit plan --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --sources openalex,crossref,unpaywall --json
nong lit export --input refs.json --format bibtex --out refs.bib --json
```

Stage19 providers are OpenAlex, Crossref, and Unpaywall only. The CLI does not scrape commercial databases, bypass paywalls, or automatically translate Chinese-English synonyms.

## Failure Handling

1. Check `status`.
2. If `status` is `error`, read `errors[0].code`.
3. For `E005`, install the missing tool/runtime/converter or set the required token.
4. For local OCR, install `pp-ocrv6-medium` unless the user explicitly needs the legacy `pp-ocrv5-mobile` path.
5. For external heavy modules, let `nong` auto-install the corresponding `Angri450.Nong.Tool.*` package unless the user requested a direct tool install.

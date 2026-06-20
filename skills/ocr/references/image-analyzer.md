# Image Structure QA Reference

Use Nong image analysis for structural QA:

```powershell
nong ocr analyze-image chart.png -o chart.analysis --json
```

This command does not perform OCR and does not recognize text. It analyzes image structure for quality checks such as whitespace, content bounds, and visible content regions.

## When To Use

- After `nong chart ...` generates a PNG.
- After `nong diagram ...` generates a PNG.
- When a user asks whether a figure appears blank, badly cropped, too sparse, or shifted.
- Before OCR, when you need a layout sanity check without consuming cloud credentials.

## Output Handling

Use the JSON result to inspect structural metrics and generated artifacts. Treat `status: "error"` as failed and fix the image path or upstream rendering issue before retrying.

## Boundary

Do not present `ocr analyze-image` as OCR, semantic image understanding, chart data extraction, or text recognition. For actual recognition, use:

```powershell
nong ocr cloud scan.png -o ocr-out --json
```

Cloud recognition requires `PADDLEOCR_ACCESS_TOKEN`.

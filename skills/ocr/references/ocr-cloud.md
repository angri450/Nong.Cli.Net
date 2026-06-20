# Cloud OCR Reference

Nong.Toolkit.Net routes cloud OCR through Nong:

```powershell
nong ocr cloud scan.png -o ocr-out --json
nong ocr to-word scan.png -o out.docx --json
```

Both commands require `PADDLEOCR_ACCESS_TOKEN` in the environment. Do not place real access tokens in repository files, command logs, or examples.

Token source: PaddleOCR/AI Studio access-token page, `https://aistudio.baidu.com/account/accessToken`. If the user has no token, say cloud OCR is blocked by credentials and use `ocr local` only after local smoke passes.

## Cloud Recognition

```powershell
$env:PADDLEOCR_ACCESS_TOKEN = "<access-token>"
nong ocr cloud scan.png -o ocr-out --json
```

Use this for image or PDF recognition when cloud access is available. If the environment variable is missing, report that cloud smoke is skipped or blocked by credentials.

Use cloud OCR rather than local OCR when the user needs PDF handling, multi-page page alignment, table/layout labels, figure/title detection, or output that will be merged with Word/NongMark evidence. Local OCR is only a single-image text recognizer.

## Word Output

```powershell
$env:PADDLEOCR_ACCESS_TOKEN = "<access-token>"
nong ocr to-word scan.png -o out.docx --json
```

Use this when the requested output is a `.docx` generated from a scanned image or PDF.

For Word/NongMark alignment work, keep the cloud page/block output as page evidence and pair it with `nong word dissect <docx> --output <slice-dir> --json`. The canonical Word side remains `nongmark/v1` (`document.json`, `content.jsonl`, `structure.json`, `format.json`, `assets/manifest.json`); cloud Markdown or plain text is preview evidence, not the canonical edit source.

## Cross-Page Images

Some PDFs contain figures or scanned regions split across page boundaries. Do not pretend local OCR can solve this. Treat cross-page stitching as a cloud/document preprocessing task:

- Render or collect page images with page numbers and dimensions.
- Detect image regions near page bottoms/tops.
- Stitch only when adjacent page regions have matching horizontal bounds and continuation evidence.
- Preserve a manifest mapping stitched images back to source page numbers and source bboxes.
- Feed the stitched asset into cloud OCR/layout only after the source mapping is recorded.

## Contract

- Use `--json` for model-readable results.
- Treat `status: "error"` as failed.
- Do not call cloud OCR through custom wrappers from Nong.Toolkit.Net.
- Do not claim cloud smoke success unless the credential is present and the command exits 0.
- Do not use local OCR output to make layout, table, Word formatting, or pandoc/NongMark alignment claims.

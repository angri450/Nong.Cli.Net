# Local OCR Success

## What the user wants

Recognize Chinese text in a single scanned image using local OCR.

## What was done

```powershell
# Check the environment
nong ocr check-env --json

# Output: localDotNetPpOcrV5.status=ok

# Run local OCR on a scanned receipt image
nong ocr local scanned_receipt.png --json
```

## Result

```json
{
  "status": "ok",
  "command": "ocr local",
  "data": {
    "blocks": [
      {"text": "内蒙古农业大学", "confidence": 0.98},
      {"text": "实验记录表", "confidence": 0.95}
    ]
  }
}
```

## Key takeaways

- Always run `ocr check-env` first. If `localDotNetPpOcrV5.status` is not `ok`, install the model before attempting `ocr local`.
- `ocr local` is single-image only. Do not pass a PDF.
- If the image is a barcode or QR code, `ocr local` will return `E006 validation_failed` with `local_ocr_preflight_skipped`. Use the decoded barcode value from `data.preflight.barcode`.

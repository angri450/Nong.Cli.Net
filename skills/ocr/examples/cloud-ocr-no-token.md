# Cloud OCR Without Token

## What the user wants

Use cloud OCR to analyze a multi-page PDF with table reconstruction.

## What happened (failure)

```powershell
nong ocr cloud report.pdf -o ocr-out --json
```

```json
{
  "status": "error",
  "errors": [{
    "code": "E004",
    "message": "PADDLEOCR_ACCESS_TOKEN is not set"
  }]
}
```

## How it was resolved

1. Told the user cloud OCR requires a PaddleOCR access token.
2. User obtained the token from `https://aistudio.baidu.com/account/accessToken`.
3. Set the environment variable:
   ```powershell
   $env:PADDLEOCR_ACCESS_TOKEN = "<token>"
   ```
4. Reran the command successfully.

## Key takeaways

- Cloud OCR (`ocr cloud`, `ocr to-word`) always requires `PADDLEOCR_ACCESS_TOKEN`.
- Do NOT write the token value in logs, JSON output, or committed files.
- If the user cannot get a token, `ocr local` is the alternative — but it is single-image only, no PDF, no layout analysis.
- Check `ocr check-env --json` first to confirm `cloudPaddleOcrVl.status` before attempting cloud commands.

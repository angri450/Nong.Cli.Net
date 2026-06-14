# Angri450.Nong.OcrModels

PP-OCRv6 dictionary files for Nong local OCR.

Contains `ppocrv6_dict.txt` (18,708 chars, medium/small) and `ppocrv6_tiny_dict.txt` (6,904 chars, tiny). No native binaries, no model weights, no Python.

## Usage

This package is consumed by `Angri450.Nong.MultiModal`. End users install models via:

```powershell
nong ocr install-model pp-ocrv6-medium --json
```

Dictionary files are embedded resources extracted at model install time.

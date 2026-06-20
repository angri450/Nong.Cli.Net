---
name: export
description: Export documents to alternative formats through nong. Trigger on EPUB, HTML, LaTeX, ODF export requests. Supports nong export epub|html|latex|odf <file.docx>.
---

# Export

Use `nong export` to convert Nong.Cli.Net documents (.docx, .nongmark) to alternative formats. No external software dependency — all exports are pure .NET.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command.

## Implemented Commands

```powershell
nong export epub <file.docx> -o <out.epub> [--title <str>] [--author <str>] [--json]
nong export html <file.docx> -o <out.html> [--json]
nong export latex <file.docx> -o <out.tex> [--title <str>] [--json]
nong export odf <file.docx> -o <out.odt> [--json]
```

## Workflows

### Academic paper to LaTeX
```powershell
nong export latex paper.docx -o paper.tex --title "My Paper"
# Compile with: pdflatex paper.tex  (requires local LaTeX installation)
```

### Reading material to EPUB
```powershell
nong export epub book.docx -o book.epub --title "Title" --author "Author"
```

### Web publishing
```powershell
nong export html document.docx -o index.html
```

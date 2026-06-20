---
name: markdown
description: Markdown ↔ NongMark bidirectional conversion via nong. Trigger on .md file conversion, GFM to NongMark, or NongMark to Markdown requests. Zero external dependencies — hand-rolled lightweight parser.
---

# Markdown

Use `nong markdown` for bidirectional conversion between GFM (GitHub Flavored Markdown) and NongMark. No external markdown library dependency.

NongMark is the authoritative source format for document creation. Markdown is a **one-way input** that gets converted to NongMark, then the full nong pipeline takes over.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command.

## Implemented Commands

```powershell
nong markdown to-nongmark <file.md> -o <out.nongmark> [--json]
nong markdown to-md <file.nongmark> -o <out.md> [--json]
```

## Workflows

### Markdown notes → Word document
```powershell
nong markdown to-nongmark notes.md -o notes.nongmark
nong word create notes.nongmark -o notes.docx
```

### NongMark → Markdown (for GitHub/portability)
```powershell
nong markdown to-md paper.nongmark -o README.md
```

## Supported GFM Syntax

| Feature | Supported |
|---------|-----------|
| ATX headings (`#`-`######`) | ✅ |
| Unordered lists (`-` `*` `+`) | ✅ |
| Ordered lists (`1.` `2.`) | ✅ |
| Blockquotes (`> `) | ✅ |
| Fenced code blocks (` ``` `) | ✅ |
| Horizontal rules (`---`) | ✅ |
| Inline formatting (`**bold**` `*italic*`) | Pass-through |
| Links / Images | Pass-through |

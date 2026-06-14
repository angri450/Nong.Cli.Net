# Angri450.Nong.Pandoc

Pure .NET Pandoc-style document model for Nong.NET.

This package is not a vendored copy of GPL Pandoc and does not require
`pandoc.exe`. It provides Nong's own semantic document layer and shared
cross-package stream contract:

```text
a.docx / a.pdf / a.pptx / a.xlsx
  -> a.slice/
     manifest.json
     document.json
     content.jsonl
     content.nongmark
     structure.json
     format.json
     assets/manifest.json
```

## Scope

- Semantic document AST: metadata, headings, paragraphs, lists, block quotes,
  tables, figures, references, raw extension blocks.
- Shared stream names and package manifest for Word/PDF/PPT/Excel adapters.
- NongMark text reader/writer for human and AI editing.
- Apache-2.0 package boundary.

## Non-goals

- It is not a full Pandoc clone.
- It does not bundle Pandoc's Haskell source or binary.
- It does not make Markdown the Word formatter.
- It does not store Word OOXML layout as rich text.

## Example

```csharp
using PandocCore;

var doc = new NongPandocDocument
{
    Metadata = { ["title"] = "校企共建方案书" },
    Blocks =
    {
        NongHeadingBlock.FromText(1, "项目摘要", "sec-summary"),
        NongParagraphBlock.FromText("中文正文（Solanum lycopersicum）继续正文。"),
        new NongTableBlock
        {
            Caption = "表1 任务分解",
            Style = "three-line",
            Headers = { "任务", "指标" },
            Rows = { new List<string> { "论文", "SCI 二区" } }
        }
    }
};

var nongmark = NongMarkTextWriter.Write(doc);
var parsed = NongMarkTextReader.Read(nongmark);
```

## Design Rule

NongMark records what the content is. Word formatters decide how it should
look. For example, `style="three-line"` means a semantic academic table; the
DOCX formatter owns the final 1.5 pt / 0.75 pt / 1.5 pt border evidence.

See `CONSTRUCTION_PLAN.md` for the cross-package alignment plan.

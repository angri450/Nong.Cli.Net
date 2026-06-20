# Format Feature Library Index

Format features extracted from various templates via the Read Word capability, organized by category.

## By Category

### Journal Papers

- [journal-paper](journal-paper.json) — Chinese journal academic paper, GB/T 7714-2025 sequential numbering

### Degree Theses

- [degree-thesis](degree-thesis.json) — Graduate degree thesis format

### Course Papers

- [course-paper](course-paper.json) — Undergraduate course paper format

### Contest Papers

- [life-sciences-contest](life-sciences-contest.json) — Life sciences contest paper (4-page limit, no personal info)

## By Feature

### Three-Line Tables
- journal-paper: top/bottom 0.75pt, header underline 0.5pt
- degree-thesis: same
- course-paper: same

### Reference Format
- journal-paper: sequential numbering [1][2][3], Word native numbering, GB/T 7714-2025
- degree-thesis: sequential numbering, 小五号 宋体
- course-paper: sequential numbering, 小五号 宋体

### Body Text Indent
- All: first-line indent 2 characters (420 twips)
- Exception: table cell text, references, headings — no indent

### Body Font Size
- journal-paper: 五号(21) 宋体
- degree-thesis: 小四号(24) 宋体
- course-paper: 五号(21) 宋体

## Changelog

| Date | Category | File |
|------|----------|------|
| 2026-05-30 | v2.0 Update | DocxCore v2.0: +template engine +charts +TOC +footnotes/endnotes +comments +track changes +content controls +font embedding +document merge +paper analysis (16 types, evidence chain, data requirements, quality diagnosis) |
| 2026-05-24 | Contest | life-sciences-contest.json |
| 2026-05-23 | Journal | journal-paper.json |
| 2026-05-23 | Thesis | degree-thesis.json |
| 2026-05-23 | Course | course-paper.json |

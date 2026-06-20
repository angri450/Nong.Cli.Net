---
name: slice
description: NongPandoc slice package inspection through nong. Trigger on slice inspect, content blocks, block IDs, package assets, content.nongmark, content.jsonl, manifest.json, AI read order, strict provenance evidence, or unified Word/PDF/Excel/PPTX package reads.
---

# Slice

Use `nong slice` as the canonical reader for NongPandoc packages produced by `word dissect`, `pdf dissect`, `excel dissect`, and `pptx dissect`.

## Nong CLI Preflight

Read [../references/nong-cli-preflight.md](../references/nong-cli-preflight.md) before the first Nong CLI command in a session. Confirm the `nong` CLI is installed and the `slice` command group.

## Commands

```powershell
nong slice inspect <slice-dir> --json
nong slice inspect <slice-dir> --strict --json
nong slice blocks <slice-dir> --json
nong slice block <slice-dir> <block-id> --json
nong slice assets <slice-dir> --json
```

## Dispatch

1. Run `slice inspect` before handing a package to an AI workflow.
2. Use `slice inspect --strict` when claims depend on provenance, page, bbox, format, asset, or diagnostic evidence.
3. Use `slice blocks` to list canonical block IDs and block order.
4. Use `slice block <id>` for block-level reads instead of opening format-specific package files directly.
5. Use `slice assets` when images, embedded media, page renderings, or extracted assets matter.

## Evidence Rules

- Treat `content.nongmark` and `content.jsonl` as canonical semantic streams.
- Treat `preview/content.txt` or `preview/content.md` as lossy previews.
- Read `data.aiReadOrder`, `data.streams`, `data.metrics`, `data.warnings`, `data.evidence`, and `artifacts` before claiming a slice is complete.
- Warnings are part of the result. Report them when they affect reading order, missing streams, suspicious fonts, missing assets, or strict evidence.
- If `slice block` cannot find an ID, go back to `slice blocks` and choose a real block ID.

## Boundaries

Do not parse package internals ad hoc when `nong slice` can answer the question. Do not treat a successful `dissect` command as proof that block-level evidence is clean; run the slice reader.

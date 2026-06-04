# 2026-06-05 Word real-case contract matrix repair

## Context

Real-case file:

`C:\Users\Administrator\Documents\Github\改-技术服务合同---内蒙古沸石土传病害研制项目.doc`

The document is a legacy `.doc` technical service contract. Word COM conversion to `.docx` produced a heavily table-wrapped document with legacy Word/WPS compatibility XML.

## Consumer Feedback That Triggered This Repair

The user feedback from the earlier Word-formatting session was direct and accurate:

- In that session, `nong` effectively had zero participation. The work fell back to raw Word COM automation.
- The root mismatch was scenario-level: `Angri450.Nong.Docx` had been strongest at generating new OpenXML documents, while the actual job was modifying the layout of an existing legacy `.doc` contract.
- Raw Word COM was painful in exactly the places a stable toolchain should avoid:
  - Word process startup and cleanup.
  - Opaque HRESULT errors.
  - File locks from leftover `WINWORD` processes.
  - Fragile merged-cell traversal.
  - `.doc` binary dependence on installed Word.
  - Poor idempotence and poor CI testability.
- The ideal `nong` experience should be a deterministic pipeline:
  - convert or read existing document
  - inspect structure and formatting
  - apply paragraph/table/style rules
  - repair OOXML compatibility artifacts
  - write schema-valid `.docx`
- The biggest product gap remains a first-class existing-document editing surface. A future `DocumentReader` plus existing writer/edit operations should make this path explicit: read -> traverse -> match rules -> modify fonts/spacing/borders -> write.

This repair does not pretend that the whole gap is closed. It closes the practical blockers exposed by the real contract sample and records the remaining product gap below.

## What Was Run

Created case workspace:

`C:\Users\Administrator\Documents\Github\word-realcase-20260605-083042`

The matrix script now exercises 46 Word/Inspect/library steps:

- `.doc` to `.docx` conversion artifact
- `word read/preview/stats/fonts/styles/validate/outline/images/comments/revisions/extract/dissect`
- `word fix-order/rebuild/protect/embed-font/merge/fill`
- `word add-*` paragraph/table/footnote/endnote/image/toc/bookmark/xref/link/comment/math
- contract template extraction and fill
- contract-to-paper draft generation
- contract-to-gongwen formatting via `GongWenFormatter`
- official letter generation via `OfficialDocWriter`
- inspect classify/structure/diagnose/refs/varplan/evidence/data-req/gap/semantics on extracted text

Final real-case result:

- `46/46` matrix steps passed.
- `word fix-order` repaired the converted contract from invalid OOXML to schema-valid DOCX.
- Generated contract template, filled template, paper draft, formatted-gongwen contract, and official-letter rewrite all validate cleanly.

## Repairs

- Fixed `ImageLister.ExtractFileName` so `word images` handles relative OpenXML part URIs instead of throwing `This operation is not supported for a relative URI`.
- Reworked font embedding to attach `FontPart` to `FontTablePart` using `FontPartType.FontOdttf`, write `EmbedRegularFont`, and use proper OOXML font obfuscation key handling.
- Expanded `ElementOrder` for real legacy Word/WPS artifacts:
  - `sectPr` now uses `cols`, not `col`.
  - `tr` ordering includes `tblPrEx` before `trPr` and table cells.
  - `pPr` ordering includes `framePr`.
  - `style` ordering uses `next`.
- Added compatibility sanitization:
  - Removes false `w:noWrap w:val="0"` from table cell properties.
  - Removes invalid `w:tblStyle` inside table-style `w:tblPr`.
- Wired sanitizer into `word fix-order` and `GongWenFormatter`.
- Fixed `OfficialDocWriter` run property ordering so generated official documents validate.
- Relaxed `WordPreview` style-reference diagnosis so legacy documents whose `styleId` is numeric but whose style name is `Normal` do not produce a false undefined-style error.
- Added regression tests for legacy compatibility cleanup, image listing after add-image, and font embedding.

## Validation

- `dotnet build .\Angri450.Nong\Cli\NongCli.csproj -c Release --nologo`: PASS, 18 existing warnings.
- `dotnet test .\Angri450.Nong\Cli.Tests\Cli.Tests.csproj -c Release --nologo`: PASS, 62/62.
- `pwsh -NoProfile -ExecutionPolicy Bypass -File .\word-realcase-20260605-083042\run-realcase-matrix.ps1`: PASS, 46/46.
- Real-case `word preview` diagnostics now report `0 errors` on both rebuilt and gongwen-formatted outputs.

## Remaining Product Gap

The source has `GongWenFormatter` and `OfficialDocWriter`, but the CLI still lacks a first-class command such as `nong inspect write-official` or `nong word format-gongwen`. The real-case matrix uses library calls for that part.

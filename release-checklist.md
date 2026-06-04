# Release Checklist — nong CLI v3.1.x

Date: 2026-06-03

## Build

- [x] `dotnet build Cli/NongCli.csproj -c Release` — 0 errors
- [x] `dotnet test Cli.Tests/Cli.Tests.csproj -c Release` — 58 PASS, 0 SKIP

## Contract

- [x] `nong commands --json` returns 71 implemented commands
- [x] `nong commands --all --json` returns 71 total (71 impl)
- [x] All real commands return stable JSON schema
- [x] Error codes E001-E009 documented
- [x] AGENT.md written
- [x] CAPABILITY.md current

## Implemented (71 commands across 10 groups)

| Group | Count | Commands |
|-------|-------|----------|
| word | 30 | read, preview, fill, rebuild, extract, dissect, stats, fonts, styles, validate, merge, outline, images, comments, revisions, infer-format, fix-order, protect, embed-font, add paragraph, add table, add footnote, add endnote, add image, add toc, add xref, add link, add bookmark, add comment, add math |
| inspect | 10 | diagnose, refs, write-paper, classify, structure, varplan, evidence, data-req, gap, semantics |
| chart | 7 | analyze, bar, anova, duncan, line, scatter, pie |
| excel | 4 | sheets, read, to-groups, create |
| diagram | 3 | flowchart, network, tree |
| genre | 2 | list, show |
| icons | 2 | list, search |
| skill | 4 | validate, scan, inventory, package |
| pptx | 2 | read, slides |
| ocr | 7 | cloud, local, check-env, analyze-image, models, install-model, to-word |

`ocr local` returns E005 (model missing) or E009 (inference not yet implemented) — honest behavior.
`ocr install-model pp-ocrv5-mobile` returns E009 — PP-OCRv5 ONNX not yet available.

## Known Limitations

- word merge: uses AdvancedFeatures.AppendDocument and returns warnings for headers/footers, numbering, and style-name conflict boundaries
- ocr cloud: markdown output only; PADDLEOCR_ACCESS_TOKEN required (PADDLEOCR_TOKEN deprecated)
- ocr local: PP-OCRv5 ONNX model not yet released; returns E005 or E009
- ocr install-model: PP-OCRv5 ONNX model not yet available; returns E009
- Duncan MRT: simplified Q-value approximation; formal papers should verify
- word rebuild: input/output must differ
- word fix-order / protect / embed-font / add: input/output must differ

## Skill Manager Legacy

- [x] `dotnet build SkillManager/SkillManager.Cli.csproj -c Release` — 0 errors (legacy only)
- [ ] Do NOT pack or publish `Angri450.Nong.Skill.Manager` in 3.1.x/2.0.0 migration
- [x] Only `Angri450.Nong.Cli` is the public tool entry point
- [x] Users should use `nong skill validate/scan/inventory/package` instead of `skill-manager`

## NuGet Publish (pending)

- [ ] Push to NuGet.org: `dotnet nuget push nupkg/Angri450.Nong.Cli.3.1.0.nupkg --api-key $env:NUGET_API_KEY`
- [ ] GitHub Release: `gh release create v3.1.0-cli nupkg/Angri450.Nong.Cli.3.1.0.nupkg`

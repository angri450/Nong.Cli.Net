# 2026-06-08 Word Format Audit Visible Evidence

## Goal

Build `word format-audit` as a productized visual-format evidence command, then use it with `word academic-format` on the three feedback DOCX files under `C:\Users\Administrator\Documents\Github\测试`.

## Changes

- Added `word format-audit` for read-only visible formatting evidence instead of treating `word validate` as completion.
- Added academic evidence for headings, body paragraphs, fonts, line spacing, tables, and Latin names inside parentheses.
- Added three-line table audit evidence: top line 1.5 pt, header separator 0.75 pt, bottom line 1.5 pt, no vertical borders, no shaded headers/cells, and no first-line indent inside table cells.
- Updated `word academic-format` to normalize heading styles, body styles, font evidence, line spacing, document-grid squeeze risk, three-line table borders, table-cell indentation, shading, parenthetical Latin italics, and common chemical formula subscripts such as `N2O` and `H2O2`.
- Shared heading detection across outline, slice, audit, and formatter paths so dirty style IDs such as `21` with style name `heading 2` are treated consistently.
- Fixed OOXML settings ordering for `m:mathPr` and Office extension settings so Word/WPS-generated documents validate after formatting.

## Problems Encountered

1. Problem: `word validate` still failed on the original and beautified samples after visual audit passed.
   Evidence: `word/settings.xml` contained `m:mathPr`, and `OpenXmlValidator` reported it as an unexpected child.
   Cause: The first settings order table placed `mathPr` near `proofState`; schema data requires it after `compat/docVars/rsids`.
   Fix: Updated `ElementOrder` to use schema order and namespace-qualified tokens for `m:mathPr`, `w14:*`, `w15:*`, and `sl:*` settings nodes.

2. Problem: The handwritten sample still had a document-grid warning in an earlier run.
   Evidence: `format-audit` reported `document_grid_detected`.
   Cause: `academic-format` removed `docGrid` only from the final section properties.
   Fix: Updated section normalization to remove `docGrid` from every section properties node.

3. Problem: The handwritten sample contained chemical formulas written as plain text, such as `N2O` and `H2O2`.
   Evidence: `word/document.xml` showed the formulas in ordinary text runs with no `w:vertAlign`.
   Cause: The formatter previously handled Latin scientific names but did not normalize chemistry notation.
   Fix: Added a deterministic chemical-formula tokenizer that applies Word subscript only to numeric formula counts and avoids ordinary numbers such as years or table column labels.

## Files Touched

- `Docx/WordFormatAuditor.cs`
- `Docx/WordAcademicFormatter.cs`
- `Docx/WordHeadingStyles.cs`
- `Docx/ElementOrder.cs`
- `Docx/OutlineReader.cs`
- `Docx/WordSlice.cs`
- `Cli/Commands/WordCommands.cs`
- `Cli/Common/Manifest.cs`
- `Cli/AGENT.md`
- `Cli.Tests/WordCommandTests.cs`
- `Cli.Tests/CliContractTests.cs`

## Verification

```text
dotnet build .\Cli\NongCli.csproj -c Release
Result: passed, 19 warnings, 0 errors

dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~WordCommandTests.WordFormatAudit|FullyQualifiedName~WordCommandTests.WordAcademicFormat|FullyQualifiedName~WordCommandTests.WordTableReflow|FullyQualifiedName~WordCommandTests.WordOutline"
Result: passed, 9 passed, 0 failed

dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter "FullyQualifiedName~CliContractTests.Commands_Json|FullyQualifiedName~CliContractTests.WordRepairPlan|FullyQualifiedName~CliContractTests.SliceInspect_Strict|FullyQualifiedName~CliContractTests.SliceFormats_IncludeUnifiedVisualEvidence"
Result: passed, 7 passed, 0 failed
```

Real DOCX samples:

```text
校企共建沸石基矿物材料教授工作站方案书-手写版.docx
Output: 校企共建沸石基矿物材料教授工作站方案书-手写版.academic-audit.docx
Before: warn, score 0, issues 165
After: pass, score 100, issues 0
Validate: ok
Tables: 18/18 three-line-like
Chemical formula check: `N2O`, `H2O2`, and `O2-` numeric counts have `w:vertAlign=subscript`

校企共建沸石基矿物材料教授工作站方案书-原始版.docx
Output: 校企共建沸石基矿物材料教授工作站方案书-原始版.academic-audit.docx
Before: warn, score 0, issues 384
After: pass, score 100, issues 0
Validate: ok
Tables: 17/17 three-line-like

校企共建沸石基矿物材料教授工作站方案书-美化版.docx
Output: 校企共建沸石基矿物材料教授工作站方案书-美化版.academic-audit.docx
Before: warn, score 0, issues 321
After: pass, score 100, issues 0
Validate: ok
Tables: 17/17 three-line-like
```

## Remaining Risks

- `format-audit` proves OOXML formatting evidence; it does not render a screenshot. Human review in Word/WPS is still the final check for subjective layout taste.
- Long, wide, and cross-page table reflow has a command path, but continuation caption policy and split thresholds still need more regression samples.
- The audit currently targets the academic profile. Other profiles should be added as separate contracts instead of weakening the academic rules.

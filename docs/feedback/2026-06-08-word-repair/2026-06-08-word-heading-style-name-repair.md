# Word heading style-name repair

## What changed

- Added shared heading detection for Word paragraph styles whose style IDs are numeric aliases but whose style names are `heading 2`, `heading 3`, `标题 2`, or similar.
- Updated `word outline` to resolve paragraph style names from the document style table.
- Updated `word dissect` to classify those paragraphs as `HeadingBlock` instead of ordinary paragraph blocks.
- Updated `word academic-format` to use the same heading evidence and canonicalize formatted paragraphs to standard styles (`Title`, `Heading1`, `Heading2`, `Heading3`, `Caption`, `Normal`).
- Added conservative academic subheading detection for labels such as `科学问题`, `研究内容`, and `SCI论文创新点`.
- Added CLI regression tests for numeric style IDs `21` and `31` with style names `heading 2` and `heading 3`.

## Why

The real `沸石` beautified DOCX used numeric paragraph style IDs for lower-level headings. Nong only recognized the level-1 style and flattened level-2/3 headings into paragraphs. That broke outline navigation and NongMark structure even though the document looked correct to a human reader.

## Files touched

- `Docx/WordHeadingStyles.cs`
- `Docx/OutlineReader.cs`
- `Docx/WordSlice.cs`
- `Docx/WordAcademicFormatter.cs`
- `Cli.Tests/WordCommandTests.cs`
- `log/2026-06-08-beautified-docx-read-notes.md`

## Tests run

- `dotnet build .\Cli\NongCli.csproj -c Release --nologo -clp:ErrorsOnly`
  - Passed, 0 errors.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo --filter "RecognizesHeadingStyleName"`
  - Passed, 2/2.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~WordAcademicFormat_ZeoliteRegression|FullyQualifiedName~RecognizesHeadingStyleName"`
  - Passed, 3/3.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~WordCommandTests"`
  - Passed, 45/45.

## Real-file verification

- Rebuilt current `nong.exe`.
- `word outline` on the beautified DOCX changed from 9 headings to 44 headings.
- Heading distribution after repair: 9 level-1, 23 level-2, 12 level-3.
- `word dissect` after repair emits 44 heading blocks, 87 paragraph blocks, and 17 table blocks.
- `word fix-order` produced `沸石/runtime/校企共建沸石基矿物材料教授工作站方案书-美化版.project-fixed.docx`.
- The project-fixed DOCX validates successfully.
- Preview warnings fell from 11 to 1; OOXML warnings fell from 10 to 0.
- The first `project-fixed` output was not a visible formatting repair; it only fixed internal structure/OOXML issues.
- After repairing `word academic-format`, generated `沸石/runtime/校企共建沸石基矿物材料教授工作站方案书-美化版.academic-fixed.docx`.
- `academic-fixed.docx` uses standard Word styles:
  - title: `Title`, center, 黑体, 44 half-points.
  - level 1 headings: `Heading1`, center, 黑体, 32 half-points.
  - level 2 headings: `Heading2`, left, 黑体, 28 half-points.
  - level 3 headings: `Heading3`, left, 黑体, 26 half-points.
  - body: `Normal`, justified, first-line indent, 宋体 / Times New Roman.

## Remaining risks

- The remaining preview warning is content-level: causal language appears without explicit causal-design indicators.
- Visual comparison against the hand-written DOCX still needs human review before choosing the final deliverable base.

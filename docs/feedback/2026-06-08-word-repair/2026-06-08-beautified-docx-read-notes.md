# Beautified DOCX Read Notes

## Goal

Read `沸石/校企共建沸石基矿物材料教授工作站方案书-美化版.docx` with the local Nong project and keep runtime artifacts inside the `沸石/` folder.

## Runtime Folder

- Created: `沸石/runtime/`

## Commands Run

- `nong word read ... --json`
- `nong word dissect ... --output 沸石/runtime/beautified-slice --json`
- `nong word preview ... --json`
- `nong word outline ... --json`
- `nong word styles ... --json`

## Tool Version Note

- The first pass used an older local Release `nong.exe`.
- Rebuilt `Cli/NongCli.csproj` from current source in Release mode and replaced `Cli/bin/Release/net8.0/nong.exe`.
- New `nong commands --json` reports 84 commands, version `3.2.5`.
- Re-ran the Word read/dissect/preview/outline commands with the rebuilt exe.

## Artifacts

- `沸石/runtime/beautified-word-read.json`
- `沸石/runtime/beautified-word-dissect.json`
- `沸石/runtime/beautified-word-preview.json`
- `沸石/runtime/beautified-word-outline.json`
- `沸石/runtime/beautified-word-styles.json`
- `沸石/runtime/beautified-slice/`

## Findings

- `word read` succeeded: 112 paragraphs, 17 tables.
- `word dissect` succeeded: 148 blocks, 0 slice warnings.
- `word preview` reported 11 warnings: 1 content warning and 10 OOXML table-style warnings.
- `word outline` found only 9 headings, all level 1.
- The document defines `heading 2` and `heading 3` styles, but paragraphs using style IDs `21` and `31` are emitted as ordinary paragraphs in the slice.
- Evidence: `content.jsonl` contains records like `p0007 paragraph 2.1 产业痛点 styleId=21 styleName=heading 2`, while `structure.json` outline only contains the nine level-1 headings.
- Rebuilt-current CLI result is the same: 9 `heading` blocks, 122 `paragraph` blocks, 17 `table` blocks; `heading 2` and `heading 3` paragraphs remain ordinary paragraphs.

## Practical Impact

The document has visible subheadings, but Nong's current structure extraction loses them as heading nodes. This makes `content.nongmark`, outline navigation, and downstream rewrite/repair workflows flatter than the real document structure.

## Repair Update

Fixed in project code:

- Added shared heading style-name detection for Word documents.
- `word outline` and `word dissect` now use `styleId`, `styleName`, and `outlineLvl` together.
- This covers Word/WPS documents where paragraph style IDs are numeric aliases such as `21` and `31`, while style names are `heading 2` and `heading 3`.

Regression tests added:

- `WordOutline_RecognizesHeadingStyleName_WhenStyleIdIsNumericAlias`
- `WordDissect_RecognizesHeadingStyleName_WhenStyleIdIsNumericAlias`

Real-file recheck:

- Before repair: `word outline` found 9 headings, all level 1.
- After repair: `word outline` found 44 headings: 9 level-1, 23 level-2, 12 level-3.
- After repair: `word dissect` emitted 44 heading blocks, 87 paragraph blocks, and 17 table blocks.
- `content.nongmark` now emits `## 2.1 产业痛点` and `### 科学问题` style headings instead of ordinary paragraph blocks.

Project-fixed DOCX:

- Created `沸石/runtime/校企共建沸石基矿物材料教授工作站方案书-美化版.project-fixed.docx`.
- `word fix-order` reported `Fixed 153 elements`.
- `word validate`: `Document is valid`.
- `word preview`: reduced from 11 warnings to 1 content warning; OOXML warnings reduced from 10 to 0.
- Remaining warning: `Content: causal language without causal design indicators`.

Important correction:

- `project-fixed.docx` was only an internal structural/OOXML repair. It was not a visual formatting repair, so opening it in Word looked mostly unchanged.
- This was a bad handoff name and a bad claim for the user's actual goal.
- The real user-facing repair path is `word academic-format`, which must visibly normalize headings, body text, and tables.

Second repair:

- Fixed `WordAcademicFormatter` so it uses style names as well as style IDs when classifying headings.
- `word academic-format` now canonicalizes paragraph styles to `Title`, `Heading1`, `Heading2`, `Heading3`, `Caption`, and `Normal`.
- Added conservative academic subheading detection for short labels such as `科学问题`, `研究内容`, and `SCI论文创新点`.
- Created `沸石/runtime/校企共建沸石基矿物材料教授工作站方案书-美化版.academic-fixed.docx`.
- `academic-fixed.docx` evidence:
  - title style: `Title`, center, 黑体, size 44.
  - `一、项目摘要`: `Heading1`, center, 黑体, size 32.
  - `2.1 产业痛点`: `Heading2`, left, 黑体, size 28.
  - `科学问题`: `Heading3`, left, 黑体, size 26.
  - body text: `Normal`, both justified, first-line indent 480, size 24, 宋体 / Times New Roman.
  - outline remains 44 headings: 9 level-1, 23 level-2, 12 level-3.

Verification:

- `dotnet build .\Cli\NongCli.csproj -c Release --nologo -clp:ErrorsOnly`: PASS, 0 errors.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo --filter "RecognizesHeadingStyleName"`: PASS, 2/2.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~WordCommandTests"`: PASS, 45/45.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo --filter "FullyQualifiedName~WordAcademicFormat_ZeoliteRegression|FullyQualifiedName~RecognizesHeadingStyleName"`: PASS, 3/3.

## Next Check

Compare against the hand-written version for content and visual quality. The project-side structure bug is fixed; the remaining work is deciding which document is the better human-facing base.

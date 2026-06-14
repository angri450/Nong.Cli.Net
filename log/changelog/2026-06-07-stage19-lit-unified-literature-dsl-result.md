# 2026-06-07 Stage19 Result: Unified Literature Retrieval DSL

## Goal

Implement first-class `nong lit` support:

- CNKI-like DSL parse/validate/plan.
- Provider-based literature metadata/OA retrieval.
- OpenAlex, Crossref, and Unpaywall Stage19 providers.
- Local strict filtering, merge/dedupe, deterministic ranking.
- JSON/Markdown/BibTeX export.
- Stable JSON CLI contract and verification.

Status: PASS.

## Implemented Commands

- `nong lit parse --query "<expr>" --json`
- `nong lit validate --query "<expr>" --json`
- `nong lit plan --query "<expr>" --sources openalex,crossref,unpaywall --json`
- `nong lit search --query "<expr>" --sources openalex,crossref,unpaywall --limit 50 --profile balanced --out refs.json --json`
- `nong lit export --input refs.json --format json|markdown|bibtex --out <path> --json`

`nong commands --json` now reports 82 implemented commands and includes only the five real `lit` commands.

## Files Changed

Core project:

- `Literature/LiteratureCore.csproj`
- `Literature/README.md`
- `Literature/Dsl/*`
- `Literature/Models/*`
- `Literature/Providers/*`
- `Literature/Pipeline/*`
- `Literature/Export/*`

CLI integration:

- `Cli/Commands/LitCommands.cs`
- `Cli/NongCli.csproj`
- `Cli/Program.cs`
- `Cli/Common/Manifest.cs`
- `Cli/AGENT.md`
- `Cli/README.md`

Tests and fixtures:

- `Tests/Tests.csproj`
- `Tests/CnkiLexerTests.cs`
- `Tests/CnkiParserTests.cs`
- `Tests/CnkiDslValidatorTests.cs`
- `Tests/ProviderFixtureTests.cs`
- `Tests/Fixtures/Literature/*`
- `Tests/QueryPlannerTests.cs`
- `Tests/LocalBooleanFilterTests.cs`
- `Tests/PaperRecordMergerTests.cs`
- `Tests/LiteratureRankerTests.cs`
- `Tests/LiteratureExportTests.cs`
- `Cli.Tests/LitCommandsJsonTests.cs`

Docs:

- `README.md`
- `README.zh-CN.md`

Verification artifacts:

- `tests-output/lit/refs.fixture.json`
- `tests-output/lit/refs.md`
- `tests-output/lit/refs.bib`

## Provider Behavior

- OpenAlex supports rough metadata search and DOI lookup with optional `NONG_LIT_OPENALEX_API_KEY` / `NONG_LIT_OPENALEX_KEY`.
- Crossref supports rough metadata search and DOI lookup/enrichment with optional `NONG_LIT_MAILTO` for polite contact.
- Unpaywall supports DOI-only legal OA lookup and requires `NONG_LIT_UNPAYWALL_EMAIL` or fallback `NONG_LIT_MAILTO`.
- Provider diagnostics expose booleans and environment variable names only, not secret values.
- Default unit tests use fake `HttpMessageHandler` and fixture JSON; no default live network tests were added.

## DSL Support

Supported fields include `SU`, `TI`, `KY`, `AB`, `FT`, `AU`, `FI`/`F`, `AF`, `JN`, `RF`, `YE`, `FU`, `CLC`, `SN`, `CN`, `IB`, `CF`, `DOI`.

Supported syntax includes quoted phrases, unquoted terms, `+`, `*`, `-`, `AND`, `OR`, `NOT`, parentheses, and `YE BETWEEN ('2000','2013')`.

Unsupported `%`, `/SEN`, `/NEAR`, `/PREV`, `/AFT`, `/PRG`, and `$N` return `E006` validation diagnostics with position/context.

## Problems Encountered

- Some subagent work initially wrote to `C:\Users\Administrator\Documents\Github\Literature`; files were moved into `Angri450.Nong\Literature`, and the parent folder was verified absent.
- Concurrent DSL/provider/pipeline slices briefly disagreed on diagnostic issue IDs (`E006` vs semantic IDs) and provider diagnostic key names. The final contract uses `E006` for DSL validation failures and non-secret boolean diagnostics for providers.
- `Unpaywall` initially returned `ok` with warning when selected alone without email. This was tightened so `lit search --sources unpaywall` without email returns `E005 dependency_missing`.
- Existing unrelated dirty state remains in PDF/OCR/MultiModal/package-lock/changelog areas and was not reverted.

## Fixes Applied

- Added `LiteratureCore` as a new pure .NET 8 project with no NuGet dependencies.
- Added `LitCommands` CLI integration and manifest entries only for implemented commands.
- Added artifact checks for `lit search --out` and `lit export`.
- Added provider fixture tests and live-network-free fake handlers.
- Added docs with explicit Stage19 limitations and no unimplemented provider advertising.

## Verification Commands And Results

Build:

- `dotnet build .\Literature\LiteratureCore.csproj -c Release --nologo` PASS, 0 warnings, 0 errors.
- `dotnet build .\Cli\NongCli.csproj -c Release --nologo` PASS, 0 warnings, 0 errors.

Tests:

- `dotnet test .\Tests\Tests.csproj -c Release --nologo` PASS, 125 passed.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo` PASS, 89 passed.
- `dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --filter LitCommandsJsonTests --nologo` PASS, 4 passed.

CLI smoke:

- `.\Cli\bin\Release\net8.0\nong.exe lit parse --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --json` PASS, `status=ok`, 4 terms.
- `.\Cli\bin\Release\net8.0\nong.exe lit validate --query "AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)" --json` PASS, `status=ok`.
- `.\Cli\bin\Release\net8.0\nong.exe lit plan --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --sources openalex,crossref,unpaywall --json` PASS, `status=ok`, 3 providers.
- `.\Cli\bin\Release\net8.0\nong.exe commands --json` PASS, `status=ok`, 82 implemented commands.
- `.\Cli\bin\Release\net8.0\nong.exe lit plan --query "TI=humic" --sources semantic-scholar --json` PASS as honest failure, exit 1, `E006 validation_failed`.
- `.\Cli\bin\Release\net8.0\nong.exe lit search --query "DOI='10.1016/j.chemgeo.2007.05.018'" --sources unpaywall --limit 5 --json` PASS as honest missing-credential failure, exit 1, `E005 dependency_missing`.

Artifact verification:

- `.\Cli\bin\Release\net8.0\nong.exe lit export --input tests-output\lit\refs.fixture.json --format markdown --style gbt7714 --out tests-output\lit\refs.md --json` PASS.
- `.\Cli\bin\Release\net8.0\nong.exe lit export --input tests-output\lit\refs.fixture.json --format bibtex --out tests-output\lit\refs.bib --json` PASS.
- `Test-Path tests-output\lit\refs.md` PASS, `True`.
- `Test-Path tests-output\lit\refs.bib` PASS, `True`.
- `(Get-Item tests-output\lit\refs.md).Length` PASS, `110`.
- `(Get-Item tests-output\lit\refs.bib).Length` PASS, `175`.

## Self-Audit Findings

Findings:

- LOW `Cli/Commands/LitCommands.cs`: `lit search` uses live providers when selected from the CLI, so network/provider availability can affect live searches. Fix status: accepted for Stage19; unit tests remain fixture-only and `Unpaywall` missing email now fails with `E005` when selected alone.
- LOW `Literature/Pipeline/LocalBooleanFilter.cs`: `FT` strict filtering cannot match remote metadata-only candidates. Fix status: documented and implemented as strict=false for unavailable full text; recall mode preserves candidates with warning.
- LOW `README.md` / `README.zh-CN.md`: docs mention only OpenAlex/Crossref/Unpaywall and Stage19 limitations. Fix status: no unimplemented providers advertised.

Verification:

- Build, tests, CLI smoke, and artifact checks listed above actually ran.
- Provider fixture tests used fake `HttpMessageHandler`; no default tests require live network.
- No API key or email values appeared in JSON outputs or this changelog.
- `nong commands --json` lists the implemented `lit` commands and not planned providers.
- Unsupported DSL operators return `E006`.
- Generated Markdown and BibTeX artifacts were verified non-zero.

Contract Check:

- JSON output uses `status`, `command`, `summary`, `data`, `issues`, `artifacts`, `metrics`, `errors`, `meta`.
- Failure paths set non-zero exit codes for validation/provider credential errors.
- Manifest and README match implemented behavior.
- `lit parse`, `lit validate`, and `lit plan` work offline.
- `lit export` writes non-zero artifacts.
- `lit search` works with implemented providers when provider conditions are met, and fails honestly for missing Unpaywall email when Unpaywall is the selected source.

Conclusion:

PASS for Stage19 readiness.

## Known Limitations

- No full-text provider is implemented; `FT` is honest metadata-bound behavior.
- No automatic Chinese-English translation or synonym expansion.
- No Semantic Scholar, PubMed, PMC, arXiv, Wanfang, Sciverse, Tavily, iFlow, AMiner, Lewen, DBLP, Qinyan, or Local PDF provider in Stage19.
- No scraping, browser automation, CAPTCHA bypass, institutional login automation, paywall bypass, or commercial database scraping.
- Live provider behavior depends on external API availability, rate limits, and optional credentials.

## Next Steps

- Stage20 can add `lit enrich`, `lit rank`, SemanticScholar/PubMed/PMC/arXiv/LocalPdf providers.
- Add opt-in live tests under `NONG_LIT_ENABLE_LIVE_TESTS=1`.
- Add deterministic synonym dictionaries only if explicitly curated.

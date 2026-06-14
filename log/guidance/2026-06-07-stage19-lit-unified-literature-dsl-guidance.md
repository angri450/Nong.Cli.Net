# 2026-06-07 Stage19 Guidance: Unified Literature Retrieval DSL

## Mission

Build a first-class Nong literature retrieval module:

```text
nong lit
```

This is a major construction task, not a small patch. Claude Code should run at full capacity: use deep reasoning, split work across multiple agents with disjoint write scopes, implement the module end to end, run verification, perform self-audit, and write the final changelog.

The strategic product goal:

```text
CNKI-like professional search expression
    -> deterministic DSL parser
    -> provider-specific rough queries
    -> legal metadata/OA data sources
    -> local strict filtering
    -> merge/dedupe
    -> ranking
    -> JSON/Markdown/BibTeX export
```

The core rule:

```text
CNKI-like DSL is the stable Nong contract.
External literature databases are providers, not the product contract.
```

## Read First

Claude Code must read these files before editing:

```text
CLAUDE.md
Cli/AGENT.md
Cli/Program.cs
Cli/NongCli.csproj
Cli/Common/Manifest.cs
Cli/Common/JsonOutput.cs
Cli/Common/ErrorCodes.cs
Cli/Commands/PdfCommands.cs
Cli.Tests/CliContractTests.cs
Tests/Tests.csproj
Cli.Tests/Cli.Tests.csproj
log/guidance/README.md
log/guidance/review-checklist.md
```

Also inspect current dirty state before editing:

```powershell
git status --short
```

Known dirty-state warning at handoff time:

```text
PDF/OCR/MultiModal changes exist.
ThirdParty/ZXing/package-lock related changes may exist.
changelog/ deletions may exist.
Do not clean, revert, move, or reformat unrelated dirty changes.
```

## P1 Current Facts And Goal

### Current Facts

- `Angri450.Nong` is a pure .NET 8 repository.
- CLI tool name is `nong`.
- CLI commands are registered in `Cli/Program.cs`.
- `nong commands --json` is backed by `Cli/Common/Manifest.cs`.
- JSON output contract is `JsonOutput`: `status`, `command`, `summary`, `data`, `issues`, `artifacts`, `metrics`, `errors`, `meta`.
- Error codes are centralized in `Cli/Common/ErrorCodes.cs`.
- Current public CLI surface is around Nong 3.2.4 with Word, Inspect, Chart, Excel, Diagram, PPTX, Skill, OCR, and PDF commands.
- Repository policy forbids JavaScript dependencies and Python implementation for core Nong features.

### User Goal

Implement a new `nong lit` module for unified literature retrieval:

```text
nong lit parse
nong lit validate
nong lit plan
nong lit search
nong lit export
```

The module must support:

- CNKI-like DSL parsing and validation.
- Provider-based metadata search.
- OpenAlex/Crossref/Unpaywall first-version provider implementation.
- Local boolean filtering.
- DOI/title-based merge and dedupe.
- Deterministic ranking.
- JSON/Markdown/BibTeX export.
- Stable JSON output for agent calls.
- Focused tests and actual CLI smoke verification.

### Non-Goals For Stage19

Do not implement these as working default providers in Stage19:

```text
SemanticScholarProvider
PubMedProvider
PmcProvider
ArxivProvider
WanfangProvider
SciverseProvider
MetasoProvider
TavilyProvider
IFlowProvider
AMinerProvider
LewenProvider
DblpProvider
QinyanProvider
LocalPdfProvider
```

It is acceptable to reserve provider registry names and capability structures for them. Do not advertise them as implemented in `nong commands --json`, `README`, or final changelog.

Do not implement web scraping, browser automation, CAPTCHA bypass, institutional login automation, paywall bypass, or commercial database scraping.

Do not publish NuGet packages, push git remotes, change branches, or expose secrets.

## P2 Design And Boundaries

### Project Layout

Create:

```text
Literature/
  LiteratureCore.csproj
  README.md
  Dsl/
    CnkiToken.cs
    CnkiLexer.cs
    CnkiParser.cs
    CnkiAst.cs
    CnkiDslValidator.cs
    CnkiQueryNormalizer.cs
  Models/
    PaperRecord.cs
    LiteratureSearchRequest.cs
    LiteratureSearchResult.cs
    ProviderSearchResult.cs
    LiteratureProviderCapabilities.cs
    LiteratureSource.cs
    RankProfile.cs
    CitationFormat.cs
  Providers/
    ILiteratureProvider.cs
    ProviderHttpClientFactory.cs
    ProviderRegistry.cs
    OpenAlexProvider.cs
    CrossrefProvider.cs
    UnpaywallProvider.cs
  Pipeline/
    LiteratureSearchPipeline.cs
    QueryPlanner.cs
    LocalBooleanFilter.cs
    PaperRecordMerger.cs
    LiteratureRanker.cs
    CitationFormatter.cs
  Export/
    JsonLiteratureExporter.cs
    MarkdownLiteratureExporter.cs
    BibTeXExporter.cs
```

Create:

```text
Cli/Commands/LitCommands.cs
```

Modify:

```text
Cli/NongCli.csproj
Cli/Program.cs
Cli/Common/Manifest.cs
Tests/Tests.csproj
Cli.Tests/Cli.Tests.csproj
README.md
README.zh-CN.md
```

Only update docs after the command behavior is real.

### Project File Contract

`Literature/LiteratureCore.csproj`:

```text
TargetFramework: net8.0
Nullable: enable
ImplicitUsings: enable
PackageId: Angri450.Nong.Literature
AssemblyName: Angri450.Nong.Literature
PackageReadmeFile: README.md
RepositoryUrl: https://github.com/angri450/Nong.NET
README.md packed to package root
```

Use only .NET runtime libraries unless a dependency is clearly needed. For Stage19, `HttpClient`, `System.Text.Json`, and `System.Xml.Linq` are enough. Avoid adding new NuGet packages.

### CLI Command Surface

Implement:

```powershell
nong lit parse --query "<expr>" --json
nong lit validate --query "<expr>" --json
nong lit plan --query "<expr>" --sources openalex,crossref,unpaywall --json
nong lit search --query "<expr>" --sources openalex,crossref,unpaywall --limit 50 --profile balanced --out refs.json --json
nong lit export --input refs.json --format json --out refs.normalized.json --json
nong lit export --input refs.json --format markdown --style gbt7714 --out refs.md --json
nong lit export --input refs.json --format bibtex --out refs.bib --json
```

Do not add `lit enrich` or `lit rank` as implemented commands in Stage19 unless they are fully tested and useful as standalone commands. Ranking and enrichment may exist as internal pipeline steps.

### JSON Contract

All `--json` commands must use existing `JsonOutput` and `ErrorEntry`.

Success:

```json
{
  "status": "ok",
  "command": "lit parse",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": {},
  "metrics": {},
  "errors": [],
  "meta": { "durationMs": 0, "version": "..." }
}
```

Failure:

```json
{
  "status": "error",
  "command": "lit validate",
  "summary": "...",
  "errors": [
    { "code": "E006", "name": "validation_failed", "message": "..." }
  ]
}
```

Set `Environment.ExitCode = 1` for command failure.

### DSL Stage19 Support

Supported fields:

```text
SU  subject, mapped locally to title + keywords + abstract + concepts/topics
TI  title
KY  keywords
AB  abstract
FT  full text; Stage19 remote providers usually cannot satisfy strict FT
AU  author
FI  first author / first responsible person
F   alias of FI
AF  affiliation
JN  journal/source
RF  references/cited references
YE  year
FU  fund
CLC classification
SN  ISSN
CN  Chinese serial number
IB  ISBN
CF  citation count
DOI DOI
```

Supported syntax:

```text
FIELD=...
'phrase'
unquoted terms
+
*
-
AND
OR
NOT
()
YE BETWEEN ('2000','2013')
```

Operator meaning:

```text
+    OR
*    AND
-    NOT
AND  AND
OR   OR
NOT  NOT
```

Keep extension slots, but do not implement:

```text
%
/SEN N
/NEAR N
/PREV N
/AFT N
/PRG N
$N
```

If users use unsupported operators, return `E006 validation_failed` with a clear message and location if possible.

Required examples:

```text
SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')
SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')*('络合'+'配合'+'螯合'+'复合物')
AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)
YE BETWEEN ('2000','2013')
DOI='10.1016/j.chemgeo.2007.05.018'
```

Do not silently perform model-based Chinese-English translation in Stage19. If English synonyms are needed, they must be present in the user query or in an explicit deterministic synonym dictionary added later.

### Provider Interface

Use capability-based interface:

```csharp
public interface ILiteratureProvider
{
    string Name { get; }
    LiteratureProviderCapabilities Capabilities { get; }

    Task<ProviderSearchResult> SearchAsync(
        LiteratureSearchRequest request,
        CancellationToken cancellationToken);

    Task<PaperRecord?> GetByDoiAsync(
        string doi,
        CancellationToken cancellationToken);

    Task<PaperRecord?> EnrichAsync(
        PaperRecord record,
        CancellationToken cancellationToken);
}
```

Capabilities:

```csharp
public sealed class LiteratureProviderCapabilities
{
    public bool Search { get; init; }
    public bool DoiLookup { get; init; }
    public bool OpenAccessLookup { get; init; }
    public bool FullTextLookup { get; init; }
    public bool CitationLookup { get; init; }
    public bool ReferenceLookup { get; init; }
    public bool WebSearch { get; init; }
    public bool RequiresApiKey { get; init; }
    public bool SupportsCnkiDslNative { get; init; }
    public bool SupportsLocalStrictFilter { get; init; }
}
```

### Stage19 Providers

Implement only:

```text
OpenAlexProvider
CrossrefProvider
UnpaywallProvider
```

Provider requirements:

- Use `HttpClient`.
- Use `System.Text.Json`.
- Use deterministic timeout.
- Add simple retry/backoff for transient failures.
- Do not log or print API keys.
- Do not print user email in normal JSON output.
- Return provider diagnostics without secrets.
- Provider unit tests must use fake `HttpMessageHandler` and fixture JSON.
- Live tests run only when `NONG_LIT_ENABLE_LIVE_TESTS=1`.

Environment variables:

```text
NONG_LIT_OPENALEX_KEY
NONG_LIT_OPENALEX_API_KEY     # accepted alias
NONG_LIT_MAILTO
NONG_LIT_UNPAYWALL_EMAIL
NONG_LIT_ENABLE_LIVE_TESTS
```

Rules:

- OpenAlex must support API key. `NONG_LIT_OPENALEX_API_KEY` and `NONG_LIT_OPENALEX_KEY` are aliases.
- Crossref may use `NONG_LIT_MAILTO` for polite contact / User-Agent.
- Unpaywall requires an email parameter. Use `NONG_LIT_UNPAYWALL_EMAIL`, fallback to `NONG_LIT_MAILTO`. If neither exists and Unpaywall is selected, return provider unavailable or validation issue, not a secret prompt.

### Provider Boundary

OpenAlex:

- Main search source.
- Map title, authors, year, venue, DOI, abstract inverted index, citation count, OA fields.
- Do not claim full text support.

Crossref:

- DOI and publication metadata enrichment.
- Map title, authors, journal/container-title, publisher, license, funder, volume, issue, page.
- Search support may be rough only; DOI lookup and enrichment are primary.

Unpaywall:

- DOI to legal OA location, PDF URL, landing page, OA status, license.
- No general search.

### Query Planning

Do not require every provider to support CNKI DSL natively.

Pipeline:

```text
DSL text
  -> CnkiLexer
  -> CnkiParser
  -> CnkiDslValidator
  -> QueryPlanner
  -> provider rough query/query set
  -> provider search
  -> normalize PaperRecord
  -> local filter
  -> merge/dedupe
  -> rank
  -> export
```

Prevent OR-combination explosion:

```text
Max generated rough queries per provider: 20 by default.
If exceeded, truncate deterministically and add a Warning issue.
```

`lit plan` must expose:

```text
parsed fields
normalized concepts
provider list
rough queries per provider
known provider limitations
credential availability as boolean flags only
```

Do not print actual API key or email.

### Local Boolean Filtering

Field mapping:

```text
SU = title + keywords + abstract + concepts/topics
TI = title
KY = keywords
AB = abstract
AU = authors
JN = venue
YE = year
DOI = doi
CF = citationCount
FT = full text
```

Filtering modes:

```text
strict
recall
```

Default: `strict`.

If query uses `FT` but no full text is available:

- strict mode: record does not satisfy the FT clause.
- recall mode: keep candidate but add match reason / issue explaining full text unavailable.

### Merge And Dedupe

Rules:

1. DOI normalized equal -> same record.
2. If DOI missing -> normalized title + year + first author approximate match.
3. Do not merge Chinese title and English title unless DOI matches.
4. Preserve `RetrievedFrom` and `SourceIds`.
5. Prefer richer values when merging:
   - DOI present beats missing.
   - Non-empty abstract beats empty.
   - OA PDF from Unpaywall beats missing.
   - Higher citation count preserved.

### Ranking

Implement profiles:

```text
balanced
classic
recent
```

Scores:

```text
balanced = 0.45 conceptCoverage + 0.20 fieldMatch + 0.15 citationScore + 0.15 recencyScore + 0.05 sourceQuality
classic  = 0.35 conceptCoverage + 0.20 fieldMatch + 0.35 citationScore + 0.05 recencyScore + 0.05 sourceQuality
recent   = 0.45 conceptCoverage + 0.20 fieldMatch + 0.05 citationScore + 0.25 recencyScore + 0.05 sourceQuality
```

Clamp all feature scores to `[0, 1]`. Do not emit `NaN`, `Infinity`, or negative relevance scores.

### Export

Formats:

```text
json
markdown
bibtex
```

Markdown:

- Basic GB/T 7714-like reference list.
- Stage19 does not need exhaustive GB/T 7714 edge coverage.

BibTeX:

- Generate stable keys.
- Escape braces, quotes, and backslashes conservatively.
- Use `@article` where venue/year exists; fallback to `@misc`.

JSON:

- Full normalized `PaperRecord` array or `LiteratureSearchResult`.

Generated artifact commands must verify output file exists and is non-zero before returning success.

## P3 Execution Steps

Claude Code should run multiple agents in parallel where write scopes do not conflict. The orchestrator must merge carefully and run final integration tests.

### Agent 0: Orchestrator And Architecture

Scope:

```text
Overall plan coordination
Read files
Track dirty state
Assign disjoint write scopes
Resolve integration conflicts
Run final audit
Write final changelog
```

Acceptance:

- No unrelated dirty file reverted.
- Final command surface matches manifest and docs.
- Final audit report exists in the result changelog.

### Agent A: DSL Core

Scope:

```text
Literature/Dsl/*
Tests/CnkiLexerTests.cs
Tests/CnkiParserTests.cs
Tests/CnkiDslValidatorTests.cs
```

Tasks:

- Implement tokenization.
- Implement AST.
- Implement parser.
- Implement validation.
- Implement query normalization.

Acceptance:

- Required DSL examples parse.
- Unsupported operators return validation errors.
- Error messages include useful location/context.
- Tests pass.

### Agent B: Models, Provider Interface, Registry

Scope:

```text
Literature/Models/*
Literature/Providers/ILiteratureProvider.cs
Literature/Providers/ProviderRegistry.cs
Literature/LiteratureCore.csproj
Literature/README.md
```

Tasks:

- Create core project.
- Define stable models.
- Define capabilities.
- Define provider registry.
- Make model JSON serialization predictable.

Acceptance:

- Core project builds.
- Models are nullable-safe.
- No provider secrets appear in model output.

### Agent C: Provider HTTP Layer And Fixtures

Scope:

```text
Literature/Providers/ProviderHttpClientFactory.cs
Literature/Providers/OpenAlexProvider.cs
Literature/Providers/CrossrefProvider.cs
Literature/Providers/UnpaywallProvider.cs
Tests/ProviderFixtureTests.cs
Tests/Fixtures/Literature/*
```

Tasks:

- Implement fake-handler testable providers.
- Map provider JSON to `PaperRecord`.
- Restore OpenAlex inverted index abstract.
- Normalize DOI.
- Handle unavailable credentials cleanly.

Acceptance:

- Provider tests use fixture JSON only.
- No live network in default tests.
- OpenAlex API key alias support exists.
- Unpaywall email fallback behavior is tested.

### Agent D: Planner, Filter, Merge, Rank

Scope:

```text
Literature/Pipeline/*
Tests/QueryPlannerTests.cs
Tests/LocalBooleanFilterTests.cs
Tests/PaperRecordMergerTests.cs
Tests/LiteratureRankerTests.cs
```

Tasks:

- Implement provider-specific rough query planning.
- Cap generated rough query combinations.
- Implement strict/recall filtering.
- Implement merge/dedupe.
- Implement ranking.

Acceptance:

- Concept-group query planning works for CNKI-like expressions.
- OR explosion warning is emitted.
- DOI dedupe works.
- Chinese/English title records do not merge without DOI.
- Scores are deterministic and finite.

### Agent E: Exporters

Scope:

```text
Literature/Export/*
Literature/Pipeline/CitationFormatter.cs
Tests/LiteratureExportTests.cs
```

Tasks:

- Implement JSON exporter.
- Implement Markdown exporter.
- Implement BibTeX exporter.
- Implement artifact validation helpers if needed.

Acceptance:

- Markdown file is non-empty.
- BibTeX file is non-empty and parseable enough for common BibTeX readers.
- JSON export round trips through `System.Text.Json`.

### Agent F: CLI Integration And Contract Tests

Scope:

```text
Cli/Commands/LitCommands.cs
Cli/NongCli.csproj
Cli/Program.cs
Cli/Common/Manifest.cs
Cli.Tests/LitCommandsJsonTests.cs
Cli.Tests/CliContractTests.cs
```

Tasks:

- Add project reference.
- Register command group.
- Add manifest entries only for commands actually implemented.
- Implement `lit parse`, `lit validate`, `lit plan`, `lit search`, `lit export`.
- Use `JsonOutput`, `ErrorCodes`, and existing CLI patterns.
- Add CLI JSON contract tests.

Acceptance:

- `nong commands --json` includes implemented `lit` commands.
- Parse/validate/plan work without network.
- Search can run against fake provider path in tests.
- JSON shape includes required top-level fields.
- Exit codes are correct.

### Agent G: Docs, Self-Audit, Changelog

Scope:

```text
README.md
README.zh-CN.md
log/changelog/2026-06-07-stage19-lit-unified-literature-dsl-result.md
```

Tasks:

- Update README only after real CLI behavior exists.
- Document command examples and limitations.
- Run self-audit using `log/guidance/review-checklist.md`.
- Write final changelog with commands, tests, problems, fixes, known limits, and audit result.

Acceptance:

- Changelog includes actual commands and results.
- Self-audit findings are included.
- Known limitations are honest.
- No unverified provider is documented as implemented.

## P4 Verification Matrix

Run these before claiming done.

### Build

```powershell
dotnet build .\Literature\LiteratureCore.csproj -c Release --nologo
dotnet build .\Cli\NongCli.csproj -c Release --nologo
```

### Tests

```powershell
dotnet test .\Tests\Tests.csproj -c Release --nologo
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo
```

If existing unrelated dirty-state failures block full tests, document the exact failure and run the most specific affected tests. Do not hide failures.

### CLI Smoke

Use the built CLI:

```powershell
.\Cli\bin\Release\net8.0\nong.exe lit parse --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --json
.\Cli\bin\Release\net8.0\nong.exe lit validate --query "AU=钱伟长 AND (AF=清华大学 OR AF=上海大学)" --json
.\Cli\bin\Release\net8.0\nong.exe lit plan --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --sources openalex,crossref,unpaywall --json
.\Cli\bin\Release\net8.0\nong.exe commands --json
```

### Export Artifact Verification

Create or reuse a deterministic fixture result JSON, then run:

```powershell
.\Cli\bin\Release\net8.0\nong.exe lit export --input tests-output\lit\refs.fixture.json --format markdown --style gbt7714 --out tests-output\lit\refs.md --json
.\Cli\bin\Release\net8.0\nong.exe lit export --input tests-output\lit\refs.fixture.json --format bibtex --out tests-output\lit\refs.bib --json
Test-Path tests-output\lit\refs.md
Test-Path tests-output\lit\refs.bib
(Get-Item tests-output\lit\refs.md).Length
(Get-Item tests-output\lit\refs.bib).Length
```

JSON success is invalid if the artifact is missing or zero bytes.

### Optional Live Tests

Default tests must not use live network.

Run live tests only if explicitly enabled:

```powershell
$env:NONG_LIT_ENABLE_LIVE_TESTS="1"
$env:NONG_LIT_OPENALEX_KEY="<set outside logs>"
$env:NONG_LIT_MAILTO="<set outside logs>"
$env:NONG_LIT_UNPAYWALL_EMAIL="<set outside logs>"
dotnet test .\Tests\Tests.csproj -c Release --filter "Category=Live" --nologo
```

Do not print env var values.

## P5 Risks And Follow-Up

### Risks

1. OpenAlex, Crossref, and Unpaywall schemas may drift. Use tolerant JSON parsing and fixture tests.
2. API credentials may be absent. Commands must fail or degrade honestly.
3. CNKI-like DSL can generate too many query combinations. Enforce deterministic caps.
4. Chinese/English synonym expansion is not implemented. Do not imply it exists.
5. Full-text filtering is limited with remote metadata providers. `FT` must be honest.
6. Existing dirty state can affect build/test. Work with it; do not revert unrelated changes.
7. Provider rate limits can break live tests. Live tests must be opt-in.

### Rollback Points

If integration becomes unstable:

```text
Keep LiteratureCore DSL/parser/tests.
Keep lit parse/validate/plan.
Defer lit search/export.
Do not leave manifest entries for non-working commands.
```

If provider implementation is blocked:

```text
Keep provider interfaces and fixture-based skeletons.
Return E005 dependency_missing or E006 validation_failed with clear credential/provider issue.
Do not advertise live provider support.
```

### Follow-Up Stages

Stage20:

```text
lit enrich
lit rank
SemanticScholarProvider
PubMedProvider
PmcProvider
ArxivProvider
LocalPdfProvider
```

Stage21:

```text
WanfangProvider
SciverseProvider
MetasoProvider or TavilyProvider
WebEvidenceRecord
DOI verification pipeline
```

Stage22:

```text
Nong.Toolkit.Net lit skill sync
skill scan
plugin validate
```

GroundPA skill sync is not part of Stage19 unless Stage19 CLI behavior is complete and verified.

## Self-Audit Requirement

After implementation, Claude Code must audit its own work before final answer.

Audit format:

```text
Findings
HIGH / MEDIUM / LOW, with file and line reference, evidence, impact, and fix status.

Verification
Commands actually run and results.

Contract Check
JSON output shape, exit codes, artifacts, manifest, README, and tests.

Conclusion
PASS / PARTIAL / FAIL for Stage19 readiness.
```

Audit must check:

- No unrelated dirty changes reverted.
- No secrets printed.
- No API key/email in JSON output or changelog.
- `nong commands --json` lists only implemented commands.
- `lit parse/validate/plan` work offline.
- Provider tests do not hit network.
- Live tests are opt-in.
- Generated artifacts are non-zero.
- Unsupported DSL operators fail with `E006`.
- No unimplemented provider is advertised as implemented.

## Changelog Requirement

Claude Code must create:

```text
log/changelog/2026-06-07-stage19-lit-unified-literature-dsl-result.md
```

The changelog must include:

```text
Goal
Implemented commands
Files changed
Provider behavior
DSL support
Problems encountered
Fixes applied
Verification commands and exact pass/fail result
Self-audit findings
Known limitations
Next steps
```

If Stage19 is only partially completed, the changelog must say `PARTIAL` and explain exactly what remains.

Do not write a success changelog unless build/test/CLI/artifact verification actually ran.

## Final Done Criteria

Stage19 is done only when:

- `LiteratureCore` builds.
- `NongCli` builds.
- Focused unit tests pass.
- CLI contract tests for `lit` pass.
- `lit parse`, `lit validate`, and `lit plan` work offline.
- `lit export` writes non-zero Markdown and BibTeX artifacts.
- `lit search` either works with implemented providers or fails honestly with machine-readable credential/provider errors.
- Manifest and docs match real implemented behavior.
- Self-audit is written.
- Changelog is written.

Do not declare Stage19 complete if:

- provider code hits the network in normal unit tests;
- `lit search --json` returns `ok` with missing output file;
- secrets are printed;
- unsupported provider names silently succeed;
- README claims unimplemented providers;
- manifest lists commands that are not real.

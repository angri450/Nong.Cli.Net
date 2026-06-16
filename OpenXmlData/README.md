# OpenXmlData

Auto-generated data files for the **DocumentFormat.OpenXml** source generator.
Do not edit these files by hand — regenerate from the back-end processor.

Consumed by:
- `ThirdParty/ThirdParty.csproj` (via `<Import Project="..\OpenXmlData\OpenXmlData.targets" />`)
- `DocumentFormat.OpenXml.Generator/SourceGenerator.targets`

## Files

- `namespaces.json` — known namespaces and their prefixes
- `schematrons.json` — Schematron constraint information
- `parts/` — per-part metadata (one JSON per OpenXml part type)
- `schemas/` — per-schema-element metadata, separated by namespace
- `typed/` — strongly-typed class generation metadata, separated by namespace
- `OpenXmlData.targets` — MSBuild item definitions wiring the above into the
  source generator's `AdditionalFiles`

## Note

This directory is **unrelated** to the Nong data layer. Nong's document database
(`NongDb`) and workspace management (`NongWorkplace`) live in the sibling
`../Data/` package (`Angri450.Nong.Data`).

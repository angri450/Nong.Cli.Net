# Angri450.Nong.Bioicons

40 scientific SVG icons embedded as assembly resources. Zero external dependencies — icons ship inside the DLL.

[![NuGet](https://img.shields.io/nuget/v/Angri450.Nong.Bioicons)](https://www.nuget.org/packages/Angri450.Nong.Bioicons)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com)

## Supported Platforms

.NET 8.0 and above (net8.0, net9.0, net10.0, net11.0). Windows, macOS, Linux.

## Install

```bash
dotnet add package Angri450.Nong.Bioicons
```

## Usage

```csharp
using Bioicons;

// Browse categories
string[] categories = IconProvider.GetCategories();
// → ["Arrows", "Biology", "Chemistry", "Experimental", "LabEquipment", "Medical"]

// Browse icons in a category
string[] icons = IconProvider.GetIcons("Biology");
// → ["antibody", "bacteria", "cell", "dna", "enzyme", ...]

// Get SVG content
string svg = IconProvider.GetSvg("Biology", "cell");

// Save to file
IconProvider.SaveSvg("Biology", "dna", "dna.svg");
```

## Icon Catalog

| Category | Count | Icons |
|----------|-------|-------|
| **Biology** | 10 | antibody, bacteria, cell, dna, enzyme, gene, microscope, protein, virus, yeast |
| **Chemistry** | 8 | atom, beaker, bond, flask, molecule, periodic, pipette, reaction |
| **Medical** | 6 | bandage, heart, lungs, pill, stethoscope, syringe |
| **Lab Equipment** | 6 | centrifuge, incubator, laminar, petriDish, shaker, spectrophotometer |
| **Arrows** | 5 | arrowDown, arrowLeft, arrowRight, arrowUp, bidirectional |
| **Experimental** | 5 | chromatography, electrophoresis, pcr, sequencing, western |

## Integration with Diagram

Bioicons are used directly by `Angri450.Nong.Diagram` for decorating nodes in scientific diagrams. Install both packages to use icons as node markers in flowcharts and mechanism diagrams.

## Dependencies

None. Pure managed code. Icons are embedded SVG strings in the assembly.

## API Reference

| Method | Description |
|--------|-------------|
| `IconProvider.GetCategories()` | List all icon categories |
| `IconProvider.GetIcons(category)` | List icon names in a category |
| `IconProvider.GetSvg(category, name)` | Get SVG markup as string |
| `IconProvider.SaveSvg(category, name, path)` | Save SVG to file |

## Source

https://github.com/angri450/Nong.NET — Issues and PRs welcome.

## License

MIT

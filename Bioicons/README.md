# Angri450.Nong.Bioicons

40 个科学 SVG 图标，嵌入式程序集资源。angri450 精选并整理，Diagram 包直接调用装饰节点，也可独立使用。零外部依赖 —— 图标随 DLL 一起发布。

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

// 浏览分类
string[] categories = IconProvider.GetCategories();
// → ["Arrows", "Biology", "Chemistry", "Experimental", "LabEquipment", "Medical"]

// 浏览分类中的图标
string[] icons = IconProvider.GetIcons("Biology");
// → ["antibody", "bacteria", "cell", "dna", "enzyme", ...]

// 获取 SVG 内容
string svg = IconProvider.GetSvg("Biology", "cell");

// 保存为文件
IconProvider.SaveSvg("Biology", "dna", "dna.svg");
```

## Icon Catalog

angri450 整理的 40 个图标，按六个分类组织：

| Category | Count | Icons |
|----------|-------|-------|
| **Biology** | 10 | antibody, bacteria, cell, dna, enzyme, gene, microscope, protein, virus, yeast |
| **Chemistry** | 8 | atom, beaker, bond, flask, molecule, periodic, pipette, reaction |
| **Medical** | 6 | bandage, heart, lungs, pill, stethoscope, syringe |
| **Lab Equipment** | 6 | centrifuge, incubator, laminar, petriDish, shaker, spectrophotometer |
| **Arrows** | 5 | arrowDown, arrowLeft, arrowRight, arrowUp, bidirectional |
| **Experimental** | 5 | chromatography, electrophoresis, pcr, sequencing, western |

## 与 Diagram 集成

Bioicons 被 `Angri450.Nong.Diagram` 直接用于科学图表节点装饰。同时安装两个包即可在流程图和机制图中使用图标作为节点标记。

## Dependencies

无。纯托管代码。图标以嵌入 SVG 字符串形式存在于程序集中。

## API Reference

| Method | Description |
|--------|-------------|
| `IconProvider.GetCategories()` | 列出所有图标分类 |
| `IconProvider.GetIcons(category)` | 列出某分类中的图标名称 |
| `IconProvider.GetSvg(category, name)` | 获取 SVG 标记字符串 |
| `IconProvider.SaveSvg(category, name, path)` | 保存 SVG 到文件 |

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0

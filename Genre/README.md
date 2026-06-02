# Angri450.Nong.Genre

Document format template library. Pure JSON templates — no code logic.

## Included Templates

| Template | File | Use Case |
|----------|------|----------|
| 期刊论文 | `journal-paper.json` | GB/T 7714 journal submission |
| 毕业论文 | `degree-thesis.json` | Bachelor/Master/Doctor thesis |
| 竞赛论文 | `contest-paper.json` | Life sciences / academic contest papers |
| 答辩 PPT | `defense-ppt.json` | Defense presentation format |
| 通知公文 | `official-notice.json` | Government/business official documents |
| 商务信函 | `business-letter.json` | Business correspondence |

## Usage

```csharp
using Nong.Genre;

// List all templates
string[] names = GenreTemplate.List();

// Load a template
string json = GenreTemplate.Load("degree-thesis");

// Pass to StyleBuilder
using var doc = JsonDocument.Parse(json);
StyleBuilder.BuildFromJson(styles, doc.RootElement);
```

## License

MIT

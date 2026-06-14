# Angri450.Nong.Genre

文档格式模板库。angri450 整理的 JSON 驱动模板 —— 纯 JSON，零代码逻辑。换一个文件 = 换一种文档格式。

## 包含的模板

| 模板 | 文件 | 用途 |
|------|------|------|
| 期刊论文 | `journal-paper.json` | GB/T 7714 期刊投稿 |
| 毕业论文 | `degree-thesis.json` | 本/硕/博学位论文 |
| 竞赛论文 | `contest-paper.json` | 生命科学/学术竞赛论文 |
| 答辩 PPT | `defense-ppt.json` | 答辩演示文稿格式 |
| 通知公文 | `official-notice.json` | 政府/企业正式公文 |
| 商务信函 | `business-letter.json` | 商务书信 |

## Usage

```csharp
using Nong.Genre;

// 列出所有模板
string[] names = GenreTemplate.List();

// 加载一个模板
string json = GenreTemplate.Load("degree-thesis");

// 传入 StyleBuilder
using var doc = JsonDocument.Parse(json);
StyleBuilder.BuildFromJson(styles, doc.RootElement);
```

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0

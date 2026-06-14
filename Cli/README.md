# Angri450.Nong.Cli (nong)

nong 命令行工具 —— angri450 为 Nong.Cli.Net 构建的统一入口。主 `nong` 是轻路由器 + 纯 .NET 轻模块；Chart / Diagram / PDF / PPTX / OCR / Imaging 走独立 `Angri450.Nong.Tool.*` dotnet tool，用户命令入口保持不变。

## Install

```bash
dotnet tool install --global Angri450.Nong.Cli --add-source https://mirrors.huaweicloud.com/repository/nuget/v3/index.json
```

The tool targets `net8.0` and the packaged build enables major-version roll-forward. On .NET 10, update the tool if an older installation does not start, or set `DOTNET_ROLL_FORWARD=LatestMajor`.

For local OCR, install the default PP-OCRv6 model once:

```bash
nong ocr install-model pp-ocrv6-medium --json
```

The OCR runtime bundle is maintained in the separate `Nong.OcrRuntime` repository and pinned independently from the CLI version, so routine CLI/Word/PDF patch releases do not republish the large runtime package. `pp-ocrv5-mobile` remains available as a legacy compatibility path.

## Usage

```bash
nong --help                  # 全部命令
nong commands --json         # 机器可读的命令列表
nong word check file.doc     # 预检 .doc/.docx
nong word read file.docx     # 提取 Word 文本
nong word fill template.docx data.json -o out.docx  # 模板填充
nong lit parse --query "SU=('腐植酸'+'腐殖酸')*('稀土'+'微肥')" --json
nong inspect diagnose file.txt # 论文质量诊断
```

`nong lit` 提供类 CNKI 文献检索 DSL、OpenAlex/Crossref/Unpaywall 元数据/OA provider、本地过滤合并排序，以及 JSON/Markdown/BibTeX 导出。Stage19 不做爬虫、付费墙绕过或自动中英同义词翻译。

angri450 将全部 Nong.Cli.Net 库能力暴露为一致的 CLI 接口，方便 AI Agent 和脚本直接调用。

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.Cli.Net](https://github.com/angri450/Nong.Cli.Net).

## License

Apache-2.0

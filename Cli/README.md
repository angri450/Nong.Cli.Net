# Angri450.Nong.Cli (nong)

nong 命令行工具 —— angri450 为 Nong.NET 构建的统一入口。Word / Excel / PPT / Chart / Diagram / OCR / Skill 全部操作在一个二进制文件里完成。

## Install

```bash
dotnet tool install --global Angri450.Nong.Cli
```

## Usage

```bash
nong --help                  # 全部命令
nong commands --json         # 机器可读的命令列表
nong word read file.docx     # 提取 Word 文本
nong paper diagnose file.txt # 论文质量诊断
```

angri450 将全部 Nong.NET 库能力暴露为一致的 CLI 接口，方便 AI Agent 和脚本直接调用。

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.NET](https://github.com/angri450/Nong.NET).

## License

Apache-2.0

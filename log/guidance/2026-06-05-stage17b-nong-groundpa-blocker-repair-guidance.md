# 2026-06-05 后续指导：Stage17b Nong/GroundPA 阻断修复后的唯一口径

> Superseded on 2026-06-05 by `2026-06-05-stage17c-pure-dotnet-ocr-guidance.md` for OCR. Do not follow the Python/PaddleOCR bridge guidance in this file for current local OCR work.

## 当前事实

- Nong CLI 当前目标版本：`3.2.2`。
- `nong commands --json` 当前口径：73 implemented commands。
- Word 当前口径：32 commands，包括 `word check` 和 `word convert`。
- CLI tests 当前口径：68/68 PASS。
- CLI 包已写入 `RollForward=LatestMajor`，用于兼容只有 .NET 9/10 运行时的机器。
- Nong.Toolkit.Net 当前口径：2.2.0，Nong CLI-first skills。

不要再把 2026-06-04 的 71 commands / 58 tests / `meta.version=3.1.0` 当作当前事实。那些文件只能作为历史记录读取。

## 必须保留的行为

1. Plugin Marketplace 只安装 skills，不安装 `nong` CLI。所有 Nong-facing skills 必须先做 `nong commands --json` preflight，缺失时提示安装或更新 `Angri450.Nong.Cli`。
2. `.doc` 不直接进入 Open XML 管线。先跑 `nong word check`，再跑 `nong word convert`，得到 `.docx` 后再 dissect/read/edit。
3. Word COM 只允许作为 `.doc -> .docx` 边界转换 fallback，不作为主编辑路径。
4. VML 图片不能静默丢失。`word dissect` 必须输出 image block、asset/Markdown 标记和 warning；`word images` 必须列出 VML。
5. `content.jsonl` 必须包含 `blockId` 和 `index`，否则 `word add --after` 的 agent 路径会再次断掉。
6. `ocr install-model pp-ocrv5-mobile` 不是 E009 空桩。`--dry-run` 输出计划；非 dry-run 安装/更新 Python PaddleOCR 依赖。
7. `ocr local` 是实现入口，但只有 `check-env` 与真实图片 smoke test 都通过后，才能写成稳定本地 OCR 路径。
8. 云端 OCR token 只写 `PADDLEOCR_ACCESS_TOKEN`，来源页面是 `https://aistudio.baidu.com/account/accessToken`。

## 发布前检查

```powershell
dotnet test .\Cli.Tests\Cli.Tests.csproj -c Release --nologo
.\Cli\bin\Release\net8.0\nong.exe commands --json
.\Cli\bin\Release\net8.0\nong.exe ocr install-model pp-ocrv5-mobile --dry-run --json
```

GroundPA 同步检查：

```powershell
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill validate .\Nong.Toolkit.Net\word --json
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill validate .\Nong.Toolkit.Net\multimodal --json
.\Nong.Cli.Net\Cli\bin\Release\net8.0\nong.exe skill scan .\Nong.Toolkit.Net --json
claude plugin validate .\Nong.Toolkit.Net
```

## 发布说明

代码修复不等于 NuGet 发布。对外使用前还要：

1. `dotnet pack` 生成 `Angri450.Nong.Cli.3.2.2.nupkg`。
2. 推 NuGet.org。
3. 推 GitHub/Gitee 源码镜像。
4. 更新 Nong.Toolkit.Net plugin 版本并重新发布。
5. 在发布记录里同时写清楚 CLI package version、`nong commands --json` 的 `meta.version` 和 git hash。

# Nong CLI 模块化重设计

日期: 2026-06-12
状态: draft

## 当前问题

CLI 是路由器，但 dotnet tool 打包机制把所有 ProjectReference 的产物全打进去了。
103MB nupkg，推 NuGet 超时，用户下载等 10 分钟。

根源：`<ProjectReference>` 在 dotnet tool 项目里 = 编译时绑定 + 打包时全量。

## 目标架构

```
nong (核心路由，纯 .NET，<8MB)
  │
  ├── [内嵌] word/excel/genre/bioicons/literature/pandoc/inspect/skill
  │         └── 纯 .NET，没有 native 依赖，可以留在 CLI 里
  │
  ├── [子进程] nong-chart     (SkiaSharp 50MB, 独立 dotnet tool)
  ├── [子进程] nong-diagram   (SkiaSharp, 独立 dotnet tool)
  ├── [子进程] nong-ocr       (PaddleInference native, 独立 dotnet tool)
  ├── [子进程] nong-pdf       (pdfium 3MB, 独立 dotnet tool)
  └── [子进程] nong-pptx      (如果也引了 SkiaSharp, 独立)
```

用户命令不变：

```powershell
nong chart bar ...      → nong 检测 nong-chart 未装 → 自动安装 → 子进程调用
nong diagram tree ...   → 同上
nong pdf render ...     → 同上
nong ocr local ...      → 同上
```

## 两层实施

### 第一层：CLI 变轻（路由 + 纯 .NET 模块）

CLI 保留的 8 个项目：Docx / Excel / Genre / Bioicons / Literature / Pandoc / Inspect / SkillManager + OcrModels

加上路由核心：命令行解析 + 子工具检测 + 自动安装 + 子进程调用。

删掉的 ProjectReference：Chart / Diagram / MultiModal / Pdf / Pptx / Imaging

预计 CLI nupkg：<8MB。

### 第二层：独立工具（每个有 native 依赖的模块）

每个模块单独打一个 dotnet tool：

| 工具 | native 依赖 | 预计大小 |
|------|-----------|---------|
| nong-chart | SkiaSharp | ~50MB |
| nong-diagram | SkiaSharp | ~50MB |
| nong-ocr | PaddleInference native | ~5MB (代码) + OcrModels 包 + runtime |
| nong-pdf | pdfium | ~5MB |
| nong-pptx | 待确认 | 待定 |
| nong-imaging | SkiaSharp | 同 chart |

它们各自有自己的 csproj 配 `<PackAsTool>true</PackAsTool>` 和 `<ToolCommandName>`。

### 第三层：路由协议

CLI 和子工具之间的协议：

```
nong <module> <command> [args] --json
  → 查路由表 → 找到 nong-chart
  → dotnet tool list --global | findstr nong-chart
  → 没装 → dotnet tool install --global Angri450.Nong.Chart
  → dotnet nong-chart <command> [args] --json  (子进程, stdout 透传)
```

路由表（硬编码在 CLI 里，轻量级）：

```csharp
static Dictionary<string, string> ToolRoutes = new() {
    ["chart"] = "nong-chart",
    ["diagram"] = "nong-diagram",
    ["ocr"] = "nong-ocr",
    ["pdf"] = "nong-pdf",
    // pptx 待定
};
```

## 改动清单

| # | 文件/目录 | 改动 | 说明 |
|---|----------|------|------|
| 1 | `Cli/NongCli.csproj` | 删掉 6 个 ProjectReference | Chart/Diagram/Pdf/Pptx/MultiModal/Imaging |
| 2 | `Cli/Commands/*.cs` | 把对应命令改成子进程调用 | 轻量，每类 ~30 行 |
| 3 | `Chart/` 目录 | 新建 `ChartCli.csproj` 配 `<PackAsTool>` | 独立 nong-chart tool |
| 4 | `Diagram/` 目录 | 同上 | nong-diagram |
| 5 | `Pdf/` 目录 | 同上 | nong-pdf |
| 6 | `MultiModal/` | 同上 | nong-ocr |
| 7 | NuGet 包 | 每个工具独立发 NuGet | 小包畅通，大包各走各的 |

## 不做的事

- 不改用户命令（`nong chart bar` 不变）
- 不改 CLI 内部逻辑（子进程 stdout JSON 原样透传）
- 不动 Word/Excel 等纯 .NET 模块

## CLI 体积预测

| 阶段 | 内容 | 大小 |
|------|------|------|
| 现在 | 14 个 ProjectReference | 103MB |
| 拆后 | 8 个纯 .NET + 路由核心 | <8MB |

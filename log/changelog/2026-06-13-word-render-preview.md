# word render-preview 实现 (2026-06-13)

## 问题

需要把 DOCX 渲染为 PNG 页面图片，让 AI 能"看到"排版效果，配合 format-audit 做视觉证据。

## 方案

不自己写渲染引擎。利用项目已有的两条链路拼接：

```
DOCX → LibreOffice --headless --convert-to pdf → PDF → nong-pdf render → PNG
```

- Step 1: 复用 `word convert` 的 LibreOffice 检测逻辑 (FindExecutable/FindLibreOfficeOnWindows)
- Step 2: 通过 `nong-pdf render` 子进程调用已有的 PDFium 渲染 (保持模块化，CLI 不直接引用 PdfCore)

## 文件改动

WordCommands.cs:
- 新增 CreateRenderPreview 命令: `word render-preview <file> -o <dir> [--dpi 150] [--json]`
- 注册到 word 命令组

Manifest.cs:
- 注册 `word render-preview` 到命令清单

## 验证

- dotnet build: 0 errors
- dotnet test: 154 passed
- help 输出正常

## 限制

- 需要安装 LibreOffice (soffice 在 PATH 上)
- 排版保真度取决于 LibreOffice 的 DOCX 导入质量
- 首次调用 nong-pdf（如果未安装）会自动下载安装

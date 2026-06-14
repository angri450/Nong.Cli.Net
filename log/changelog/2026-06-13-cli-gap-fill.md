# CLI 功能缺口补全 (2026-06-13)

## PPTX create — verified DONE

已完整实现。`PptxCommands.CreateCreatePptx()` 含 JSON spec 解析、BuildPptx 幻灯片构建、错误处理、artifact 验证。Manifest 中标记为 implemented。

## PDF ocr-pdf — real OCR text layer

**改动**: `PdfCommands.CreateOcrPdf()` 加 `--with-ocr` 参数。

**之前**: 只渲染页面为 JPEG 图片，底部加 "[Page N - OCR ready]" 占位文本，无实际 OCR。

**现在**: `--with-ocr` 时：
1. PDFium 渲染每页为 JPEG
2. `PdfOcrRecognizerAdapter` 调用 `nong-ocr local --force --json` 子进程
3. 解析 OCR 结果 (blocks, bbox, text, confidence)
4. 将 bbox 从图像坐标映射到 PDF 坐标 (Y 轴翻转)
5. 在 PDF 每页上嵌入 OCR 识别的文本 (AddText)
6. 输出指标含 `ocrTextBlocks` 计数

不含 `--with-ocr` 时保持原有行为 (纯图片 + 占位文本)。

## Word compare — verified DONE

已完整实现。`WordCommands.CreateCompare()` 含 DiffParagraphs 段落级比较、added/removed/modified 三种差异类型、样式变化检测。

## 路线图状态

`log/guidance/2026-06-10-phase2-cli-feature-gaps-roadmap.md`:
10 项缺口 9 项 DONE。仅剩 Word render-preview (复杂度极高, 低优先级, 10-20h)。

## 验证

- dotnet build: 0 errors
- dotnet test: 154 passed
- nong pdf ocr --help: --with-ocr 可见

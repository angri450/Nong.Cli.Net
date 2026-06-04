# 2026-06-03 阶段 6 指导：Word / Inspect 生成能力

## 目标

阶段 6 开始做“文档生成”，但不要一口气做完整论文写作系统。

这一阶段只做三个最有价值的生成命令：

```powershell
nong word fill template.docx data.json -o out.docx --json
nong word rebuild input.docx -o clean.docx --json
nong inspect write paper spec.json -o paper.docx --json
```

## P0 命令

### `nong word fill`

用途：模板填充。

底层优先用：

```csharp
DocxCore.DocxTemplate.Fill(...)
```

### `nong word rebuild`

用途：清理 WPS/Word 手工格式污染，输出更干净的 docx。

底层优先用：

```csharp
DocxCore.StyleRebuilder.RebuildAllParagraphs(...)
```

### `nong inspect write paper`

用途：从 JSON spec 生成论文初稿 docx。

底层优先用：

```csharp
Nong.Inspect.PaperWriter
```

## 暂缓

暂缓这些命令：

- `word merge`
- `word protect`
- `word embed-font`
- `inspect write official`
- `inspect write letter`
- 复杂参考文献自动生成

原因：阶段 6 先证明模板填充、格式重建、论文生成三条主线。

## JSON artifacts

所有写文件命令必须返回：

```json
{
  "artifacts": {
    "docx": "out.docx"
  }
}
```

如果有输入输出文件大小，也写进 metrics：

```json
{
  "metrics": {
    "input_bytes": 12345,
    "output_bytes": 23456
  }
}
```

## 建议 ClaudeCode 任务

```text
目标：实现阶段 6 Word / Inspect 生成能力。

只做：
1. nong word fill <template> <data> -o <docx>
2. nong word rebuild <file> -o <docx>
3. nong inspect write paper <spec> -o <docx>

要求：
- 复用已有 DocxCore / Nong.Inspect API，不在 CLI 内写复杂 OOXML。
- 写文件命令必须支持 --json。
- artifacts.docx 必须是最终输出路径。
- 默认不原地覆盖输入文件，必须要求 -o。
- 输出路径已存在时可覆盖，但要在 summary 中说明。
- Release build 0 错误。
```

## 验收标准

1. 模板填充生成的 docx 能被 `nong word preview` 读取。
2. rebuild 后的 docx 能被 `nong word preview` 诊断。
3. write paper 生成的 docx 能被 `nong word read` 提取文本。

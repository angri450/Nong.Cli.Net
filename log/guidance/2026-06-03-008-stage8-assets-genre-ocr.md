# 2026-06-03 阶段 8 指导：Icons / Genre / OCR

## 目标

阶段 8 做辅助资产能力，不碰主链路大重构。

优先顺序：

1. `genre list/show`
2. `icons list/search`
3. `ocr local/cloud`

OCR 放最后，能晚就晚。

## P0：genre

```powershell
nong genre list --json
nong genre show journal-paper --json
```

用途：让模型知道有哪些格式模板、文体模板、JSON spec 模板。

`genre show` 应输出：

```json
{
  "name": "journal-paper",
  "description": "",
  "schema": {},
  "example": {}
}
```

## P1：icons

```powershell
nong icons list --json
nong icons search "rice" --json
```

用途：生命科学/农学图示素材检索。

只做查询和路径返回，不做复杂绘图。

## P2：ocr

```powershell
nong ocr local image.png --json
nong ocr cloud image.png --json
```

OCR 依赖重、失败面大。阶段 8 只允许做 wrapper，不允许为 OCR 重写大系统。

如果本地依赖不可用，必须返回：

```json
{
  "status": "error",
  "errors": [
    {
      "code": "E005",
      "name": "dependency_missing"
    }
  ]
}
```

## 建议 ClaudeCode 任务

```text
目标：实现阶段 8 辅助资产能力。

优先只做：
1. nong genre list
2. nong genre show <name>
3. nong icons list
4. nong icons search <query>

OCR 只做依赖探测和错误返回；如果现有 MultiModal/OCR API 不稳定，就保留 not implemented，不要硬接。

要求：
- 新建 GenreCommands.cs / IconsCommands.cs。
- 返回结构要省 token，避免输出整份大模板。
- 对大内容提供 --full 选项；默认摘要。
- Release build 0 错误。
```

## 验收标准

阶段 8 的重点不是“功能多”，而是让 skill 层能查：

- 有哪些模板？
- 模板怎么用？
- 有哪些图标素材？
- OCR 依赖是否可用？

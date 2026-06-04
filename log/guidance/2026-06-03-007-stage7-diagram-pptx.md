# 2026-06-03 阶段 7 指导：Diagram / PPTX 输出

## 目标

阶段 7 做展示材料，但只做“科研汇报最小闭环”。

优先级：

1. `diagram flowchart`
2. `diagram network`
3. `pptx read`
4. `pptx create-from-json`

## P0：diagram

建议命令：

```powershell
nong diagram flowchart spec.json -o flow.png --json
nong diagram network spec.json -o net.png --json
```

JSON 输出必须包含：

```json
{
  "artifacts": {
    "png": "flow.png"
  }
}
```

阶段 7 不要做复杂图谱 DSL，只接受已有 Diagram 包能支持的 spec。

## P1：pptx

建议命令：

```powershell
nong pptx read slides.pptx --json
nong pptx create-from-json spec.json -o slides.pptx --json
```

`pptx create-from-json` 只支持：

- title slide
- bullet slide
- image slide
- chart image slide

不要做复杂主题、动画、母版。

## 建议 ClaudeCode 任务

```text
目标：实现阶段 7 Diagram / PPTX 最小展示闭环。

只做：
1. nong diagram flowchart <spec.json> -o <png>
2. nong diagram network <spec.json> -o <png>
3. nong pptx read <file>
4. nong pptx create-from-json <spec.json> -o <pptx>

要求：
- 新建 DiagramCommands.cs / PptxCommands.cs。
- 复用已有 Diagram / Pptx 包 API。
- 所有生成命令 artifacts 返回输出路径。
- 不实现动画、复杂主题、OCR、交互图。
- Release build 0 错误。
```

## 验收标准

阶段 7 完成后，应该能做到：

```powershell
nong chart bar groups.json -o fig.png --json
nong diagram flowchart method.json -o method.png --json
nong pptx create-from-json slides.json -o report.pptx --json
```

这表示论文图、方法图、汇报 PPT 已经形成最小闭环。

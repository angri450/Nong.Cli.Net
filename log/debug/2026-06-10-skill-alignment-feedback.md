# CLI 层问题：2026-06-10 skill 对齐审查（from nong-toolkit debug）

日期：2026-06-10

## 审查触发

nong-toolkit 2.4.0 对齐审查中发现以下 CLI 侧问题。详见 Nong.Toolkit.Net `log/debug/2026-06-10-skill-cli-alignment-review.md`。

## Findings

### MEDIUM — `inspect write-paper` — `keywords` 校验与 skill 文档不一致

`inspect write-paper` 要求 `keywords` 为分号分隔的字符串（`"kw1; kw2; kw3"`），传入 `string[]` 会报 E006：

```json
{ "code": "E006", "message": "keywords must be a string." }
```

但 skill 层的参考文档 `write-paper-spec.md` 声明类型为 `string[]`，示例也是数组格式。

**两个修复方向**：
1. CLI 放宽校验，同时接受 `string` 和 `string[]`。
2. CLI 保持不变，修正 skill 文档。

建议方向 1：分号分隔字符串是 CLI 内部格式，对外 spec 用数组更符合 JSON 惯例，CLI 应在解析时自动 join。

**影响**：`inspect write-paper` 命令。

### MEDIUM — `inspect write-paper` — sections 字段名 `content` vs `body` 不一致

CLI 当前接受 `"content"` 字段（已验证通过），不接受 `"body"` 字段。但 skill 文档 `inspect/SKILL.md` 的示例用的是 `"body"`，这会导致用户和模型困惑。

**建议**：CLI 同时接受 `"content"` 和 `"body"` 作为别名，或至少给 E006 错误消息提示正确的字段名。

**影响**：`inspect write-paper` 命令。

### LOW — `word preview` 生成的论文 sections=0

用 `nong inspect write-paper` 生成论文后，`nong word preview` 报告 `sections: 0`，但文档实际上有 7 个章节和 52 个段落。`nong word stats` 同样报告 `Sections: 0`。

需要确认：是 write-paper 生成的 DOCX 没有创建正确的 section 结构，还是 word preview/stats 的 section 统计逻辑有问题。

**影响**：`word preview` / `word stats` / `inspect write-paper` 之一。

### INFO — 全部命令就绪，生产可用

`nong commands` 报告 109 条命令。skill scan 通过。CLI 版本 4.0.1，NuGet 最新。

# 2026-06-03 阶段 9 指导：Agent Contract / Skill 同步

## 目标

阶段 9 不新增核心功能，专门稳定 agent 调用契约。

`nong` 的价值不是命令多，而是模型知道什么时候调用、怎么解析结果、失败后怎么补救。

## P0：命令契约

新增或强化：

```powershell
nong commands --json
nong commands --group word --json
nong schema command "chart bar" --json
```

如果不想新增 `schema` 命令，也可以让 `commands --json` 包含：

```json
{
  "name": "chart bar",
  "args": [],
  "options": [],
  "input_formats": [],
  "output_artifacts": [],
  "examples": []
}
```

## P0：错误契约

所有命令必须稳定：

- `status`
- `command`
- `summary`
- `data`
- `issues`
- `artifacts`
- `metrics`
- `errors`
- `meta`

错误必须有：

- `code`
- `name`
- `message`

## P1：Nong.Toolkit.Net 同步

阶段 9 再动 skill 层。

目标不是把所有命令写进 skill，而是写调用策略：

```text
读取 docx：优先 nong word read
论文诊断：优先 nong inspect diagnose / nong paper diagnose
统计分析：优先 nong chart analyze
画柱状图：优先 nong chart bar
Excel 转分组数据：优先 nong excel to-groups
```

## 建议 ClaudeCode 任务

```text
目标：稳定 agent 调用契约，不新增大功能。

要求：
1. 检查所有已实现命令的 JSON schema 是否一致。
2. commands --json 增加 args/options/examples/artifacts 字段。
3. 给每个已实现命令补 1-2 条机器可读 examples。
4. 新增 docs/agent-contract.md 或 Cli/AGENT.md。
5. 暂时不要改 Nong.Toolkit.Net；只生成同步建议文档。
6. Release build 0 错误。
```

## 验收标准

阶段 9 完成后，另一个模型只看 `nong commands --json` 和 `AGENT.md`，就应该知道：

1. 该调用哪个命令。
2. 输入文件需要什么格式。
3. 输出里该读哪个字段。
4. 失败后看哪个错误码。

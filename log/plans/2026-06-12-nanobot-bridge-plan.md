# NanoBot 桥接规划：工具自动注册 + Skill 路由

日期: 2026-06-12
状态: done (Phase 1 CLI-side: `nong commands --format openai-tools` complete)

## 背景

Nong 三项目架构：Toolkit (Skill) → CLI (执行) → NanoBot (Agent Runtime)。

Toolkit 有 17 个 skill，CLI 有 109 条命令。NanoBot 需要自动发现这些能力并暴露为 LLM function-calling 工具。

## 当前 NanoBot 已完成

| 组件 | 状态 |
|------|------|
| SkillLoader 二段式 (catalog → skill → reference) | done |
| run_nong bridge (workspace, allowlist, timeout) | done |
| nong commands --json 发现 | done |
| system status 端点 (/status) | done |

## 未完成

| 缺口 | 影响 |
|------|------|
| 109 条 CLI 命令未注册为 AgentLoop 工具 | 目前只能显式调 run_nong，不能自动匹配 |
| Skill trigger 匹配未接入 AgentLoop | catalog 加载了但没用于 skill 激活 |
| OCR/Chart/PDF/Diagram 模块未接入 | 只有 word 模块部分可用 |
| 上下文窗口管理 | 17 个 skill 全量注入会超出 token 限制 |

## 施工计划

### Phase 1: CLI → function-calling schema

从 `nong commands --json` 自动生成 OpenAI 格式的 tools 数组：

```
name: "ocr_local"
description: "Local PP-OCR recognition with pure .NET runtime"
parameters: {
  type: "object",
  properties: {
    image:  { type: "string", description: "Path to image file" },
    force:  { type: "boolean", description: "Skip preflight check" },
    json:   { type: "boolean" }
  },
  required: ["image"]
}
```

实现：CLI 侧加 `nong commands --format openai-tools`，或者 NanoBot 侧解析 `--json` 输出转 schema。

### Phase 2: Skill 路由注入

每个 SKILL.md 的 route table → NanoBot 系统提示词片段。当用户说话时，AgentLoop 根据匹配度注入 1-2 个 skill prompt，保持上下文 <10K token。

### Phase 3: 多模块接入

OCR (刚完成的 v6) / Chart / PDF / Diagram 四个模块的命令注册为 NanoBot 工具。

### Phase 4: 确认机制

涉及下载大文件 (install-model) 或消耗 API token (ocr cloud) 的命令需要用户显式确认才执行。

## 不做的事

- NanoBot 不做模型推理 — OCR native 渲染走 CLI worker 子进程
- 不把 17 个 SKILL.md 全量塞进上下文 — 按需加载

## 执行顺序

1. `nong commands --format openai-tools` (CLI 侧 ~100 行)
2. NanoBot AgentLoop 工具注册 + Skill 匹配 (~200 行)
3. 上下文窗口管理 (~100 行)
4. 确认机制 (~80 行)

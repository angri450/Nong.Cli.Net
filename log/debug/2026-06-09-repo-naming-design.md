# 三仓库命名统一设计

## 时间
2026-06-09

## 现状

合并 Nong.Toolkit.Net 进 Nong.NanoBot.Net 后，剩下两个仓库。当前名称：

| 仓库 | 含义 | 问题 |
|------|------|------|
| `Nong.NanoBot.Net` | 个人 Agent 运行时 | `.net` 后缀像域名不像仓库名 |
| `Angri450.Nong` | 农学文档 CLI 工具集 | 带个人前缀 `Angri450`，与 NanoBot 读不出关联 |
| ~~Nong.Toolkit.Net~~ | 即将合并，消失 |

## 评估维度

### 要不要统一品牌？

`Nong`（农）是农学文档工具的品牌，有明确的领域含义——农业科研。Nong 的用户群体是农学研究者、论文写作者，通过 `dotnet tool install Angri450.Nong.Cli` 安装。

`NanoBot` 是 Agent 运行时的品牌，用户群体是 AI Agent 使用者，通过 WebUI 或 CLI 操作。

这两个品牌的用户有没有交集？有。用 NanoBot 跑 Agent 的人，Agent 会自动调用 nong 命令处理文档。但用户不需要知道底层的 nong 命令怎么写的——Agent 替他们调。

所以两个品牌可以独立存在，但要让人知道它们之间的关系。

### 要不要去掉 `Angri450` 前缀？

NuGet 包名是 `Angri450.Nong.Cli`，GitHub 仓库是 `angri450/Nong.NET`。`Angri450` 是作者标识，不是功能描述。

如果要更专业的外观，可以去掉，但代价是：
- NuGet 包改名 = 旧安装命令失效
- GitHub 仓库改名 = 所有外部链接断裂
- README / CLAUDE.md / skill 中的引用全部要改

**结论**：NuGet 包名不动（破坏面太大），GitHub 仓库名可以考虑微调，但不需要大改。

## 推荐方案

### 方案：两个仓库，父子命名

```
nanobot（Agent 平台）
  └── nong（文档引擎，内置在 nanobot 的 skill 包中）
```

| 项 | 当前 | 建议 |
|----|------|------|
| Agent 运行时仓库 | `Nong.NanoBot.Net` | `nanobot` 或保持不变 |
| 文档工具仓库 | `Angri450.Nong` | 不变（NuGet 包名已固定） |
| 用户感知的品牌 | 模糊 | "NanoBot 是你的 Agent，Nong 是它写论文用的工具箱" |

### 为什么 Nong 不改名

1. NuGet 包 `Angri450.Nong.Cli` 已在用，`dotnet tool install` 命令依赖这个名
2. GitHub 仓库 `angri450/Nong.NET` 已被外部引用（README、skill、changelog）
3. "Nong" = 农，词短、好记、有领域辨识度
4. 改名成本高、收益低

### 为什么 NanoBot 可以考虑微调

1. `Nong.NanoBot.Net` 读起来像网站地址而不是项目名
2. 如果觉得别扭，可以改 repo 名为 `nanobot`（GitHub URL 变成 `angri450/nanobot`）
3. 但这个改动也影响外链和 clone 地址
4. 如果不太在意，可以不动

## 合并后的品牌关系

```
用户
  │
  │  打开 NanoBot WebUI
  │  跟 Agent 说"帮我诊断这篇论文"
  │
  ▼
NanoBot（Agent 运行时）
  │
  │  Agent 调用 skill "inspect"
  │  skill 指导 Agent 执行 nong inspect diagnose paper.txt
  │
  ▼
Nong CLI（文档执行引擎）
  └── 返回诊断结果 JSON
```

两个品牌各司其职：
- **NanoBot** = 用户看到的界面，Agent 住的地方
- **Nong** = Agent 工具箱里的农学文档工具，用户不直接接触，但知道它在

## 结论

两个名字都**不需要大改**。合并 Nong.Toolkit.Net 后自然清晰：

- `Nong.NanoBot.Net` = Agent 运行时 + 内置技能包
- `Angri450.Nong` = 独立 CLI 工具（NuGet 分发，Agent 通过 bridge 调用）

名字之间的关系靠 README 和文档说明，不靠品牌统一。两个工具的服务对象不同（终端用户 vs Agent），分开命名是合理的。

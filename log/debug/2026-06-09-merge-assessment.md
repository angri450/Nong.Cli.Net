# 合并方案评估：Nong.Toolkit.Net → Nong.NanoBot.Net

## 时间
2026-06-09

## 用户想法

把 Nong.Toolkit.Net 和 Angri450.Nong 都合并进 Nong.NanoBot.Net，用一个仓库解决版本不对齐问题。

## 实话

**Nong.Toolkit.Net 应该合并进 Nong.NanoBot.Net**。理由：

1. Nong.Toolkit.Net 是纯文档（0 行 C#），合并成本几乎为零
2. Nong.NanoBot.Net 已经有 SkillLoader.cs，但技能目录空着——11 个 skill 正好填进去
3. 版本同步：skill 和 runtime 同一个 git commit，不再有 "GroundPA 说 Nong 3.2.5 但实际是 4.0.0" 这种漂移
4. 开发流程统一：改 nong 命令 + 更新对应 skill 在同一个 PR 里完成
5. Nong.NanoBot.Net 已有完善的 CLAUDE.md + agent.md + AGENTS.md，Nong.Toolkit.Net 正好缺这些

**Angri450.Nong 不该合并进 Nong.NanoBot.Net**。理由：

1. Nong 是独立分发的 NuGet CLI 工具，3200+ 文件，15 个 vendored 第三方库源码全量编译
2. 如果合并，Nong.NanoBot.Net 的体积直接被撑爆，但它实际只需要调用 `nong` 进程，不需要编译 Nong 源码
3. Nong 的用户（`dotnet tool install Angri450.Nong.Cli`）和 NanoBot 的用户是两组人，不需要绑在一起
4. 两个仓库的关系是正确的：NanoBot 通过 Nong bridge 消费 Nong 的命令输出，通过进程调用，不通过源码引用

**合并后变成两个仓库**：
```
Nong.NanoBot.Net（runtime + skills 在一起）
  ├── Nanobot.Core/       ← Agent 运行时
  ├── skills/             ← 原 Nong.Toolkit.Net 的 11 个 skill
  └── 通过 nong bridge 调用 →
                              Angri450.Nong（独立仓库，不变）
                                ├── nong CLI（dotnet tool install）
                                └── 93 个命令
```

## 和原有 AGENTS.md 的冲突

AGENTS.md 原来写 "Nong.Toolkit.Net 通过 plugin bootstrap 接入"。合并后需要改成 "Nong.Toolkit.Net 已内置为 Nong.NanoBot.Net 第一方技能包"。这是合理的升级：用户拿到 NanoBot 就应该有 Word / Chart / PDF 等基础技能可用，不需要额外安装。

## 不做的

- 不合并 Angri450.Nong 源码
- 不改变 Nong 的独立分发模型（NuGet dotnet tool）
- 不改变 nong CLI 的命令表面（93 个命令保持不变）

## 详细评估

见 `C:\Users\Administrator\Documents\Github\Nong.NanoBot.Net\changelog\2026-06-09-groundpa-merge-assessment.md`

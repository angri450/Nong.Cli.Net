# 2026-06-03 完成后审计指导

## 结论

根据 `changelog/2026-06-03-005-010-stages5-10-complete.md`，ClaudeCode 已经完成阶段 5-10 的一次性实现。

当前项目已经具备 20 个真实命令：

- Word：read / preview / fill / rebuild
- Inspect：diagnose / refs / write-paper
- Chart：analyze / anova / duncan / bar
- Excel：sheets / read / to-groups
- Diagram：flowchart / network
- Genre：list / show
- Icons：list / search

这已经足够作为 `nong` 的第一版科研 agent CLI 骨架。

## 明天第一件事

不要继续加功能。先做验收审计。

重点不是证明“能编译”，而是证明：

1. 命令 JSON 契约一致。
2. 文件输出 artifacts 可用。
3. Excel -> groups -> analyze -> bar 这条链能真实跑通。
4. Word fill/rebuild 生成的 docx 能再被 word read/preview 读取。
5. `pack` 出来的 nupkg 可以本地安装并用全局 `nong` 调用。

## 必测工作流

### 1. Excel 到图表

```powershell
nong excel to-groups data.xlsx --sheet Sheet1 --group A --value B --json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
```

验收：

- `groups.json` 能被 `chart analyze` 接受。
- `fig.png` 真实存在且非空。
- `chart bar --json` 的 `artifacts.png` 指向真实文件。

### 2. Word 生成到再读取

```powershell
nong word fill template.docx data.json -o filled.docx --json
nong word preview filled.docx --json
nong word read filled.docx --json
```

验收：

- `filled.docx` 存在。
- `preview` 不报 fatal error。
- `read` 能读出填充后的核心字段。

### 3. 论文诊断

```powershell
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json
```

验收：

- `diagnose` 输出包含 paperType / evidence / dataReqs / gap / quality。
- `refs` 输出包含 references / risks / citations。
- 不要要求它判断文献真实性；这一步只做格式和对应关系风险。

### 4. 本地 tool 安装

```powershell
dotnet tool install --global --add-source Cli/bin/Release Angri450.Nong.Cli
nong --version
nong commands --json
```

验收：

- 新终端能直接调用 `nong`。
- `commands --json` 能列出 20 个真实命令。

## 重点风险

### 风险 1：PPTX read 是暂桩

日志明确写了：

> pptx read 暂桩，PptxCore 需进一步适配

所以不要把 PPTX 算进正式可用能力。阶段 7 目前只算 Diagram 完成，PPTX 是待补。

### 风险 2：阶段 5-10 跑得太快

一次性完成多个阶段，容易出现：

- 命令能跑，但 JSON 字段不一致。
- artifacts 写了路径，但文件不存在。
- 非 JSON 输出可读，JSON 输出不适合 agent。
- nupkg 能 pack，但本地 tool 安装后缺依赖或找不到资源文件。

这些不是失败，是正常审计项。

### 风险 3：`inspect write-paper` 命名

原路线是：

```powershell
nong inspect write paper spec.json -o paper.docx
```

日志显示实际实现为：

```powershell
nong inspect write-paper spec.json -o paper.docx
```

这个选择可以接受，因为更短、省 token。但要在 `commands --json` 和 `AGENT.md` 中固定，不要两套命名混用。

## 下一步建议

明天只做一个阶段：`阶段 11：验收审计与修补`。

不要新增命令。

只允许修：

- JSON schema 不一致
- artifacts 路径错误
- 命令帮助与实际不一致
- `commands --json` 缺 examples
- nupkg 安装后资源丢失
- 明确的运行时异常

## 阶段 11 ClaudeCode 任务

```text
你在 C:\Users\Administrator\Documents\Github\Angri450.Nong 工作。

目标：阶段 11，只做完成后审计与修补，不新增功能。

请按以下顺序执行：

1. 构建：
   dotnet build Cli/NongCli.csproj -c Release
   dotnet pack Cli/NongCli.csproj -c Release

2. 审计 commands --json：
   - 真实命令数量是否为 20
   - 每个真实命令是否有 description/examples/artifacts 或至少清晰说明
   - inspect write-paper 的命名是否与 AGENT.md 一致

3. 审计 JSON schema：
   - 所有真实命令必须包含 status/command/summary/data/issues/artifacts/metrics/errors/meta
   - 错误必须包含 code/name/message
   - 生成文件命令必须写 artifacts

4. 跑最小工作流：
   - word read/preview
   - inspect diagnose/refs
   - excel to-groups -> chart analyze -> chart bar
   - word fill -> word preview

5. 修复发现的问题。

禁止：
   - 不要新增新命令
   - 不要重构 ThirdParty
   - 不要动 GroundPA-Toolkit
   - 不要处理 OCR/PPTX 复杂能力

输出：
   - 写 changelog/2026-06-03-011-stage11-audit.md
   - 说明通过项、修复项、遗留风险
```

## 最终判断

如果阶段 11 通过，`nong` 就可以进入第一版内部使用。

后面再考虑：

- GroundPA-Toolkit skill 同步
- PPTX 真实实现
- OCR 接入
- 许可证与 NOTICE
- NuGet 正式发布

# 模块化拆分后续施工方案：从包身份到终审

日期: 2026-06-13
状态: done

## 背景

上一轮已经把主 `nong` 从重功能集合改成轻路由器：`chart` / `diagram` / `ocr` / `pdf` / `pptx` / `imaging` 走独立 dotnet tool，主 CLI 本地 pack 结果约 12.04 MB，CLI.Tests 154 项通过。

但这只是第一步。当前剩余问题不是“命令跑不跑”，而是“能不能长期发布、维护、审计”：

- 工具包 `PackageId` 仍复用核心库包 ID，例如 `Angri450.Nong.Chart` 同时像库包又像 tool 包。
- `chart` / `diagram` / `imaging` 单包仍约 83 MB，根因是 SkiaSharp / HarfBuzz native assets 和 `ThirdParty.dll` 仍在每个工具包内。
- `ThirdParty.dll` 约 21.66 MB，是所有重包共同大头；不能在发布前盲拆，但必须进入审计范围。
- PP-OCRv6 命令文案和旧 v5 口径还没有完全统一。
- 现在可以本地跑通，不等于 NuGet 安装、镜像同步、跨平台运行都可靠。

## 总目标

把模块化拆分从“本地能跑”推进到“可发布、可回滚、可审计”：

1. 主 CLI 只负责路由和轻功能，不再引用重 native 功能包。
2. 核心库包和 dotnet tool 包身份分离，避免 NuGet 语义混乱。
3. 大工具包有明确体积策略，不再靠碰运气上传。
4. 每个阶段都有 build / test / pack / local install 闸门。
5. 最后输出一次工程审计，明确哪些可发布、哪些不能发布、哪些必须另开阶段。

## 施工顺序

### 阶段 0：冻结当前拆分基线

目的：先把当前可运行状态固定为基线，避免继续施工时不知道是新问题还是旧问题。

动作：

- 确认上一轮拆分改动仍能构建：
  - `Cli/NongCli.csproj`
  - `Chart/tools/nong-chart.csproj`
  - `Diagram/tools/nong-diagram.csproj`
  - `Pdf/tools/nong-pdf.csproj`
  - `Pptx/tools/nong-pptx.csproj`
  - `MultiModal/tools/nong-ocr.csproj`
  - `Imaging/tools/nong-imaging.csproj`
- 跑 `dotnet test Cli.Tests\Cli.Tests.csproj -c Release`。
- 保留 `log/changelog/2026-06-13-ppocrv6-modular-split.md` 作为当前结果记录。
- 不发布 NuGet，不 push 远端。

验收：

- 7 个项目 Release build 通过。
- CLI.Tests 通过。
- 当前 pack 体积记录存在。

停止条件：

- 如果 CLI.Tests 失败，先修回当前拆分，不进入包身份调整。

### 阶段 1：工具包 PackageId 重命名

目的：先解决最危险的发布语义问题。库包和工具包必须分开，否则后面发布到 NuGet 会把“库”和“命令行工具”的身份搅在一起。

建议命名：

| 工具命令 | 当前工具 PackageId | 新工具 PackageId |
| --- | --- | --- |
| `nong-chart` | `Angri450.Nong.Chart` | `Angri450.Nong.Tool.Chart` |
| `nong-diagram` | `Angri450.Nong.Diagram` | `Angri450.Nong.Tool.Diagram` |
| `nong-pdf` | `Angri450.Nong.Pdf` | `Angri450.Nong.Tool.Pdf` |
| `nong-pptx` | `Angri450.Nong.Pptx` | `Angri450.Nong.Tool.Pptx` |
| `nong-ocr` | `Angri450.Nong.MultiModal` | `Angri450.Nong.Tool.Ocr` |
| `nong-imaging` | `Angri450.Nong.Imaging` | `Angri450.Nong.Tool.Imaging` |

保留：

- `Angri450.Nong.Cli` 仍是主工具包。
- `ToolCommandName` 不改，用户命令仍是 `nong chart ...` / `nong-chart ...`。
- 核心库包 ID 保留给未来库包发布，不再作为 tool 包发布。

主要改动文件：

- `Chart/tools/nong-chart.csproj`
- `Diagram/tools/nong-diagram.csproj`
- `Pdf/tools/nong-pdf.csproj`
- `Pptx/tools/nong-pptx.csproj`
- `MultiModal/tools/nong-ocr.csproj`
- `Imaging/tools/nong-imaging.csproj`
- `Cli/Program.cs` 或路由表所在文件
- 相关 README / help 文案 / 测试断言

验证：

- build 全部工具项目。
- `dotnet test Cli.Tests\Cli.Tests.csproj -c Release`。
- `dotnet pack` 全部工具，确认 nupkg 文件名变为 `Angri450.Nong.Tool.*`。
- 从临时本地源安装至少 2 个工具：
  - `Angri450.Nong.Tool.Chart`
  - `Angri450.Nong.Tool.Pdf`
- 通过主 CLI 路由和直接工具命令各跑一个 JSON 路径。

验收：

- 旧核心库 ID 不再由 tool csproj 产出。
- 主 CLI 自动安装时使用新工具包 ID。
- 用户命令不变。

停止条件：

- 如果 NuGet local install 不能识别新工具包，先修工具包 metadata，不进入体积拆分。

### 阶段 2：打包体积闸门

目的：把“包太大推不上去”变成可测量、可阻断的问题，而不是发版当天才发现。

新增一个本地 pack audit 入口，优先放在 `tools/` 或文档指定命令中，保持纯 .NET / PowerShell，不引入 JS。

建议输出：

- 包名
- nupkg 大小
- 比上一轮大小变化
- nupkg 内 top 10 大文件
- 是否超过阈值

建议阈值：

| 包类型 | 阈值 | 处理 |
| --- | ---: | --- |
| `Angri450.Nong.Cli` | 15 MB | 超过即失败 |
| 轻工具包 | 20 MB | 超过需说明 |
| native 工具包 | 50 MB | 超过 warning，发布前必须有风险记录 |
| 单包 | 100 MB | 默认不发布，必须拆或改策略 |

覆盖包：

- `Angri450.Nong.Cli`
- `Angri450.Nong.Tool.Chart`
- `Angri450.Nong.Tool.Diagram`
- `Angri450.Nong.Tool.Pdf`
- `Angri450.Nong.Tool.Pptx`
- `Angri450.Nong.Tool.Ocr`
- `Angri450.Nong.Tool.Imaging`

验证：

- 运行一次完整 pack audit。
- 把结果写入新的 changelog 或审计记录。

验收：

- 不需要人工解压 nupkg，也能看出哪个文件占体积。
- CI 或本地发版前能复用同一个闸门。

停止条件：

- 如果 audit 工具本身不稳定，暂时保留手工 `dotnet pack` + 解压统计，但不得跳过体积记录。

### 阶段 3：native / RID 体积策略

目的：解决 `chart` / `diagram` / `imaging` 仍约 83 MB 的根因。

先做调查，不直接盲拆：

1. 解包 `Angri450.Nong.Tool.Chart` / `Diagram` / `Imaging`，列出 SkiaSharp / HarfBuzz native assets 分平台占比。
2. 确认 dotnet global tool 对 RID-specific assets 的实际安装行为。
3. 比较两条路线。

路线 A：当前平台优先包

- 优点：最快降低 Windows x64 发包体积。
- 做法：先只发布当前验证平台的 native assets。
- 风险：跨平台用户需要单独包或明确提示。

路线 B：native runtime 拆包

- 优点：长期正确，工具包小，native 按平台分发。
- 做法示例：
  - `Angri450.Nong.Native.Skia.WinX64`
  - `Angri450.Nong.Native.Skia.LinuxX64`
  - `Angri450.Nong.Native.Skia.OsxArm64`
  - 工具包按 RID 或安装逻辑解析 native 包。
- 风险：发布矩阵变大，安装逻辑复杂，需要跨平台 smoke test。

推荐执行：

- 先选路线 A 做 Windows x64 可发布闭环。
- 同时把路线 B 写成长期方案，等主线稳定后再拆 native runtime 包。

主要改动范围：

- 工具 csproj 的 runtime assets include / exclude。
- native resolver 或 probing path。
- pack audit 阈值。
- README 的平台支持说明。

验证：

- 当前 Windows x64 机器实际生成 PNG：
  - `nong chart histogram ... --json`
  - `nong diagram flowchart ... --json`
  - `nong word images --analyze ... --json`
- local tool install 后再跑同样 smoke。
- 如果删除非当前 RID assets，必须确认错误提示对其他平台清晰，不伪装成成功。

验收：

- Windows x64 工具包体积明显下降，目标小于 50 MB。
- 当前平台功能不回退。
- 非当前平台策略写清楚。

停止条件：

- 如果 RID assets 被删后 dotnet tool 安装或运行路径不稳定，回退到全平台包，但把大包发布风险保留为 P0。

### 阶段 4：ThirdParty 边界审计

目的：评估是否需要把 `ThirdParty.dll` 从单一地基拆成多个功能地基。这个阶段只审计和出方案，不在同一轮里硬拆。

要回答的问题：

- `Chart` 真正需要 ThirdParty 里的哪些目录？
- `Diagram` 真正需要哪些目录？
- `Imaging` 真正需要哪些目录？
- `Pdf` 是否真的需要完整 ThirdParty？
- `Docx` / `Excel` / `Pptx` 是否共享 OpenXML / ClosedXML 边界过宽？

审计产物：

- `ThirdParty` 源码目录到模块的使用矩阵。
- 每个工具包中 `ThirdParty.dll` 的必要性判断。
- 拆分候选：
  - `Nong.ThirdParty.OpenXml`
  - `Nong.ThirdParty.Skia`
  - `Nong.ThirdParty.Chart`
  - `Nong.ThirdParty.Pdf`
- 不拆原因和风险。

原则：

- 发布前不做大范围 ThirdParty 拆分，除非大包已经无法发布。
- 不为第三方源码目录新增独立 upstream `.csproj`。
- 如果未来拆，只拆本仓库维护的 ThirdParty 边界项目，并保留 fork snapshot 规则。

验证：

- 静态引用扫描。
- build 矩阵验证。
- 至少一个工具包的试拆 spike，但 spike 可以不合入主线。

验收：

- 得到可执行的 ThirdParty 拆分方案或明确暂不拆结论。

停止条件：

- 如果拆分会影响大多数核心库，推迟到发布后作为单独大阶段。

### 阶段 5：命令合同和用户文案清理

目的：让功能和用户看到的口径一致，尤其是 PP-OCRv6 和工具短命令。

清理项：

- OCR 可见文案统一到 PP-OCRv6，避免 v5/v6 混用。
- `ocr models` / `ocr install-model` / dry-run 输出确认 v6 优先口径。
- 独立工具 help 不再显示不自然路径，例如能避免就不要暴露 `nong-chart chart bar`。
- 主 CLI 外部工具缺失时，安装提示要显示新 `Angri450.Nong.Tool.*` 包名。
- 所有外部工具错误仍输出结构化 JSON，不能混入普通文本破坏 agent 调用。

验证：

- `nong ocr models --json`
- `nong ocr install-model pp-ocrv6-mobile --dry-run --json`
- `nong chart --help`
- `nong-chart --help`
- 缺失工具 / 输入文件不存在 / native 加载失败三类错误路径。

验收：

- 用户文案不再自相矛盾。
- JSON 合同稳定。
- help 可读，但不要求本阶段重写整个命令树。

### 阶段 6：本地安装闭环

目的：模拟真实用户安装，而不是只跑项目 bin 目录。

动作：

- 打包所有待发布 nupkg 到临时目录。
- 使用临时 `--tool-path` 安装主 CLI 和子工具。
- 从一个干净 PATH 场景验证：
  - 主 `nong` 可启动。
  - 未安装子工具时有清晰安装提示或自动安装路径。
  - 已安装子工具时路由正确。
  - 直接执行 `nong-chart` / `nong-pdf` 可用。

建议 smoke：

- `nong --version`
- `nong commands --json`
- `nong chart histogram ... --json`
- `nong diagram flowchart ... --json`
- `nong pdf merge ... --json`
- `nong pdf split ... --json`
- `nong ocr models --json`
- `nong-imaging images missing.docx --analyze --json`

验收：

- 不依赖开发机项目输出目录也能跑。
- 本地 nupkg 安装方式和 NuGet 安装方式一致。
- 所有 smoke 结果写入 changelog。

停止条件：

- 如果 local install 失败，不发布，不进入最终审计。

### 阶段 7：发布前总审计

目的：给出“能不能发”的明确结论。

审计维度：

| 维度 | 检查 |
| --- | --- |
| 架构 | 主 CLI 是否仍只做路由和轻功能 |
| 包身份 | 核心库 ID 和 tool ID 是否分离 |
| 体积 | 每个 nupkg 是否在阈值内，超阈值是否有解释 |
| 命令合同 | JSON schema / errors / artifacts 是否稳定 |
| native | PDFium / Skia / HarfBuzz / OCR runtime 加载路径是否清楚 |
| 测试 | build / Cli.Tests / smoke / local install 是否齐全 |
| 文档 | README / plans / changelog / guidance 是否同步 |
| 发布风险 | 哪些包可以发，哪些包必须暂缓 |

审计输出文件：

- `log/changelog/YYYY-MM-DD-tool-package-identity-and-pack-gates.md`
- `log/debug/YYYY-MM-DD-modular-release-audit.md` 或新的 audit 文档
- 必要时更新 `log/guidance/2026-06-10-package-dependency-map.md`

最终结论格式：

- 可发布：
  - 包列表
  - 版本号
  - 验证命令
- 暂缓发布：
  - 包列表
  - 阻塞原因
  - 下一步
- 不处理：
  - 明确不是本轮范围的事项

验收：

- 审计结论必须是明确的 `GO` / `NO-GO`，不能写成“基本可以”。
- 如果任一重包仍超过 100 MB，默认 `NO-GO`，除非用户明确接受风险。

## 下一步立即执行

下一步先做阶段 1：工具包 `PackageId` 重命名。

原因：

- 它是发布前的语义硬伤。
- 改动范围小，可验证。
- 不先解决它，后面 pack audit 和 local install 都会继续围绕错误包名积累结果。

执行完成后再做阶段 2 的体积闸门。不要先做 ThirdParty 大拆分。

## 不做事项

- 本方案不直接发布 NuGet。
- 不 push GitHub / Gitee / GitCode。
- 不在同一阶段拆 `ThirdParty`。
- 不把 OCR runtime 重新塞回主仓库。
- 不改变用户命令入口。

## 风险

- `PackageId` 改名对已经用旧 ID 安装工具的用户是破坏性变化，但当前处在拆分发布前，越早改越安全。
- RID / native 拆包可能引入跨平台安装复杂度，必须用 local install 和真实 smoke 卡住。
- `ThirdParty` 拆分收益大但风险也大，不能和发版抢同一个窗口。
- PP-OCRv6 是近期变化，模型/runtime 兼容性后续仍要跟 PaddleOCR 版本继续跟踪。

# PptxCore 主题库

## BuildFromJson 可用（10 套）

| # | 文件 | 主题名 | 强调色 | 场景 |
|---|------|--------|--------|------|
| 1 | [professional.json](professional.json) | Professional | 深蓝 #1F4E79 | 商务汇报 |
| 2 | [academic.json](academic.json) | Academic | 暗红 #8B0000 | 论文答辩 |
| 3 | [midnight-executive.json](midnight-executive.json) | Midnight Executive | 午夜蓝 #1E2761 | 高端演示 |
| 4 | [coral-energy.json](coral-energy.json) | Coral Energy | 珊瑚红 #F96167 | 创业路演 |
| 5 | [teal-trust.json](teal-trust.json) | Teal Trust | 青绿 #028090 | 医疗/环保 |
| 6 | [cherry-bold.json](cherry-bold.json) | Cherry Bold | 樱桃红 #990011 | 战略宣导 |

## 内置主题（ThemePresets 静态属性，6 套）

- `Professional` — 深蓝系，适合企业汇报
- `Academic` — 暗红系，适合学术答辩
- `Modern` — 绿蓝紫系，适合科技演示
- `Minimal` — 灰阶系，极简风格
- `Warm` — 橙黄系，故事讲述
- `Cool` — 蓝青系，数据报告

### 使用内置主题

```csharp
SlideBuilder.Create().Theme(ThemePreset.Professional)
```

### 使用 JSON 主题（BuildFromJson）

**先将 JSON 复制到项目目录**（禁止直接从 skill 路径引用——路径含插件版本号，升级后失效）：

```powershell
cp <skill-root>/pptx/formats/*.json <project-dir>/formats/
```

```csharp
var theme = ThemePreset.BuildFromJson("formats/midnight-executive.json");
SlideBuilder.Create().Theme(theme)
```

新增 JSON 主题不需要改代码。创建 `formats/xxx.json`，INDEX.md 加一行即可。

## 主题文件格式

```json
{
  "accent1": "1F4E79",     // 主强调色
  "accent2": "2E75B6",     // 辅强调色
  "accent3": "4A90D9",     // 第三强调色
  "dark1": "1A1A1A",       // 深色文字
  "light1": "FFFFFF",      // 浅色背景
  "dark2": "333333",       // 次要深色
  "light2": "F5F5F5",      // 次要浅色
  "bodyFont": "Calibri",   // 西文正文字体
  "headFont": "Calibri",   // 西文标题字体
  "bodyCJK": "微软雅黑",   // 中文正文字体
  "headCJK": "微软雅黑",   // 中文标题字体
  "notes": "说明文字，人读，机器不用。"
}
```

## 幻灯片尺寸

| 名称 | 宽度 | 高度 | 比例 |
|------|------|------|------|
| 16:9 | 960 pt | 540 pt | 1.78 |
| 4:3 | 960 pt | 720 pt | 1.33 |

# OCR 能力扩张：4 新命令

## What Changed

### New Commands

| 命令 | 功能 | 输入 | 核心逻辑 |
|------|------|------|----------|
| `ocr batch` | 批量目录 OCR | 目录 + pattern | `Directory.EnumerateFiles` → 串行 OCR → 聚合 JSON |
| `ocr video` | 视频帧 OCR | 视频文件 | `VideoCapture` → dHash 帧去重 → OCR → SRT 字幕 |
| `ocr screen` | 屏幕区域 OCR | 屏幕坐标 | `Graphics.CopyFromScreen` → Mat → OCR |
| `ocr camera` | 摄像头 OCR | camera device | `VideoCapture(device)` → 定时抓帧 → OCR |

### 关键设计决策

- **dHash 帧去重** (video): 8x8 灰度差异哈希，Hamming distance < threshold (默认12) 跳过重复帧。20 帧测试视频减到 2 张唯一帧。
- **SRT 输出** (video): 每帧自动生成 3 秒持续时间的字幕条目。
- **Screen WinOnly**: `Graphics.CopyFromScreen` + User32 `GetSystemMetrics`，无 `System.Windows.Forms` 依赖。
- **共享 OCR 客户端工厂**: `CreateOcrClient()` 返回 `(IDisposable, string)` — v6 优先，v5 回退。
- **Native 预加载** (video/camera): 调用 `PpOcrV5Client.CheckEnvironment()` 在 `VideoCapture` 前加载 native DLL。

### 文件改动

| 文件 | 改动 |
|------|------|
| `Cli/Commands/OcrCommands.cs` | +520 行：4 命令 + 7 辅助方法 + Trunc/GetPrimaryScreenBounds/dHash/SRT |
| `Cli/NongCli.csproj` | 添加 `System.Drawing.Common` 9.0.0 NuGet 引用 |
| `MultiModal/MultiModalCore.csproj` | 无改动 |

### 已添加的辅助方法

```
CreateOcrClient()      — v6/v5 客户端工厂 (共享)
InvokeRecognize()      — 统一识别调用
RecognizeOcrFrame()    — Mat → temp PNG → OCR
ComputeDHash()         — 8x8 差异哈希
HammingDistance()      — 汉明距离
WriteSrt()             — SRT 字幕写入
IsImageExtension()     — 图片扩展名检查
GetPrimaryScreenBounds() — User32 P/Invoke
Trunc()               — 字符串截断
FrameOcrResult record  — 帧 OCR 结果
```

### 新依赖

`System.Drawing.Common` 9.0.0 (仅 `ocr screen` 的 `Graphics.CopyFromScreen` 需要)。默认 OCR 和批量命令不需要此包。

### 审计结果 (2026-06-12)

| 项目 | 状态 | 说明 |
|------|------|------|
| ocr batch 并行化 | PARTIAL | 当前单线程串行。可选升级到 `QueuedPaddleOcrAll` |
| RecogniceOcrFrame 临时文件 | OK | Mat → PNG 临时文件是当前 API 约束，v6/v5 客户端只接受文件路径 |
| ModelId 硬编码 | OK | PpOcrV6Client 的 `ModelId` 仍硬编码为 `pp-ocrv6-medium`，装饰性问题 |
| CreateOcrClient 返回类型 | OK | `(IDisposable, string)` 足够此阶段使用 |
| System.Windows.Forms 依赖 | AVOIDED | 用 `GetSystemMetrics` P/Invoke 代替 `Screen.PrimaryScreen` |
| opencv_videoio DLL 依赖 | NOTED | video/camera 需要 `opencv_videoio_ffmpeg*.dll`，当前在 runtime nupkg 中包含 |

## Verification

```
ocr batch  test_*.png (3 files)      → PASS  all recognized correctly
ocr video  ocr_vid_test.avi (20f)    → PASS  2 unique frames, SRT valid
ocr screen --region "100,100,300,200" → PASS  captured terminal window text
ocr camera --device 99               → PASS  graceful error: "Cannot open camera"
```

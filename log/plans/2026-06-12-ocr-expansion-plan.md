# OCR 能力扩张规划

日期: 2026-06-12
状态: pending

## 当前能力

| 命令 | 输入 | 引擎 | 状态 |
|------|------|------|------|
| `ocr local` | 单张图片 (PNG/JPG) | PP-OCRv6/v5 本地 | done |
| `ocr cloud` | 图片/PDF | PaddleOCR-VL-1.6 | done |
| `ocr to-word` | 图片/PDF → DOCX | PaddleOCR-VL-1.6 | done |
| `ocr analyze-image` | 图片 | SkiaSharp 像素分析 | done |
| `ocr check-env` | — | 环境探测 | done |
| `ocr models` | — | 模型清单 | done |
| `ocr install-model` | CDN/NuGet | 模型安装 | done |

## 扩张方向

### 1. 视频帧 OCR

```
nong ocr video <video.mp4> -o <dir> --json
```

流程：
```
VideoCapture → 按 FPS 抽帧 → 去重(相邻帧相似度 > 95% 跳过)
→ PaddleOcrAll.Run(frame) → 按帧聚合结果 → 输出 JSON + 字幕 SRT
```

改动量：~150 行 (VideoCapture + 帧去重 + 聚合)，0 个新文件。

去重是核心优化 — 视频里同一行文字可能出现在连续 100 帧里，不跳过就是浪费算力。

### 2. 批量图片 OCR

```
nong ocr batch <dir> --pattern "*.png" --json
```

流程：
```
Directory.EnumerateFiles(dir, pattern) → QueuedPaddleOcrAll
→ 并行推理 (CPU 核数-1 个并发) → 聚合 JSON
```

QueuedPaddleOcrAll 已内置在 Sdcb.PaddleOCR 中，只需包装。改动量 ~80 行。

### 3. 屏幕区域 OCR

```
nong ocr screen --region 100,100,800,600 --json
```

流程：
```
System.Drawing.Graphics.CopyFromScreen(region) → Bitmap → Mat
→ PaddleOcrAll.Run → 文本输出
```

纯 Windows，不改 OCR 管线。改动量 ~60 行。

### 4. 摄像头实时 OCR

```
nong ocr camera --device 0 --interval 1000 --json
```

流程：
```
VideoCapture(0) → 每秒抓一帧 → OCR → 输出
```

改动量 ~80 行。

## 优先级

| 优先级 | 方向 | 理由 |
|--------|------|------|
| P0 | 批量图片 OCR | QueuedPaddleOcrAll 已就绪，改动最小 |
| P1 | 视频帧 OCR | 逻辑清晰，去重是关键优化点 |
| P2 | 屏幕区域 OCR | Windows only，使用场景窄 |
| P3 | 摄像头实时 OCR | 交互式，需要 UI 反馈 |

## 不做的事

- 不做视频实时 OCR (每帧都跑太慢，无实用价值)
- 不做语音 + OCR 融合
- 不做视频字幕自动翻译

## 命令面设计

最终 `nong ocr` 命令组：

```
ocr local        单张图片
ocr batch        批量图片目录 (NEW)
ocr video        视频文件抽帧  (NEW)
ocr screen       屏幕区域      (NEW)
ocr camera       摄像头采集    (NEW)
ocr cloud        云端 OCR
ocr to-word      OCR → Word
ocr analyze-image 图片结构分析
ocr check-env    环境检查
ocr models       模型清单
ocr install-model 模型安装
```

8 → 12 命令，新增 4 个。

# nong ocr CLI 命令规范

日期：2026-06-02
状态：规划中，待开发

---

## 概述

`nong ocr` 是 OCR 层（Angri450.Nong.MultiModal）的 CLI 入口。覆盖本地 PaddleOCR、云端 PaddleOCR-VL、图像结构分析、OCR 结果转 Word 四类操作。

底层实现：LocalOcrClient、PaddleOcrVlClient、ImageAnalyzer、LayoutToWordConverter。

---

## MultiModal 包 5 个文件 → CLI 命令映射

### 1. LocalOcrClient.cs（本地 PaddleOCR 客户端）

对应命令：

#### `nong ocr local <image> [-o <dir>] [--lang <zh|en>]`
本地 OCR 识别图片文字。

输入：图片文件（PNG/JPEG）
选项：
- `--lang zh` 中文识别，`--lang en` 英文识别
- `--gpu` 启用 GPU 加速
- `--batch <files...>` 批量识别多张图

输出：每行一个识别结果块，含文本+坐标

实现：LocalOcrClient.RecognizeAsync() → 遍历 LocalOcrBlock 列表

#### `nong ocr check-env`
检查本地 OCR 环境是否就绪（Python + PaddleOCR 是否安装）。

实现：LocalOcrClient.CheckEnvironmentAsync()

---

### 2. PaddleOcrVlClient.cs（云端 OCR 客户端）

对应命令：

#### `nong ocr cloud <image> [-o <dir>] [--token <token>]`
云端 OCR 识别（PaddleOCR-VL-1.6）。支持图片、PDF。

选项：
- `--token` API Token（也可通过环境变量 PADDLEOCR_TOKEN 传入）
- `--url <url>` 直接提交 URL 而非上传文件
- `--poll <seconds>` 轮询间隔，默认 5 秒

实现：PaddleOcrVlClient.SubmitFileAsync() → WaitForJobAsync() → DownloadResultsAsync()

#### `nong ocr to-word <image> [-o <docx>] [--token <token>]`
云端 OCR 结果直接输出为 Word 文档（含文字+图片+表格布局）。

实现：PaddleOcrVlClient.ProcessToWordAsync()

---

### 3. ImageAnalyzer.cs（图像结构分析器）

对应命令：

#### `nong ocr analyze-image <image> [--json]`
分析图像结构（不用做 OCR，只分析内容区域分布）。

输出：
```
原始尺寸: 1920×1080
内容区域: (120, 80) → (1800, 1000)
空白率: 15.2%
黑色像素: 342,000
图形像素: 89,000
文字区域: 2 个
  Region 1: (120, 80, 1700, 200) — Text
  Region 2: (120, 250, 1700, 750) — Text
```

实现：ImageAnalyzer.Analyze()

---

### 4. LayoutToWordConverter.cs（布局→Word 转换器）

内部使用（被 `nong ocr to-word` 调用），不暴露独立命令。

---

### 5. OcrModels.cs（数据模型）

纯数据类，内部使用。

---

## 命令总数：6 个

| 类别 | 数量 | 命令 |
|------|------|------|
| 本地 OCR | 2 | local, check-env |
| 云端 OCR | 2 | cloud, to-word |
| 图像分析 | 1 | analyze-image |
| 布局转换 | 1 | 内部调用（to-word） |

---

## 第一版实施计划

| 命令 | 优先级 | 说明 |
|------|--------|------|
| local | P1 | 本地 OCR，基础能力 |
| check-env | P1 | 环境检查 |
| cloud | P2 | 需要 API Token |
| to-word | P2 | 云端+Word 组合 |
| analyze-image | P2 | 图像结构分析 |

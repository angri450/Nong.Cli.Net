# 读一个 Word 切片包

## 用户想要什么

把一篇论文 DOCX 切片后，用 slice 命令检查包的完整性并读证据。

## 做了什么

```powershell
# 1. 切片
nong word dissect paper.docx -o paper.slice --json

# 2. 查看包信息
nong slice inspect paper.slice --json
```

输出：
```json
{
  "status": "ok",
  "data": {
    "schemaVersion": "nongmark/v1",
    "sourceFormat": "docx",
    "blockCount": 156,
    "hasStructure": true,
    "hasFormat": true,
    "hasDiagnostics": true,
    "assetCount": 3
  }
}
```

```powershell
# 3. 列出所有块的 ID 和类型
nong slice blocks paper.slice --json

# 4. 读取第一个图片块
nong slice block paper.slice img0001 --json

# 5. 列出所有素材
nong slice assets paper.slice --json
```

## 结果

`slice inspect` 确认包完整（156 块、有结构和格式流、3 个资产）。

`slice block img0001` 返回块的完整 JSON：类型、文字、位置、关联资产 ID、OCR 结果（如果有）。

## 关键点

- slice 命令读的是切片包，不是原始 docx/pdf/pptx/xlsx。
- `slice inspect` 是第一眼看的命令——快速确认包里有啥、是否完整。
- `slice blocks` 给所有块的目录，`slice block --block-id <id>` 精准读单个块。
- AI 应该优先读 `content.nongmark`（包内文件），slice 命令用于精确取证。

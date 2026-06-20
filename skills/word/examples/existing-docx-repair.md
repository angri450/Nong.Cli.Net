# 旧合同样式清理

## 用户想要什么

一份老旧的技术服务合同 DOCX，复制粘贴过多次，样式污染严重：重复样式 ID、错序样式、脏样式引用，Word 打开就提示"文档包含无法读取的内容"。

## 做了什么

```powershell
# 1. 先做预检
nong word check contract.docx --json

# 2. 看文档概览
nong word diagnose contract.docx --json

# 3. 清理 OOXML 样式污染
nong word clean-styles contract.docx -o contract.clean.docx --json

# 4. 验证修复结果
nong word validate contract.clean.docx --json
```

## 结果

`word diagnose` 报告：7 个 warning（重复样式 ID、错序样式引用），0 个 error。

`word clean-styles` 输出：清理了重复样式、固定了样式 ID 顺序、移除了脏样式引用。

`word validate` 确认：OOXML schema validation 通过。

## 关键点

- 旧合同的典型问题是样式污染，不是内容损坏。先用 `word diagnose` 诊断，确认问题类型。
- `word clean-styles` 是清理 OOXML 样式污染的专门命令。不要用 `word rebuild`（老名字，语义太大容易产生误解）。
- 清理样式不会改变内容。如果内容本身也有问题（缺图注、表格乱序），另外处理。
- 不要去 Powerpoint 或 Excel 那里找 Word 的修复命令——这是 Word skill 的范畴。

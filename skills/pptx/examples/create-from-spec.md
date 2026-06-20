# PPTX 从 JSON 创建

## 用户想要什么
用 JSON spec 快速生成一份简单的演示文稿。

## 做了什么
```powershell
# 准备 spec
echo '{"slides":[{"kind":"title","title":"项目汇报","subtitle":"2026年度"},{"kind":"content","title":"工作进展","items":["完成田间试验","数据分析中","论文撰写中"]}]}' > spec.json

# 创建 PPTX
nong pptx create spec.json -o report.pptx --json
```

## 结果
生成两页 PPTX：首页标题+副标题，第二页标题+三条内容要点。

## 关键点
- `pptx create` 从 JSON spec 生成新 PPTX，不是修改已有文件
- spec 支持 title slide 和 content slide 两种 slide 类型
- content slide 的 items 通过 bullet 列表展示
- 复杂排版（表格、图表、图片）需要用更多 spec 字段

# inspect refs vs lit search — 边界对比

## 用户想要什么

检查一篇论文的参考文献质量。AI 需要判断用 `inspect refs` 还是 `lit search`。

## 场景对比

### 场景 A：内部引用检查

用户说："看看我这篇论文的参考文献有没有问题"。

**应该用 `inspect refs`。**

```powershell
nong inspect refs paper.txt --json
```

做了什么：检查论文引用列表的质量——引用是否缺失、格式是否一致、年份是否矛盾、引用序号是否正确、有没有"幽灵引用"。

**`inspect refs` 不联网**，只分析论文文本内的引用信息。

### 场景 B：外部文献检索

用户说："找一下 2020-2025 年关于玉米氮肥利用率的文献"。

**应该用 `lit search`。**

```powershell
nong lit search --query "SU=玉米 AND SU=氮肥利用率 AND YE BETWEEN(2020,2025)" --json
```

做了什么：在外部文献数据库（OpenAlex、Crossref、Unpaywall）中检索文献。

**`lit search` 联网**，需要外部 provider。

## 一句话区分

| 命令 | 做什么 | 数据源 | 联网 |
|------|--------|--------|------|
| `inspect refs` | 检查论文内部引用质量 | 论文文本 | 否 |
| `lit search` | 检索外部文献数据库 | OpenAlex/Crossref/Unpaywall | 是 |

## 关键点

- 不要混用：引用检查和文献检索是两个完全不同的任务。
- 如果用户问题是"我的引用格式对吗"，用 `inspect refs`。
- 如果用户问题是"帮我找几篇相关论文"，用 `lit search`。
- `inspect refs` 结果中的引用格式问题需要用户手工修复——当前没有自动修引用格式的命令。

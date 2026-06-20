# 校验单个 Skill

## 用户想要什么

修改了 `word/SKILL.md` 之后，确认文件结构和引用正确。

## 做了什么

```powershell
nong skill validate .\word --json
```

## 结果

```json
{
  "status": "ok",
  "command": "skill validate",
  "data": {
    "skill": "word",
    "path": "word\\SKILL.md",
    "hasFrontmatter": true,
    "frontmatterFields": ["name", "description"],
    "hasContent": true,
    "referenceLinks": 7,
    "validReferenceLinks": 7,
    "examplesDir": true,
    "exampleCount": 4
  }
}
```

validate 通过：frontmatter 有 name 和 description，7 个 reference 链接全部有效，examples 目录存在且有 4 个文件。

## 关键点

- 每次改完 SKILL.md 或 references 后都要跑 `skill validate`。
- `validReferenceLinks` 必须等于 `referenceLinks`——任何一个链接指向不存在的文件都会报错。
- 如果新增了 references，validate 会自动检测链接有效性。
- 在打包插件之前，每个改过的 skill 都要单独 validate 一次。

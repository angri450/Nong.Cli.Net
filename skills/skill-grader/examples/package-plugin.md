# 打包整个插件

## 用户想要什么

经过一系列 skill 修改后，打一个完整的 plugin zip 包。

## 做了什么

```powershell
# 先做 inventory 确认所有 skill 都在
nong skill inventory . --json

# 确认 15 个 skill 在列表中，没有缺失

# 做安全扫描
nong skill scan . --json

# 确认 findings 为空

# 打包
nong skill package . --json
```

## 结果

```json
{
  "status": "ok",
  "command": "skill package",
  "data": {
    "packagePath": "../Nong.Toolkit_archive/package-artifacts/nong-toolkit-2.4.0.zip",
    "skillCount": 15,
    "bytes": 94358
  }
}
```

打包产物在仓库外的 `../Nong.Toolkit_archive/package-artifacts/`，zip 包含 plugin 可安装面。

## 关键点

- `skill package` 会自动跑 validate（全部 skill）+ scan。单独跑 validate/scan 是可选的确认步骤。
- 如果 scan 有 High findings，package 会失败。修复后重新打包。
- 打包产物不要提交到 Git 仓库——放在仓库外。
- 版本号在 `plugin.json` 中管理，打包前确认版本号已更新。

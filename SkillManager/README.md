# Angri450.Nong.Skill.Manager (DEPRECATED)

此工具已废弃。angri450 已将 Skill 管理功能迁移到 `nong skill` CLI 命令中：

```bash
dotnet tool install --global Angri450.Nong.Cli
nong skill validate <dir> --json
nong skill scan <dir> --json
nong skill inventory <dir> --json
nong skill package <dir> --json
```

## 仅供本地调试

项目保留可编译状态以备内部兼容，但不再发布或推广：

```bash
dotnet build SkillManager/SkillManager.Cli.csproj -c Release
```

## Author

Built by [angri450](https://github.com/angri450). Source: [Nong.NET](https://github.com/angri450/Nong.NET).

## License

Apache-2.0

# 公开仓库约定

## Git 身份

- 作者与提交者：`Sable Drift`
- 邮箱：`sable-drift@users.noreply.github.com`
- 提交说明：使用中文，推荐格式为 `类型：具体改动`

示例：

```text
功能：增加 Steam 截图增量同步
修复：构建失败时立即终止打包
文档：更新 Windows 真机验证记录
```

不得把真实姓名、账号密码、Token、存档、截图、运行数据库或日志提交到公开仓库。

## 分支建议

- `main`：保持可构建或明确记录构建失败原因。
- 开发功能使用短期功能分支，完成后合并到 `main`。
- 每次修复编译问题后更新 `docs/DEVELOPMENT_PROGRESS.md`。

## 发布前检查

```powershell
python scripts/validate-source.py
./scripts/build.ps1 -Configuration Release
./scripts/package.ps1 -Configuration Release
git status
git log --oneline --decorate -10
```

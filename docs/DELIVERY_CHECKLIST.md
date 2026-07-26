# 交付检查清单

## 仓库内容

- [x] `main` 分支存在完整分阶段 Git 历史。
- [x] 源码、测试源码、脚本、需求、架构、进度、限制、安全和交接文档齐全。
- [x] `.gitignore` 排除运行数据库、日志、真实存档、媒体和常见凭据。
- [x] 源码包要求包含 `.git/HEAD` 与对象历史。

## 当前已执行验证

- [x] `python scripts/validate-source.py` 源码结构检查。
- [x] `git diff --check` 空白与补丁格式检查。
- [x] JSON、XML/XAML、YAML、解决方案结构和关键 IPC 常量静态检查。
- [x] Git 对象完整性检查。
- [ ] Windows `dotnet restore/build/test`。
- [ ] Playnite 10 插件加载。
- [ ] 真实 Ludusavi/Rclone/游戏端到端验证。

## 打包要求

- [x] 源码 ZIP 根目录统一为 `GameSaveCenter/`。
- [x] 包含 `.git`。
- [x] 排除 `bin/`、`obj/`、`artifacts/`、运行数据库、日志、真实存档/截图和密钥。
- [x] 最终 ZIP 进行完整性测试并生成 SHA-256。

## 使用限制

- [x] 明确标记为开发预览源码，而非生产安装包。
- [x] 自动恢复默认关闭。
- [x] 未通过真机测试前不得用于唯一的重要存档副本。

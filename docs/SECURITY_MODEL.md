# 安全模型

## 信任边界

- Playnite 插件只负责 UI、Playnite 数据适配和本地 IPC。
- Worker 执行所有外部进程和文件 I/O。
- Ludusavi 是存档复制/版本/恢复引擎。
- Rclone 自己保存云端凭据，GameSaveCenter 只保存 Remote 名称和目标路径。

## 默认安全策略

- Named pipe 使用当前用户限制和协议版本检查。
- IPC 单消息最大 4 MiB。
- 外部命令通过参数列表传递，不拼接 shell 命令。
- Rclone 只开放 `copy` 与 `check` 适配器。
- 媒体归档只新增；源删除不传播到归档。
- 智能保留只预览，不自动删除。
- 自动恢复默认关闭且 DTO 不提供无确认执行入口。
- 恢复必须生成 PreRestore 并写审计记录。
- 自定义存档候选只生成草案。

## 凭据与隐私

以下内容不得进入 Git、ZIP 或普通日志：

- Rclone 配置文件与 OAuth Token；
- WebDAV、SFTP、网盘密码；
- 真实存档、截图和视频；
- SQLite 运行数据库；
- 用户目录的完整扫描清单。

`.gitignore` 和源码打包脚本包含相应排除规则，但发布前仍需人工检查。

# Windows 真机验证计划

每项结果记录为：`通过 / 失败 / 跳过`，并保存错误日志。不要在同一轮同时开启本地备份、恢复和云端双向行为。

## A. 构建门禁

- [ ] `dotnet --info` 显示 .NET 8 或更高版本的稳定版 SDK（.NET 9 可用）。
- [ ] `dotnet restore GameSaveCenter.sln` 成功。
- [ ] `dotnet build GameSaveCenter.sln -c Release` 无错误。
- [ ] `dotnet test tests/GameSaveCenter.Core.Tests -c Release` 全部通过。
- [ ] `scripts/package.ps1` 生成 Worker 与扩展目录。
- [ ] Playnite 启动后插件无加载错误。

## B. 基础 IPC 与配置

- [ ] Worker 只启动一个实例。
- [ ] 命名管道 Ping 成功。
- [ ] 设置保存后重启 Playnite仍然存在。
- [ ] Worker 数据库位于 `%LOCALAPPDATA%\GameSaveCenter`。
- [ ] 日志中没有 Token、密码或完整 Rclone 配置内容。

## C. 游戏库与启动方式

至少测试：

- [ ] Steam 普通启动；
- [ ] Xbox/Game Pass 普通启动；
- [ ] Epic/Ubisoft/EA/GOG 中实际使用的平台；
- [ ] Playnite Game Action 启动；
- [ ] 直接从平台客户端启动；
- [ ] 一个 MOD loader（例如 SKSE/SMAPI/MO2/Mod Engine）；
- [ ] loader 退出但游戏继续运行时，会话不应提前结束；
- [ ] 同一游戏不会同时出现两个逻辑会话。

## D. Ludusavi 备份

选择一个可丢弃测试存档：

- [ ] 游戏匹配名称正确；
- [ ] 手动单游戏备份成功；
- [ ] 全部备份不会处理未匹配游戏；
- [ ] 游戏退出后生成最终备份任务；
- [ ] 游玩超过配置间隔后生成定时备份；
- [ ] 没有变化时 Ludusavi 行为符合预期；
- [ ] 备份历史、文件数、大小和备注可见；
- [ ] 锁定状态在 Ludusavi 中真实生效；
- [ ] 体积/文件数异常会产生 finding。

## E. 媒体增量同步

- [ ] Steam F12 截图归类到正确 AppID 游戏；
- [ ] 重复运行同步不会再次复制相同文件；
- [ ] 同图改名仍由 SHA-256 去重；
- [ ] Xbox Game Bar 截图正确归类或进入待人工处理；
- [ ] Epic/Ubisoft/EA/GOG 的自定义目录生效；
- [ ] `*.png` 等匹配模式生效；
- [ ] 共享目录不会把明显属于其他游戏的文件归入当前游戏；
- [ ] 错误归类可在 UI 重新分配；
- [ ] 删除源截图不会删除归档副本；
- [ ] 正在写入的视频不会复制半成品。

## F. Rclone 单向云端复制

使用测试 Remote 和空目录：

- [ ] 初始全局云端开关关闭时不会上传；
- [ ] 开启后只调用 `copy`，不调用 `sync/delete/purge`；
- [ ] 本地断网时本地备份仍成功，云任务失败可见；
- [ ] 恢复网络后重新复制成功；
- [ ] `rclone check --one-way` 可验证测试目录；
- [ ] 删除本地测试副本不会自动删除云端文件；
- [ ] 多电脑使用不同 `<MachineName>` 子目录。

## G. 安全恢复

只测试可完全丢弃的存档：

- [ ] 游戏、平台客户端写盘进程和 MOD manager 已关闭；
- [ ] 恢复前创建新的 PreRestore；
- [ ] PreRestore 有备注并锁定；
- [ ] 预览失败时不写入真实存档；
- [ ] 指定历史版本恢复成功；
- [ ] 恢复后游戏能够读取；
- [ ] 撤销恢复回到 PreRestore；
- [ ] 人为制造失败时回滚路径可用；
- [ ] 恢复过程中没有云端任务覆盖本地。

## H. 自动识别与 WGS

- [ ] 候选扫描不会无界扫描整盘；
- [ ] 缓存、日志、shader、截图目录被降权；
- [ ] `.sav/.dat/.profile` 等变化被加权；
- [ ] 接受候选只生成规则草案；
- [ ] 没有静默改动 Ludusavi 自定义配置；
- [ ] Xbox WGS 只在确认映射后备份；
- [ ] 首轮不执行 Xbox WGS 覆盖恢复。

## I. 发布门禁

- [ ] 所有严重错误已关闭或写入已知限制；
- [ ] ZIP 不包含存档、截图、数据库、日志、Token 或密码；
- [ ] ZIP 包含 `.git/HEAD` 和完整提交历史；
- [ ] `DEVELOPMENT_PROGRESS.md` 与实际结果一致；
- [ ] 发布版本号、extension.yaml、程序集版本和文件名一致。

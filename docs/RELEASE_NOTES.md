# 0.1.1 Development Preview

- 修复选中游戏后操作按钮仍保持禁用的问题。
- 命令接入 WPF `CommandManager`，选择变化时重新计算可执行状态。
- 刷新列表后保留已选游戏，首次加载自动选择第一款游戏。
- 记录 Windows 真机构建、Playnite 加载、Ludusavi 匹配与手动备份验证结果。

# 0.1.0 Development Preview

## 已实现源码

- Playnite 10 GenericPlugin 与 Apple HIG 启发的统一面板；
- Playnite 游戏库导出、Game Action/MOD loader 识别；
- Worker 命名管道、SQLite、任务、日志和审计；
- Ludusavi 匹配、单游戏/全部/定时/退出备份；
- 版本备注、锁定、历史索引、差异和保留预览；
- PreRestore、安全恢复、回滚和撤销流程；
- 外部游戏进程与多进程 MOD 启动链兜底；
- Steam、Game Bar、Windows 和自定义平台媒体增量同步；
- SHA-256 去重、稳定写入检测、原子复制和误归类修正；
- Rclone `copy/check` 安全适配；
- 候选存档路径评分、WGS 辅助扫描和规则草案；
- Windows 构建/测试/打包/安装脚本；
- Core xUnit 测试源码与跨平台结构校验。

## 尚未声明完成

- Windows 编译、Playnite 真机加载和真实游戏端到端验证；
- Worker 主动推送后台通知；
- 公共截图目录的完整会话时间归类；
- 默认会话的启动前/退出后文件快照差异；
- Rclone 远端设备摘要摄取及完整多设备冲突 UI；
- 未知进程映射学习 UI。

详见 `IMPLEMENTATION_LIMITATIONS.md`。

## 0.1.0 开发预览修订 1

- `global.json` 不再锁死不存在的 `8.0.420`，现在允许 .NET 8 或更高稳定版 SDK；用户现有的 .NET 9.0.302 可被解析使用。
- `build.ps1` 对每一个 `dotnet` 原生命令检查退出码，失败立即终止，不再显示虚假的“构建完成”。
- `package.ps1` 只有在编译和测试成功后才创建打包目录，并对 Worker 发布退出码进行检查。
- 新增 GitHub Actions Windows 构建、测试、打包工作流。
- 插件作者和完整 Git 历史统一改为英文笔名“Sable Drift”，提交信息统一使用中文。

- 修复 Playnite 插件日志初始化：使用 `LogManager.GetLogger()`，兼容当前 PlayniteSDK 6.16.0。
- 清理 Windows 首次真实编译暴露的主要可空引用警告。
- Git 作者统一改为英文笔名 `Sable Drift`。

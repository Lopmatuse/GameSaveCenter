# 0.4.0 Development Preview

- 将用户提供的完整 Apple-inspired WPF/Codex 设计规范保存到仓库，并新增强制 UI 变更门禁。
- 外观设置新增“跟随 Playnite / 浅色 / 深色”，保存后管理面板即时重算主题色板。
- 未匹配游戏会话开始时异步记录有界文件快照，退出后比较新增和修改文件并生成可解释存档路径候选。
- 候选路径持久化到 SQLite，支持查看依据、接受生成 Ludusavi 规则草案以及忽略候选。
- Worker 启动时清理过期检测快照，避免异常退出后长期积累。
- 任务详情新增复制错误和任务 ID；失败/取消的备份与媒体同步任务支持安全重试。
- Game Bar 与 Windows 公共媒体目录新增无重叠会话时间窗口归类；同时运行多个游戏时自动退回文件名匹配，避免猜测。
- 共享主题敏感资源开始集中到 `Themes/DesignTokens.xaml`，Dashboard 与设置页复用同一色板键。

# 0.3.5 Development Preview

- 修复任务耗时只读属性绑定错误，恢复管理面板自动刷新。
- 历史查询会主动与 Ludusavi 对账，备份 ZIP 已生成但索引缺失时能够自愈。
- 新增大型游戏库搜索、状态筛选、排序和结果计数。
- 重构任务进度列与底部状态区，空闲时不再显示空进度框。
- 新增面向第三方 Playnite 主题的对比度派生色板，修复浅色主题黑块和深色主题低对比。
- 启用像素对齐与 ClearType 渲染，移除正文透明度和按钮悬停缩放，改善 DPI 下文字锐度。

# 0.3.4 Development Preview

- 修复中文 Windows 双击一键批处理时乱码、命令截断和 PowerShell 未启动的问题。
- 新增 ASCII-only 的 `GameSaveCenter-Run.cmd`，中文入口保留为兼容包装。
- PowerShell 安装脚本使用 UTF-8 BOM，并持续记录 `artifacts/one-click-install.log`。
- 构建前新增批处理 ASCII/CRLF 与 PowerShell BOM 检查。

# 0.3.3 Development Preview

- 修复开发安装后 Playnite 仍加载旧版扩展的问题。
- 新增双击式一键构建、测试、打包、原子安装和启动流程。
- 打包文件名改为动态版本，安装后强制核验清单与 DLL 版本。
- 包含 0.3.2 的悬停动画 Freezable 精准修复。

# 0.3.1 Development Preview

- 管理面板增加左侧应用导航，功能入口与详情标签保持同步。
- 新增主题自适应拟态毛玻璃：半透明渐变表面、模糊环境光、细高光边框与柔和阴影。
- 浅色、深色和 Windows 高对比度模式使用不同色板；高对比度自动关闭环境光和透明表面。
- 新增页面进入、游戏切换、标签切换、任务选择、状态胶囊、卡片悬停、导航悬停和按钮悬停动画。
- 动画只使用 Opacity 与 RenderTransform，并遵循 Windows 客户区动画设置。
- 设置页新增“启用界面动画”“启用毛玻璃”和毛玻璃强度 20–100%，支持实时预览。
- 设置页同步采用玻璃卡片、环境光和进入动画。
- 跨平台源码校验新增 WPF Trigger 层级、TargetName 和 XAML 事件处理器检查。

# 0.3.0 Development Preview

- 管理面板支持可配置的轻量自动刷新，手动长任务执行期间仍能看到实时进度。
- 任务页新增进度条、耗时、任务 ID、完整详情和 Queued/Running 任务取消入口。
- 修复排队阶段取消可能无法写入 Cancelled 状态及清理任务 Token 的问题；运行中取消会终止对应外部工具进程，避免孤儿进程。
- Worker 启动时自动把异常退出遗留的 Queued/Running 任务标记为 `WORKER_RESTARTED`。
- 自动任务成功、失败或取消时可显示 Playnite 通知；设置页可关闭通知。
- 新增诊断中心：查看有效 Worker/Ludusavi/备份策略，复制诊断摘要并打开数据、存档、媒体和 Worker 日志目录。
- `settings.get` 使用稳定的非敏感 DTO，Rclone 远端只暴露是否已配置。

# 0.2.0 Development Preview

- Worker 运行设置持久化到本地文件，重启后不再丢失 Ludusavi 路径。
- Playnite 启动、游戏事件和刷新都会可靠发送设置；刷新会重新导出游戏库并匹配 Ludusavi。
- Worker 启动等待提升至 30 秒，记录启动输出并处理同路径失效残留进程。
- 默认使用 ZIP 多版本：完整版本 3、每组差异版本 5，可在设置页调整格式、压缩和数量。
- 备份任务区分新版本、不同内容、无变化和 Simple 当前副本更新，并保留真实错误码与诊断。
- 修复备份历史复合主键、同一版本更新时间、刷新后历史消失和旧记录清理。
- 任务、存档、媒体与审计时间统一转换为 Windows 本地时间显示。
- 仪表盘与设置页按 Apple HIG 启发重构：主题资源、圆角卡片、弱边框、紫蓝强调、状态点、空状态和浅色/深色兼容。
- 新增 `KNOWN_ISSUES.md`，持续记录 GSC-001 至 GSC-019 的修复与回归状态。

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

## 构建热修复

- 修复 Apple 风格按钮按压模板的 WPF 名称作用域错误（`MC4111`）。
- 按压反馈仍保留 0.97 倍缩放和轻微透明度变化。

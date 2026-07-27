# 项目记忆与不可丢失约束

更新时间：2026-07-27
当前版本：`0.3.4-development-preview`

本文档用于跨会话、上下文压缩或更换开发者时恢复完整项目意图。修改需求、架构、完成状态或安全边界时，必须同步更新本文档和 `DEVELOPMENT_PROGRESS.md`。

## 用户确认的最终方向

采用 **方案二：Playnite 插件 + 后台助手 Worker**。

- Playnite 是唯一主要 UI 和游戏库入口。
- Worker 没有第二套复杂主界面，只负责耗时、持续和系统级任务。
- 不强制从 Playnite 启动游戏：Playnite 事件优先，Worker 进程侦测兜底。
- 支持通过 MOD Organizer 2、SKSE、SMAPI、Mod Engine、Reloaded-II 等加载器启动。
- 截图/录像不是“多版本备份”，而是只新增、去重、可归类的媒体同步。
- 截图来源重点：Steam、Xbox/Game Bar、Epic、Ubisoft、EA、GOG；没有统一截图目录时允许按游戏配置自定义来源，并使用游戏会话辅助归类。
- 从项目建立开始使用 Git；合理阶段自行提交；交付 ZIP 必须包含 `.git` 完整历史。
- 项目内持续维护功能进度表、需求记忆、已知限制和可供 Codex 接手的提示词。
- 公开仓库 Git 作者使用英文笔名“Sable Drift”，提交说明统一使用中文；不得改用平台名称或真实姓名作为作者。
- UI 采用 Apple HIG 启发风格：清晰层级、宽松留白、圆角分组、克制材质、语义化状态和轻量动画；不是仿冒 macOS，也不能牺牲 Windows/Playnite 可用性。

## 当前已经进入仓库的实现

### 工程与协议

- 四项目分层：`Contracts`、`Core`、`Worker`、`Playnite`，另有 Core xUnit 测试工程。
- 插件与 Worker 使用版本化命名管道请求/响应协议。
- SQLite 保存游戏映射、策略、会话、任务、异常、历史摘要、媒体索引、媒体来源、候选路径和审计。
- Windows 构建、测试、发布、开发安装、静态校验和含 `.git` 源码打包脚本已建立。

### 存档

- Ludusavi 健康检查、游戏扫描/匹配、单游戏与全部备份、历史列表、备注、锁定和指定版本恢复适配。
- Playnite 退出事件和 Worker 外部进程会话可触发退出备份；默认支持 30 分钟间隔备份。
- 文件数量、总体积、零字节、异常下降、长会话无变化等可靠性校验。
- 分层历史保留算法与 UI 预览；不自动删除。
- 安全恢复状态机：关闭检查、PreRestore、预览、恢复、校验、失败回滚、撤销恢复。

### 游戏启动与 MOD

- 插件导出 Playnite 游戏、平台 ID、安装目录和多个 Game Action。
- Worker 基于已知 EXE、安装路径、Action、父子进程和 MOD loader 建立游戏会话。
- 同一游戏的多进程启动链会合并为一个逻辑会话，避免 loader 先退出导致过早备份。

### 媒体

- Steam AppID 截图目录、Xbox/Game Bar/Windows 公共目录、安装/Action 相邻目录和每游戏自定义来源。
- 新文件增量复制、稳定写入检测、原子复制和 SHA-256 全局去重。
- 源文件删除不会删除归档；媒体可以在 UI 中重新归类。
- 自定义来源支持匹配模式和共享目录标记，数据库升级会自动补列。

### 云端与识别

- Rclone 只提供 `copy/check` 适配，受全局和每游戏上传开关共同控制。
- 有界候选路径扫描、可解释评分、Xbox WGS 辅助候选和 Ludusavi 自定义规则草案。
- 多设备冲突核心判定算法已存在，但远端设备摘要摄取尚未形成闭环。

## 当前明确未完成或待验证

1. 当前交付环境没有可用 .NET SDK/MSBuild，因此最新 0.3.1 改动只能做结构校验；早期版本已由用户在 Windows 完成 restore/build/test/package 和 Playnite 加载。
2. Windows 已验证游戏库、运行状态、Ludusavi 匹配和测试存档备份；ZIP 多版本、安全恢复、Rclone、真实媒体来源与 MOD 复杂会话仍需端到端回归。
3. Worker → Playnite 的主动事件推送尚未完成；0.3.0 先通过管理面板轻量轮询实现任务进度、完成通知和取消。面板关闭时不保证即时通知。
4. 公共截图目录目前以文件名和已知来源匹配为主；会话时间窗口归类与未识别收件箱仍需完善。
5. 候选存档扫描已有基础，但“游戏启动前快照 + 退出后差异”的默认会话闭环尚未接入。
6. 多设备冲突尚缺 Rclone 远端 sidecar/摘要读取和完整 UI。
7. 未知进程/MOD 启动链尚缺人工学习并持久化映射的 UI。
8. 智能保留只预览，不自动删除；恢复时对正在执行的独立云任务尚缺全局暂停锁。

## 2026-07-27 真机缺陷结论与 0.2.0 决策

- Windows 已完成 build/test/publish/package，插件可加载，游戏库与运行状态可读取。
- `Unmatched` 与 Backup Failed 的直接原因是 Worker 的 `ludusaviExecutable` 为空；Ludusavi 0.31.0 CLI 对测试游戏和 Bongo Cat 均能返回 score 1.0。
- 运行时设置必须持久化；Playnite 启动、刷新和游戏事件必须再次发送完整设置。
- 刷新必须重新导出 Playnite 游戏库、重新匹配并显式重载当前游戏详情。
- 默认采用 ZIP 多版本，不再把 Simple 单副本误称为完整历史；保留数量由 GameSaveCenter 显式控制。
- SQLite 备份历史以 `(playnite_id, backup_id)` 为主键，同一 ID 更新时必须更新创建时间。
- 所有 UTC 继续用于持久化和通信，UI 展示统一调用本地时区。
- 任务页面必须展示 ErrorCode/ErrorMessage，不能只显示“执行失败”。
- UI 继续作为 Playnite 内嵌页面，不绘制不存在的 macOS 窗口按钮；通过动态主题资源兼容浅色和深色模式。
- 完整缺陷状态见 `KNOWN_ISSUES.md`。

## 2026-07-27 0.3.0 继续开发记忆

- 用户暂时无法进行 Windows 测试，允许先继续开发不依赖即时真机反馈的功能。
- 管理面板打开时每 10 秒轻量刷新，可在设置中关闭或调整为 5–300 秒。
- 自动刷新必须在手动备份等待期间继续工作，使任务进度和取消入口可用；不得再次复用全局 `IsBusy` 作为轮询锁。
- 任务页支持取消 Queued/Running 任务。取消只请求安全中止，不应强行终止正在写文件的外部进程。
- Playnite 通知仅有 Info/Error 两种严重级别；自动任务完成、失败和取消由面板观察到状态变化后通知。
- `settings.get` 使用 `WorkerSettingsSnapshotDto` 返回非敏感有效设置；Rclone 远端只返回是否配置，不暴露目标文本。
- 诊断摘要可复制，包含版本、有效路径、备份策略、游戏计数和最近失败任务；不得包含密码、Token 或完整 Rclone 配置。

## 安全约束

1. 默认关闭启动前自动恢复。
2. 恢复前检查游戏、启动器和 MOD 管理器是否关闭。
3. 恢复前创建当前存档快照并锁定为 `PreRestore`。
4. 云端默认 `rclone copy`，禁止默认 `rclone sync`、`delete` 或 `purge`。
5. 源截图删除时，不自动删除已归档副本。
6. 未确认的存档候选路径不得直接进入自动恢复流程。
7. Xbox WGS 只做辅助识别和备份，不能假定所有结构均可安全还原。
8. 不在日志、Git、配置示例中保存 OAuth Token、WebDAV 密码或 Rclone 密钥。
9. 未通过的编译、测试和真机验证不得写成“已完成”。

## 四阶段范围

### 第一阶段：最小可用版本

- Playnite 插件骨架
- Worker 骨架与 IPC
- Ludusavi 路径配置
- 游戏列表与匹配状态
- 手动单游戏/全部备份
- 退出后自动备份
- 默认 30 分钟定时备份
- 基础成功/失败反馈
- 日志页面
- Playnite/MOD/外部进程游戏会话识别
- Steam、Xbox 及自定义来源截图增量同步

### 第二阶段：可靠性

- 文件数量、大小、零字节和异常下降校验
- 长时间游玩无存档变化提醒
- 云端上传状态与校验
- 每游戏策略
- 版本备注与锁定
- 智能分层历史保留
- 媒体写入完成检测、哈希去重、误归类修正、未识别收件箱

### 第三阶段：恢复

- 历史版本浏览和时间线
- 文件差异展示
- PreRestore 快照
- 恢复确认、执行后校验、失败回滚、撤销恢复
- 恢复期间暂停云同步，避免旧云端数据反向干扰

### 第四阶段：自动识别

- 文件变化前后快照
- 候选路径评分
- Xbox WGS 辅助识别
- Ludusavi 自定义规则草案生成
- 多设备冲突检测
- 未知游戏进程与 MOD 启动链学习
- 截图目录候选发现和公共截图会话归类

## 当前兼容基线

- Playnite 10.56
- PlayniteSDK 6.16.0
- Playnite 插件目标：.NET Framework 4.6.2
- Worker 目标：.NET 8 / Windows x64
- 构建基线：项目目标为 .NET 8；`global.json` 允许使用 .NET 8 或更高稳定版 SDK，用户当前 .NET 9.0.302 可参与构建
- Ludusavi 推荐：0.30+
- Ludusavi for Playnite 仅作为交互行为参考，不作为运行依赖

Playnite 11 的 SDK 与迁移边界仍可能变化。本项目先稳定支持 Playnite 10，并隔离 Playnite 适配层。

## 下一位开发者的首要工作

1. 在 Windows 执行 `scripts/build.ps1`，修复真实编译错误并提交。
2. 通过 `scripts/package.ps1` 生成扩展目录和 Worker，再安装到 Playnite 10。
3. 使用可丢弃 Steam 游戏按 `WINDOWS_TEST_PLAN.md` 跑通备份、媒体、恢复和撤销。
4. 实现 Worker 主动任务事件推送、公共截图会话归类、显式前后快照和远端设备摘要。
5. 每完成一组功能同步更新本文档与进度表，并保留分阶段 Git 提交。

## 交付要求

- 源码、文档、脚本、测试与完整 `.git` 一并交付。
- ZIP 不得包含真实用户存档、截图、Token、密码、本机运行数据库或日志。
- 发布说明必须明确区分“源码已实现”“静态校验已通过”“Windows/真机已验证”。

## 2026-07-27 真机缺陷与验证记忆

- Windows 构建、测试、发布、打包与 Playnite 开发安装均已成功。
- Ludusavi CLI 匹配正常；`Unmatched` 根因为 Worker 的 `ludusaviExecutable` 未被可靠传入且重启后不持久化。
- 通过 IPC `settings.update` 写入路径后，整个游戏库重新匹配为 `Ready`。
- 测试游戏手动备份成功并产生历史版本。
- UI 中依赖 `SelectedGame`、`SelectedBackup` 等条件的命令没有触发 `CanExecuteChanged`，导致“立即备份/校验/侦测路径/保存策略”等按钮一直禁用；0.1.1 已修复。
- 后续必须持续修复：设置持久化、完整刷新、Worker 生命周期、本地时间显示、诊断信息以及深色主题视觉。


## 2026-07-27 0.3.1 UI 继续开发记忆

- 用户明确将 UI 与动画视为同等重要，并偏好 Blur/毛玻璃视觉。
- 插件是 Playnite 内嵌 `UserControl`，不拥有宿主 HWND，因此采用主题自适应拟态玻璃，不声称实现系统级 backdrop blur。
- 新界面增加固定左侧导航，并与详情 Tab 双向同步；不添加红黄绿窗口按钮。
- 毛玻璃由半透明渐变表面、模糊环境光、细边框和阴影组成，文字和内容本身不能模糊。
- 动画只操作 `Opacity`、`TranslateTransform`、`ScaleTransform`，遵循 Windows 客户区动画设置。
- 设置新增 `EnableUiAnimations`、`EnableGlassEffects`、`GlassEffectStrength`，旧设置默认分别为 true、true、78。
- 高对比度模式自动关闭环境光和半透明，避免为了视觉牺牲可访问性。
- `scripts/validate-source.py` 已增加 XAML Trigger/TargetName/事件处理器语义检查；仍不能替代 Windows WPF 编译。

## 2026-07-27 0.3.2 崩溃根因记忆

- `extensions.log` 没有 GameSaveCenter 堆栈，真正异常在 `playnite.log`。
- 根因不是 Blur 或页面进入动画，而是 WPF 会冻结 Style Setter 中共享的 `TranslateTransform`/`ScaleTransform`。
- 鼠标经过侧栏或指标卡时，`AnimateTranslate` 对冻结对象执行 `BeginAnimation`，Playnite 捕获为不可恢复扩展异常。
- 后续所有代码动画必须使用元素独占且未冻结的 Transform；遇到 `IsFrozen` 必须 `CloneCurrentValue()` 后回写。


## 2026-07-27 0.3.3 开发安装链路记忆

- 用户应用 0.3.2 精准动画修复后，Playnite 扩展管理仍显示 0.3.1，并继续触发旧版闪退。截图证明实际安装目录没有被新产物替换。
- 新增仓库根目录 `GameSaveCenter-一键构建安装运行.cmd`，双击后自动停止 Playnite 和 Worker、清理、构建、测试、打包、原子安装、版本核验并重新启动 Playnite。
- `package.ps1` 不再写死 0.2.0 文件名，而是从 extension.yaml 动态读取版本。
- `install-dev.ps1` 不再忽略旧目录删除失败，并核对安装后的清单版本和 DLL 文件版本。


## 2026-07-27 0.3.4 一键脚本编码记忆

- Windows `cmd.exe` 不能可靠解析无 BOM UTF-8 且包含中文的批处理正文；即使脚本第一行执行 `chcp 65001`，解析阶段仍可能已经发生乱码和命令截断。
- 根目录双击入口必须保持 ASCII-only 和 CRLF；所有中文提示放到带 UTF-8 BOM 的 PowerShell 脚本中。
- 一键流程失败时优先读取 `artifacts/one-click-install.log`，成功版本核验读取 `artifacts/last-dev-install.txt`。

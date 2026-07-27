# 开发实现进度

更新时间：2026-07-27
当前版本：`0.3.0-development-preview`

状态定义：

- ✅ **已开发**：代码和文档已进入 Git；不依赖 Windows 专属环境即可验证的部分已完成结构检查。
- 🧪 **已开发待 Windows 验证**：源码已实现，但必须在 Windows、Playnite、真实 Ludusavi/Rclone/游戏数据上编译或验证。
- 🚧 **部分实现**：核心算法或基础链路已完成，仍缺真实平台数据、远端摄取或完整 UI 闭环。
- ⬜ **未开发**：没有可用实现。

> Windows 真机已完成编译、单元测试、Worker 发布、PEXT 打包和 Playnite 加载。0.2.0 当前改动在本执行环境只完成静态校验，仍需在 Windows 重新 build/test/package 并按 `WINDOWS_TEST_PLAN.md` 回归。

## 工程与治理

| 功能 | 状态 | 备注 |
|---|---|---|
| Git 仓库与分阶段提交 | ✅ | `main` 分支，完整 `.git`；历史提交已改为中文，作者统一为“Sable Drift” |
| 项目记忆文件 | ✅ | `PROJECT_MEMORY.md` |
| 需求、架构、安全与 UI 文档 | ✅ | `REQUIREMENTS.md`、`ARCHITECTURE.md`、`design/APPLE_UI_GUIDE.md` |
| Codex 延续开发提示词 | ✅ | `CODEX_CONTINUATION_PROMPT.md` |
| Windows 构建/测试/打包/安装脚本 | ✅ | 用户已在 Windows/.NET 9.0.302 完成 build、test、publish、package 与开发安装 |
| 含 `.git` 的源码打包脚本 | ✅ | `scripts/package-source.ps1` 使用 ZipFile，包含隐藏目录 |
| 跨平台源码结构校验 | ✅ | `scripts/validate-source.py` 已通过 |
| Core 单元测试源码 | ✅ | 6 组 xUnit 测试；当前环境未执行 |
| Windows 真机编译与 Playnite 加载 | ✅ | 0.1.1 已在 .NET 9.0.302 构建、测试、打包并加载；0.2.0 待重新回归 |

## 0.3.0 本轮新增状态

| 项目 | 状态 | 说明 |
|---|---|---|
| 管理面板自动刷新 | 🧪 | 页面打开时按 5–300 秒配置轮询仪表盘；手动长任务运行期间仍可刷新进度 |
| 后台任务取消 | 🧪 | 任务页可选中 Queued/Running 任务发送取消请求；修复排队阶段取消未落库与外部进程残留问题 |
| 任务完成通知 | 🧪 | 自动任务成功、失败或取消后写入 Playnite 通知；手动操作继续给出明确结果 |
| 任务进度详情 | 🧪 | 任务列表新增进度条、耗时、任务 ID、完整错误详情和取消入口 |
| 诊断中心 | 🧪 | 展示 Worker/Ludusavi/备份策略/有效目录，支持复制诊断与打开数据、存档、媒体和 Worker 日志目录 |
| 有效设置 DTO | ✅ | `settings.get` 改为稳定的非敏感契约，不再依赖匿名 JSON 形状 |

## 0.2.0 本轮修复状态

| 项目 | 状态 | 说明 |
|---|---|---|
| Worker 设置持久化 | 🧪 | `%LOCALAPPDATA%\GameSaveCenter\worker-settings.json` 原子写入，重启恢复 |
| 刷新完整同步 | 🧪 | 发送设置、导出全部 Playnite 游戏、重匹配、加载仪表盘与当前游戏详情 |
| Worker 生命周期 | 🧪 | 30 秒等待、启动日志、同路径失效进程重启 |
| ZIP 多版本策略 | 🧪 | 默认完整 3、差异 5、zstd 3；设置页可调整 |
| 历史数据库迁移 | 🧪 | 主键迁移为 `(playnite_id, backup_id)`，同 ID 更新时间可刷新 |
| 任务真实错误 | 🧪 | 稳定错误码、退出码、stdout/stderr 诊断进入任务详情；Worker 重启会把遗留任务标记为 `WORKER_RESTARTED` |
| 本地时间显示 | 🧪 | 历史、任务、媒体、审计 DTO 提供 Local 属性 |
| UI 主题重构 | 🧪 | 内嵌页面，无伪 macOS 窗口按钮；跟随 Playnite 主题资源 |

完整缺陷编号和回归门禁见 `KNOWN_ISSUES.md`。

## 第一阶段：最小可用版本

| 功能 | 状态 | 备注 |
|---|---|---|
| Playnite 插件骨架 | 🧪 | PlayniteSDK 6.16.0 / net462 / GenericPlugin |
| Apple HIG 启发 UI | 🧪 | 0.2.0 重构主题资源、圆角卡片、弱边框、状态点、空状态与浅色/深色兼容；待视觉回归 |
| Worker 与命名管道 IPC | 🧪 | 当前用户管道、协议版本、消息上限、超时、错误返回和任务取消 |
| SQLite 状态存储与升级补列 | 🧪 | WAL；保存游戏、策略、会话、任务、历史、媒体、来源、候选与审计 |
| Ludusavi 路径配置/健康检查 | 🧪 | 运行设置持久化；启动/刷新重发；显示实际路径与版本，待重启回归 |
| 游戏列表与 Ludusavi 匹配状态 | 🧪 | Steam/GOG ID 优先，名称匹配兜底 |
| 手动备份单个游戏 | 🧪 | 首个 Simple 备份已真机成功；0.2.0 改为 ZIP 多版本并增强诊断，待连续版本回归 |
| 一键备份全部匹配游戏 | 🧪 | 长超时命令与逐游戏任务记录 |
| 退出后自动备份 | 🧪 | Playnite 事件与进程侦测会话均可触发 |
| 默认 30 分钟定时备份 | 🧪 | 每游戏可配置，最低 5 分钟 |
| 基础成功/失败反馈 | 🧪 | 管理面板轮询任务变化并显示 Playnite 通知；尚未实现 Worker 主动推送事件流 |
| 日志与审计页面 | 🧪 | 任务、异常、恢复状态机审计 |
| 外部进程/MOD 启动侦测 | 🧪 | Playnite Action、已知 EXE、MOD loader、重复会话去重 |
| Steam 截图增量同步 | 🧪 | Steam AppID 目录、SHA-256 去重、原质量归档 |
| Xbox/Game Bar 媒体同步 | 🚧 | 公共 Captures 目录和文件名匹配已实现；会话时间窗口归类待真机完善 |
| Epic/Ubisoft/EA/GOG 媒体来源 | 🧪 | 安装/Action 附近常见目录 + 每游戏自定义目录与匹配模式 |
| 误归类媒体修正 | 🧪 | UI 可把选中媒体重新归类到另一游戏 |

## 第二阶段：可靠性

| 功能 | 状态 | 备注 |
|---|---|---|
| 文件数量/大小/零字节校验 | 🧪 | Core 规则与 Worker finding 已实现 |
| 异常变化提醒 | 🧪 | 文件数骤降、体积骤降、长会话无变化等 |
| 云端上传状态 | 🚧 | 媒体状态和任务错误已实现；游戏级云端校验摘要仍可增强 |
| Rclone 安全单向复制 | 🧪 | 只调用 `copy`/`check`；不调用 `sync/delete/purge` |
| 每游戏策略 | 🧪 | 启停、定时、间隔、媒体、上传、分层保留参数 |
| 版本备注和锁定 | 🧪 | 调用 Ludusavi API 更新并刷新索引 |
| 智能历史版本保留 | 🧪 | 分层保留算法与 UI 预览；安全起见没有自动删除 |
| 媒体写入稳定性与哈希去重 | 🧪 | 原子复制、写入稳定检测、全局 SHA-256 去重 |
| 自定义媒体来源升级兼容 | 🧪 | `shared_directory` 自动补列 |

## 第三阶段：安全恢复

| 功能 | 状态 | 备注 |
|---|---|---|
| 历史版本浏览 | 🧪 | 复合主键、更新时间和刷新重载已修复；ZIP 多版本待真机验证 |
| 文件差异展示 | 🧪 | 对已索引 manifest 比较新增/删除/修改；旧版本无 manifest 时结果有限 |
| PreRestore 自动快照 | 🧪 | 恢复前强制创建、备注并锁定 |
| 恢复预览与确认 | 🧪 | UI 二次确认；自动恢复默认关闭 |
| 恢复后校验 | 🧪 | 再执行预览检查；需要真实 Ludusavi 输出验证 |
| 失败回滚 | 🧪 | 恢复失败后尝试恢复 PreRestore |
| 撤销恢复 | 🧪 | 选取最近 PreRestore，再走同一安全流程 |
| 云同步暂停语义 | 🚧 | 恢复流程不会主动调用云上传；真正的并发云任务暂停锁仍可增强 |

## 第四阶段：自动识别

| 功能 | 状态 | 备注 |
|---|---|---|
| 文件变化候选扫描 | 🚧 | 限定目录、深度和最近修改时间的候选扫描已实现；完整“启动前/退出后”差分快照仍未接入默认会话 |
| 候选路径评分 | ✅ | 可解释评分、缓存降权、会话末/WGS/重复模式加权算法及测试源码 |
| Xbox WGS 辅助识别 | 🧪 | 扫描 Packages/SystemAppData/wgs 候选；不承诺所有游戏可恢复 |
| Ludusavi 自定义规则草案 | 🧪 | 用户确认后只生成草案，不静默改动 Ludusavi 配置 |
| 多设备冲突检测 | 🚧 | 核心判定算法与测试源码已实现；Rclone 远端元数据清单摄取和 UI 尚未完成 |
| 未知游戏/MOD 启动链识别 | 🚧 | 已知进程映射和多进程退出去重已实现；人工“学习并保存新映射”的 UI 尚未完成 |
| 公共截图会话归类 | 🚧 | 名称归类已实现；基于游戏会话时间的高置信归类待 Windows 数据验证 |

## 交付判定

当前交付是**有完整 Git 历史、可继续开发、可在 Windows 构建的开发预览源码**，不是经过真实游戏存档恢复验证的生产安装包。禁止在完成 `WINDOWS_TEST_PLAN.md` 前把它用于唯一的重要存档副本。

## 2026-07-27 Windows 首次构建反馈

用户环境已安装 .NET SDK `9.0.302`，但旧版 `global.json` 锁定 `8.0.420`，导致 `restore/build/test/publish` 均未执行。旧脚本没有检查原生命令退出码，因此随后仍错误输出“构建完成”，并在打包阶段才以缺少 `GameSaveCenter.Playnite.dll` 暴露问题。

本修订已经：

- 将 SDK 选择改为以 .NET 8 为最低目标、允许滚动到更高稳定主版本；
- 对 `dotnet --info/restore/build/test/publish` 全部检查退出码；
- 构建失败时立即停止，禁止继续打包或开发安装；
- 增加公开仓库 Windows CI 工作流。

状态仍为“待 Windows 重新验证”，不能据此声明项目已经编译通过。


## 最近验证记录
- 2026-07-27：Windows + .NET SDK 9.0.302 已成功执行还原并编译到 Playnite 项目；修复 `IPlayniteAPI.CreateLogger` 与 PlayniteSDK 6.16.0 不兼容的问题，改用官方 `LogManager.GetLogger()`，并清理本轮构建暴露的空引用警告。

## 2026-07-27 Windows 真机验证进展

已验证：

- Playnite 成功加载插件，Worker 可通信。
- Playnite 游戏库与运行状态可同步到 GameSaveCenter。
- Ludusavi 0.31.0 可匹配 `Bongo Cat` 与自定义 `GameSaveCenter Test`。
- Worker 收到 `ludusaviExecutable` 后，两款游戏均进入 `Ready`。
- `GameSaveCenter Test` 手动备份成功，历史列表显示 1 个文件、11 字节。

已确认并待修复：

- Worker 重启后 Ludusavi 可执行文件路径丢失，设置尚未持久化。
- “刷新”尚未重发设置、重新导出游戏库和重新匹配。
- Worker 冷启动等待和残留进程处理不稳。
- UTC 时间尚未转换为本地时间。
- 深色主题文字对比度和按钮视觉需重构。

本次 `0.1.1` 修复：

- 选中游戏、备份版本、候选路径或媒体后，相关按钮会立即重新计算可用状态。
- 页面刷新后保留原选择；没有原选择时自动选择第一款游戏。

## 2026-07-27 XAML 构建检查补强

- [x] 修复任务状态 `DataTemplate.Triggers` 被错误嵌入 `StackPanel` 的 `MC3015`。
- [x] 新增构建前 XAML 结构检查，覆盖属性元素父级、TargetName 缺失和 Transform 名称作用域风险。
- [ ] 在 Windows 上重新执行 `scripts/build.ps1`，确认 Playnite 项目编译通过。

# GameSaveCenter

GameSaveCenter 是一个面向 Windows PC 游戏的本地优先存档与媒体管理系统，采用 **Playnite 插件 + 后台 Worker** 架构：

- **Playnite 插件**：统一 UI、游戏库、启动/退出事件、策略和安全恢复入口。
- **后台 Worker**：执行 Ludusavi、Rclone、进程侦测、截图/录像增量同步、校验和恢复编排。
- **Ludusavi**：作为存档扫描、版本备份和恢复引擎。
- **Rclone**：作为可选的云端单向复制与校验通道。

目标平台包括 Steam、Xbox PC / Game Pass、Epic、Ubisoft Connect、EA App、GOG，以及通过 MOD Organizer 2、SKSE、SMAPI、Mod Engine、Reloaded-II 等加载器启动的游戏。

> **当前状态：`0.6.35-development-preview`。** 大型 Playnite 游戏库同步先使用 SQLite 缓存并将大批量 Ludusavi 匹配移到 Worker 后台队列；未打开 GameSaveCenter 前不自动提交整库匹配，900+ 游戏库也不在 Playnite 启动阶段自动启动 Worker，打开面板或启动游戏后才按需启动；打开面板后会先显示缓存并等待空闲窗口再释放整库同步；后台匹配按已安装/最近游玩优先，并在 Playnite 启动后的 25 秒空闲窗口之后才开始，避免 900+ 游戏启动时长期占用进程和磁盘。大型库在 Dashboard 打开前也不启动任务通知长轮询，避免与独立 Ludusavi 集成争抢命名管道；打开工作区后才启用通知，并继续使用指数退避。Dashboard 首屏优先显示持久化缓存，打开侧栏时会加入已有同步任务，不再因点击插件重复排队全库刷新。Dashboard/Settings 页面构造失败时会返回隔离的诊断降级视图；Worker 健康探测会先等待忙碌实例恢复，不会因一次短暂超时误杀现有进程。六个工作区现在统一使用页面级纵向滚动，表格/ListBox 使用显式有限共享视口并保持虚拟化，矮窗口可以继续访问下方操作；Dashboard 外壳会统一转发所有工作区的响应式尺寸状态。

## 核心原则

1. 自动备份可以积极，自动恢复必须保守。
2. 恢复前始终创建并锁定 `PreRestore` 快照。
3. 截图/录像采用增量同步与 SHA-256 去重，不跟随每个存档版本重复打包。
4. 从 Playnite 启动时优先使用精确事件；从平台客户端、快捷方式或 MOD 管理器启动时由 Worker 进程侦测兜底。
5. 云端默认只使用 `rclone copy` 与 `rclone check`，不调用 `sync/delete/purge`。
6. 未确认的 Xbox WGS、未知存档路径和媒体归类只进入候选流程，不静默生效。
7. 所有恢复、回滚、任务和异常均写入本地审计记录。

## 已落地的源码范围

- Apple HIG 启发的 Playnite 侧边栏总览、主题自适应毛玻璃、可关闭微动效、游戏策略、备份历史、媒体、候选路径、任务与日志页面；
- Playnite 游戏库、平台 ID、安装路径和多个 Game Action/MOD 启动动作导出；
- Worker 命名管道 IPC、SQLite 状态库、任务队列、可取消任务和结构化日志；
- Ludusavi 单游戏/全部/定时/退出备份、历史索引、备注、锁定、恢复与回滚编排；
- 外部进程与多进程 MOD 启动链识别基础；
- Steam、Xbox/Game Bar、Windows 公共目录和自定义来源的截图/录像增量同步；
- 公共媒体共享目录单次扫描、保守自动归类、待归类收件箱、人工移动归类与“忽略但保留副本”；
- 文件数量、大小、零字节、异常下降、长时间无变化等校验；
- 分层历史保留预览、候选存档路径评分、Xbox WGS 辅助扫描；
- Rclone 单向复制与校验适配；
- 管理面板自动刷新、Playnite 任务通知、任务进度详情和可复制诊断中心；
- 未匹配游戏会话的启动前/退出后文件快照、候选持久化、判断依据、接受/忽略流程；
- 失败或取消的备份/媒体任务可复制完整详情，并在明确支持的任务类型上执行安全重试；
- Core、Worker SQLite 与 Playnite 设置迁移测试，以及跨平台源码结构校验。

部分高级闭环仍需 Windows 真机和真实数据继续完善，详见进度表。

当前源码与真机完成度的分层评估见 [`docs/FEATURE_COMPLETION_ASSESSMENT.md`](docs/FEATURE_COMPLETION_ASSESSMENT.md)。

## 仓库结构

```text
src/GameSaveCenter.Playnite   Playnite 10 插件（.NET Framework 4.6.2 / WPF）
src/GameSaveCenter.Worker     Windows 后台助手（.NET 8 / win-x64）
src/GameSaveCenter.Core       领域逻辑与安全算法
src/GameSaveCenter.Contracts  插件与 Worker 的版本化 IPC 契约
tests/                        xUnit 测试源码
scripts/                      Windows 构建、验证、打包和开发安装脚本
docs/                         需求、架构、进度、限制、安全和交接文档
docs/design/                  Apple HIG 启发的 UI 规范
```

## 一键开发安装

Windows 上可直接双击仓库根目录：

```text
GameSaveCenter-一键构建安装运行.cmd
```

脚本会自动关闭 Playnite/Worker、清理、构建、测试、打包、替换实际扩展目录、核验版本并重新启动 Playnite。安装报告保存到 `artifacts/last-dev-install.txt`。

## Windows 构建

建议环境：

- Windows 10/11 x64；
- Playnite 10.56 或兼容的 Playnite 10 稳定版；
- .NET 8 或更高版本的稳定版 SDK；已验证配置允许使用 .NET 9 SDK 构建 .NET 8 目标；
- .NET Framework 4.6.2 Developer Pack；
- Ludusavi 最新稳定版；
- Rclone 最新稳定版（仅云端复制需要）。

在 PowerShell 中执行：

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./scripts/build.ps1 -Configuration Release
./scripts/package.ps1 -Configuration Release
./scripts/install-dev.ps1 -PlayniteExtensionsPath "$env:APPDATA\Playnite\Extensions"
```

完整步骤见 [`docs/INSTALLATION.md`](docs/INSTALLATION.md)，真机验证门禁见 [`docs/WINDOWS_TEST_PLAN.md`](docs/WINDOWS_TEST_PLAN.md)。

## 源码静态校验

在有 Python 3 的环境执行：

```bash
python scripts/validate-source.py
```

该检查不能替代真实的 `dotnet restore/build/test` 和 Playnite 加载测试。

## Git 与继续开发

仓库从初始化开始按阶段提交。继续开发前请依次阅读：

1. [`docs/PROJECT_MEMORY.md`](docs/PROJECT_MEMORY.md)
2. [`docs/DEVELOPMENT_PROGRESS.md`](docs/DEVELOPMENT_PROGRESS.md)
3. [`docs/REQUIREMENTS.md`](docs/REQUIREMENTS.md)
4. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
5. [`docs/CODEX_CONTINUATION_PROMPT.md`](docs/CODEX_CONTINUATION_PROMPT.md)
6. [`docs/PUBLIC_REPOSITORY.md`](docs/PUBLIC_REPOSITORY.md)
7. [`docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`](docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md)
8. [`docs/design/UI_CHANGE_GATE.md`](docs/design/UI_CHANGE_GATE.md)
9. [`docs/FEATURE_COMPLETION_ASSESSMENT.md`](docs/FEATURE_COMPLETION_ASSESSMENT.md)

公开仓库默认使用英文笔名维护者 **Sable Drift**；Git 提交说明统一使用中文。

源码交付包包含完整 `.git`，但不会包含真实存档、截图、运行数据库、日志或凭据。

## 安全声明

在完成 Windows 真机恢复测试前，本项目应视为**开发预览版**。不要将它作为重要存档的唯一副本，不要对 Xbox WGS 或唯一存档执行首次恢复测试，也不要启用自动恢复或双向云同步。

# GameSaveCenter Codex 完整开发交接

更新时间：2026-07-31
交接版本：`0.6.22-development-preview`
仓库：`https://github.com/Nikilua/GameSaveCenter.git`
主分支：`main`
插件 ID：`66e9f2d7-67bb-43ef-b62a-b8e60734fcec`
Git 作者：`Sable Drift`
提交说明：使用中文

这份文件用于把此前多个 ChatGPT/Codex 会话中的关键上下文完整转移到另一台电脑。它不是逐字聊天导出，而是可执行的工程交接：包含用户目标、架构、版本演进、已完成能力、真实验证、安全边界、剩余工作和继续开发指令。下一位 Codex 不应依赖旧聊天记录，必须以本文件和仓库当前源码为准。

### 0.6.20 跨线程崩溃交接

- 2026-07-31 的真实 `extensions.log` 显示 `DashboardView.OnViewModelPropertyChanged` 在 Worker 任务刷新后从非 UI 线程访问 `IsLoaded`，抛出 WPF `InvalidOperationException` 并使 Playnite 插件崩溃。
- `OnViewModelPropertyChanged` 的首个控件相关操作必须是 `Dispatcher.CheckAccess()`；后台回调只重新投递自身，之后才允许读取 `IsLoaded` 或 ViewModel 属性、安排动画。
- `RequestBackgroundRefreshAsync` 和 `RefreshAfterSynchronizationAsync` 必须保持为 `Task`，禁止恢复为 `async void`。定时器和任务事件均须等待刷新任务，绑定状态必须经 `ApplyOnUi` 写入。
- 必须在真实 Playnite 中保持面板打开，循环完成/取消任务、慢 Worker 和卸载重开面板，确认 `extensions.log` 无跨线程异常。

### 0.6.22 共享主题令牌交接

- Dashboard 与设置页禁止重新添加页面级硬编码颜色；所有语义材质、环境光、图标底色、安全提示、主按钮和阴影均使用 `Themes/DesignTokens.xaml`。
- 页面环境光不是实时窗口模糊：保持少量静态 Ellipse，且高对比度或关闭玻璃时必须隐藏；禁止对列表/表格添加 BlurEffect。

### 0.6.21 云端重试与 UI 交接

- 云端 `rclone copy` 失败不会重跑 Ludusavi 本地备份：只会在 `cloud_retry_queue` 中排队安全单向 copy，并跨 Worker 重启恢复。
- 退避顺序为 1、5、15、60、240、720 分钟，最多六次自动尝试；成功清队列，耗尽后审计并停止。Rclone 或备份目录未就绪时必须暂停，禁止任务风暴。
- `GscNumericTextBox` 与 `IntegerRangeValidationRule` 用于所有整数设置；完整输入后失焦提交。不要恢复 58 DIP 策略框或逐字符整数绑定。

## 1. 用户的最终目标

GameSaveCenter 是 Playnite 10 的一体化游戏管理插件，目标包括：

- 游戏存档识别、备份、版本历史、安全恢复与校验；
- 截图和录像归档、去重、归类、媒体收件箱与云端复制；
- 每游戏多个修改器、多个 Cheat Table、本地导入、启动策略；
- 完全位于插件内的 FLiNG 目录搜索、版本选择、下载、解压和绑定，不打开网页、不使用 GCM；
- 任务、日志、诊断、Worker、Ludusavi、Rclone 和多设备状态；
- Apple-inspired 原生 WPF UI，兼容普通窗口、窄窗口、高 DPI、深浅主题；
- 大型 Playnite 游戏库可用。用户实际约有 1000 款游戏，不能每次打开插件都全量重新匹配或扫描。

用户明确允许持续开发、提交并推送到 `origin/main`，不要求每完成一个小功能就停下来确认。但这不授权静默删除存档、覆盖远端数据、关闭安全软件或绕过反作弊。

## 2. 技术架构

```text
Playnite 10
└─ GameSaveCenter.Playnite
   ├─ .NET Framework 4.6.2
   ├─ 原生 WPF / XAML UserControl
   ├─ Playnite 游戏库、生命周期与设置入口
   └─ 命名管道 IPC 客户端
              │
              │ GameSaveCenter.Worker.v1 / JSON
              ▼
GameSaveCenter.Worker
├─ .NET 8 Windows
├─ 命名管道 IPC 服务端
├─ SQLite 持久化
├─ 备份、恢复、媒体、修改器与任务编排
├─ Ludusavi 外部进程适配
└─ Rclone 单向安全适配
```

工程：

- `GameSaveCenter.Contracts`：IPC DTO、消息类型和枚举；
- `GameSaveCenter.Core`：纯算法，包括保留策略、冲突检测、候选评分；
- `GameSaveCenter.Worker`：文件、SQLite、进程、Ludusavi、Rclone 和后台任务；
- `GameSaveCenter.Playnite`：Playnite SDK、WPF UI、ViewModel 和用户交互；
- `GameSaveCenter.Core.Tests`：xUnit 测试。

关键运行时数据：

- SQLite：`%LOCALAPPDATA%\GameSaveCenter\gamesavecenter.db`
- Worker 设置：`%LOCALAPPDATA%\GameSaveCenter\worker-settings.json`
- Worker 日志：`%LOCALAPPDATA%\GameSaveCenter\Logs\worker-launch.log`

## 3. 绝对不能破坏的约束

### 存档和云端

- Playnite 是唯一主要 UI，不改成 HTML、WebView、Electron、Avalonia、WinUI 3 或 MAUI。
- Ludusavi 继续作为存档引擎，不自行重写存档覆盖语义。
- 自动恢复默认关闭。
- 恢复前必须创建并锁定 `PreRestore`。
- 恢复必须确认游戏已关闭，并等待正在进行的云端上传。
- Rclone 只允许安全单向操作。禁止引入 `sync`、`delete`、`purge`、`move`。
- 多设备冲突只比较摘要，不自动选择赢家、不自动下载恢复、不覆盖任一设备。
- 分层保留目前只给预览，不自动删除历史。这是刻意的安全边界。

### 修改器

- 一个游戏支持多个修改器、多个 CT 和多个版本。
- 每项工具独立启用、独立决定是否随游戏启动。
- 自动启动默认关闭。
- 不提供反作弊绕过。
- 不关闭 Defender，不创建 Defender 排除项。
- 只结束本次会话由 Worker 真实启动且记录了 PID 的工具。
- FLiNG UI 必须在插件内部完成搜索和下载，不打开网页。
- 可以参考 Game Cheats Manager 的产品流程，但不能复制或链接其 GPL 源码。

### UI

- 必读：
  - `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`
  - `docs/design/APPLE_UI_GUIDE.md`
  - `docs/design/UI_CHANGE_GATE.md`
- 不伪造 macOS 红黄绿宿主窗口按钮。
- 毛玻璃只用于侧栏和少量浮层；文字、大型列表和每一行禁止实时模糊。
- 颜色来自 `DynamicResource` 和共享设计令牌。
- 不使用 Emoji 作为正式图标。
- 动画优先 `Opacity`、`TranslateTransform`、`ScaleTransform`；禁止用 Width、Height、Margin、GridLength 做高频动画。
- 任何动画 Transform 必须是元素独占且未冻结；如果 `IsFrozen`，先 `CloneCurrentValue()`。
- 保留键盘焦点、Tooltip、长文本省略、100%–200% DPI 和关闭动画/透明降级。

### Git 和交付

- 不修改插件 ID。
- 不 squash 现有历史。
- 不使用 `git reset --hard` 清理用户工作。
- 每批功能更新项目记忆、进度、已知限制、发布说明和 Windows 测试计划。
- 构建失败时禁止继续安装旧 DLL 并报告成功。
- 推送前必须通过源码门禁、Release 编译、测试、打包和版本一致性检查。

## 4. 重要历史与问题演进

### 0.1–0.4

- 建立 Playnite 插件、Worker、SQLite、命名管道和 Ludusavi 基础闭环。
- 完成游戏同步、匹配、备份、历史、任务、媒体和设置。
- Windows 真机确认 Playnite 能加载插件，Ludusavi 能匹配测试游戏并产生 ZIP。
- 修复设置中 Worker/Ludusavi 路径混淆。
- 修复 WPF `ResourceDictionary.MergedDictionaries` 的 `MC3074`。
- 修复缺失 `GscStatusPill` 导致的运行时 `XamlParseException`。
- 修复共享冻结 `TranslateTransform` 动画导致的崩溃。
- 完成全局媒体收件箱、保守归类、人工归类、忽略保留副本和旧数据库升级。

### UI 重构

用户反馈旧界面在普通窗口下非常拥挤，缩小时“存档历史”消失、ComboBox 出现系统白色、页签裁剪、按钮重叠。随后完成：

- 一级工作区：首页、存档中心、修改器中心、媒体中心、任务中心、维护中心；
- 左侧导航不再和右侧详情页签索引双向耦合；
- Wide/Medium/Compact/Narrow 自适应；
- Compact 下隐藏固定游戏列表，使用当前游戏选择器；
- Apple-inspired 设计令牌、按钮、输入框、ComboBox、DataGrid、ScrollBar、页签和微动效；
- 多主题和关闭透明降级；
- 页签右侧圆角、滚动条、搜索框、表头和表格对齐修复。

### 大型游戏库性能

用户约有 1000 款游戏，曾感觉每次打开插件都像重新扫描。代码审查确认数据本来就持久化，但启动逻辑未充分利用缓存：

- 每次启动对所有游戏顺序执行 Ludusavi `find`；
- 首次打开 Dashboard 又重复全量同步；
- Dashboard 对每游戏读取备份、媒体和策略，形成 N+1；
- 打开游戏历史强制执行 Ludusavi `backups`；
- 每 10 秒重建完整首页。

已完成优化：

- 游戏描述哈希、匹配时间和匹配缓存；
- 同步任务复用、短时间冷却和事件合并；
- Dashboard 缓存优先；
- SQLite 批量聚合摘要；
- 历史缓存优先，只有显式刷新或缓存为空才对账；
- 工作区按需加载；
- 任务增量变化而不是高频完整首页轮询；
- Worker 信号唤醒的有界长轮询，SQLite 快照作为重启兜底。

不要回退为每次打开全部重新匹配、扫描或读取完整媒体历史。

### 0.5 修改器中心

- SQLite `game_tools`、`game_tool_versions`；
- 多修改器、多 CT、多版本；
- EXE、ZIP、目录、CT 导入；
- Zip Slip、条目数、单文件和总展开体积限制；
- SHA-256、文件缺失、打开目录、解绑保留文件；
- 每项自动启动、延迟、管理员权限、退出关闭；
- PID 跟踪和反作弊默认阻止；
- FLiNG 本地目录缓存、插件内搜索、版本列表、下载、安全解压和自动绑定；
- 多 EXE 包必须由用户显式选择主程序；
- Inspector 可切换活动版本。

### 0.6.1–0.6.5

- 首页“需要关注”卡片变成可操作入口，点击进入具体 Warning/Error，显示原因和建议。
- 恢复增加活跃会话与 PID 检查、云传输闸门。
- FLiNG 最大下载 2 GiB，压缩包有严格上限。
- 多设备生成不含路径、内容和凭据的 sidecar，Rclone 只用 `copy/lsf/cat`。
- 未知 EXE/MOD Loader 人工映射学习与删除。
- 游戏级云端状态持久化。
- Rclone 失败可只重试云端上传，不重复本地备份。
- 任务变化使用信号唤醒长轮询。

### 0.6.6

- 设置 JSON 导出、导入；
- `SchemaVersion=1`；
- 文件大小、枚举和数值范围校验；
- 新机器缺失 Worker/Ludusavi/Rclone/存档和媒体目录迁移报告；
- 导入只改变设置编辑副本，点击 Playnite 保存才应用；
- 媒体总量、截图、录像、收藏和空间占用 SQLite 聚合；
- 媒体收藏、备注、系统默认打开和资源管理器定位；
- 元数据操作不移动、不删除归档文件。

### 0.6.7

- 当前游戏媒体按文件名、备注和来源即时搜索；
- 全部、截图、录像、收藏筛选；
- 只为当前选中截图生成最大宽度 480 像素预览；
- `BitmapCacheOption.OnLoad` 后释放文件句柄，图像 `Freeze()`；
- 不支持格式和录像继续用系统默认程序打开。

## 5. 当前主要代码入口

### 插件和 UI

- `src/GameSaveCenter.Playnite/GameSaveCenterPlugin.cs`
- `src/GameSaveCenter.Playnite/Views/DashboardView.xaml`
- `src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs`
- `src/GameSaveCenter.Playnite/ViewModels/DashboardViewModel.cs`
- `src/GameSaveCenter.Playnite/Themes/DesignTokens.xaml`
- `src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettings.cs`
- `src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml`
- `src/GameSaveCenter.Playnite/Converters/MediaThumbnailConverter.cs`

### Worker

- `src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs`
- `src/GameSaveCenter.Worker/Persistence/SqliteStateStore.cs`
- `src/GameSaveCenter.Worker/Services/BackupOrchestrator.cs`
- `src/GameSaveCenter.Worker/Services/RestoreOrchestrator.cs`
- `src/GameSaveCenter.Worker/Services/MediaSyncService.cs`
- `src/GameSaveCenter.Worker/Services/GameToolService.cs`
- `src/GameSaveCenter.Worker/Services/FlingTrainerCatalogSource.cs`
- `src/GameSaveCenter.Worker/Services/DeviceStateService.cs`
- `src/GameSaveCenter.Worker/Services/TaskCoordinator.cs`
- `src/GameSaveCenter.Worker/Infrastructure/LudusaviClient.cs`
- `src/GameSaveCenter.Worker/Infrastructure/RcloneClient.cs`

### 契约和测试

- `src/GameSaveCenter.Contracts/MessageTypes.cs`
- `src/GameSaveCenter.Contracts/DashboardDtos.cs`
- `src/GameSaveCenter.Contracts/OperationDtos.cs`
- `src/GameSaveCenter.Contracts/GameToolDtos.cs`
- `src/GameSaveCenter.Contracts/DeviceStateDtos.cs`
- `tests/GameSaveCenter.Core.Tests`
- `scripts/validate-source.py`

## 6. 已确认的真实验证与当前证据

历史上用户真机确认：

- Playnite 能加载插件；
- 版本能在附加组件页正确显示；
- Worker 可通信；
- 游戏库和运行状态能同步；
- Ludusavi 0.31.0 能匹配测试游戏；
- 手动备份能产生 ZIP 并显示历史；
- 0.4.2 后侧栏可打开；
- 当前 Git 构建、安装和 push 链路可用。

最近自动验证通常包括：

```powershell
python scripts/validate-source.py
dotnet build GameSaveCenter.sln -c Release --no-restore
dotnet test tests\GameSaveCenter.Core.Tests\GameSaveCenter.Core.Tests.csproj -c Release --no-build --no-restore
git diff --check
git fsck --full
.\scripts\package.ps1 -Configuration Release -SkipBuild
```

0.6.6 已验证：

- Release 编译 0 错误；
- 13/13 Core 测试通过；
- 源码/XAML/IPC/版本门禁通过；
- PEXT 和 ZIP 打包成功；
- 提交 `8994213 功能：完善设置迁移与媒体管理` 已推送。

0.6.10 新增独立 Playnite 设置迁移测试；一键构建现在运行 Core、Worker SQLite 和 Playnite 设置三组测试。

`NU1900` 通常表示无法读取 NuGet 漏洞数据端点，不等于编译失败；仍需观察是否出现真正 restore/package 错误。

## 7. 仍未完成或仍需验证

必须区分“源码没有实现”和“源码已实现但未真机证明”。

### 源码仍未完整实现

1. 多设备远端备份下载和人工冲突解决向导。当前只有只读摘要比较。
2. 保留策略候选的安全清理执行。当前只预览，不删除。
3. Playnite Add-ons 数据库正式发布：0.6.13 已准备 installer/add-on 清单，仍需仓库所有者创建 Release、上传 PEXT 并向官方数据库发 PR。

### 0.6.13 远端备份恢复交接

- 远端目录结构已经由上传实现确认：`<设备名>/Saves` 保存该设备的完整 Ludusavi 备份库。
- `RemoteBackupStagingService` 将所选设备的完整库下载到 `DataDirectory/RemoteBackups/<opaque-id>/Vault`，下载与哈希检查持有同一云传输锁。
- 暂存只有在 Rclone 检查通过、且 Ludusavi 从隔离路径列出所选 Backup ID 后才写入 manifest；句柄七天过期。
- `RestoreOrchestrator.ExecuteRemoteAsync` 从隔离库读取目标版本，但 PreRestore 和失败回滚始终使用本机正式备份库。
- 设备冲突决策依然只记录判断；下载和恢复是两个独立按钮及两次确认，禁止自动执行。
- 仍需两个真实设备/Rclone 后端及低风险游戏的端到端验证，不得以 Release 编译和路径单测代替。
4. `DashboardView.xaml`、`DashboardViewModel.cs` 和 `SqliteStateStore.cs` 的进一步模块拆分。0.6.11 已先拆出 Dashboard 与 SQLite 媒体域。

### 已实现但必须真机回归

1. 使用可丢弃存档完整验证 PreRestore、恢复、失败回滚和撤销恢复。
2. Rclone `copy/check`、断网重试、取消和恢复闸门。
3. 1000 款游戏的冷启动、首次打开、搜索和长时间运行。
4. 公共媒体时间归类、200 项分批收件箱和超大目录性能。
5. FLiNG 在线目录页面变化、真实下载、多 EXE、安全软件隔离。
6. 自动启动修改器、多个 CT、提权、退出关闭和 MOD Loader。
7. 100%/125%/150%/175%/200% DPI、多套 Playnite 主题、关闭透明和关闭动画。
8. 0.6.6 设置迁移的 Playnite 宿主流程、0.6.7 媒体搜索和截图预览。

### 不应“为了完成率”直接实现

- 自动删除存档历史；
- 自动选择某台设备覆盖另一台设备；
- 自动双向云同步；
- 自动恢复；
- 静默下载安装或运行修改器；
- 自动 Defender 白名单；
- 反作弊绕过。

这些功能如果继续开发，必须提供清晰预览、确认、回收或回滚策略，并用可丢弃数据验证。

## 8. 下一位 Codex 的推荐顺序

1. `git pull origin main`，确认工作区干净和版本一致。
2. 完整阅读本文件及：
   - `docs/PROJECT_MEMORY.md`
   - `docs/DEVELOPMENT_PROGRESS.md`
   - `docs/FEATURE_COMPLETION_ASSESSMENT.md`
   - `docs/IMPLEMENTATION_LIMITATIONS.md`
   - `docs/KNOWN_ISSUES.md`
   - `docs/WINDOWS_TEST_PLAN.md`
   - 三份 UI 规范。
3. 先运行 Release 构建、35 项测试和源码门禁。
4. 优先协助用户完成 0.6.1–0.6.19 Windows 真机回归；发现真实错误时停止扩功能，先修复。
5. 若继续安全源码开发，推荐：
   - 媒体、远端恢复与任务事件链路的真实 Windows/Rclone 回归；
   - 多设备冲突的人工决策、隔离下载与受保护恢复，但不要自动选择或覆盖；
   - 将 Dashboard 按工作区逐步拆分；
   - SQLite 集成测试和设置迁移测试已完成；继续扩展高风险路径的隔离测试。
6. 删除/覆盖类能力最后处理，并先设计回收站或显式恢复路径。

## 9. 可直接粘贴给新 Codex 的提示词

```text
你正在继续开发 GameSaveCenter。请先完整阅读：

- docs/CODEX_FULL_HANDOFF.md
- docs/PROJECT_MEMORY.md
- docs/DEVELOPMENT_PROGRESS.md
- docs/FEATURE_COMPLETION_ASSESSMENT.md
- docs/IMPLEMENTATION_LIMITATIONS.md
- docs/KNOWN_ISSUES.md
- docs/WINDOWS_TEST_PLAN.md
- docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md
- docs/design/APPLE_UI_GUIDE.md
- docs/design/UI_CHANGE_GATE.md

然后检查 git status、git log、Directory.Build.props、extension.yaml 和实际源码。
不要依赖旧聊天，不要假设文档中的功能一定存在。

保持 Playnite + 原生 WPF + Worker + SQLite + Ludusavi + 可选 Rclone 架构。
不要修改插件 ID，不引入 WebView，不复制 GCM GPL 源码，不实现反作弊绕过。
Rclone 禁止 sync/delete/purge/move；恢复前必须 PreRestore；多设备不得自动覆盖。

先运行：

python scripts/validate-source.py
dotnet build GameSaveCenter.sln -c Release --no-restore
dotnet test tests\GameSaveCenter.Core.Tests\GameSaveCenter.Core.Tests.csproj -c Release --no-build --no-restore

如果用户提供真机错误，优先修复错误；否则按交接文件的剩余优先级连续开发，
完成一批后更新项目文档、版本、测试、打包，使用中文提交并推送 origin/main。
不要完成一个微小改动就停下来询问。
```

## 10. 获取完整 Git 历史

本仓库保存了完整提交历史。新机器执行：

```powershell
git clone https://github.com/Nikilua/GameSaveCenter.git
cd GameSaveCenter
git log --oneline --decorate --graph --all
```

聊天中没有进入 Git 的临时表述不应覆盖仓库事实。本文件已经保留影响设计和开发决策的全部关键上下文；更细的每版本证据见 `docs/RELEASE_NOTES.md` 和 `docs/DEVELOPMENT_PROGRESS.md`。

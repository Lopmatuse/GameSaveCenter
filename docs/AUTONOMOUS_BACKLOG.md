# 无人值守开发积压清单

本清单是无人值守开发代理选择单一工作项的唯一来源。任务状态只能使用：`PROPOSED`、`READY`、`IN_PROGRESS`、`IMPLEMENTED`、`BLOCKED_ENVIRONMENT`、`BLOCKED_USER_DECISION`。

## PROPOSED

### GOV-002：校准积压清单与交付证据状态

- **优先级**：P1
- **状态**：IMPLEMENTED
- **发现日期**：2026-08-01
- **根因与影响**：本轮只读审计发现积压清单仍将 DOC-001 标记为 `PROPOSED`，但 `792186f` 已完成该文档一致性修正；部分 UI 条目的自动化测试数量也停留在早期计数。状态和证据漂移会使无人值守代理错误判断可领取任务、重复实现已完成工作，或把源码自动化误当作真机验证。
- **建议范围**：逐项核对积压条目与提交、`PROJECT_MEMORY.md`、`DEVELOPMENT_PROGRESS.md`、`KNOWN_ISSUES.md` 和 `WINDOWS_TEST_PLAN.md`；仅校准任务状态、当前证据、依赖和阻塞条件，保留可追溯的历史证据；明确 `ENV-001` 是真实 Playnite 回归的唯一环境前置。
- **非目标**：不修改插件、Worker、XAML、版本、安装包、用户数据或 Git 历史；不因文档校准而解除 `BLOCKED_ENVIRONMENT`，也不把自动化结果写成真机完成。
- **验收标准**：每个条目均有唯一且当前的状态；已实现项目不再保持 `PROPOSED`；测试计数与最新可复查结果一致；所有真实 Playnite/DPI/主题/云端/恢复缺口仍明确指向隔离环境或相应测试计划。

### GOV-001：补齐无人值守开发治理文档

- **优先级**：P0
- **状态**：IMPLEMENTED
- **发现日期**：2026-07-31
- **根因与影响**：仓库缺少 `docs/AUTONOMOUS_DEVELOPMENT_RULES.md`、`docs/QUALITY_GATES.md`，且此前没有此积压清单。虽然现有 `AGENTS.md`、设计门禁和 Windows 测试计划包含部分约束，但代理无法从单一来源确定 READY 工作项、状态流转与最低验收证据，容易导致无人值守运行越过范围或重复领取工作。
- **建议范围**：建立两份缺失的治理文档；将任务状态、领取规则、最小验证集、环境阻塞记录格式和提交要求集中化；引用既有 `AGENTS.md`、`WINDOWS_TEST_PLAN.md` 与 UI 门禁，避免复制并漂移安全约束；把当前仍需真实 Windows/Playnite 验证的事项按优先级拆分为后续候选任务。
- **非目标**：不修改插件业务行为、WPF/XAML、版本、安装脚本或发布流程；不把需要真实设备、Rclone 或 Playnite 证据的事项标记为已验证。
- **验收标准**：三份治理文档均存在且互相链接；每个可领取条目有唯一 ID、优先级、状态、范围、验收证据和阻塞条件；至少一个经过审查的非破坏性工作项可标记为 `READY`。

### UI-001：WPF-UI 4.3.0 兼容性 POC 与迁移基线

- **优先级**：P0
- **状态**：BLOCKED_ENVIRONMENT
- **前置条件**：GOV-001 已完成；当前工作树干净；不得修改 Playnite 宿主 Chrome、插件 ID、业务命令或 Worker。
- **根因与影响**：现有 Apple-inspired WPF 样式虽已覆盖基础主题与控件，但尚未验证用户指定的 WPF-UI 4.3.0 是否能在 net462 / Playnite 10 宿主中进行资源隔离且不污染宿主。未经 POC 直接全量替换会有资源键冲突、程序集加载和 XAML 解析崩溃风险。
- **范围**：核验目标框架、WPF-UI 许可/依赖与 Playnite 兼容性；以作用域资源字典完成最小可编译 POC；保留现有 DesignTokens、动态主题、虚拟化与命令绑定；明确回退策略。只在满足门禁后创建后续迁移任务。
- **非目标**：不替换业务层；不更改版本或安装流程；不改变真实存档、数据库、云端或用户配置；不声明完成全量 UI 迁移。
- **验收标准**：Release 构建与全量单元测试通过；`validate-source.py`、XAML/UI 静态检查、`git diff --check` 和 `git fsck --full` 通过；Playnite 加载 POC 后不存在新增资源/绑定/跨线程异常；WPF-UI 资源不能全局污染宿主。
- **阻塞条件**：WPF-UI 或其依赖不兼容 net462/Playnite，或 POC 无法保证资源隔离时，必须记录 `BLOCKED_ENVIRONMENT` 并继续使用现有共享令牌方案。
- **当前证据**：2026-08-01 已确认 NuGet 的 WPF-UI 4.3.0 含 net462 程序集；插件以局部 `UserControl.Resources` 合并主题与控件字典，Release 构建、50 个单元测试、源码/XAML 门禁及打包通过，PEXT 含 Wpf.Ui、Abstractions 与 net462 运行依赖。Dashboard 不在 XAML 解析时创建探针；维护中心的显式加载入口会在构造/资源解析失败时保留页面与重试入口。现有 Playnite/Worker 是用户正在使用的实例；未获隔离实例授权前不得关闭进程或覆盖其插件目录，故实际加载、Dialog/Snackbar 交互、主题/DPI 与宿主污染验证尚未执行。

### ENV-001：可审计的隔离 Playnite 真机测试环境

- **优先级**：P0
- **状态**：PROPOSED
- **前置条件**：不得停止、关闭或覆盖用户正在使用的 Playnite/Worker；不得把 `.tmp/` 或测试库纳入 Git。
- **根因与影响**：当前工作区的 `.tmp/playnite-ui-test/Playnite` 仅含 `DatabasePath: library` 和独立扩展文件，尚不能以可审计证据证明 Playnite 会使用独立配置、扩展数据根和单独进程，而非复用用户 `%AppData%\\Playnite` 或向现有单实例转发。启动它可能影响用户实例，因而不能作为 UI-001 真机验收环境。
- **范围**：只建立并验证一个独立 Playnite 数据根、空测试库、独立扩展目录和可记录的启动 PID 边界；证明不读取/写入用户 AppData、现有库或现有扩展目录后，才允许执行后续 UI/功能 smoke test。
- **非目标**：不启动现有用户 Playnite；不删除用户数据；不安装到用户扩展目录；不执行备份、恢复、云端镜像或媒体删除。
- **验收标准**：启动方式与官方 portable/独立数据机制有可复查依据；启动前后均能记录测试 PID 与数据根；用户实例 PID/路径未变；测试扩展与 Worker（如启动）均从测试目录加载；测试后日志能证明无用户 AppData/库写入。
- **阻塞条件**：无法证明 Playnite 数据隔离或无法获得安全的独立启动方式时，保持 `BLOCKED_ENVIRONMENT`，不得以 `--hidesplashscreen`、复制安装目录或强制结束进程代替证据。

### DOC-001：完成度评估版本与证据一致性复核

- **优先级**：P2
- **状态**：IMPLEMENTED
- **前置条件**：不得提升程序集、插件清单或正式版本；必须以当前提交中的可复查证据更新评估。
- **根因与影响**：`docs/FEATURE_COMPLETION_ASSESSMENT.md` 仍标记 `0.6.20-development-preview`，而当前程序集、插件清单、`DEVELOPMENT_PROGRESS.md` 和 `PROJECT_MEMORY.md` 已记录 `0.6.22`。版本漂移会使完成度、真机验证范围和交接判断失真。
- **范围**：核对版本来源、当前源码覆盖和真实 Windows/Playnite 证据；只更新评估文档的版本、日期、百分比说明和未验证边界，不能把静态检查写成真机结论。
- **非目标**：不修改产品版本、程序集、`extension.yaml`、发布流程或功能实现。
- **验收标准**：评估版本与现有清单/程序集一致；每个百分比有可追踪来源；明确区分 0.6.22 已通过的自动化测试与仍被隔离环境阻塞的 UI/真实云端/恢复回归。

### UI-002：共享设计令牌与控件模板全量复审

- **优先级**：P0
- **状态**：IMPLEMENTED
- **前置条件**：UI-001 结论已记录；若 POC 不兼容，则使用现有资源体系的等价实现。
- **范围**：统一 Button、TextBox、数值输入、ComboBox、CheckBox、ScrollBar、TabControl、DataGrid、ListBox、Popup、Tooltip、Toast 和焦点环；补齐浅/深/跟随 Playnite、高对比度、关闭透明和关闭动画的安全降级。
- **验收标准**：所有共享控件 Normal/Hover/Pressed/Disabled/Focus 状态可用；大列表保持 Recycling 虚拟化；不出现页面级硬编码主题色、默认宿主白色控件、冻结 Transform 动画或大型区域 BlurEffect。
- **当前证据**：2026-08-01 已将设置卡片收敛至共享 `GscSurface`；共享令牌覆盖主/普通按钮、数值/文本输入、ComboBox、CheckBox、Slider、滚动条、Tooltip、ProgressBar、焦点环和进度不确定状态。新增 `validate-source.py` 共享控件门禁并实际执行。Release 构建为 0 警告/0 错误，Core 13、Worker 21、Playnite 16 项测试通过；打包产生 0.6.22 ZIP/PEXT，Worker smoke 为 0.6.22.0。UI Skill 静态审查为 0 errors（项目现有的布局提示和未跟踪 `.tmp/` 中的宿主主题提示未隐藏）。

### UI-003：Dashboard 与 Settings 自适应布局、可访问性及真机视觉回归

- **优先级**：P0
- **状态**：BLOCKED_ENVIRONMENT
- **前置条件**：UI-002 已实现并通过自动化门禁。
- **范围**：修复所有工作区的裁切、长文本 Tooltip、窄窗口重排、数值输入完整编辑、表格/页签/弹层对齐与真实状态反馈；在隔离目录验证 100%–200% DPI、1600×900 至 980×640、键盘导航、主题及减少动画。
- **验收标准**：真实 Playnite 加载、页面打开关闭与缩放均无 XAML/绑定/Dispatcher 异常；所有真机未覆盖条件在 Windows 测试计划中明确记录，不伪造完成。
- **当前证据与阻塞**：2026-08-01 已完成源代码层修复：侧栏导航获得有限高度内的键盘可达滚动；紧凑模式把顶部操作压缩为带 Automation Name/Tooltip 的图标；Settings 加入横向访问通道、窄屏边距/副标题降级、滑块 Automation Name；环境光与焦点环不再用负 Margin。源码门禁、Release 构建（0 警告/错误）、50 项测试与 UI Skill 审查（0 errors）通过。任务仍要求真实 Playnite 的加载/主题/DPI/键盘验证；`.tmp/playnite-ui-test` 不能证明独立 AppData/单实例边界，且用户实例正在运行，故不得启动它或覆盖用户扩展。需先完成 ENV-001。

### UI-004：WPF-UI 成熟控件的生产迁移与宿主回归

- **优先级**：P0
- **状态**：BLOCKED_ENVIRONMENT
- **前置条件**：用户已明确授权继续源码层生产迁移；真实 Playnite 验收仍以 ENV-001 证明独立数据根和进程边界为前置。必须保留插件 ID、业务命令、绑定、虚拟化和共享主题令牌。
- **根因与影响**：基线中的 WPF-UI 4.3.0 仅以惰性“界面探针”和少量顶部控件验证程序集、资源隔离与异常恢复，生产页面仍有大量重复模板。源码迁移现已完成，但未经 Windows 编译与宿主验证仍可能存在 API/XAML 兼容、资源键冲突或 Playnite 默认主题泄漏，因此不能宣称真机完成。
- **范围**：将适用的输入、下拉、信息卡、动作按钮、对话框和通知迁移到 WPF-UI 的局部资源体系；主导航/Tab、DataGrid、游戏列表、数值校验和性能敏感虚拟化区经审查保留现有 WPF 模板，避免改变信息架构、列行为、键盘路径或 Recycling。每类控件保留原有命令、主题、错误/取消状态与宿主回退。
- **非目标**：不注入 Playnite 全局资源；不修改窗口 Chrome；不以探针或截图替代生产交互；不删除现有安全错误处理、数据或备份。
- **验收标准**：真实隔离 Playnite 中 Dashboard、Settings、Dialog/Snackbar 正常加载；Light/Dark/Follow/高对比度、关闭透明/动画、100%–200% DPI 和 1600×900–980×640 无新增资源、绑定或 Dispatcher 异常；大库 ListBox/DataGrid 保持 Recycling；框架控件创建失败时页面可恢复且不污染宿主。
- **阻塞条件**：无法证明 WPF-UI 局部资源在 Playnite 10/net462 中稳定、可回退且无宿主污染时，保持自定义共享令牌方案并改为 `BLOCKED_ENVIRONMENT`，不得强行替换生产控件。
- **当前证据**：2026-08-01 已完成可在源码层安全落地的生产迁移：新增视图局部 `WpfUiProduction.xaml` 适配层；Dashboard 的 6 个指标卡、59 个生产动作按钮、14 个策略/工具/媒体开关、10 个普通文本输入和 13 个下拉选择，以及 Settings 的 5 个设置卡、2 个动作按钮、14 个开关、6 个路径输入和 3 个下拉选择均使用 WPF-UI 模板。数值校验输入、高密度 DataGrid/ListBox、搜索清除按钮和本地兜底浮层继续保留原生 WPF；59 个 Command、全部 Binding、Recycling 虚拟化和业务事件数量与基线一致。Dashboard/Settings 的 Snackbar 使用框架局部适配；确认和设置导入报告分别使用插件内 Dialog 与 MessageBox，因为真实日志证明 WPF-UI `ContentDialogHost` 是 Playnite Window 级单例，不能放入嵌入式页面。文件导入导出的磁盘读取/写入移出 UI 线程，未引入新的 `async void`。针对真实 `Wpf.Ui.Controls.Button`、`GscSoftShadowColor` 和 `ContentDialogHost.RegisterHost` 崩溃，已分别增加资源作用域/主题令牌/窗口 Host 回归门禁。Windows Release build 为 0 警告/错误，Core 13、Worker 21、Playnite 19 项测试通过；真实 Playnite、DPI、主题、键盘及宿主污染验证仍被 ENV-001 阻塞。

## 本次审计证据

- 2026-08-01：GOV-002 完成台账校准。`792186f` 已将 `FEATURE_COMPLETION_ASSESSMENT.md` 校准至 `0.6.22-development-preview`，故 DOC-001 从 `PROPOSED` 改为 `IMPLEMENTED`；当前可复查自动化基线为源码门禁通过、Release 0 warning/0 error、Core 13 + Worker 21 + Playnite UI 46 = 80 项测试通过、UI Skill 静态审查 0 errors、`git diff --check`/`git fsck --full`、PEXT 打包和 Worker `0.6.22.0` smoke 通过。历史条目中的较低测试数保留为当时提交的证据，不能误写成当前基线。`ENV-001` 仍是实际 Playnite 加载、多主题、DPI、键盘和滚动验收的唯一环境前置；没有启动 `.tmp` 或用户实例。

- 2026-08-01：本次只读审计确认用户进程仍为 `D:\\software\\Playnite\\Playnite\\Playnite.DesktopApp.exe`（PID 28708）及用户 AppData 扩展下的 Worker（PID 31444）。工作区 `.tmp/playnite-ui-test/Playnite` 仅声明 `DatabasePath: library`，未提供可审计的 portable/独立配置根证明；其扩展目录已含本次测试插件。已登记 ENV-001，未启动该副本，`.tmp/` 保持未跟踪。
- 2026-08-01：为验证 GSC-083/GSC-084，向 `.tmp/playnite-ui-test/Playnite/Extensions/66e9f2d7-67bb-43ef-b62a-b8e60734fcec` 复制了本次已打包的测试文件；启动副本后，桌面自动化只识别到 `D:\\software\\Playnite\\Playnite\\Playnite.DesktopApp.exe` 的“数据备份错误”窗口和该路径的 PID。未点击、关闭、终止或写入该用户实例，未操作其 Worker、备份、恢复或云端。此现象进一步证明复制目录不能建立 ENV-001 的单实例/数据根边界；真机验收继续保持 `BLOCKED_ENVIRONMENT`，`.tmp/` 不纳入 Git。
- 2026-08-01：继续使用官方 `--userdatadir D:\\workplace\\Github\\GameSaveCenter\\.tmp\\playnite-isolated-data` 及唯一启动 PID 测试隔离；该数据根只创建了 `Extensions`、`JITProfiles` 与 `playnite.log`，日志原样记录 `Application already running, shutting down.`，测试进程立即退出。没有获得独立可交互窗口，未触碰用户 AppData、用户 Worker、备份、恢复或云端。现有 Playnite 的全局单实例行为仍阻塞 ENV-001，后续不得以再次启动或结束用户实例绕过。
- 2026-08-01：只读审计确认 `docs/FEATURE_COMPLETION_ASSESSMENT.md` 仍为 `0.6.20-development-preview`，与当前 0.6.22 交接文档不一致；已登记 DOC-001，不在没有 READY 条目的本次运行中直接改写评估。

- 2026-08-01：UI-002 完成共享 WPF 控件和主题令牌复审。使用 Codex 附带 Python 运行 `scripts/validate-source.py` 退出码 0；UI Skill 审查退出码 0（0 errors、32 warnings、111 info，其中 `.tmp/` 的复制宿主占 17 条警告，其余为既有 Dashboard/环境光布局审查项）；`dotnet restore`、Release build（0 警告/错误）和 50 项测试通过。`scripts/package.ps1 -Configuration Release -SkipBuild` 生成 0.6.22 PEXT/ZIP，`scripts/verify.ps1 -Worker <打包 Worker>` 报告 0.6.22.0；`git diff --check`、`git fsck --full` 均为退出码 0，后者仅报告既有 dangling 对象。未启动 `.tmp/` 副本或用户 Playnite，隔离视觉回归移交 UI-003/ENV-001。

- 2026-08-01：UI-003 已完成可自动验证的 Dashboard/Settings 响应式与可访问性收口：项目自身 UI Skill 告警从 15 降至 11（总数从 32 降至 28；17 条仍来自未跟踪 `.tmp/` 的 Playnite 复制主题）。剩余项目提示均需真机在有限 `Grid` 行中复查，并不等同于已经确认的布局缺陷。源码门禁、Release build 和 50 项测试均通过；实际 Playnite 验收被 ENV-001 阻塞。

- 2026-08-01：无 READY 项时的只读审计确认，现有 WPF-UI 仅为惰性 POC，尚未迁移生产控件；已登记 UI-004。由于 UI-001/UI-003/ENV-001 尚无独立 Playnite 证据，本次没有修改 XAML、程序集、安装或用户数据。

- 2026-08-01：GOV-001 已补齐 `AUTONOMOUS_DEVELOPMENT_RULES.md` 与 `QUALITY_GATES.md`，并建立了 UI-001/002/003 的依赖、范围、非目标、验收条件和阻塞规则。UI-001 曾按依赖顺序登记为 `READY`，现因真机隔离证据不足转为 `BLOCKED_ENVIRONMENT`；不会推送或合并 `origin/main`。
- 2026-08-01：使用 Codex 附带解释器 `C:\\Users\\lopmatuse\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe` 执行 `scripts/validate-source.py`，退出码为 `1`，原样输出为：`XAML GameSaveCenter resource missing: ...GameSaveCenterSettingsView.xaml -> GscButtonBase`、`...DesignTokens.xaml -> GscErrorTintBrush`、`Numeric input must commit complete values on LostFocus: DashboardView.xaml DuringPlayIntervalMinutes`。这是 GOV-001 发现的 UI-001 基线缺陷，未在治理任务中修改代码。`git diff --check` 通过；`git fsck --full` 仅报告既有 dangling tree，不是对象损坏。

- 2026-07-31：仓库根目录为 Git 工作树，启动时 `main` 无未提交修改；`rg --files` 未找到 GOV-001 所列三份治理文档。
- 2026-07-31：`scripts/build.ps1 -Configuration Release` 通过：Release 构建 0 警告/0 错误，Core 13、Worker 20、Playnite 设置迁移 11 项测试均通过。
- 2026-07-31：`scripts/package.ps1 -Configuration Release -SkipBuild` 成功生成 `0.6.21` ZIP 与 PEXT；`scripts/verify.ps1` 确认打包 Worker 文件版本为 `0.6.21.0`。该 smoke 检查未启动、停止或替换已安装的 Playnite/Worker。
- 2026-07-31：`python scripts/validate-source.py` 未能运行，退出码 9009；PATH 中的 `python.exe` 是 Microsoft Store 占位符，系统未安装 Python 解释器。此项必须在配置 Python 3 后重跑，不能以 XAML 结构检查替代。

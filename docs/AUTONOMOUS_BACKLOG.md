# 无人值守开发积压清单

本清单是无人值守开发代理选择单一工作项的唯一来源。任务状态只能使用：`PROPOSED`、`READY`、`IN_PROGRESS`、`IMPLEMENTED`、`BLOCKED_ENVIRONMENT`、`BLOCKED_USER_DECISION`。

## PROPOSED

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
- **当前证据**：2026-08-01 已确认 NuGet 的 WPF-UI 4.3.0 含 net462 程序集；插件以局部 `UserControl.Resources` 合并主题与控件字典，Release 构建、48 个单元测试、源码/XAML 门禁及打包通过，PEXT 含 Wpf.Ui、Abstractions 与 net462 运行依赖。现有 Playnite/Worker 是用户正在使用的实例；未获隔离实例授权前不得关闭进程或覆盖其插件目录，故实际加载、Dialog/Snackbar 交互、主题/DPI 与宿主污染验证尚未执行。

### UI-002：共享设计令牌与控件模板全量复审

- **优先级**：P0
- **状态**：PROPOSED
- **前置条件**：UI-001 结论已记录；若 POC 不兼容，则使用现有资源体系的等价实现。
- **范围**：统一 Button、TextBox、数值输入、ComboBox、CheckBox、ScrollBar、TabControl、DataGrid、ListBox、Popup、Tooltip、Toast 和焦点环；补齐浅/深/跟随 Playnite、高对比度、关闭透明和关闭动画的安全降级。
- **验收标准**：所有共享控件 Normal/Hover/Pressed/Disabled/Focus 状态可用；大列表保持 Recycling 虚拟化；不出现页面级硬编码主题色、默认宿主白色控件、冻结 Transform 动画或大型区域 BlurEffect。

### UI-003：Dashboard 与 Settings 自适应布局、可访问性及真机视觉回归

- **优先级**：P0
- **状态**：PROPOSED
- **前置条件**：UI-002 已实现并通过自动化门禁。
- **范围**：修复所有工作区的裁切、长文本 Tooltip、窄窗口重排、数值输入完整编辑、表格/页签/弹层对齐与真实状态反馈；在隔离目录验证 100%–200% DPI、1600×900 至 980×640、键盘导航、主题及减少动画。
- **验收标准**：真实 Playnite 加载、页面打开关闭与缩放均无 XAML/绑定/Dispatcher 异常；所有真机未覆盖条件在 Windows 测试计划中明确记录，不伪造完成。

## 本次审计证据

- 2026-08-01：GOV-001 已补齐 `AUTONOMOUS_DEVELOPMENT_RULES.md` 与 `QUALITY_GATES.md`，并建立了 UI-001/002/003 的依赖、范围、非目标、验收条件和阻塞规则。UI-001 已登记为下一项 `READY`；不会推送或合并 `origin/main`。
- 2026-08-01：使用 Codex 附带解释器 `C:\\Users\\lopmatuse\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\python\\python.exe` 执行 `scripts/validate-source.py`，退出码为 `1`，原样输出为：`XAML GameSaveCenter resource missing: ...GameSaveCenterSettingsView.xaml -> GscButtonBase`、`...DesignTokens.xaml -> GscErrorTintBrush`、`Numeric input must commit complete values on LostFocus: DashboardView.xaml DuringPlayIntervalMinutes`。这是 GOV-001 发现的 UI-001 基线缺陷，未在治理任务中修改代码。`git diff --check` 通过；`git fsck --full` 仅报告既有 dangling tree，不是对象损坏。

- 2026-07-31：仓库根目录为 Git 工作树，启动时 `main` 无未提交修改；`rg --files` 未找到 GOV-001 所列三份治理文档。
- 2026-07-31：`scripts/build.ps1 -Configuration Release` 通过：Release 构建 0 警告/0 错误，Core 13、Worker 20、Playnite 设置迁移 11 项测试均通过。
- 2026-07-31：`scripts/package.ps1 -Configuration Release -SkipBuild` 成功生成 `0.6.21` ZIP 与 PEXT；`scripts/verify.ps1` 确认打包 Worker 文件版本为 `0.6.21.0`。该 smoke 检查未启动、停止或替换已安装的 Playnite/Worker。
- 2026-07-31：`python scripts/validate-source.py` 未能运行，退出码 9009；PATH 中的 `python.exe` 是 Microsoft Store 占位符，系统未安装 Python 解释器。此项必须在配置 Python 3 后重跑，不能以 XAML 结构检查替代。

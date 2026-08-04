# 项目记忆与不可丢失约束

更新时间：2026-08-05
当前版本：`0.6.47-development-preview`

> 0.6.47 针对用户提供的 900+ 游戏日志增加了非破坏性 Worker 健康恢复：超大目录中已有 Worker 只是暂时忙碌时不再自动 Kill/重启；用户日志实际加载的是 0.6.22，必须先用 0.6.47 包复测，并把独立 LudusaviPlaynite 的 967 次 findTitle 请求分开统计。

> 0.6.46 在 0.6.45 的 Dashboard 延迟回调、平移/缩放动画异常边界上，再覆盖通知/确认派发、后台集合回写和超大目录缓存重试取消；任何 UI 回调异常都不应再穿透到 Playnite Dispatcher。

> 0.6.44 将共享表格行高提高到 60 DIP、表头提高到 50 DIP，视口提升到 520–820 DIP；外层页面滚动不被取消，短窗口仍可通过页面滚动访问表格下方内容。

> 0.6.43 针对 900+ 游戏 Playnite 启动竞态增加了第二道熔断：如果 `OnApplicationStarted` 时 Playnite 游戏库暂时为空，插件会等待宿主库稳定后再决定是否启动 Worker；自动同步在实际捕获到 500+ 游戏且 Dashboard 尚未打开时会直接返回，不创建 Worker/IPC 整库匹配任务。这样可以避免 0.6.22 日志中出现的 967 次 Ludusavi 启动和启动期管道超时循环。真实 Playnite 回归仍需在用户环境验证。

> 0.6.42 对 500+ 游戏库增加了启动熔断：只读取 SQLite 持久化游戏摘要，首次打开 Dashboard、Playnite 自动库事件和后台延迟任务不会自动提交整库 Ludusavi 匹配；显式刷新仍可执行整库同步，游戏启动时仍会单独 Upsert 当前游戏。共享 DataGrid 和表格外壳统一使用动态主题、圆角和有限视口。真实 Playnite、DPI、主题与 900+ 库回归仍待用户环境验证。

> 0.6.41 在 0.6.40 的低高度信息保留与主题兜底基础上，修正共享上下文按钮 disabled 时被折叠的问题：操作仍保留在布局中，仅降低透明度，避免状态变化造成内容跳动。当前真实 Playnite、主题、DPI 和 900+ 游戏库仍待用户侧验证。

> 0.6.36 针对 900+ 游戏库继续收紧后台负载：整库描述仍先持久化，后台 Ludusavi 匹配只优先已安装或 90 天内游玩的游戏，每轮最多 64 个；未安装条目会在安装、启动或后续同步时再处理。Worker 使用当前用户级单实例互斥，健康恢复窗口延长到 45 秒；六个工作区共享单一页面纵向滚动通道，DataGrid 保留内部双轴滚动和虚拟化。真实 Playnite 渲染与 900+ 库回归仍待用户环境验证。

> 0.6.35 在 0.6.34 的大型库启动闸门上继续收紧 Dashboard 首次打开路径：900+ 游戏库在 Playnite 启动阶段不自动启动 Worker，打开面板或启动游戏后才按需启动；已有 SQLite 游戏缓存时先显示缓存并等待 60 秒空闲窗口，空缓存使用 10 秒延迟后才开始整库同步；Dashboard 未打开前也不启动任务通知长轮询，避免 900+ 游戏启动阶段与其他 Ludusavi 集成争用；用户手动刷新会取消延迟并明确执行同步，视图卸载会取消未开始的同步。

> 0.6.32 在大型游戏库启动上增加交互闸门：仅启用插件不会提交 900+ 游戏的整库匹配；Dashboard 作为明确目录入口时才释放同步，Settings 保存只更新 Worker 设置，不再顺带触发整库匹配，SQLite 持久化缓存仍优先显示。

> 0.6.31 修复大型游戏库打开侧栏时的重复全量同步：Dashboard 会加入正在运行的同步任务，最近五分钟内已完成的同步不会再次排队；同时所有工作区表格显式使用有限的 360–640 DIP 视口，保留内部虚拟化和页面级滚动。

> 0.6.30 在大型库启动保护上补齐了事件时序：先建立同步静默窗口，再启动 Worker/后台匹配；100+ 游戏库的通知事件监听延后至 60 秒，避免在 Worker 初始化和独立 LudusaviPlaynite 全库请求期间反复连接命名管道。该调整只影响通知与启动调度，不改变 SQLite 缓存和真实任务状态。

> 0.6.29 在 0.6.28 的工作区响应式转发与维护设备表有限视口基础上，将页面级滚动扩展到六个工作区，并为 DataGrid/ListBox 引入动态有限视口；这让表格能显示更多行且保留下方操作区，但不等于已完成 Playnite 宿主真机验证。

### 2026-08-04 共享页签与滚动通道

- 六个物理工作区的内部页签必须使用 `GscRedesignWorkspaceTabControl` / `GscRedesignWorkspaceTabItem`；页签标题放在独立横向 `ScrollViewer` 中，内容区保持独立的星号测量行，不能让页签把工作区撑出水平溢出。
- 首页风险提醒、GamePicker 抽屉和修改器/FLiNG 列表必须有明确的纵向滚动通道；大型列表继续使用虚拟化与 Recycling。任何后续布局调整都不能把这些列表放回无限测量的 StackPanel。
- 共享勾选标记的前景使用 `GscOnAccentTextBrush`，禁止写死白色/黑色；所有新工作区颜色、页签选中态和边框继续使用 DynamicResource。
- 新增的共享页签模板只动画渲染属性；真实宿主页签滚动、主题、高对比度和 DPI 仍需隔离 Playnite 实例验证。

### 2026-08-04 六工作区功能入口补齐

- `MediaCenterView` 不再只是媒体列表：可预览截图/录像、编辑备注/收藏、批量收藏/应用备注、重新归类，并可新增、启停、移除媒体来源。
- `TrainerCenterView` 必须保留多 EXE 导入确认、活动版本选择、管理员权限、启动延迟和退出后关闭；新导入仍默认关闭自动启动。
- `MaintenanceView` 的设备状态必须提供摘要、人工决策和受保护远端恢复的“下载并校验 / 创建快照并恢复”两步入口，不能因物理拆分而退化为只读表格。
- 新页面的响应式布局由代码后置统一协调：FLiNG 搜索结果/版本在 980 DIP 以下上下堆叠，任务摘要在低高度收起；不动画 Width/Height/Margin/GridLength。

### 2026-08-04 六工作区物理入口收口

- `OverviewWorkspaceTab`、`SaveWorkspaceTab`、`TrainerWorkspaceTab`、`MediaWorkspaceTab`、`TaskWorkspaceTab`、`MaintenanceWorkspaceTab` 是当前唯一可见工作区入口；旧 Dashboard Tab 只能保持隐藏迁移回退，不能恢复为可见副本。
- `SaveCenterView`、`TrainerCenterView`、`MediaCenterView`、`MaintenanceView` 继续继承父级 `DashboardViewModel` DataContext，不复制 Worker 请求、SelectedGame 或 GamePicker。它们只负责布局、主题、虚拟化和现有命令入口。
- 媒体中心的目标游戏 ComboBox 是业务归类目标，不是全局 GamePicker；任务和维护中心保持全局视角，不显示当前游戏选择器。
- 新工作区的 DataGrid/ListBox 必须保留行/列虚拟化和 Recycling；任何后续视觉改动不得给列表行或大滚动区增加 BlurEffect。
- 旧视图删除前要先完成独立 Playnite 宿主回归，确认命令、绑定、键盘、DPI、主题和卸载订阅无回归。

### 2026-08-04 首页物理工作区迁移

- `OverviewView` 已从 `DashboardView` 的巨型 XAML 中提取为真实 UserControl，保留 `OverviewWorkspaceView` 对现有响应式布局协调器的窄接口；布局只能通过 `ApplyResponsiveColumns` 和只读列/面板访问器调整。
- 首页新 Tab 是唯一可见渲染入口；旧 `OverviewTab` 暂时保持隐藏，仅作为迁移回退，不能重新显示或与新首页同时渲染。删除旧块前先完成剩余五个工作区迁移并补齐独立宿主回归。
- 新工作区必须继续从父级继承 Dashboard DataContext，不能复制 `SelectedGame`、GamePicker 或 Worker 请求；如果需要局部资源，优先使用 `DynamicResource` 和共享字典，不在 View 中静态捕获主题。
- `OverviewView` 的最近任务 DataGrid 必须保持有限 Grid 祖先、行/列虚拟化和 Recycling；风险提醒中的 `OpenAttentionFindingCommand` 必须保留真实 finding 参数与维护中心导航。

当前自动化基线为 Core 13、Worker 21、Playnite UI 64，共 98 项 Release 测试；这只代表源码/自动化通过，不能替代独立 Playnite 的宿主渲染、主题、DPI、键盘和页面生命周期回归。

### 2026-08-04 任务中心物理迁移

- `TaskCenterView` 是第二个真实提取的工作区；它必须保持全局视角，不得重新放入 GamePicker 或选中游戏上下文。
- 任务列表的 `TasksView`、状态/进度 OneWay 绑定和 Copy/Retry/Cancel 命令继续由父级 Dashboard DataContext 提供；迁移不能把任务结果复制到新的 Worker 请求层。
- 旧 `TaskTab` 当前必须隐藏，`TaskWorkspaceTab` 才是唯一可见任务渲染入口；剩余工作区迁移完成后统一删除旧 Tab 和冗余命名控件。

### 2026-08-04 全局 GamePicker 单一上下文

- `GamePickerViewModel` 是全局游戏选择器的轻量本地状态层。它只接收 `GameStatusDto` 摘要，不持有 Playnite `Game`，不在搜索输入时调用 Worker。
- `GamePicker.ItemsView` 必须保持 WPF 虚拟化/Recycling；搜索、状态筛选、平台筛选和排序都属于本地视图操作。当前防抖为 180ms，旧筛选任务通过 `CancellationTokenSource` 取消。
- 现在唯一的当前游戏入口是 `GameSwitcherHost`/`CompactGameSelector` 顶部按钮。`GameBrowserPanel` 只作为有限高度抽屉出现，Expanded 不再常驻三栏游戏浏览器；首页不得恢复 `OverviewGameSelector`。
- 任务中心和维护中心是全局工作区，不显示当前游戏入口。收件箱、媒体重分配、进程映射等“选择目标游戏”ComboBox 是业务目标选择，不属于全局当前游戏选择器。
- GamePicker 的搜索、筛选、平台、排序和最近游戏 ID 会延迟写入 `GameSaveCenterSettings`；View 卸载时必须调用 `CancelDeferredUiWork`，不能让 Dispatcher/防抖回调继续触碰卸载页面。
- `GameStatusDto.IsInstalled` 与 `LastPlayedUtc` 来源于 Playnite `Game.IsInstalled`/`Game.LastActivity`，但不能把 LastActivity 纳入 Ludusavi 匹配输入哈希；它是 UI 排序元数据。
- 首页关注卡片必须显示至少一组具体 `AttentionFindings` 摘要，不能只显示数字和“去维护中心查看”。完整诊断仍以维护中心 FindingsGrid 为准。

### 2026-08-01 可选 WPF-UI 探针的列表测量边界

- `UiFrameworkProbeView` 的 `ProbeChecklist` 必须在高度为 132 DIP 的 `Grid` 行内，不能重新放回 `StackPanel`。探针仍是惰性 opt-in：其资源或创建失败不能阻止 Dashboard 打开。
- 这项布局规则只处理探针，不授权将生产 DataGrid/ListBox 移到无限高度容器、取消虚拟化或改动宿主对话边界。

### 2026-08-01 Settings 主题更新必须合并

- `QueueAdaptiveThemeUpdate` 是设置页所有滑块/主题/玻璃开关事件的唯一延迟主题入口；它只保留最新设置状态，避免每个 `ValueChanged` 都重建局部调色板和 WPF-UI 资源。Loaded、可见与导入后的显式刷新仍可直接调用 `ApplyAdaptiveTheme`。
- `BeginUiSafely` 返回是否真正成功投递；若 Dispatcher 已关闭或投递失败，必须释放 `adaptiveThemePending`，不能让后续设置更新永久失效。页面卸载同样释放该门闩。

### 2026-08-01 有限宽度下拉项的长文本访问

- `GscComboBoxLongText` 是 Dashboard 的受限宽度游戏、修改器和版本选择器的唯一长文本模板：仅用省略与自引用 Tooltip 提供完整内容，不增大 Popup、不更改选择绑定，也不为下拉项加入动画或效果。
- 禁止回退到没有完整内容入口的 `DisplayMemberPath`；真实 Popup 键盘/DPI 回归仍需要隔离 Playnite。

### 2026-08-01 高密度表格长文本的统一可达性

- `GscLongTextCell` 只用于 Dashboard 高密度表格中可能被窄列裁切的名字、设备、原因和日志文本；它复用 `GscLeftCellText` 的省略/左对齐，通过绑定自身最终 `Text` 提供完整 Tooltip，不重新读取数据或建立每行动画。
- 不得为得到完整文本而取消列宽、虚拟化或为 DataGrid 行附加 `BlurEffect`；真实 Tooltip/DPI/键盘验证仍需隔离 Playnite。

### 2026-08-01 当前可复查自动化基线

- 当前基线是 Core 13、Worker 21、Playnite UI 49，共 83 项 Release 测试；并同时通过源码门禁、UI Skill 静态审查（0 errors）、`git diff --check`、`git fsck --full`、PEXT 打包和 Worker `0.6.22.0` smoke。历史提交中记录的较低测试数是当时的快照，不得作为当前完成度。
- 这不是实际 Playnite 验收的替代物：`ENV-001` 未证明独立数据根/扩展目录/PID 前，不得启动 `.tmp` 副本或用户实例，也不能宣称多主题、DPI、键盘或滚动真机完成。

### 2026-08-01 紧凑工具栏必须保留动作可达性

- Dashboard 顶部操作在非 Expanded 布局只能收起文字标签，不能删除按钮、Command、Automation Name 或 Tooltip。`SetToolbarLabelsVisible` 必须覆盖刷新、全部备份、媒体同步、修改器导入/目录及诊断全部六项，以便窄宽度仍保留鼠标、键盘与屏幕阅读器路径。
- 这项收口不得改为动态插入/删除工具栏子元素；后台刷新也不能改变 `TopActionsPanel` 的测量宽度，避免定时更新触发页面跳动。

### 2026-08-01 Settings 紧凑窗口横向访问

- Settings 外层滚动区禁止页面级横向滚动；长路径仍由各自 TextBox 提供可编辑的横向访问。自动化数值字段按扣除页面边距后的 `contentWidth` 决定一/二/三列，避免 950–1019 DIP 宿主宽度错误挤入三列。

### 2026-08-01 Popup 的透明与动画局部资源

- `GscPopupAllowsTransparency` 与 `GscPopupAnimation` 必须由 `ApplyMaterialResources` 随页面局部调色板更新。玻璃模式可用透明/`Fade`，关闭透明、高对比度或 `EnableUiAnimations=false` 时必须分别成为 `false`/`PopupAnimation.None`。
- 不可为了无障碍降级删除 ComboBox 的 Popup、可访问展开契约、滚动或键盘路径；默认 XAML 资源使用不透明/无动画安全值，避免首次资源解析或宿主不支持时泄漏效果。

### 2026-08-01 Dashboard 的两级材质层级

- `GscSurface` 是无 Effect 的主要阅读面；`GscElevatedSurface` 才绑定动态 `GscSurfaceEffect`。Dashboard 仅把后者用于 `GameBrowserPanel` 和 `GameDetailCard` 两个永久主工作区，避免统计卡、表单、诊断卡和列表附近堆叠阴影。
- 这条层级规则不能通过给 DataGrid/ListBox 行或滚动内容加阴影来替代；浮层、侧栏和 Dialog 继续走各自受主题/无障碍控制的 Effect 键。真实滚动与主题视觉回归仍需 `ENV-001`。

### 2026-08-01 共享材质 Effect 的无障碍回退

- `ApplyMaterialResources` 是 Dashboard 与 Settings 的局部阴影资源唯一入口。关闭透明或高对比度时，`GscSurfaceEffect`、`GscPrimaryButtonEffect`、`GscSidebarEffect`、`GscPopupEffect`、`GscDialogEffect` 和 `GscSliderThumbEffect` 必须是真正的 `null`，不能用 `Opacity=0` 保留昂贵的 Effect visual。
- 正常玻璃模式只为固定卡片、侧栏和浮层恢复冻结的轻量阴影；绝不将阴影或模糊加到 DataGrid、ListBox、媒体行或任何大型滚动内容。初始 XAML 资源也必须声明这些空键，以保持静态资源门禁和安全解析。

### 2026-08-01 插件级通知/确认的安全 UI 调度

- `GameSaveCenterPlugin` 的错误、成功、任务通知、宿主通知和确认必须经 `TryInvokeUi`。它检查 Playnite Dispatcher 关闭态并记录竞态；仅在 Dispatcher 确认关闭时拦截调度异常，处理器自身的真实异常必须保留给原错误边界；确认无法显示时必须返回取消，尤其不能让恢复等危险操作继续。
- 通知调度失败不能伪造成功；应保留既有回退/日志。真实关闭中的通知和确认仍需 `ENV-001`。

### 2026-08-01 Worker 回调更新 Dashboard 集合的 Dispatcher 边界

- `DashboardViewModel.ApplyOnUi` 是 Worker 回调更新 `ObservableCollection`、筛选和选中项的唯一同步 UI 入口。它必须先检查 Playnite Dispatcher 关闭态，并捕获 `Invoke` 与关闭竞态；正常路径继续使用 `DispatcherPriority.DataBind`，禁止后台直接访问绑定集合。
- 关闭时仅跳过无法呈现的 UI 更新并记录原始异常；不能将真实 Worker 状态伪造为成功或用延迟掩盖失败。真实慢 Worker/关闭回归仍需 `ENV-001`。

### 2026-08-01 Toast 的材质降级

- Dashboard Toast 的阴影只能在玻璃效果已启用且非高对比度时创建；关闭透明或高对比度必须完全省略 `DropShadowEffect`，保留由局部调色板提供的实体背景、边框、文本和关闭路径。
- 该规则不允许以删除真实错误详情、自动隐藏或动画来换取稳定性；实际多主题 Toast 回归仍需 `ENV-001`。

### 2026-08-01 Dashboard 的 ViewModel 事件所有权

- Dashboard 的 `PropertyChanged` 与 `AttentionCenterRequested` 只能在页面 Loaded 时订阅，Unloaded 时必须解除；不能仅依靠事件处理器中的 `IsLoaded` 早退，因为该方式仍会持有页面并制造后台回调。
- 重复 Loaded 必须由订阅标记防止重复挂接；重新打开页面后所有真实状态动画、关注中心导航和命令入口仍应存在。

### 2026-08-01 响应式布局的缩放合并

- Dashboard/Settings 不能在每一个 `SizeChanged` 同步重新设置全部响应式属性；必须保存最近 `Size` 并用单次 `DispatcherPriority.Render` 回调合并。回调必须在 `IsLoaded` 为 false 时跳过，卸载要清除挂起标记。
- 这只是渲染性能优化，不能延迟或丢失首次 Loaded 布局，也不能删改紧凑模式中的横向访问、命令、焦点或虚拟化。

### 2026-08-01 Settings 页面卸载与异步反馈

- 设置导入/导出可在页面卸载后继续完成真实文件操作，但后续 `DataContext`、Snackbar、MessageBox 和成功提示必须通过 `CanPresentUiFeedback`（已加载且 Dispatcher 可用）边界。无法呈现时记录而不是让视觉反馈异常回流。
- `Unloaded` 必须取消 SettingsShell 的入口动画；不能让页面脱离视觉树后继续保留动画。真实关闭/切换页回归仍需 `ENV-001`。

### 2026-08-01 语义状态色必须可动态重算

- `GscInfo/Success/Warning/ErrorBrush` 与对应图标填充在 Dashboard/Settings 中均由 `ApplyAccentResources` 写入局部资源。需要随主题/高对比度变化的状态点、图标和状态文字必须使用 `DynamicResource`，不能用 `StaticResource` 捕获首次 Brush。
- 高对比度下状态色可退化为系统可读前景；任务、健康与错误仍保留文字/图形语义，绝不能只依赖颜色。真实切换回归仍需 `ENV-001`。

### 2026-08-01 品牌图标的动态强调色前景

- 在强调色表面上的 Dashboard/Settings 图标不能写死 `White`；必须使用 `GscOnAccentTextBrush`，由 `AdaptiveThemePaletteFactory` 根据宿主强调色计算，并在高对比度时提供系统 `HighlightText`。该令牌的使用点必须是 `DynamicResource`。
- 多主题外观调整不能移除图标的 Automation、Tooltip、布局或动画；真实 Follow/自定义宿主色/高对比度渲染仍需 `ENV-001`。

### 2026-08-01 Dashboard Toast 的计时器所有权

- Dashboard 的 Toast 自动关闭计时器必须由页面集中持有并在容量淘汰、显式关闭、动画结束及 `Unloaded` 时停止和移除。不能只清空视觉容器：悬停/自动关闭回调仍会保留卡片和页面，导致关闭后继续向 Dispatcher 投递。
- 收口只影响视觉资源生命周期；通知仍由真实任务结果触发，错误详情与用户关闭入口不能被删除。真实连续通知/关闭页面回归仍需 `ENV-001`。

### 2026-08-01 数值输入的 Dispatcher 关闭边界

- `SelectAllOnKeyboardFocus` 的延迟全选只是一项输入便利功能：必须先检查 Dispatcher 的启动/完成关闭态，并捕获守卫后仍可能发生的 `InvalidOperationException`。页面卸载时不可让该投递成为 Playnite Dispatcher 的未处理异常；不能以此改变数值绑定、范围校验或失焦提交。
- 回归测试锁定此关闭边界。真实页面关闭后的输入焦点回归仍需 `ENV-001` 的隔离 Playnite 实例，不能用用户正在运行的宿主替代。

### 2026-08-01 插件异步事件边界

- `GameSaveCenterPlugin` 的设置同步、应用/库/游戏生命周期和任务通知计时器不得新增 `async void` 业务路径；通过 `FireAndForget` 的可观测 Task continuation 收敛最终故障。WPF 必需的视图事件边界仍必须在入口处完整捕获。
- `ReportBackgroundFailure` 要先记录业务异常，再尝试真实错误通知；通知层异常只能追加日志，不能重新从 Playnite Dispatcher、Timer 或未观察 Task 逸出。通知轮询门闩只在成功进入后释放。

### 2026-08-01 多主题强调色与资源生命周期

- `AdaptiveThemePaletteFactory` 是 Dashboard/Settings 局部主题的唯一强调色推导入口：Follow Playnite 优先读取 `HighlightGlyphBrush`，强制浅色/深色保留稳定紫色，高对比度使用 Windows 系统 Window/Highlight 颜色；按钮前景会依强调色计算，不能固定假设白字。
- `GscAccent*`、`GscPrimaryButton*`、`GscOnAccentTextBrush` 以及 `GscAmbientAccentBrush`、`GscAccentShadowColor` 的运行时使用点必须是 `DynamicResource`；页面每次应用主题都调用 `ApplyAccentResources` 写入自己的 `Resources`，绝不把这套调色板注入 `Application.Current.Resources` 或 Playnite 宿主。
- `GscSelectionTextBrush` 在普通主题等同主文本，在高对比度时必须为 `SystemColors.HighlightTextColor`；高对比度的 Accent Tint/图标容器也必须是不透明系统 Highlight，不能回退为半透明品牌色。
- WPF-UI 使用的 `AccentFillColor*`、`TextFillColor*`、`ControlFillColor*`、`ControlStrokeColor*`、`Card*` 和 `FocusStrokeColor*` 仅通过 `ApplyWpfUiResources` 写入 Dashboard/Settings 的局部 `Resources`；资源键由已安装的 WPF-UI 4.3.0 实际字典回归测试验证，不得以未验证的全局主题 API 替代。
- STA 回归测试真实创建带 `HighlightGlyphBrush` 的宿主资源并检查色板派生；当前源码验证为 Core 13、Worker 21、Playnite 35，共 69 项测试，仍不能替代 `ENV-001` 的真实 Playnite Light/Dark/Follow/High Contrast/DPI 回归。

### 2026-08-01 全量 UI 源码交付与真机边界

- 已完成的源码重构覆盖 Dashboard 六个工作区（总览、存档、修改器、媒体、任务、维护）及 Settings：共享设计令牌和控件模板、数值输入完整值提交、紧凑布局、命令映射、错误/取消路径、焦点、主题降级和延迟 Dispatcher 回调均有回归覆盖；不得以页面改版为由删除既有功能入口或 Recycling 虚拟化。
- 本轮自动化证据：`validate-source.py` 通过；UI Skill 静态审查为 0 错误（仍有项目和 `.tmp` 副本的提示，不能忽略）；Release 构建 0 警告/0 错误；Core 13、Worker 21、Playnite 34，共 68 测试通过；PEXT 包检查与 Worker `0.6.22.0` smoke 通过。
- 以上仅为源码和自动化证据。真实 Playnite 的资源加载、Light/Dark/Follow/High Contrast、透明/动画降级、100%--200% DPI、980×640--1600×900、键盘和大库性能仍受 `ENV-001` 阻塞。没有可审计的独立数据根、扩展目录、测试库和本次启动 PID 边界时，不得启动/关闭/覆盖用户 Playnite 或用户插件目录。

### 2026-08-01 async-void UI 事件边界保护

- Dashboard 定时刷新保留为 WPF 事件边界，但在最终 `catch` 中记录真实异常；取消任务命令改为受保护的 `Task`，确认、Worker IPC 和刷新均在同一异常边界内，避免宿主 Dispatcher 收到未处理异常。
- 新增回归断言覆盖事件入口与取消命令形态；当前验证为 Core 13、Worker 21、Playnite 34，共 68 项测试通过。真实 Playnite 运行时回归仍受 `ENV-001` 阻塞。

### 2026-08-01 Dashboard 共用命令异常边界

- 覆盖 Dashboard 真实业务命令的 `Run(Func<Task>)` 已不再是 `async void`；命令入口观察 `RunAsync` 的最终故障，确保未来异常不会静默成为未观察 Task。
- 失败反馈为分层降级：页面状态始终更新；正常情况下继续调用插件真实错误通知；通知层异常时记录原始业务异常和通知异常，绝不伪造成功或让异常回流至 Playnite Dispatcher。
- 本轮验证：Release 0 警告/0 错误，Core 13、Worker 21、Playnite 34，共 68 项测试通过；源码门禁、UI Skill（0 errors）、PEXT 打包与 Worker `0.6.22.0` smoke 通过。隔离 Playnite 加载验证仍需 `ENV-001`。

### 2026-08-01 大型滚动布局审计

- Dashboard 的 DataGrid/ListBox 已由 XML 结构测试保护：不允许置于纵向 `StackPanel` 或外层 `ScrollViewer`，必须保留有限 `Grid` 测量；ListBox 还要求 `Recycling` 和逻辑滚动，DataGrid 必须继续使用共享表格样式。
- UI Skill 的项目内 10 条“StackPanel 邻近大型控件”提示已核实为数据模板/同级文本容器的保守正则匹配；不以无意义的 XAML 重写消除它们。实际 100%--200% DPI 仍须 `ENV-001` 中隔离 Playnite 验证。

### 2026-08-01 WPF-UI 生产资源解析边界

- Playnite 的实际 Dashboard 加载已证明：`WpfUiProduction.xaml` 不能把 WPF-UI 默认类型样式仅放在 Dashboard/Settings 的同级 `WpfUiBase.xaml` 合并字典中；该布局会在解析 `BasedOn="{StaticResource {x:Type ui:Button}}"` 时抛出 `XamlParseException`，资源名为 `Wpf.Ui.Controls.Button`。
- `WpfUiProduction.xaml` 必须自行先合并 `WpfUiBase.xaml`，使类型键和适配器在同一字典解析作用域；Dashboard/Settings 仅合并 DesignTokens 后的 Production 字典，避免重复或依赖合并顺序。任何新增的生产 WPF-UI 类型适配器都遵守此边界。
- `WpfUiProduction.xaml` 不得以 `StaticResource` 使用父级 `DesignTokens.xaml` 的 `Gsc*` 令牌（已证实 `GscSoftShadowColor` 与 `GscSharedFocusVisual` 会在 Playnite BAML 解析期失败）；必须用 `DynamicResource` 让它们在控件加入 Dashboard/Settings 的 UserControl 资源树后解析。不得为了规避此问题把 DesignTokens 合并进 Production，因为会固定或遮蔽宿主的自适应主题调色板。
- 该规则由 STA `XamlReader.Parse` 回归测试覆盖。测试只证明资源可被 WPF 解析，不能替代隔离 Playnite 的主题、DPI、Dialog/Snackbar 或宿主污染验证；未经授权不得覆盖正在运行的用户插件目录。
- `.tmp/playnite-ui-test` 即使含本地 `config.json` 和测试扩展目录，也不能视为隔离真机环境：一次启动尝试只暴露出 `D:\software\Playnite\Playnite\Playnite.DesktopApp.exe` 的用户窗口；随后官方 `--userdatadir` 启动只在 `.tmp` 创建日志并因 `Application already running, shutting down.` 立即退出。不得操作用户窗口；必须先证明独立启动 PID、配置/扩展数据根及日志均不触及用户 AppData，才能继续 UI 真机测试。

### 2026-08-01 UI-004 生产 WPF-UI 资源与回退边界

- `Themes/WpfUiProduction.xaml` 是生产框架控件的唯一适配层；它只能在 Dashboard/Settings 的 `UserControl.Resources` 中、且在 `DesignTokens.xaml` 之后合并。禁止将其注入 `Application.Current.Resources` 或调用会改变 Playnite 宿主主题的全局 API。
- 低密度 Card、Button、ToggleSwitch、普通 TextBox/ComboBox 使用 WPF-UI；数值校验输入、高密度 DataGrid/ListBox、搜索清除按钮和安全兜底浮层继续保留原生 WPF。迁移不得改变 Command、Binding、Automation Name、Tooltip、键盘路径或 Recycling 虚拟化。
- `GscWpfUiActionButton`、Toolbar、Context 等适配样式必须保留原按钮的 Margin、紧凑高度和“禁用时隐藏”行为；不能把 `ui:Button` 样式应用到原生 `Button`，反之亦然。
- Playnite 内嵌页面绝不能声明 WPF-UI `ContentDialogHost` 或构造 `ContentDialog`：它是 Window 级单例，Dashboard、Settings、探针或其他扩展的任意重复注册都会令宿主崩溃。通知可使用页面局部 Snackbar；确认必须使用 Dashboard 的插件内 Dialog，设置导入报告使用 MessageBox。错误通知的详情入口不可删除，重叠确认必须安全取消而不能堆叠模态层。
- 设置导入/导出的文件元数据、读取和写入不得阻塞 UI 线程；UI 依赖对象和 DataContext 更新仍在 Dispatcher 线程。生产事件边界不得新增不可控 `async void`。
- 本批已具备 Windows/.NET SDK 8.0.423 的 Release build（0 警告/错误）与 66 项自动化测试证据；真实 Playnite、DPI、主题、键盘和宿主污染仍需 ENV-001，不能由构建、STA 资源加载测试或包内容检查替代。

### 2026-08-01 UI-003 自适应布局与验证边界

- Dashboard 侧栏导航必须位于有限高度的 `ScrollViewer` 内；紧凑模式只隐藏工具栏可见文案，不能隐藏其 Automation Name、Tooltip、命令或键盘激活能力。
- Settings 整页可纵向滚动；窄宽度必须提供横向访问而不是裁切固定列。响应式代码只可从 `SizeChanged`/Loaded 等 UI 事件更新依赖对象。
- 装饰性环境光使用元素自己的 `RenderTransform`，焦点环使用控件边界内的可见 Border；不得以负 Margin 修补布局。Blur 仍只限少量固定环境光，不能进入列表/表格行。
- UI-003 已有源码/构建/测试证据，但真实 Playnite 的 DPI、主题、键盘、窗口缩放和绑定异常证据尚缺。`.tmp` 复制安装不能证明数据隔离，必须先满足 ENV-001。

### 2026-08-01 UI-002 共享控件边界

- 通用阅读表面必须基于 `GscSurface`，设置页不得重新复制玻璃、描边和阴影；页面只保留布局性 Padding/Margin。
- `GscSlider`、`GscPrimaryButton`、`GscButtonBase`、输入、下拉、复选框、滚动条、Tooltip、ProgressBar 与焦点环由 `DesignTokens.xaml` 集中提供。新增或改动这些控件时必须同步维护 `check_shared_wpf_control_guards()`，并保持主题资源为 `DynamicResource`。
- 按压反馈仅在模板实例的 `RenderTransform` 上缩放；不能把可动画的 Transform 放到 Style Setter 中。不要为静态验证使用假进度或 `Task.Delay`。
- UI-002 只有源码、构建、自动化和包内容证据；UI Skill 的项目级静态审查仍列出既有 Dashboard 布局提示。实际 Playnite/DPI/主题/键盘渲染须由独立数据根完成，不能以 `.tmp` 安装副本或用户实例替代。

### 2026-08-01 治理与 UI 基线

- 无人值守工作必须由 `docs/AUTONOMOUS_BACKLOG.md` 中可追踪的条目驱动，并遵守 `docs/AUTONOMOUS_DEVELOPMENT_RULES.md` 和 `docs/QUALITY_GATES.md`；没有 `READY` 条目时先审计和登记，不能隐式扩大实现范围。
- UI-001 的首个基线缺陷是 Settings 不能依赖 DashboardView 的局部资源或代码后置事件。通用 `GscButtonBase` 应迁入共享字典且不得携带 View 专属 `EventSetter`；Dashboard 可保留动画专用局部样式。
- `GscErrorTintBrush` 是 `GscTextBox` 的直接错误填充资源，必须存在于共享主题令牌中。数值输入门禁应匹配嵌套属性路径（如 `SelectedGame.Policy.DuringPlayIntervalMinutes`）而非仅裸字段名。该问题待 UI-001 处理。

### 2026-08-01 WPF-UI POC 约束

- WPF-UI 4.3.0 具备 net462 资产并通过 Release 构建；资源只能由 GameSaveCenter 子树的 `UserControl.Resources` 合并，禁止注入 Playnite 全局资源或修改宿主 Chrome。
- POC 的 Dialog/Snackbar 构造与显示必须整体处于可测试的异常边界内；异常记录到 Playnite 日志并显示局部错误面板，记录/显示回调再次失败时退化到 `Trace`，不得从 `async void` UI 事件逃逸。
- 打包必须显式包含 Wpf.Ui、Wpf.Ui.Abstractions、System.Memory、System.Buffers、System.Runtime.CompilerServices.Unsafe 与 System.ValueTuple；否则实际 Playnite 会在加载资源时缺程序集。
- 当前只完成静态、构建、测试和包内容验证。用户现有 Playnite 正在运行，未经独立实例和隔离目录许可不得停止进程或覆盖插件，故 POC 的实际宿主加载尚未验证。
- 任何可选 UI 框架探针均不得作为 Dashboard XAML 的即时子控件；必须由明确的用户操作惰性创建，并在构造或资源解析失败时记录错误、保留主页面和可见的重试入口。

### 0.6.22 主题令牌边界

- WPF-UI 生产适配层必须自己提供圆角输入模板：`GscWpfUiTextBoxTemplate`、`GscWpfUiComboBoxTemplate` 和隐式 `ComboBoxItem` 样式；不能让 Playnite 宿主重新注入默认白色 Popup。
- TextBox 模板中的 `PART_ContentHost` 必须绑定水平/垂直滚动条可见性，否则诊断摘要和长路径在有限高度下会被裁切；按钮使用 `ui:Button.CornerRadius`，原生复选框使用 `GscCheckBox`。

- 页面不得重新引入 `#RRGGBB` 或 `#AARRGGBB` 装饰色；语义状态、环境光、图标容器、提示面、主按钮和阴影必须从 `Themes/DesignTokens.xaml` 获取。
- Playnite 页面级环境光只允许少量固定 Ellipse，且高对比度或关闭毛玻璃时由现有主题逻辑隐藏；列表、表格和滚动行不得新增 `BlurEffect`。

### 0.6.22 全局 GamePicker 详情加载边界

- 语义状态面（恢复提示、风险提示、错误淡色、取消状态和环境光）必须由 `AdaptiveThemePaletteFactory.ApplyAccentResources` 统一生成；不得只依赖 `DesignTokens.xaml` 的静态默认色。高对比度下使用实色降级，透明关闭时不保留半透明材质或 BlurEffect。
- 全局 GamePicker 只允许一个 `GamePicker.ItemsView` 列表入口；六个物理工作区不得复制全局游戏搜索/筛选器。
- GamePicker 批量替换大量轻量摘要时必须抑制逐项 `ObservableCollection` 通知并在完成后发出一次 `Reset`；不要在 `ICollectionView.DeferRefresh()` 期间逐项修改集合，否则 WPF `ListCollectionView` 会抛出 deferred refresh 异常。列表仍必须保持虚拟化与 Recycling。
- GamePicker 选择变化必须使用单一 `SelectedItem` 事件入口；`SelectedGame` 仅作为兼容绑定属性，不得再次触发同一详情加载。
- 详情 IPC 不支持撤回已写入命名管道的请求，因此存档、媒体和修改器详情必须使用代际令牌与当前 `PlayniteId` 检查，旧响应只能被丢弃，不能回写新游戏界面。
- Dashboard 卸载必须取消并释放详情加载、GamePicker 防抖和延迟设置保存；这不是业务任务取消，不能影响 Worker 中真实备份/恢复/同步任务。
- 工作区 DataGrid 至少保留 180 DIP 的可读高度并使用内部滚动；媒体来源规则的表单可以局部滚动，但 DataGrid 必须处于星号行和有限容器内，不能放回无限测量的 StackPanel/ScrollViewer。

### 0.6.21 云端重试与 WPF 数值编辑边界

- 只有本地备份已成功、后续安全单向 Rclone copy 失败才进入 `cloud_retry_queue`；恢复、删除、覆盖和远端镜像不属于该队列。
- 原始失败后最多自动重试六次，退避为 1、5、15、60、240、720 分钟；成功必须移除队列，耗尽后保留游戏失败状态和审计记录，手动云端重试仍可重新开始安全链路。
- 自动化回归必须覆盖策略上限、旧 SQLite 数据库的表/索引迁移、跨重启恢复、成功清队列和配置缺失时的延后扫描。
- Rclone 未配置或本地备份目录不可用时 Worker 不应创建反复失败任务；意外的配置竞态至少延后五分钟再检查。
- 绑定到整数的 WPF 数值输入必须使用完整值提交（`LostFocus`）和 `IntegerRangeValidationRule`，不能恢复成 `UpdateSourceTrigger=PropertyChanged`；策略分钟框最小宽度为 84 DIP 以上。
- `GscTextBox` 模板必须对 `Validation.HasError` 提供直接可见的错误色边框与填充；不能只依赖在部分 Playnite 画面中不明显的 Validation Adorner。

### 0.6.20 Dashboard UI 线程边界

- Worker 任务事件、定时器和其他异步续体可以在非 UI 线程触发 `DashboardViewModel.PropertyChanged`；View 订阅者必须先执行 `Dispatcher.CheckAccess()`，不得先读 `IsLoaded`、控件或依赖属性。
- `RequestBackgroundRefreshAsync` 与 `RefreshAfterSynchronizationAsync` 必须返回 `Task`。定时器和 Worker 事件回调负责等待任务，不能把这两条后台路径改回 `async void`。
- 后台刷新对 `IsBackgroundRefreshing`、`StatusMessage` 及其他绑定属性的读写必须经 `ApplyOnUi`；这条约束不因当前调用者恰好来自 DispatcherTimer 而放松。

### 0.6.19 媒体控制与保留清理边界

- 媒体全局开关按来源拆分：Steam、Xbox Game Bar、Windows Screenshots、游戏相邻目录和自定义来源。默认均为启用，旧 JSON 缺字段时必须保持此兼容默认值。
- `game_policy.Enabled` 只控制自动动作；用户明确发起的备份、恢复、校验和媒体同步不可被它静默阻止。游玩中媒体同步必须独立于游玩中备份启用状态。
- 删除自定义媒体来源只影响 SQLite 中的规则，绝不可删除来源目录、原始媒体、归档副本或收件箱记录。
- 保留策略当前只能预览，不能以文件系统猜测 Ludusavi Vault 布局去删除版本。只有 Ludusavi 提供稳定的单版本删除契约后，才能实现真正的自动清理任务。

### 0.6.18 任务事件推送边界

- Worker 通过独立、当前用户范围的 `GameSaveCenter.Worker.Events.v1` 向已打开的管理面板推送任务状态。
- 该事件通道只是最佳努力体验增强：每个订阅者仅有 128 条有界队列，过慢的 UI 丢弃最旧进度，绝不反压真实任务。
- 正确性仍来自 SQLite、`tasks.changes` 与 `tasks.changes.wait`。Worker 重启、事件管道关闭或错过事件后必须重新对齐快照，禁止将事件流当作唯一事实来源。

本文档用于跨会话、上下文压缩或更换开发者时恢复完整项目意图。修改需求、架构、完成状态或安全边界时，必须同步更新本文档和 `DEVELOPMENT_PROGRESS.md`。

完成度百分比与剩余功能必须以 `FEATURE_COMPLETION_ASSESSMENT.md` 为准，并始终区分源码覆盖、真机验证和安全可用度。

### 0.6.13 远端恢复安全边界

- 远端恢复必须分成“隔离下载并校验”和“明确确认后恢复”两步，不得根据冲突决策自动执行。
- Rclone 只允许从已选择设备的 `Saves` 子树 copy 到 `DataDirectory/RemoteBackups/<opaque-id>/Vault`；设备名、ID 与规范化根路径必须阻止路径穿越。
- 下载完成后必须通过 Rclone 哈希检查，并由 Ludusavi 从隔离库确认所选游戏与 Backup ID；任一失败都不得签发可恢复暂存。
- 已验证暂存七天过期。恢复前仍在本机正式备份库创建并锁定 PreRestore；目标恢复从隔离库读取，失败回滚从本机库读取。
- 点击恢复后必须在创建 PreRestore 前重新校验远端与隔离库及 Backup ID，不能只信任暂存时的历史结果。
- 真实 Rclone 后端和低风险游戏的端到端验证完成前，不得声称远端恢复已达到稳定版安全等级。

### 0.6.12 媒体预览资源边界

- 媒体缩略图只允许在虚拟化容器可见时请求；96px 列表图和 480px 选中预览共享最多 96 项的文件版本 LRU。
- 缓存图像必须 OnLoad 后 Freeze 并关闭流，不能阻止原始或归档文件被移动、替换或删除。
- 同一时间只允许选中项的一个内嵌视频预览，默认静音；完整播放继续交给系统文件关联。
- WPF `MediaElement` 受 Windows Media Foundation 编解码器限制，加载失败不得影响元数据、定位或系统打开入口。

### Playnite 官方更新边界

- 插件不得在 Playnite 运行期间下载后自替换 DLL；插件不能热重载，官方安装会整体替换扩展目录。
- 自动更新的受信任入口是 Playnite Add-ons 数据库。仓库维护 installer manifest 和待提交的 add-on manifest，版本、扩展 ID 与 PEXT 文件名由静态门禁校验。
- 只有 GitHub Release 资产已匿名可下载且官方数据库 PR 已合并，才能声称自动更新上线；此前只能称为“发布准备完成”。

### 0.6.11 模块拆分约束

- `SqliteStateStore` 与 `DashboardViewModel` 采用按领域 partial 渐进拆分；移动代码时不得顺便改变 SQL、IPC、绑定名或安全语义。
- 源码门禁必须聚合读取 `SqliteStateStore*.cs` 与 `DashboardViewModel*.cs`，不能重新假设所有实现位于单文件。
- 当前已拆出媒体域；后续优先拆备份/设备/修改器领域，并用现有集成测试验证行为不变。

### 0.6.10 设置迁移测试边界

- Playnite 插件继续目标 net462；设置测试宿主可使用 net472 以兼容当前测试运行器，但不得反向提高插件运行要求。
- 可移植设置导入必须先完整反序列化并验证，再复制到编辑对象；未知架构、枚举、越界值或超大输入失败时不得留下部分修改。
- 缺失路径迁移报告只观察文件系统，不得创建目录、下载程序或改写路径。
- 一键构建必须同时运行 Core、Worker SQLite 和 Playnite 设置迁移测试。

### 0.6.9 多设备冲突决策记录

- “优先本机/远端”只是持久化的人工判断，不授权 Worker 下载、恢复、删除或覆盖任何备份。
- 决策键是 Playnite 游戏 ID 与远端设备名；新的 sidecar 比较可继续展示历史判断和备注。
- 真正执行远端恢复前仍必须另行设计下载校验、PreRestore、显式确认和失败回滚。

### 0.6.8 媒体批量元数据

- 批量媒体操作只允许收藏和备注元数据，禁止从多选能力推导批量移动、覆盖或删除授权。
- 批量请求必须限制为 1–500 个去重 ID，并在一个 SQLite 事务中完成；部分记录缺失时整体回滚。
- Worker 专属 SQLite 测试使用 Windows TFM 独立项目，不能为了引用 Worker 而把跨平台 Core 测试改为 Windows-only。

### 0.6.7 媒体页崩溃防护、检索与预览

- WPF `Run.Text` 的所有数据绑定都必须显式使用 `Mode=OneWay`，包括可写 DTO 属性；同一模板中属性可写性变化时不能重新引入宿主级崩溃。
- 源码门禁的正则自身必须用已知缺陷样本验证，不能只因验证脚本退出码为零就认为规则真正匹配到了 XAML。
- 需要由 Windows PowerShell 5.1 直接解析的无 BOM 脚本保持 ASCII 输出文本，避免旧版 PowerShell 按本地代码页读取 UTF-8 后破坏字符串与语法。
- 0.6.6 媒体统计功能的真机回归必须使用 0.6.7 或更高版本；0.6.6 打开媒体页可能带崩 Playnite。
- 当前游戏媒体通过 `ICollectionView` 在内存中按文件名、备注和来源即时过滤，不重新请求 Worker。
- 筛选支持全部、截图、录像和收藏；筛选只影响当前视图，不修改 SQLite。
- 只对当前选中的截图创建最大宽度 480 像素的冻结预览，采用 `OnLoad` 后释放文件句柄；禁止为 1000 个列表项同时解码原图。
- 录像及不受 WPF 解码器支持的格式继续使用系统默认程序打开，不在 Playnite UI 线程实现视频播放器。

### 0.6.6 设置迁移与媒体元数据

- 设置导出采用 `SchemaVersion=1` 的 JSON 包；导入只修改设置编辑副本，必须由 Playnite 保存流程持久化并同步 Worker。
- 导入报告只检查新机器上的可执行文件和目录是否存在，不自动创建目录、不复制程序，也不包含 Rclone 密码。
- 媒体统计必须通过 SQLite 聚合查询，禁止为了显示数量和容量先加载所有媒体行。
- 收藏和备注是非破坏性元数据；“打开媒体”使用系统文件关联，“在目录中显示”只定位归档副本。
- 任何媒体清理、批量删除或覆盖仍需单独的回收站与确认设计，不能从元数据能力推导授权。

### 0.6.5 任务事件、云端重试与修改器入口选择

- `tasks.changes.wait` 是本地命名管道上的有界长轮询：Worker 在任务状态变化时唤醒等待者；超时、Worker 重启或游标丢失时，客户端继续以 SQLite 快照恢复正确状态。
- `cloud.upload.retry` 只重复安全的单向 `rclone copy`，不得调用 Ludusavi 重新备份，也不得引入 `sync/delete/purge/move`。
- 修改器 ZIP/目录有多个候选 EXE 时必须由用户明确选择；不能再按体积最大的文件静默绑定。检查阶段和实际解压阶段都必须保留 Zip Slip、条目数和展开体积限制。
- 一个工具的多个版本必须保留并可从 Inspector 选择活动版本；切换后需显式保存，默认不自动启动。

### 0.6.2 多设备只读摘要

- 设备状态只保存每游戏最新备份的 ID、时间、总大小、文件数和 Playnite 游戏标识；绝不包含存档内容、路径或云端凭据。
- 用户从维护中心手动刷新时，Worker 原子写入本机 sidecar，使用 Rclone `copy` 上传，并只用 `lsf/cat` 读取其他设备 sidecar。
- 冲突结果仅供人工决定；没有远端下载、恢复、删除、同步或自动选择赢家的实现。

### 0.6.1 可靠性与可解释关注项

- 首页“需要关注”指标是可操作入口：有 Warning/Error finding 时切换到维护中心的异常与日志，并选中具体项，展示游戏、原因与建议处理方式；没有关注项时只给出明确状态提示。
- Dashboard 的高频刷新先读取 `tasks.changes` 内存增量；只有任务变化、Worker 重启造成的游标失效或一分钟的摘要刷新周期到达时才重新拉取完整首页快照。
- 恢复会检查实际活跃会话和 SQLite 中记录且仍存活的游戏进程；恢复取得全局上传闸门，等待正在进行的 `rclone copy` 完成并阻止新上传，绝不强杀上传进程。
- FLiNG 下载限制单文件最大 2 GiB；导入 ZIP 限制条目数、单条目与总展开大小，并在失败时删除本次新建的安装目录。真实安全软件隔离和超大包边界仍需 Windows 真机回归。

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
- 用户提供的 `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md` 是后续 UI 设计与实现的长期基准；所有新增控件和改版必须同时通过 `docs/design/UI_CHANGE_GATE.md`。

## 当前已经进入仓库的实现

### 0.5.9 WPF 控件几何与搜索规则

- ScrollBar 的 Thumb 不得使用会在短边上归一化成透镜形的超大 CornerRadius；纵向和横向必须分别限定厚度与最小长度。
- Dashboard 必须接管完整 TabControl 模板，禁止仅修改 TabItem 后继续叠加 Playnite 宿主标签线或默认 Chrome。
- DataGrid 圆角外框必须与首末列 Header 的上圆角一致；表格锁定/选择列必须使用共享主题复选框。
- 含图标按钮必须使用双列/统一 ContentPresenter，并显式水平、垂直居中；不得依赖字体图标默认基线。
- 搜索占位文本仅在“文本为空且未获得键盘焦点”时显示；输入后必须出现统一清除按钮，清除后保留输入焦点。

### 0.5.8 WPF UI 收口与插件内反馈

- 所有滚动条必须使用方向独立的圆角 Thumb 模板，纵向 Thumb 不得继承横向最小宽度。
- DataGrid 默认居中，路径/详情列显式左对齐并提供 Tooltip；状态灯、文字、进度条必须同一基线。
- Playnite 宿主窗口按钮区域必须通过顶部安全列避让，禁止页面操作按钮贴右边缘。
- 确认、结果和后台通知优先在插件内容宿主内展示；面板关闭时才回退 Playnite 原生通知。
- 紧凑侧栏 Logo 使用固定安全盒和 Uniform 矢量缩放，不得随列宽裁切。

### 0.5.7 大型游戏库缓存优先

- Dashboard 打开时必须先展示 SQLite 持久化快照，不得等待 Playnite 全库同步或 Ludusavi 逐游戏匹配完成。
- 相同游戏库同步请求按指纹合并；Worker 只对新增、匹配输入变化或超过七天冷却期的未匹配游戏调用 Ludusavi。
- Dashboard 游戏摘要必须使用批量聚合 SQL，禁止为每款游戏分别读取完整备份、媒体和策略。
- 备份历史默认读取 SQLite 索引；缓存为空、用户显式刷新或真实备份/恢复完成时才与 Ludusavi 校准。
- 详情按当前一级工作区懒加载，Ludusavi 版本在 Worker 内缓存六小时。

### 0.5.6 UI 设计系统收口

- WPF 选中态必须显式保持主题主/次文字颜色，禁止继承宿主的黑色 `HighlightText`；鼠标与键盘焦点使用圆角强调环，不删除键盘可访问性。
- Dashboard 的统一响应式边界是 1280 / 980 / 880 DIP；媒体、存档和修改器属于游戏作用域，任务与维护始终使用完整工作区。
- 用户界面不得直接展示 DTO/C# 类型全名或把修改器原始长文件名作为主标题；内部值必须映射为可理解文本，原值只进入 Tooltip 或技术详情。
- 媒体中心的待归类、当前媒体和来源规则是三个独立局部工作区；任务中心筛选通过集合视图完成，不改变 Worker 的真实任务数据。
- 滚动条、进度条、复选框、下拉框和焦点态必须使用共享模板，禁止重新漏出宿主默认白色或方块控件。

### 0.5.5 后台任务反馈与周期语义

- 任务完成通知必须由插件生命周期级监测负责，不能依赖 Dashboard 是否打开；监测只读取轻量任务列表，不得因为显示通知而自动启动已被用户关闭的 Worker。
- 成功通知必须保留 Worker 的真实 `DetailMessage`，尤其不能丢失“游玩中定时备份：存档无变化，历史未新增”；任务按 TaskId 去重，插件启动时禁止补发历史任务。
- 重要即时操作需要明确成功反馈，队列任务由最终状态反馈；高风险恢复继续保留操作前确认，普通成功通知不得抢夺前台游戏焦点。
- 游玩中备份的间隔是固定调度周期：检查允许最多约 5 秒迟到，但下一周期仍从原计划时间递推，不能累计漂移。
- 运行中改变间隔、关闭后重新启用游玩中备份时，从变化时刻重新倒计时；上一轮定时备份未结束时不允许堆积重叠请求。

### 0.5.4 崩溃防护与游玩中备份

- WPF `Run.Text` 绑定必须显式为 OneWay，绝不能让只读 DTO 显示属性落入默认 TwoWay；否则虚拟化列表创建时会抛出 `XamlParseException` 并可能带崩 Playnite。
- 每游戏 `DuringPlayIntervalMinutes` 的产品约定是 1–1440 分钟，UI、IPC 持久化、Worker 配置和会话调度必须完全一致。
- 只有 Ludusavi 报告 `New` 或 `Different` 时才应新增历史；`Same` 是成功的无变化备份，不得伪造新版本。
- Backup 任务必须携带用户可见的触发来源（手动、游玩中定时、退出后），否则用户无法区分“未触发”和“成功但无变化”。

### 0.5.3 紧凑布局与表格可用性修复

- Compact 导航只保留图标与 Worker 状态灯；不得保留会被裁切的状态文字。
- 任何自定义 ScrollBar 模板必须将 `Minimum`、`Maximum`、`Value` 和 `ViewportSize` 绑定到 `PART_Track`，并按 `Orientation` 单独处理尺寸与页面命令。
- 媒体中心是数据密集型页面：使用局部纵向 ScrollViewer，待归类和已归类表格均至少保留约四行的可见高度，不能由前置自动行挤成单行。
- FLiNG 搜索结果和右侧版本必须联动：搜索完成及用户切换目录项后均自动加载版本。
- 自动轮询禁止插入或移除布局元素，也不得触发选择动画；否则会造成可见抖动。

### 0.5.2 模块化自适应 UI

- 一级导航真正决定工作区内容：存档中心只保留历史和候选路径，修改器只保留工具页，媒体只保留媒体页，任务/维护不保留游戏浏览器。
- 宽度断点为 1320 / 1050 / 880 DIPs；Compact 模式不再维持三栏，改为顶部当前游戏选择器。
- 备份策略由“策略”按钮按需展开；正常状态不再重复出现在底部；自动刷新进度不再进入布局流，避免页面震动。
- 共享控件资源拥有 ComboBox、Popup、滚动条和进度条，第三方 Playnite 主题不能回退为白色系统控件。

### 0.5.0 修改器中心

- 修改器与 CT 以 Playnite GameId 绑定，不依赖会变化的游戏名。
- FLiNG 只是一种在线来源；本地导入、启动和生命周期不依赖在线目录是否可用。
- 目录适配器只访问 `https://flingtrainer.com`，搜索使用 SQLite 缓存，UI 不打开浏览器或 WebView。
- 所有新工具默认 `AutoStart=false`。自动启动失败不阻断游戏，常见反作弊线索默认阻止自动启动。
- ZIP 解压路径必须经过 `ArchivePathGuard`，越界条目整体拒绝。
- 删除 UI 操作为解除绑定并保留文件；避免在尚未完成回收站设计前静默删除用户修改器。

### 0.4.3 启动与响应式界面修复

- 0.4.2 的本机配置曾把 `WorkerExecutable` 写成 `ludusavi.exe`，导致每次健康检查都打开 Ludusavi GUI，随后以退出码 0 结束并触发下一次刷新重试。
- 设置加载会识别这一特定混淆：把有效 Ludusavi 路径迁到 `LudusaviExecutable`，并恢复插件包内的 `Worker/GameSaveCenter.Worker.exe`。
- Worker 启动器只接受正确文件名，使用进程内信号量合并并发启动，并在启动前就创建可诊断日志。
- Dashboard 的搜索框独占筛选首行；ComboBox 使用完整深浅主题模板；Tab 内容明确 Stretch。
- 高度不足 800 DIPs 时收起统计卡片，把垂直空间留给主工作区；紧凑宽度使用固定最小游戏列表宽度和更小栏间距。

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
- 共享目录由全局媒体任务单次扫描：文件名唯一匹配或明确且无重叠的会话时间窗口才自动归类；其余媒体复制到 `_Inbox/Pending`，并持久化 `Inbox/Assigned/Ignored` 状态与可解释原因。
- Playnite 媒体页提供全局待归类列表、目标游戏选择、人工归类和“忽略并保留副本”；人工操作只移动归档副本，原始截图/录像不被删除。

### 云端与识别

- Rclone 只提供 `copy/check` 适配，受全局和每游戏上传开关共同控制。
- 有界候选路径扫描、可解释评分、Xbox WGS 辅助候选和 Ludusavi 自定义规则草案。
- 未匹配游戏默认在启动时记录有界文件快照，退出后对比新增/修改文件并沉淀候选；用户可接受生成草案或忽略，候选不会静默生效。
- 多设备冲突核心判定算法已存在，但远端设备摘要摄取尚未形成闭环。

## 当前明确未完成或待验证

1. 当前交付环境没有可用 .NET SDK/MSBuild，因此最新 0.4.1 改动只能做结构校验；早期版本已由用户在 Windows 完成 restore/build/test/package 和 Playnite 加载。
2. Windows 已验证游戏库、运行状态、Ludusavi 匹配和测试存档备份；ZIP 多版本、安全恢复、Rclone、真实媒体来源与 MOD 复杂会话仍需端到端回归。
3. Worker → Playnite 已采用 `tasks.changes.wait` 信号唤醒长轮询提供近实时任务通知；它不是无限期双工连接，Worker 重启或游标失效时必须回退 SQLite 快照。
4. 公共截图目录已接入文件名、无重叠会话时间窗口和全局未识别收件箱；复杂并发会话、真实文件时间语义、超大公共目录性能和媒体预览仍需 Windows 数据调优。
5. “游戏启动前快照 + 退出后差异”的默认会话闭环已经接入，但目录深度、扩展名、性能和评分阈值仍需使用真实未匹配游戏调优。
6. 多设备冲突尚缺 Rclone 远端 sidecar/摘要读取和完整 UI。
7. 未知进程/MOD 启动链尚缺人工学习并持久化映射的 UI。
8. 智能保留只预览，不自动删除；恢复云端闸门已实现，但 Rclone 的游戏级上传/校验状态、失败重试和多设备冲突闭环仍未完成。

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
4. 实现 Worker 主动任务事件推送和远端设备摘要；回归并调优现有媒体收件箱、公共截图会话归类与前后快照候选链路。
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

## 2026-07-27 0.3.5 真机反馈记忆

- 用户库约 965 款游戏，游戏列表必须具备搜索、状态筛选和排序，不能依赖长列表滚动。
- Wo Long 备份任务已真机成功，磁盘确认存在 ZIP 和 `mapping.yaml`；“历史为空”属于索引/刷新链路问题，不是备份引擎失败。
- 底部错误明确指出 `TaskStatusDto.DurationDisplay` 被错误地以可回写模式绑定，并导致自动刷新停用。
- Playnite 社区主题不止浅色/深色两类。后续 UI 禁止依据单个文字色简单二分主题，必须验证宿主背景与文字对比度并派生控件色板。
- 用户认为文字模糊主要是 WPF 渲染/DPI 问题。避免整体文字 Opacity 和悬停缩放，保持整数像素位移、布局取整和 ClearType。
- 空闲状态不得保留无内容的进度条轨道；状态消息与任务进度必须分区并留出间距。

## 2026-07-27 0.4.0 UI 规范、自动候选与任务操作记忆

- 用户上传的完整 UI 设计提示词已原样保存到 `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`，不能只当作一次性聊天提示。
- `docs/design/UI_CHANGE_GATE.md` 是强制门禁：真实 WPF、集中设计令牌、克制玻璃、主题/高 DPI/可访问性、真实任务状态、版本安装核验均不可绕过。
- 外观设置支持 `FollowPlaynite / Light / Dark`；第三方主题颜色不稳定时允许固定局部浅色或深色，但不改变 Playnite 外壳。
- 未匹配游戏开始会话时异步生成有界文件快照，退出后按目录聚合新增/修改文件并交给 `SaveCandidateScorer`；仅生成候选，不自动改 Ludusavi。
- 候选路径在 SQLite 中持久化，详情页显示判断依据和状态；接受路径后生成规则草案，忽略后保留审计，已接受路径不会被后续扫描重置。
- Worker 启动时清理超过两天的孤立检测快照，避免异常退出长期留下文件。
- 失败/取消的 Backup、MediaSync 支持安全重试；恢复等高风险任务禁止通用重放。任务详情可复制完整错误和任务 ID。
- Game Bar/Windows 共享媒体目录在明确 SessionId 且没有其他游戏会话重叠时，可按会话开始前 2 分钟至结束后 10 分钟的窗口辅助归类；重叠时只保留文件名匹配，避免猜测。
- 0.4.0 在本容器只通过静态校验，Windows 必须重点验证会话启动性能、候选准确率、主题模式切换和重试按钮命令状态。


## 2026-07-28 0.4.1 全局媒体收件箱记忆

- 原 0.4.0 只有未归类数量字段，公共目录无法唯一归类的文件会被跳过，缺少真实可处理对象。
- 0.4.1 新增 `media.classification_state` 与 `media.classification_reason`，旧数据库先补列、规范化记录，再建立分类索引，避免升级时引用不存在字段。
- Game Bar、Windows Screenshots 与共享自定义目录统一由 `MediaInbox` 全局任务扫描，避免按每个游戏重复遍历同一目录。
- 自动归类必须满足：文件名只命中一个游戏；或请求带明确 SessionId、时间位于开始前 2 分钟至结束后 10 分钟、且无其他游戏会话重叠。文件名多游戏歧义会显示明确原因。
- 无法唯一判断时复制到 `_Inbox/Pending`，首轮每次最多新增 200 个待处理项，防止历史截图一次性淹没界面；后续扫描依靠 SHA-256 跳过已导入项并逐批补齐。
- 人工归类移动归档副本到目标游戏目录；若归档副本意外缺失，只从原文件重新复制，永不移动或删除原始媒体。忽略操作移动到 `_Inbox/Ignored`，仍保留副本与审计。
- 新增 `media.inbox.list`、`media.inbox.ignore` IPC；原 `media.reassign` 从数据库字段更新升级为真实文件移动与校验。
- 0.4.1 已通过跨平台结构、XAML、IPC、媒体收件箱门禁、Git diff 和对象完整性检查，但尚未执行 Windows `dotnet build/test/package` 与 Playnite 真机加载。
## 2026-07-28 0.4.2 侧栏资源崩溃记忆

- Windows 真机已确认 0.4.1 能编译、安装并被 Playnite 10.56 识别；失败点不是插件发现或 Worker 启动，而是点击侧栏后创建 `DashboardView`。
- 崩溃堆栈：`DashboardView.InitializeComponent()` -> `XamlParseException` -> 找不到区分大小写的静态资源 `GscStatusPill`。
- `StaticResource` 在 XAML 加载时必须解析成功；不存在会使整个侧栏构造失败。`DynamicResource` 名称缺失通常不会立即抛同类异常，但会造成样式丢失，也必须纳入门禁。
- 0.4.2 新增 `GscStatusPill`，并去除不存在的 `GscCardBrush/GscHairlineBrush` 引用。
- 后续任何 UI 改动除结构和 TargetName 检查外，还必须验证全部 `Gsc*` 资源引用能够在当前 XAML或共享 Themes 字典中解析。


## 2026-08-02 UI-005 最终视觉重构记忆

- 用户确认此前“UI 收口”视觉变化不足，最终目标改为在现有 WPF/Playnite 平台上重做信息架构，同时不增加或减少功能。
- HTML 只作为视觉/响应式原型，绝不能进入产品。原型暴露的 Standard 标题按钮重叠、Compact 导航与 Worker/Ludusavi 状态卡越界，已转化为生产 XAML 的硬门禁：独立 Grid 行、固定紧凑尺寸、模板级居中、侧栏裁切和有限高度游戏库。
- 游戏级工作区（存档、修改器、媒体）共享顶部当前游戏选择器；完整游戏搜索、筛选和排序在 Expanded 常驻，在 Standard/Compact/Narrow 通过“游戏库”显式打开，不能丢失。
- 视觉方向继续采用 Apple-inspired 原生 WPF：主题自适应拟态毛玻璃、低对比度描边、大圆角、克制环境光。禁止修改 Playnite 顶层 HWND、禁止模糊正文、禁止全局注入 WPF-UI 资源。
- UI-005 源码完成不等于真机完成。当前环境无法运行 Windows build/Playnite，因此必须保留 `BLOCKED_ENVIRONMENT`；只有一键构建、87 项测试、打包/Worker smoke 和隔离 Playnite 的 DPI/主题/反复打开回归全部通过后，才能标为最终交付。

## 2026-08-04 0.6.22 UI-057 共享游戏入口与可滚动列表规则

- 生产 Dashboard 只保留顶部全局 GamePicker；游戏级工作区共享 `SelectedGame`，任务与维护工作区不显示游戏选择器。
- 所有 Dashboard 内部 `ListBox` 必须同时声明 `VirtualizingPanel.IsVirtualizing=True`、`VirtualizationMode=Recycling`、`ScrollViewer.CanContentScroll=True`、纵向 `Auto` 和横向 `Disabled`，避免长列表被裁切或产生页面级横向滚动。
- `DataGrid` 必须位于有限 `Grid` 测量路径，统一复用共享样式并保留内部滚动与虚拟化；媒体来源规则的表单可滚动，但表格必须处于独立 `*` 行。
- 首页需关注入口必须以按钮/键盘可达，提供无障碍名称；具体原因来自持久化的 `AttentionFindings`，不能只显示数字。
- WPF-UI 输入控件继续使用共享圆角模板和 `DynamicResource`；不得回退到系统白色 TextBox/ComboBox 或共享冻结 Transform。

## 2026-08-04 0.6.22 UI-058 共享表格和低高度滚动规则

- 提取工作区的 DataGrid 必须继承 `WpfUiProduction.xaml` 中的隐式表头、单元格和行模板；局部 DataGrid 样式应使用 `BasedOn="{StaticResource {x:Type DataGrid}}"`，避免回退到宿主默认白色/方形行。
- 表格视口默认至少 220 DIP、行高约 48–54 DIP，表格必须保留内部横纵滚动、`CanContentScroll`、行/列虚拟化；MinHeight 不是用来掩盖错误测量，父级仍需是有限的 `Grid` `*` 行。
- 修改器设置是次级检查器，低高度时必须放入有限高度 ScrollViewer；主工具列表保留 star 行和独立滚动，不能被设置表单推走。
- 共享表格行只使用动态主题刷和轻微圆角；禁止对大型滚动区域或列表行添加 BlurEffect。

## 2026-08-04 0.6.22 UI-059 低高度检查器与系统偏好规则

- 媒体详情检查器属于次级信息，必须放在有限高度的独立 `ScrollViewer` 中；低高度时优先保证媒体主表格仍可见和可滚动。
- 维护诊断摘要不得放在无限测量的 Auto 行；文本区域设置有限 `MinHeight/MaxHeight` 并使用内部滚动，避免诊断内容推走 Findings 表格。
- Dashboard 和设置页监听 `SystemParameters.StaticPropertyChanged`，运行中系统高对比度、透明效果或动画偏好变化时重新应用局部色板和布局；视图卸载必须取消订阅，防止 Playnite 生命周期泄漏。

## 2026-08-04 0.6.22 UI-060 概览与任务详情规则

- 任务详情属于次级内容，必须使用有限高度 `ScrollViewer`；任务表保持独立 `*` 行和内部滚动，长错误不能推走表格。
- 概览窄宽度堆叠时，风险提醒列表必须有有限高度滚动通道，不能用 Auto 行无限扩张并遮挡最近活动。

## 2026-08-04 0.6.22 UI-061 表格排序规则

- 共享工作区表头模板必须显示 `SortDirection` 的升序/降序反馈；排序箭头使用 `DynamicResource` 强调色，不得回退到宿主默认白色箭头或无反馈。

## 2026-08-04 0.6.22 UI-062 GamePicker 键盘规则

- GamePicker 的搜索框和虚拟化列表共用 Enter/Esc 处理；搜索框焦点下 Enter 确认、Esc 关闭，不能只把事件挂在列表上。

## 2026-08-04 0.6.22 UI-063 存档中心低高度规则

- 存档历史底部元数据/恢复操作、候选路径判断依据和候选操作不得作为无限 Auto 行；必须分别放入有限高度的 ScrollViewer，避免窄窗口或长文案推走上方 DataGrid。
- SaveCenter 的表格仍位于 `Grid` 的 `*` 行，DataGrid 自身保留纵横滚动、CanContentScroll、行/列虚拟化；次级区域只滚动自身，不得用页面级 ScrollViewer 掩盖测量问题。
- 三个次级滚动区的 MaxHeight 由 SaveCenter 响应式代码按窗口实际宽高计算，最低高度用于保留操作可达性；最终仍需 Windows 低高度、DPI 与真实长文案回归。

## 2026-08-04 0.6.22 UI-064 表格文本规则

- 提取工作区共享 `DataGridCell` 默认左对齐，适合游戏名、路径、详情和原因等阅读型文本；状态、进度、操作类模板必须显式指定 Center 或 Stretch，不能依赖默认值。
- 共享表头、单元格、行和排序箭头继续使用 DynamicResource 与圆角模板；不可为修正单个表格而回退到宿主默认 DataGrid 样式。

## 2026-08-04 0.6.22 UI-065 维护中心设备页规则

- 设备对比表上方和下方的人工决策/远端恢复操作属于次级内容，必须各自有限滚动；不能让 WrapPanel 或长状态文本占用无限 Auto 行。
- `MaintenanceView.ApplyResponsiveLayout` 统一管理设备操作区限高；表格继续位于 `*` 行并保留虚拟化，不使用页面级 ScrollViewer。

## 2026-08-04 0.6.22 UI-066 顶部操作栏规则

- Dashboard 顶部操作区属于局部滚动通道，必须允许水平 Auto；禁止用 Hidden 掩盖高 DPI、第三方主题或新增模块操作造成的裁剪。
- Narrow/Compact 继续隐藏非必要文字，但不得隐藏按钮本身；页面主体和工作区列表仍禁止全局水平滚动。

## 2026-08-04 0.6.22 UI-067 修改器目录联动规则

- FLiNG 在线库的搜索结果属于独立工作区；选择结果必须在 `TrainerCenterView` 内触发 `LoadTrainerReleasesCommand`，不能依赖已隐藏的旧 Dashboard 标签页事件。
- 提取工作区后，任何原先挂在 `DashboardView` 旧内容上的选择变化行为，都必须迁移到对应工作区的 XAML/code-behind，并用门禁测试确认仍可达。

## 2026-08-04 0.6.22 UI-068 首页摘要归属规则

- `OverviewView` 是首页摘要、需关注和最近活动的唯一视觉归属；Dashboard 外壳不得重新渲染旧的六张统计卡片。
- 删除重复摘要时必须保留 `OpenAttentionCenterCommand`、`AttentionFindings` 和维护中心定位行为；目标是释放垂直空间，不是删除关注信息。

## 2026-08-04 0.6.22 UI-069 工作区提取完成规则

- Dashboard 是导航/上下文外壳，不再保留旧工作区的隐藏 Tab 或开发探针视觉树；生产内容唯一归属为 `OverviewView`、`SaveCenterView`、`TrainerCenterView`、`MediaCenterView`、`TaskCenterView`、`MaintenanceView`。
- 响应式代码必须就近维护：Dashboard 只负责六个工作区可见性和全局 GamePicker，工作区 code-behind 负责自身检查器、表格和低高度滚动。
- 媒体中心当前游戏媒体页必须提供本地 `MediaSearchText`/`MediaFilter` 筛选；筛选应使用 CollectionView 本地刷新，不能因每次输入产生 Worker IPC。
- 源码门禁和 WPF 测试遇到架构提取时，优先读取真实工作区文件并验证命令/绑定/虚拟化，不得通过恢复旧隐藏控件来满足历史字符串检查。
- 删除旧开发探针入口后，不再从 Dashboard 构造 `UiFrameworkProbeView`；若未来保留探针，只能作为独立诊断加载器，不得进入 Playnite 共享窗口解析路径。

## 2026-08-04 UI-070 共享表格密度规则

- 工作区 DataGrid 的行高、表头高度和最小可读视口统一由 `DesignTokens.xaml` 的 `GscTableRowHeight`、`GscTableHeaderHeight`、`GscTableMinHeight` 提供；不要在单个 View 中重新写固定 220/50/40 数值。
- 表格必须仍位于有限 `Grid` 行内，并由 DataGrid 自身提供 `CanContentScroll`、纵向/横向滚动和虚拟化。不要用页面级 ScrollViewer 包住 DataGrid 来解决高度问题。
- 低高度时应压缩或滚动次级操作区；表格默认保留约四行可读内容，再由内部滚动访问更多记录。

UI-071：外壳的边距现在随四档宽度和低高度状态收缩；共享 `DataGridRow.MinHeight` 使用
`GscTableRowHeight`，不允许隐式模板把工作区表格降回旧的 48 DIP。紧凑窗口要把空间留给
工作区的星号行和内部滚动区域，不得用全页面缩放或裁剪内容来“适配”。

UI-072：DataGrid 交替行必须使用 `GscTableAlternateRowBrush` 动态资源；浅色/深色由
Dashboard 调色板计算，高对比度返回透明，选中态和悬停态仍由共享行模板负责。

UI-073：`GscTableMinHeight` 的正常值是 280 DIP，但 Dashboard 在低于 760/650 DIP 时动态降为
220/180 DIP；这是防止最小视口挤压检查器的布局保护，不得改成全页面裁剪或外层无限滚动。

UI-074：抽出的工作区拥有自己的 ResourceDictionary，主题切换时必须通过
`AdaptiveThemePaletteFactory.ApplyRuntimeThemeResources` 同步到每个工作区；只更新 Dashboard
根资源会让工作区继续使用静态 DesignTokens 颜色。

UI-075：所有可键盘选择的 `ListBoxItem` 必须保留 `GscSharedFocusVisual`；不得用 `FocusVisualStyle={x:Null}`
掩盖自定义卡片模板。隐式列表项使用共享圆角、悬停和选中资源，工作区特化模板也必须复用焦点环。

UI-076：自定义游戏上下文按钮和设置分类 `TabItem` 也必须显式绑定 `GscSharedFocusVisual`，不能只依赖
鼠标 Hover 状态。

UI-077：诊断和高级设置区域不得回退到宿主默认 `Expander`。统一使用共享 `GscExpander` 圆角模板，
内容区仍由消费方的 `TextBox`/`ScrollViewer` 负责长文本滚动；共享模板不得引入列表级 BlurEffect。

UI-078：生产 `ListBox` 必须继承共享滚动契约：`CanContentScroll=True`、纵向 `Auto`、横向禁用、
`VirtualizingPanel.IsVirtualizing=True` 与 `Recycling`。列表项模板可以局部覆盖，但不得让大列表退回
宿主默认方形选中态或关闭虚拟化。

UI-079：滚动条悬停 Thumb 必须使用当前运行时 Accent/AccentHover，不能在调色板应用路径中保留固定紫色
常量；主题切换和高对比度下滚动条仍需与按钮、焦点环和选中态保持同一强调色语义。
### UI-080：列表滚动与键盘焦点局部化

- `WpfUiProduction.xaml` 的隐式 `ListBox` 现在统一使用 `PanningMode=VerticalOnly`、`TabNavigation=Local`、`DirectionalNavigation=Contained` 与 Recycling 虚拟化。
- 该契约覆盖全局游戏选择器、修改器列表、FLiNG 目录和其他生产列表；页面仍可为特殊列表显式覆盖，但不能退回宿主默认滚动行为。
### UI-081：悬停色必须来自运行时调色板

- Dashboard 游戏列表的强悬停色不得使用 `StaticResource`；必须绑定 `GscRowHoverStrongBrush` 的 `DynamicResource`。
- 调色板在高对比度下将该资源降为透明，保留系统高亮、焦点和选中状态的可读语义。
### UI-082：首页堆叠布局的滚动边界

- `OverviewSecondaryScrollViewer` 只包住首页右侧的统计/风险内容，不得包住最近活动 DataGrid。
- `ApplyResponsiveHeight` 在堆叠模式设置有限 `MaxHeight` 与 `VerticalScrollBarVisibility=Auto`，宽屏恢复 Disabled，避免双重无限测量。

### UI-083：DataGrid 行为契约

- 共享 `DataGrid` 隐式样式必须保持 `CanContentScroll=True`、`PanningMode=VerticalOnly`、纵向/横向 Auto、`TabNavigation=Local`、`DirectionalNavigation=Contained` 和 Recycling 虚拟化。
- 工作区 keyed 样式可以覆盖视觉或只读属性，但不得关闭共享虚拟化、局部滚动或键盘导航；大表格仍必须位于有限 Grid 行内。

### UI-084：空状态必须保留局部滚动契约

- 空状态属于 DataGrid/ListBox 容器的叠加提示，不得用外层页面 `ScrollViewer` 或 `StackPanel` 改变表格测量。
- 统一使用 `GscEmptyStateText`，文案应说明当前为空的原因和下一步动作；`IsHitTestVisible=False`，保证表格仍可获得键盘焦点。
- 新增空状态时优先绑定 `ICollectionView.IsEmpty` 或可观察集合 `Count`，不得为显示提示额外发起 Worker 查询。

### UI-085：媒体收件箱空状态按 Count 切换

- 待归类媒体文案必须绑定 `UnassignedMedia.Count`；不能让静态 TextBlock 覆盖有数据的 DataGrid。

### P0-001：大型库匹配必须与 IPC 解耦

- `GameCatalogService.UpsertAndMatchAsync` 先持久化轻量 `GameDescriptorDto`；当全库或大批量待匹配项进入时，不能在命名管道请求内顺序启动大量 Ludusavi 进程。
- 大批量匹配使用后台队列，队列失败不应让 Worker 退出；SQLite 中已有匹配和 Dashboard 摘要必须先可读。
- Playnite 的任务通知轮询不是 Worker 健康检查，Worker 启动/繁忙/重启时必须指数退避，不能每秒反复连接命名管道。
- 900/1000 游戏回归要同时区分 GameSaveCenter 自己的匹配和独立 `LudusaviPlaynite` 扩展；不能把第三方扩展的 967 个请求误归因给 GameSaveCenter，但应在诊断中提示两者并行会放大负载。

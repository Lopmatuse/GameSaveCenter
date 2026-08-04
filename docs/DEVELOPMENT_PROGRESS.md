# 开发实现进度

更新时间：2026-08-04
当前版本：`0.6.22-development-preview`

## 2026-08-04 UI-055 WPF-UI 输入控件圆角与滚动模板收口

- [x] WPF-UI 生产适配层现在拥有插件级 TextBox/ComboBox 模板：输入框、下拉主体、Popup、ComboBoxItem 均使用统一圆角与 DynamicResource 主题资源，不再依赖 Playnite 宿主的方形/白色默认样式。
- [x] WPF-UI Button 明确设置 `ui:Button.CornerRadius`；存档中心的锁定复选框改用共享 `GscCheckBox`，避免单个原生控件漏出宿主样式。
- [x] TextBox 内容滚动器绑定 `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility`，诊断、长路径和多行只读内容在有限空间中保留内部滚动通道。
- [x] 新增 STA 资源回归，确认生产模板、隐式 ComboBoxItem 样式和按钮圆角存在；补充共享控件与存档页签门禁。
- [x] 已通过源码校验、XAML 结构检查、Release 测试（Core 13 + Worker 21 + Playnite UI 67 = 101 项）和 Release 构建（0 警告、0 错误）。
- [ ] 独立 Playnite 宿主中的 WPF-UI Popup 实际渲染、滚动条拖动、125%/150% DPI 和高对比度仍需真机验证。

## 2026-08-04 UI-056 表格最小可读高度与媒体来源滚动边界

- [x] Dashboard、首页、存档、媒体、任务和维护工作区的 DataGrid 统一保留至少 180 DIP 的可读高度；数据超过可视区域时继续使用内部纵向/横向滚动，不让页面通过裁剪隐藏表格。
- [x] 媒体“来源规则”改为“表单局部滚动 + DataGrid 占用剩余星号行”，避免 DataGrid 被外层 StackPanel/ScrollViewer 无限测量而失去虚拟化和滚动边界。
- [x] 新增 XAML 结构回归，确认来源规则表格不位于 StackPanel，表单滚动器有明确高度上限；高对比度取消状态也改用实色语义降级。
- [x] 已通过源码校验、XAML 结构检查、Release 测试和 Release 构建；Playnite 宿主中的真实表格拖动、DPI 与高对比度仍需真机验证。

## 2026-08-04 UI-054 语义状态资源主题收口

- [x] 将错误淡色、恢复提示、风险提示、设置页环境光和取消状态纳入 `AdaptiveThemePalette`，不再在浅色/深色/蓝色强调色切换后继续使用静态默认色。
- [x] 高对比度下语义提示背景自动降级为实色，避免半透明填充导致状态不可辨识；普通模式仍使用克制的透明度。
- [x] 增加 STA 资源回归和资源键门禁，确认共享资源能在主题切换后重新生成。
- [ ] 真实 Playnite 主题、Windows 高对比度和关闭透明效果的宿主渲染仍需真机验证。

## 2026-08-04 UI-053 GamePicker 键盘确认与鼠标交互收口

- [x] GamePicker 方向键只移动当前选择，不再因 `SelectionChanged` 立即关闭抽屉；鼠标点击或 Enter 确认后关闭，Esc 可随时关闭。
- [x] 新增列表级 `PreviewMouseLeftButtonUp` / `PreviewKeyDown` 入口和结构门禁，保留 Tab、方向键、Enter、Esc 的原生焦点路径。
- [x] 重新运行源码校验、XAML 结构检查、Release 测试（Core 13 + Worker 21 + Playnite UI 66 = 100 项）和 Release 构建（0 警告、0 错误）。
- [ ] 独立 Playnite 宿主中的键盘焦点可见性、输入法、DPI、主题和卸载后的实际行为仍需 Windows 真机回归。

## 2026-08-04 UI-052 GamePicker 大库批量刷新优化

- [x] 全局 `GamePickerViewModel.SetItems` 使用批量通知集合替换轻量游戏摘要，250/1000 款游戏刷新不会逐项触发 WPF 视图重测量；对外仍保持 `ObservableCollection` 绑定兼容。
- [x] 保留本地筛选、排序、平台选项重建和已选游戏回退语义；批量结束后只发送一次 `Reset`，再执行一次筛选/排序刷新。
- [x] 新增 250 项替换通知回归测试，确保不会退回逐项 `Add` 通知，也不在 `CollectionView.DeferRefresh()` 期间修改集合。
- [x] 串行运行源码校验、XAML 结构检查、Release 测试（Core 13 + Worker 21 + Playnite UI 66 = 100 项）和 Release 构建（0 警告、0 错误）。
- [ ] 1000 款真实 Playnite 游戏库的首次加载、窗口打开耗时、虚拟化滚动和 DPI/主题宿主渲染仍需 Windows 真机回归。

## 2026-08-04 UI-051 快速切换详情的代际取消与单一入口门禁

- [x] 顶部 GamePicker 选择变化只响应一次 `SelectedItem` 通知，避免同一选择重复排队详情 IPC 请求。
- [x] 存档、媒体和修改器详情加载增加代际令牌；旧请求即使已写入命名管道，也不会在新游戏选择后回写过期集合或选中项。
- [x] 页面卸载会取消并释放详情加载、GamePicker 筛选和延迟设置保存，避免 Dispatcher/Timer 残留。
- [x] 新增源码门禁：GamePicker 列表绑定只出现一次，六个工作区不包含全局 GamePicker 入口，并锁定详情代际保护。
- [x] 串行运行源码校验、Release 测试（Core 13 + Worker 21 + Playnite UI 65 = 99 项）和 Release 构建（0 警告、0 错误）。
- [ ] 快速切换、键盘、125%/150% DPI 和卸载后的实际宿主行为仍需隔离 Playnite 真机回归；当前不把源码门禁当作宿主渲染证据。

## 2026-08-04 UI-050 共享页签、滚动与主题适配收口

- [x] 为六个物理工作区统一接入 `GscRedesignWorkspaceTabControl` / `GscRedesignWorkspaceTabItem`：页签使用圆角胶囊、动态主题色和独立横向滚动通道，窄窗口不会裁切最后一个页签。
- [x] 首页风险与提醒区域增加局部纵向滚动；全局 GamePicker、修改器列表和 FLiNG 列表显式启用纵向滚动与虚拟化，避免低高度或大库下内容被顶出。
- [x] 共享复选框勾选标记改用 `GscOnAccentTextBrush`，不再写死白色；新工作区继续使用 `DynamicResource`，普通内容不添加 BlurEffect。
- [x] 新增资源/页签/滚动门禁并通过源码校验、Release 测试和 Release 构建；当前仍为 Core 13 + Worker 21 + Playnite UI 65 = 99 项测试。
- [ ] 独立 Playnite 宿主中的页签滚动、Popup、主题、高对比度、125%/150% DPI 和反复卸载仍需 Windows 实机验证。

## 2026-08-04 UI-049 抽取工作区功能入口补齐

- [x] 媒体中心补回视频预览、单项备注/收藏保存、重新归类、批量收藏/备注、来源目录新增/启停/移除等真实命令入口；预览使用现有 `MediaVideoSourceConverter`，未给媒体列表增加 BlurEffect。
- [x] 修改器中心补回多 EXE 导入确认、活动版本选择、管理员权限、启动延迟和退出后关闭设置；FLiNG 目录仍使用本地缓存、后台任务和真实下载命令。
- [x] 维护中心补回设备摘要、人工决策和受保护远端恢复的下载校验/创建快照恢复入口；任务中心、修改器目录在窄宽度下切换为单列。
- [x] 新增入口保留 DynamicResource、长文本 Tooltip、DataGrid/ListBox 虚拟化与 Recycling；新增结构门禁并完成 STA 构造回归。
- [x] 源码校验、Release 测试和 Release 构建通过；当前为 Core 13 + Worker 21 + Playnite UI 65 = 99 项测试。
- [ ] Playnite 宿主渲染、真实主题、1000 游戏库、125%/150% DPI、键盘和反复卸载回归仍未在当前环境运行。

## 2026-08-04 UI-045/046/047/048 六工作区收口

- [x] 新增 `MediaCenterView`、`MaintenanceView`、`SaveCenterView`、`TrainerCenterView` 四个真实 WPF UserControl；媒体待归类/媒体库/来源规则、维护诊断/设备/审计/进程映射、存档历史/候选路径/策略、修改器已安装/FLiNG 在线库均使用现有 Dashboard DataContext 和真实命令。
- [x] Dashboard 现在为六个工作区提供唯一可见的物理入口：Overview、Save、Trainer、Media、Task、Maintenance；旧 SaveHistory、Candidate、Trainer、Media、Diagnostic、DeviceStatus、Logs 和 Task 标签均隐藏为迁移回退，避免重复渲染。
- [x] 新工作区保留 DataGrid/ListBox 虚拟化与 Recycling、长文本省略和 Tooltip、共享 DynamicResource；普通内容保持近不透明，未向列表行和大型滚动区域添加 BlurEffect。
- [x] 新增四工作区资源/命令/虚拟化门禁测试与 STA 构造回归；源码校验、Release 构建通过；当前为 Core 13 + Worker 21 + Playnite UI 65 = 99 项测试。
- [ ] Playnite 宿主渲染、真实主题、1000 游戏库、125%/150% DPI、键盘和反复卸载回归仍未在当前环境运行；不能据此宣称宿主 UI 完成。

## 2026-08-04 UI-043 首页工作区物理拆分

- [x] 新增 `Views/OverviewView.xaml` 与代码后置，将首页概览、最近任务、风险提醒和关注原因从巨型 `DashboardView.xaml` 提取为独立 UserControl。
- [x] 首页继续复用真实 Dashboard 命令、绑定、关注项导航和任务数据；响应式协调器通过 `OverviewView.ApplyResponsiveColumns` 更新宽屏/堆叠布局，不复制选中游戏或 Worker 状态。
- [x] 首页最近任务保持有限 Grid 测量、DataGrid 行/列虚拟化、长文本 Tooltip 和主题资源；没有给滚动行添加 BlurEffect。
- [x] 保留旧首页 Tab 作为隐藏迁移回退，当前只渲染新 `OverviewWorkspaceTab`；后续工作区迁移完成后删除旧块。
- [x] 新增物理拆分与虚拟化结构回归测试；源码校验、Playnite UI 62 项测试和 Release 构建通过。
- [ ] Playnite 宿主中首页 XAML 解析、主题、键盘、DPI 和旧 Tab 清理仍需独立实例验证；当前不能宣称完整六工作区拆分完成。

## 2026-08-04 UI-044 任务中心物理拆分

- [x] 新增 `Views/TaskCenterView.xaml` 与代码后置；任务摘要、状态筛选、虚拟化任务表、状态点、进度和详情恢复动作已从 Dashboard 中提取。
- [x] 任务中心保持全局视角，不显示 GamePicker；复制详情、安全重试和取消任务继续绑定现有真实命令，任务状态/进度均为 OneWay 显示。
- [x] 任务列表保持有限 Grid 测量、行/列虚拟化、Recycling 兼容和长文本省略；旧任务 Tab 暂时隐藏作为迁移回退。
- [x] 新增任务工作区物理拆分门禁；源码校验、Release 构建和 Playnite UI 62 项测试通过。
- [ ] Playnite 真机中的任务事件、键盘、主题、DPI 和页面反复开关仍需独立实例验证。

## 2026-08-04 UI-042 全局游戏上下文与唯一 GamePicker

- [x] 新增轻量 `GamePickerItem` / `GamePickerViewModel`，搜索在本地缓存上执行并经过 180ms 防抖；快速输入会取消旧筛选，不会按字符请求 Worker。
- [x] 支持已安装、全部、已匹配、有备份、需处理、未匹配筛选，平台筛选，名称/最近游玩/最近备份排序，游戏删除或筛选后自动回退到可见游戏。
- [x] GamePicker 状态（搜索、筛选、排序、平台和最近游戏）写入插件设置并使用延迟保存；GameStatusDto 现在携带安装状态和最近游玩时间。
- [x] Expanded/Standard/Compact/Narrow 均使用顶部唯一游戏上下文入口；取消 Expanded 常驻游戏浏览栏，抽屉仍保留虚拟化/Recycling 列表，删除首页重复游戏 ComboBox。
- [x] 首页“需要关注”卡片现在直接显示缓存中的游戏、标题和原因摘要，并可进入维护中心查看完整详情。
- [x] 新增 GamePicker 筛选、排序、持久化、回退和本地搜索测试；更新大型游戏库 UI 门禁与 0.6.22 响应式断点。
- [x] `validate-source.py`、Release 测试 96 项、Release 构建 0 警告/0 错误、`git diff --check` 通过。
- [ ] Playnite 宿主实际渲染、100/500/1000 游戏规模、125%/150% DPI、主题和键盘回归仍需 Windows 环境验证；当前不能据此宣称真机 UI 完成。

## 2026-08-01 UI-041 可选探针的有限列表布局

- [x] 兼容性探针的固定高度检查列表从 `StackPanel` 移入明确的 132 DIP `Grid` 行，避免无限测量模式和误导性的静态性能告警；按钮、进度、Snackbar 与错误反馈入口不变。
- [x] 新增 XAML 结构回归测试，锁定列表名称、固定行、Grid 祖先与非 `StackPanel` 路径。
- [x] 整体源码验收：源码门禁、UI Skill 静态审查（0 errors）、Release 下 Core 13 + Worker 21 + Playnite UI 49 = 83 项测试、`git diff --check`、`git fsck --full`、PEXT 打包与 Worker `0.6.22.0` smoke 通过。
- [ ] 该可选诊断页的实际宿主加载仍由 `ENV-001` 阻塞；Dashboard 不会在解析时构造它。

## 2026-08-01 UI-040 设置页主题更新合并

- [x] 毛玻璃强度滑块、视觉开关和主题切换现在通过单一 `QueueAdaptiveThemeUpdate` 合并 Dispatcher 回调；快速拖动只重算最新一份局部调色板/WPF-UI 资源，避免后台回调积压造成设置页卡顿。
- [x] Dispatcher 关闭或页面卸载会释放挂起门闩；Loaded/可见、导入后等需要立即呈现的路径仍直接刷新，未改变真实设置绑定或保存。
- [x] 回归测试锁定合并门闩、关闭释放与安全调度结果。
- [x] 自动验证：源码门禁、UI Skill 静态审查（0 errors）、Release 下 Core 13 + Worker 21 + Playnite UI 48 = 82 项测试、`git diff --check` 与 `git fsck --full` 通过。
- [ ] 真实 Playnite 中连续拖动滑块、切换主题和关闭页面的性能回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-039 有限宽度下拉项的长文本访问

- [x] 游戏选择、修改器入口选择、版本选择及媒体归类的有限宽度 ComboBox 统一使用 `GscComboBoxLongText`，保持省略、完整 Tooltip 与主题文字资源；不通过扩大浮层或工具栏来避免裁切。
- [x] 新增回归测试，锁定六个受限宽度选择器都使用共享模板，并避免回退到无 Tooltip 的 `DisplayMemberPath`。
- [x] 自动验证：源码门禁、UI Skill 静态审查（0 errors）、Release 下 Core 13 + Worker 21 + Playnite UI 48 = 82 项测试、`git diff --check` 与 `git fsck --full` 通过。
- [ ] 实际 Popup 的长文本、键盘展开和 DPI 回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-038 高密度表格长文本可访问性

- [x] Dashboard 的近期任务、任务队列、进程映射、设备状态与日志标题改用共享 `GscLongTextCell`：在任意主题下左对齐、省略超长内容，并提供完整文本 Tooltip；不改变列宽、命令、绑定、选择或数据读取时机。
- [x] 新增源码回归测试，锁定共享样式、自引用 Tooltip、关键长文本列、Recycling 虚拟化及无 `BlurEffect` 约束。
- [x] 自动验证：源码门禁、UI Skill 静态审查（0 errors）、Release 下 Core 13 + Worker 21 + Playnite UI 47 = 81 项测试、`git diff --check` 与 `git fsck --full` 通过。
- [ ] 实际 Playnite 中的鼠标 Tooltip、键盘选择和 100%–200% DPI 回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-037 紧凑工具栏的可访问操作回归门禁

- [x] Dashboard 顶部的刷新、全部备份、媒体同步、修改器导入/目录和诊断操作在紧凑宽度统一转换为图标优先模式；每个入口仍保留真实 Command、Automation Name 与 Tooltip，只有文字标签会随布局收起。
- [x] 新增源码回归测试，锁定六个入口的图标、标签、命令、自动化名称、提示与 `Expanded` 断点收放逻辑，防止未来缩放优化误删功能或键盘可达性。
- [x] 自动验证：源码门禁、UI Skill 静态审查（0 errors）、Release 下 Core 13 + Worker 21 + Playnite UI 46 = 80 项测试、`git diff --check`、`git fsck --full`、PEXT 打包与 Worker `0.6.22.0` smoke 通过。
- [ ] 隔离 Playnite 的实际缩放、主题与键盘回归仍由 `ENV-001` 阻塞；未启动或操作用户实例。

## 2026-08-01 UI-036 Popup 的透明与动画无障碍降级

- [x] 共享 ComboBox Popup 的 `AllowsTransparency` 与 `PopupAnimation` 已改为局部动态资源：玻璃模式可使用透明浮层与 Fade；关闭透明、高对比度或关闭 UI 动画时回退为不透明、无动画 Popup。
- [x] 下拉选项、滚动、键盘展开/方向键、焦点和 `ExpandCollapse` 契约不变；浮层阴影继续复用已有低成本资源，不为列表项创建视觉效果。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 45 = 79 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实 Popup 多主题渲染仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-035 Dashboard 两级材质层级

- [x] `GscSurface` 现为清晰的无阴影阅读面；只有 Dashboard 左侧游戏浏览与右侧详情两个永久工作区使用 `GscElevatedSurface`。统计、表单、诊断与数据表容器不再为每个小分组附加阴影。
- [x] 侧栏、Popup、Dialog、Toast 与两个主工作区仍在允许的玻璃模式使用轻量提升；关闭透明/高对比度继续通过局部资源回退，无新增大面积模糊、列表效果或布局动画。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 45 = 79 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实渲染与滚动性能仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-034 共享材质 Effect 的辅助功能回退

- [x] Dashboard 与 Settings 的本地调色板现在集中决定 Surface、主按钮、侧栏、Popup、Dialog 和 Slider Thumb 的轻量阴影；关闭透明或高对比度时使用真正的 `null` Effect，而不是透明的效果对象。
- [x] 常规玻璃模式仍只在固定卡片/浮层使用克制阴影；列表行、DataGrid、媒体缩略图和滚动内容没有新增 BlurEffect 或每项视觉效果。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 45 = 79 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实辅助功能与多主题渲染仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-033 插件通知与确认 Dispatcher 边界

- [x] 插件级错误、成功、任务通知和安全确认现在共用关闭态保护；无法安全显示的危险确认默认取消，绝不在关闭中继续恢复或其他确认操作。
- [x] 通知仍会走既有 Playnite 回退与真实日志路径；没有吞掉业务错误或伪造通知成功。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 45 = 79 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实关闭宿主通知/确认回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-032 Dashboard Worker 回调 Dispatcher 边界

- [x] Worker 任务监听更新 Dashboard 游戏、任务、筛选和选中绑定集合时，现会检查宿主 Dispatcher 生命周期并捕获关闭竞态；正常情况仍以 `DataBind` 同步顺序更新，绝不在后台线程直接修改 WPF 绑定集合。
- [x] 关闭中跳过的 UI 更新会写入真实日志；任务、备份、云端、媒体和错误状态的业务结果不被伪造或吞掉。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 44 = 78 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实慢 Worker/关闭宿主回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-031 Toast 高对比度与无玻璃回退

- [x] 浮层通知的软阴影只在用户启用玻璃且未使用高对比度时创建；关闭毛玻璃/高对比度使用同一主题资源的实体、无阴影回退，减少不必要的视觉效果与渲染负担。
- [x] 真实任务反馈、错误详情、关闭、自动隐藏和轻量进入/退出动画未变；没有向列表或滚动区域添加模糊。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 43 = 77 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实 Toast 多主题回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-030 Dashboard ViewModel 事件生命周期收口

- [x] Dashboard 的 ViewModel 属性变化和关注中心事件现仅在页面 Loaded 期间订阅，Unloaded 与任务订阅停止同步解除；反复打开页面不会重复订阅。
- [x] 真实任务、命令、状态绑定和重新打开后的动画/关注中心可达性均保留，降低关闭 Dashboard 后的页面持有和无效 Dispatcher 工作。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 42 = 76 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实开关页/后台任务回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-029 响应式缩放布局合并

- [x] Dashboard 与 Settings 的连续 `SizeChanged` 现在合并为下一渲染帧的最后一个尺寸，避免拖动窗口时重复执行多组列宽、面板可见性和紧凑模式赋值。
- [x] 页面卸载后不执行排队的响应式布局；现有 980 DIP 紧凑布局、横向访问、虚拟化及所有操作入口保持不变。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 41 = 75 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实 DPI/缩放回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-028 Settings 卸载期异步反馈保护

- [x] 设置导入/导出继续在后台完成文件 I/O；续体现在只会在页面仍加载、Dispatcher 可用时刷新绑定或显示 Snackbar/MessageBox，关闭页面不会伪造结果或再触发未处理 UI 回调。
- [x] Settings 卸载会取消其入口 `Opacity` / `TranslateTransform` 动画；反馈降级会记录真实错误，避免“报告错误”本身使 Playnite 崩溃。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 40 = 74 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实页面关闭回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-027 语义状态色高对比度动态化

- [x] 信息、成功、警告、失败色及其图标填充已进入 Dashboard/Settings 的本地动态调色板；状态点、表格任务状态、健康反馈与设置卡图标不再捕获首次加载的静态 Brush。
- [x] 普通主题维持既有 Apple-inspired 低饱和语义色；高对比度会计算 Windows 系统色/主前景的可读降级，状态仍同时通过文字和图标表达。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 39 = 73 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实主题/高对比度渲染仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-026 品牌图标多主题前景收口

- [x] Dashboard 和 Settings 的品牌强调色图标不再固定白色，改用共享的 `GscOnAccentTextBrush`；它会随 Follow Playnite、浅/深色、自定义强调色和高对比度的本地调色板即时变化。
- [x] 改动只替换图标前景资源，不影响布局、毛玻璃环境光、命令、绑定或视觉动画；已添加回归门禁，禁止重新引入固定白色品牌前景。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 38 = 72 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实主题/高对比度渲染仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-025 Dashboard Toast 生命周期与性能收口

- [x] Dashboard 现在集中追踪每个浮层通知的 Dispatcher 计时器；超过四条通知时淘汰的旧卡片会立即停止计时器，页面卸载时会停止全部计时器、取消动画并释放容器，避免短暂页面引用和关闭后的 UI 投递。
- [x] Toast 继续基于真实任务反馈显示，不改变错误详情入口、鼠标悬停暂停、自动关闭时长或 `Opacity` / `TranslateTransform` 动画范围。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 37 = 71 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实 Playnite 生命周期回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-024 数值输入焦点关闭态保护

- [x] 共享数值输入的键盘全选行为现先检查 Dispatcher 关闭态，并捕获检查与投递之间的关闭竞态；Playnite 卸载嵌入页面时不会再因非业务性的全选便利功能抛出未处理 UI 异常。
- [x] 保留原有鼠标光标、完整值编辑、范围校验和失焦提交语义；关闭中的页面只跳过全选，绝不伪造或修改设置值。
- [x] 自动验证：`validate-source.py` 与 UI Skill 静态审查（0 errors）通过；Release 下 Core 13 + Worker 21 + Playnite UI 36 = 70 项测试通过，`git diff --check`、`git fsck --full`、PEXT 打包及 Worker `0.6.22.0` smoke 通过。真实 Playnite 生命周期回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-023 插件生命周期异步异常边界

- [x] 插件的设置同步、库/游戏生命周期回调和任务通知计时器不再依赖 `async void` 运行真实后台操作；统一观察 `Task` 的最终故障，避免异常成为未观察任务或从定时器回调逸出。
- [x] 保留真实失败通知；若 Playnite 通知或页面反馈本身再次失败，记录原始故障及报告故障，不让“展示错误”引发宿主 Dispatcher 崩溃。任务通知轮询仅在实际取得门闩时释放，保持非重叠轮询。
- [x] 自动验证：Release 构建 0 warning/0 error，Core 13 + Worker 21 + Playnite 35 = 69 项测试通过；源码门禁与 UI Skill（0 errors）通过。隔离 Playnite 的生命周期真机回归仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-022 多主题强调色动态令牌

- [x] Dashboard 与 Settings 现在在各自的局部资源树中统一生成 Accent、Hover、Pressed、Tint、图标填充、主按钮与前景文字令牌；跟随 Playnite 时优先读取宿主 `HighlightGlyphBrush`，强制浅色/深色仍使用稳定紫色回退，高对比度改用 Windows 系统颜色。
- [x] Dashboard 的固定环境光与主按钮阴影同样从该局部 Accent 派生；仅保留既有固定背景效果，关闭毛玻璃/高对比度时仍折叠环境光，绝不向列表、表格或滚动内容扩散 BlurEffect。
- [x] 高对比度下强调色背景改为不透明 Windows `Highlight`，选中行、导航、页签和下拉项文字走 `HighlightText`，避免半透明色或 Accent 前景在系统选中背景上失去对比；普通主题仍保留原有柔和 Tint。
- [x] WPF-UI 4.3.0 的实际 Fluent 资源键已在页面局部覆盖：Card、Button、ToggleSwitch、TextBox 与 ComboBox 的 Accent、文字、填充、边框和焦点环与原生模板共用同一色板；不会向 Playnite 全局资源或其他扩展写入主题。
- [x] 所有共享模板、Dashboard 与 Settings 的上述令牌引用从 `StaticResource` 改为 `DynamicResource`，避免主题切换后残留初始紫色；列表/表格没有新增模糊或逐项动画，现有固定环境光和不透明降级不变。
- [x] 新增 STA 回归测试，实际创建宿主资源并断言强调色与按钮前景由宿主色板派生，同时禁止三个核心 XAML 资源字典重新静态捕获强调色。
- [x] 自动验证：`validate-source.py` 通过；UI Skill 0 errors（27 条已登记的 `.tmp`/布局提示）；Release 构建 0 warning/0 error；Core 13 + Worker 21 + Playnite 35 = 69 项测试通过。隔离 Playnite 多主题/DPI/键盘验收仍由 `ENV-001` 阻塞。

## 2026-08-01 UI-021 大型列表有限测量与虚拟化结构门禁

- [x] 以 XAML XML 祖先结构审计 Dashboard 的所有 DataGrid/ListBox：大型滚动控件必须位于有限 `Grid` 布局路径，不得处于纵向 `StackPanel` 或外层 `ScrollViewer`；ListBox 必须明确启用 `Recycling` 与逻辑滚动，DataGrid 必须使用共享 `GscDataGrid`。
- [x] 此门禁区分“数据模板中两行文字的 StackPanel”与真正的滚动容器；当前 UI Skill 10 条项目布局提示属于前者的保守正则匹配，保留以供真机 DPI 复查，但不会为了消除提示破坏正确布局。
- [ ] Windows Sandbox 在当前系统没有可执行入口，功能状态查询需要管理员提升；现有 Playnite 用户实例仍有全局单实例保护，隔离真机 UI 验收继续由 `ENV-001` 阻塞，未启动或修改用户 Playnite。
- [x] 自动验证：Release 构建 0 warning/0 error，Core 13 + Worker 21 + Playnite 34 = 68 项测试通过；其余源码与 UI 静态门禁在提交前复跑。

## 2026-08-01 UI-020 Dashboard 共用命令异常边界保护

- [x] 将覆盖刷新、备份、恢复、校验、路径检测、媒体、任务、云端、诊断和修改器命令的 `Run(Func<Task>)` 从 `async void` 收敛为可观测 `Task`；未预期故障会被记录而非成为未观察任务异常。
- [x] 统一命令、取消任务和本地操作的失败反馈；如果插件通知层自身失败，保留原始异常及通知异常日志，并继续在页面显示真实失败信息，避免为“显示错误”而造成宿主 Dispatcher 崩溃。
- [x] 自动验证：`validate-source.py`、UI Skill（0 errors）、`git diff --check` 与 `git fsck --full` 均通过；Release 构建为 0 warning/0 error，Core 13 + Worker 21 + Playnite 34 = 68 项测试通过，PEXT 打包与 Worker `0.6.22.0` smoke 通过。真实 Playnite 仍受 `ENV-001` 隔离环境阻塞。

## 2026-08-01 UI-019 async-void 事件边界保护

- [x] Dashboard 定时刷新事件增加最终异常边界并记录真实失败；取消任务从 `async void` 改为受保护的 `Task`，确认、Worker IPC 与刷新统一在同一 `try/catch` 内。
- [x] 源码门禁、UI Skill（0 errors）、Release 构建（0 warning/0 error）、Core 13 + Worker 21 + Playnite 34 = 68 项测试、PEXT 打包和 Worker `0.6.22.0` smoke 均通过；真实 Playnite/DPI/theme/keyboard 仍受 `ENV-001` 阻塞。

## 2026-08-01 QA-001 Worker smoke 默认路径修复

- [x] 修复 `verify.ps1` 在 `powershell -File` 宿主中的参数默认值时序：Worker 默认路径改在脚本体内基于已初始化的 `$PSScriptRoot` 计算，避免空路径导致 smoke 尚未检查包内容即失败。
- [x] 该脚本只读取打包 Worker 的文件版本及可选工具版本，不启动、停止、安装或覆盖任何 Playnite/Worker 实例。
- [x] 通过 `powershell -File scripts\verify.ps1` 实测默认路径，Worker 文件版本为 `0.6.22.0`；源码门禁、UI 静态审查（0 errors）、Release 构建（0 warning/0 error）以及 13 Core + 21 Worker + 31 Playnite 测试均通过。

## 2026-08-01 UI-018 设置页 Dispatcher 生命周期保护

- [x] 设置页的首次入场、主题切换、动画开关和玻璃强度回调统一经关闭态保护器投递；宿主关闭或 Dispatcher 不可用时不再将延迟回调抛向 WPF 未处理异常路径。
- [x] 不改变设置保存、导入导出、Snackbar/MessageBox 的真实异常报告或主题刷新行为；新增共享回归断言覆盖 Dashboard 与 Settings 两个页面。
- [x] 自动验证：源码门禁通过，UI 静态审查 0 errors（27 项既有/隔离副本 warnings），Release 构建 0 warning/0 error，13 Core + 21 Worker + 31 Playnite 测试通过；真实 Playnite 的主题、DPI 与键盘回归仍由隔离环境阻塞。

## 2026-08-01 UI-017 Dashboard Dispatcher 生命周期保护

- [x] Dashboard 的延迟 UI 回调统一经过 `BeginUiSafely`：在 Dispatcher 开始/完成关闭时跳过回调，并捕获不可用 Dispatcher 的异常；Worker 属性通知、视觉设置、注意事项导航、首次进入动画与对话框焦点均不再直接向已卸载宿主投递。
- [x] 后台刷新本身继续返回 `Task` 并在 ViewModel 内报告真实异常；本轮没有以 `Task.Delay`、假状态或吞异常掩盖 Worker 故障。
- [x] `validate-source.py` 更新为识别这一更强的转发模式，同时仍要求属性变化先检查 Dispatcher；新增专项回归测试。
- [x] 自动验证：源码门禁通过，UI 静态审查 0 errors（27 项既有/隔离副本 warnings），Release 构建 0 warning/0 error，13 Core + 21 Worker + 31 Playnite 测试通过；真实 Playnite 的主题、DPI 与键盘回归仍由隔离环境阻塞。

## 2026-08-01 UI-016 全工作区命令可达性门禁

- [x] 新增回归门禁：自动枚举 `DashboardViewModel` 的每个公开 `ICommand`，要求其在重构后的 Dashboard XAML 中保留普通或相对源绑定入口，防止后续视觉调整遗漏备份、恢复、云端、媒体、任务、修改器、诊断、设备或进程映射功能。
- [x] 门禁首次发现并修复 `LoadDetailsCommand` 没有显式入口：存档操作区现提供“刷新详情”，用于重新读取选中游戏的备份、媒体和策略细节；此检查只验证入口可达性，不伪造业务成功。
- [x] 自动验证：源码门禁通过，UI 静态审查 0 errors（27 项既有/隔离副本 warnings），Release 构建 0 warning/0 error，13 Core + 21 Worker + 30 Playnite 测试通过；真实 Playnite 的主题、DPI 与键盘回归仍由隔离环境阻塞。

## 2026-08-01 UI-015 设备决策安全表单自适应

- [x] 多设备状态页的人工决策改为具名“决策 / 判断依据 / 操作”字段组：宽屏三列、中等宽度两列、980 DIP 以下单列，理由和保存入口不再被表格或高 DPI 挤压。
- [x] 明确显示“只保存判断依据，不会执行远端操作或删除远端内容”；刷新、保存人工决策、下载并校验、创建快照并恢复命令及二步安全恢复顺序均保留。
- [x] 自动验证：源码门禁通过，UI 静态审查 0 errors（27 项既有/隔离副本 warnings），Release 构建 0 warning/0 error，13 Core + 21 Worker + 29 Playnite 测试通过；真实 Playnite 的主题、DPI 与键盘回归仍由隔离环境阻塞。

## 2026-08-01 UI-014 媒体来源与规则自适应布局

- [x] 媒体来源新增表单改为目录优先字段组，980 DIP 以下单列；文件模式、共享目录开关和添加操作不再与长路径互相压缩。
- [x] 已配置来源由流式卡片改为全宽纵向行，路径占独立省略/Tooltip 行；“启用并更新”与“移除”命令、共享状态文字及“不删除源文件”的业务语义未变。
- [x] 自动验证：源码门禁通过，UI 静态审查 0 errors（27 项既有/隔离副本 warnings），Release 构建 0 warning/0 error，13 Core + 21 Worker + 28 Playnite 测试通过；真实 Playnite 的主题、DPI 与键盘回归仍由隔离环境阻塞。

## 2026-08-01 UI-013 设置页全表单自适应

- [x] 核心工具路径、外观主题/玻璃强度、自动化间隔均改为共享字段组：常规宽度按 2/2/3 列显示，720 DIP 以下单列，950 DIP 以下的自动化间隔为两列；不再以固定标签列挤压路径、滑块或数值输入。
- [x] 所有数值字段仍保留全选编辑、失焦提交和既有范围验证；导入/导出操作改为自然换行，不删除设置项、绑定、Tooltip 或真实错误处理。
- [x] 自动验证：源码门禁通过，UI 静态审查 0 errors（27 项既有/隔离副本 warnings），Release 构建 0 warning/0 error，13 Core + 21 Worker + 27 Playnite 测试通过；真实 Playnite 的主题、DPI 与键盘回归仍由隔离环境阻塞。

## 2026-08-01 UI-012 修改器中心自适应工作流

- [x] “已安装”将导入操作置于独立的自然换行操作行；在 1180 DIP 以下，虚拟化的工具列表与设置检查器改为上下阅读顺序，启动延迟、开关及启动/保存/目录/解除绑定操作不再互相挤压。
- [x] FLiNG 在线库的搜索、刷新操作独立于搜索输入；在相同断点，虚拟化的搜索结果与可下载版本改为上下布局，下载绑定入口保持可见。
- [x] 本轮不新增模糊效果或逐行动画，继续只复用固定环境光与现有圆角半透明表面，避免大游戏库滚动负担；新增源码回归测试锁定响应式切换和 Recycling 虚拟化。
- [x] 自动验证：`validate-source.py` 通过；UI 静态审查为 0 errors（27 项既有/隔离副本 warnings）；Release 构建为 0 warning/0 error，13 Core + 21 Worker + 26 Playnite 测试通过；0.6.22 `.pext` 打包内容检查通过（242 个条目）。`verify.ps1` 在当前调用方式因参数默认值读取空 `PSScriptRoot` 失败，未将其计入 Worker 烟雾测试通过。

## 2026-08-01 UI-011 恢复源码与 UI 静态门禁

- [x] 已定位系统 `python` 只是 Microsoft Store 占位程序（9009），改用 Codex 随附的固定 Python 运行时后，`scripts/validate-source.py` 通过：JSON/XML/YAML、XAML 资源语义、IPC、版本、SQLite、大库性能与 Windows 启动器门禁均无错误。
- [x] `wpf-apple-desktop-ui` 静态审查通过，扫描 166 个 XAML：0 errors、27 warnings、111 info。17 项负 Margin/焦点警告及绝大多数颜色信息来自未提交 `.tmp` 中的隔离 Playnite 副本；项目自身保留 9 项“大型可滚动控件邻近 StackPanel”的人工复核提示，未误报为自动通过或自动修复。
- [x] 后续每个 UI 提交均可使用该固定解释器执行两道静态门禁；真实 Playnite 主题/DPI 验收仍须满足 ENV-001 的隔离单实例边界。

## 2026-08-01 UI-010 设置页存档策略自适应表单

- [x] 设置页“存档格式与历史版本”从固定五列改为清晰的字段组栅格：常规宽度两列、紧凑宽度一列，避免压缩压缩方式、版本数与等级输入框。
- [x] 完整版本数、差异版本数和压缩等级仍使用共享数值编辑器、全选编辑、失焦提交和原有范围验证；备份格式与压缩方式绑定未改变。
- [x] 新增 Playnite UI 回归测试锁定紧凑单列行为和三项数值绑定。Release 构建 0 警告/0 错误，Core 13、Worker 21、Playnite 25 项测试通过；隔离 Playnite 真机验收仍由 ENV-001 阻塞。

## 2026-08-01 UI-009 媒体库 Inspector 自适应布局

- [x] 媒体库在宽屏维持预览与元数据编辑的双栏结构；宿主宽度低于 1180 DIP 时，预览自动占据上方整行，备注、重新归类和批量操作在下方获得完整宽度，不再因并排预览而拥挤。
- [x] 这项调整只改变选中项 Inspector 几何；媒体 DataGrid 的扩展选择、行列虚拟化、缩略图绑定、媒体打开、归类、收藏和元数据命令均保持不变。
- [x] 新增 Playnite UI 回归测试锁定 Inspector 切换阈值、布局元素和虚拟化条件。Release 构建 0 警告/0 错误，Core 13、Worker 21、Playnite 24 项测试通过；隔离 Playnite 真机验收仍由 ENV-001 阻塞。

## 2026-08-01 UI-008 维护中心响应式诊断布局

- [x] 维护中心的 Worker、Ludusavi 和版本策略卡片从固定三列改为按宿主宽度自动 3/2/1 列，长版本和目录文本不再被窄窗口压扁；卡片仍使用清晰阅读面，不给滚动数据添加模糊。
- [x] 未知进程/MOD 启动器映射编辑器改为可换行输入带，EXE、游戏选择和绑定命令在高 DPI 下保持可访问，现有映射 DataGrid 与删除命令不变。
- [x] 新增 Playnite UI 回归测试锁定三档诊断栅格、最小输入宽度及绑定入口。Release 构建 0 警告/0 错误，Core 13、Worker 21、Playnite 23 项测试通过；隔离 Playnite 真机验收仍由 ENV-001 阻塞。

## 2026-08-01 UI-007 任务中心可恢复性重构

- [x] 任务页筛选被收纳进带说明的工作区工具栏，明确筛选只影响可见结果，不会取消、重排或重新执行后台任务；现有状态、游戏与任务类型三项绑定保持不变。
- [x] 选中任务的复制详情、安全重试和取消任务移到错误说明下方的可换行操作带，长错误和高 DPI 不再挤压恢复动作；三项操作仍由原有 `CanExecute` 与真实任务状态控制。
- [x] 新增 Playnite UI 回归测试锁定筛选区和恢复操作命令入口。Release 构建 0 警告/0 错误，Core 13、Worker 21、Playnite 22 项测试通过；隔离 Playnite 真机验收仍由 ENV-001 阻塞。

## 2026-08-01 UI-006 总览与游戏存档工作区重构

- [x] 选中游戏标题不再与四个主要操作争用同一水平行。备份、校验、侦测路径、策略现在位于标题下方的可换行操作带；在窄宽度或 150%/200% DPI 下不会挤压游戏名，也没有移除任何命令或安全策略入口。
- [x] 总览调整为“需要处理 + 下一步”双卡结构；下一步卡只绑定真实的刷新、全部备份和关注中心命令。近期活动表补充本地时间列，保留任务选中绑定和虚拟化 DataGrid 样式。
- [x] 新增 Playnite UI 回归测试锁定高 DPI 操作带和四个命令入口。Release 构建 0 警告/0 错误，Core 13、Worker 21、Playnite 21 项测试通过；隔离 Playnite 主题/DPI 真机验收仍受 ENV-001 阻塞。

## 2026-08-01 UI-005 毛玻璃性能与高对比度降级

- [x] Dashboard 的三枚和 Settings 的两枚固定环境光是页面中唯一允许带 `BlurEffect` 的元素；关闭毛玻璃或进入高对比度后，现改为 `Collapsed` 而非仅 `Opacity=0`，避免保留无意义的效果视觉树，同时不影响启用毛玻璃时的环境光层次。
- [x] Settings 的高对比度路径与 Dashboard 一致地改走不透明主题调色板；新增 Playnite UI 回归测试锁定两个页面的折叠行为与该无障碍条件。Release 构建 0 警告/0 错误，Core 13、Worker 21、Playnite 20 项测试通过。
- [ ] `python scripts/validate-source.py` 与 UI Skill 静态检查本轮无法运行：系统只解析到 Microsoft Store 占位 `python.exe`，退出码为 9009；未将其记为通过。真实 Playnite 主题/DPI 验收仍由 ENV-001 的隔离单实例条件阻塞。

## 2026-08-01 UI-004 WPF-UI ContentDialogHost 单例崩溃修复

- [x] 最新 `crash.zip` 证实此前两项资源解析修复后出现第三个独立崩溃：`ContentDialogHost.RegisterHost(Window)` 抛出 `Only one ContentDialogHost instance is allowed per Window.`；这是 WPF-UI 窗口级注册限制，不是 Worker 超时或业务任务失败。
- [x] Dashboard、Settings 和惰性探针不再声明 `ContentDialogHost` 或构造 `ContentDialog`。Dashboard 保留已有插件内半透明确认层（普通/危险确认、取消、Esc 和真实 TaskCompletionSource 路径不变），设置导入报告改用可靠的 MessageBox；页面级 Snackbar 和本地 Toast 仍作为非模态反馈。
- [x] 新增 1 项 Playnite UI 回归测试，检查所有嵌入式页面不再注册窗口级 Host，Dashboard 仍调用本地确认层、设置仍有报告路径；同步更新源码门禁。Release 构建 0 警告/0 错误、Playnite UI 测试 19 项通过；真实 Playnite 仍需隔离实例验证。

## 2026-08-01 UI-004 WPF-UI 主题令牌二次崩溃修复

- [x] 分析 `crash.zip` 中两次独立的 Playnite 崩溃：首先缺失 `Wpf.Ui.Controls.Button`，修复后第二次在 `DashboardView.InitializeComponent()` 因 `StaticResourceHolder` 找不到 `GscSoftShadowColor` 崩溃；两者均发生在插件页面构造阶段，不能以隐藏控件或捕获业务命令异常规避。
- [x] `WpfUiProduction.xaml` 继续在自身作用域静态合并 WPF-UI 默认类型样式，但对父级 `DesignTokens.xaml` 提供的 `GscSoftShadowColor`、`GscSharedFocusVisual` 改为 `DynamicResource`，避免 Playnite BAML 在兄弟字典尚未可见时解析失败，也不在 Production 内部合并令牌而破坏运行时主题调色板。
- [x] 新增 STA `UserControl` 资源树布局回归测试，覆盖 Card、Button、ToggleSwitch、TextBox、ComboBox 的父级令牌解析；源码门禁禁止 Production 适配器重新以 `StaticResource` 使用上述令牌。受控 WPF 测试通过，但仍不能替代隔离 Playnite 的真实加载、主题、DPI 和键盘回归。
- [ ] 已尝试启动 `.tmp/playnite-ui-test`，但桌面自动化只发现 `D:\software\Playnite\Playnite\Playnite.DesktopApp.exe` 的窗口（“数据备份错误”）；未对该用户实例点击、关闭或写入。随后使用官方 `--userdatadir` 创建独立 `.tmp` 数据根，隔离 `playnite.log` 记录 `Application already running, shutting down.`，测试 PID 立即退出。复制安装目录、`DatabasePath: library` 或 `--userdatadir` 均未形成可交互的单实例边界，UI-004 真机验收继续由 ENV-001 阻塞。

## 2026-08-01 UI-004 WPF-UI 生产资源作用域崩溃修复

- [x] 用户提供的 Playnite 日志确认 `0.6.22` 打开 Dashboard 时在 `DashboardView.InitializeComponent()` 抛出 `XamlParseException`：`Wpf.Ui.Controls.Button` 类型键未被解析为资源。该异常会直接打开扩展崩溃窗口，不是 Worker 管道短暂不可用。
- [x] 根因是 `WpfUiProduction.xaml` 作为 Dashboard/Settings 的同级合并字典解析时，`GscWpfUiButton` 的 `BasedOn="{StaticResource {x:Type ui:Button}}"` 看不到另一个同级 `WpfUiBase.xaml` 中的 WPF-UI 默认类型样式；编译与包内容检查不能覆盖这种 Playnite BAML 资源作用域。
- [x] `WpfUiProduction.xaml` 现在直接合并 `WpfUiBase.xaml`，Dashboard/Settings 不再重复同级合并基础字典。新增 STA XAML 资源字典回归测试，实际解析 DesignTokens + Production adapters 并断言 `GscWpfUiButton` 可用，避免重新引入同级作用域依赖。
- [ ] 已生成修复源码，但未覆盖正在运行的用户 Playnite 或扩展目录。仍需在隔离 Playnite 中打开 Dashboard、Settings、Dialog/Snackbar 并检查 `playnite.log` 无资源错误后，才可解除 UI-004 的真机阻塞。

## 2026-08-01 UI-004 生产 WPF-UI 控件迁移（源码完成，环境阻塞）

- [x] Windows 首次 Release 验证发现 `Wpf.Ui.Controls.Card` 不公开 `CornerRadius` 属性；已按 WPF-UI 4.3.0 模板改用 `Border.CornerRadius` 附加属性，并在 `validate-source.py` 增加回归门禁，防止再次生成 MC4005。
- [x] 新增 `Themes/WpfUiProduction.xaml`，以视图局部适配样式统一 WPF-UI Card、Button、ToggleSwitch、TextBox 与 ComboBox；资源仍仅由 Dashboard/Settings 的 `UserControl.Resources` 合并，`WpfUiThemeScope` 不触碰 Playnite 全局资源。
- [x] Dashboard 已迁移 6 个指标卡、59 个生产动作按钮、14 个策略/工具/媒体开关、10 个普通文本输入和 13 个下拉选择；Settings 已迁移 5 个设置卡、2 个动作按钮、14 个开关、6 个路径输入和 3 个下拉选择。数值校验编辑器、DataGrid/ListBox、搜索清除按钮和安全兜底浮层继续使用原生 WPF。
- [x] 生产通知使用 WPF-UI Snackbar 优先，确认使用插件内 Dialog，设置导入报告使用 MessageBox；错误通知仍保留“查看详情”恢复入口。设置导入/导出文件读写使用 `Task.Run`，没有新增 `async void` UI 事件。
- [x] 语义复核确认 Dashboard 的 59 个 Command 和 320 个 Binding、Settings 的 26 个 Binding 与基线数量一致；DataGrid/ListBox 的 Recycling、行列虚拟化及业务程序集、数据库、备份、媒体和 Worker 文件均未修改。
- [x] `scripts/validate-source.py`、XAML XML 解析、`git diff --check` 和 UI Skill 静态审查通过；项目范围为 0 errors、11 warnings、52 info，warnings 是既有的保守 StackPanel 邻近检查。
- [x] 修复后已在 Windows/.NET SDK 8.0.423 执行 Release restore/build：0 警告/0 错误；Core 13、Worker 21、Playnite 17 项测试通过。UI-004 仍因缺少可审计的隔离 Playnite 实例而保持真机环境阻塞，需继续验证主题、DPI、键盘、Dialog/Snackbar 与宿主无污染。

## 2026-08-01 UI-003 响应式布局与可访问性收口（真机阻塞）

- [x] Dashboard 侧栏导航改为有限高度的共享滚动区；紧凑模式把刷新、全部备份和媒体同步操作收为可访问名称与 Tooltip 完整保留的图标按钮，避免标题、游戏选择器和工具栏争夺宽度。
- [x] Settings 支持横向访问，窄屏缩小边距、低高度隐藏重复副标题；设置标题能换行，滑块具备 Automation Name。环境光和焦点环改为元素独占 Transform/边框，不再使用负 Margin 修补几何。
- [x] `validate-source.py` 新增响应式容器、键盘导航、自动化名称与紧凑行为门禁。源码验证、Release build（0 警告/错误）、Core 13、Worker 21、Playnite 16 项测试通过；UI Skill 静态检查为 0 errors、28 warnings（其中 `.tmp` 复制宿主占 17 条）。
- [ ] UI-003 真机验收被隔离环境阻塞：不能启动未证明数据根独立的 `.tmp` Playnite 副本，也不能影响正在运行的用户 Playnite。需先完成 ENV-001，再按 `WINDOWS_TEST_PLAN.md` 执行主题、DPI、键盘和工作区回归。

## 2026-08-01 UI-002 共享 WPF 控件与主题令牌复审

- [x] 设置卡片改为基于共享 `GscSurface`，避免每个设置区重建玻璃材质、边框与阴影；主/普通按钮、TextBox、数值输入、ComboBox、CheckBox、Slider、ScrollBar、Tooltip、ProgressBar 和焦点环均有共享主题入口。
- [x] 新增圆角 Tooltip 模板、可见的 ComboBox 键盘焦点、滑块 Hover/Dragging/Disabled 状态，以及进度不确定状态的真实说明；状态色和材质继续从动态令牌解析，不影响 Playnite 宿主窗口。
- [x] `validate-source.py` 新增共享控件存在性与设置卡片复用门禁；已通过。Release build 为 0 警告/0 错误，Core 13、Worker 21、Playnite 16 项测试通过；非破坏性包与 Worker 文件版本 smoke 均通过（0.6.22.0）。
- [ ] 真实 Playnite 渲染、100%–200% DPI、窄窗口、键盘导航和高对比度仍需要可审计的独立数据根。没有启动 `.tmp` 副本或用户日常 Playnite；这些验证由 READY 的 UI-003 与 ENV-001 处理。

## 2026-08-01 无人值守治理与 UI 迁移基线

- [x] 建立 `AUTONOMOUS_DEVELOPMENT_RULES.md` 与 `QUALITY_GATES.md`，将任务状态流转、单项领取、安全边界、UI Skill、最低验证与真机证据要求固化为仓库规则。
- [x] 将 WPF-UI 兼容性 POC 登记为下一项 `UI-001`；后续共享控件、页面迁移和真机回归都有明确依赖与验收条件，不会直接替换 Playnite 宿主或业务层。
- [x] 使用 Codex 附带 Python 重现 UI 基线门禁失败：Settings 对 Dashboard 局部 `GscButtonBase` 的跨视图依赖、`GscErrorTintBrush` 缺失，以及数值门禁对嵌套属性路径的误匹配。GOV-001 不修改 UI 代码，以上问题已移交 UI-001。
- [x] GOV-001 的文档、`git diff --check` 和对象完整性检查已完成；由于 UI 基线门禁当前返回退出码 1，本轮不引用此前构建/测试记录冒充这一次的 UI 验证。Playnite/UI 真机重构回归尚未开始。

## 2026-08-01 UI-001 WPF-UI 4.3.0 局部兼容性 POC

- [x] 通过中央包版本管理引入 WPF-UI 4.3.0；NuGet 包含 net462 资产。新增 `Themes/WpfUiBase.xaml`，只由 `UiFrameworkProbeView` 的 `UserControl.Resources` 合并，不写入 Playnite 全局资源。
- [x] 维护中心增加临时“界面探针”页，覆盖 WPF-UI Button、ToggleSwitch、TextBox、NumberBox、ComboBox、Card、SymbolIcon、ProgressRing、ContentDialogHost、SnackbarPresenter 与列表焦点；不绑定任何备份、恢复、云端或媒体业务状态。
- [x] UI-001 修复基线门禁：Settings 不再依赖未声明的 Dashboard 局部 `GscButtonBase`、`GscTextBox` 的错误填充令牌存在，数值编辑门禁能识别嵌套绑定路径。Dialog/Snackbar 的构造和显示位于受保护委托内；失败会记录日志并在 POC 内显示真实错误。新增 3 个专项回归测试。
- [x] 打包补齐 Wpf.Ui、Wpf.Ui.Abstractions、System.Memory、System.Buffers、Unsafe 和 ValueTuple，PEXT 内部断言这些依赖存在。Release 构建 0 警告/错误，Core 13、Worker 21、Playnite 14 项测试通过；源码门禁与 UI Skill 静态检查通过（后者仍报告既有布局警告）。
- [x] 复核修复：探针不再内联于 Dashboard XAML。维护中心仅在显式点击后通过反射构造控件；构造、资源解析或宿主失败会记录日志，显示可重试的恢复面板且不影响 Dashboard。新增构造成功/失败回归后，Core 13、Worker 21、Playnite 16 项测试通过。
- [ ] 真实 Playnite POC 验证被安全隔离条件阻塞：检测到用户现有 Playnite 与 Worker 正在运行，未关闭它们、未替换用户插件目录。需独立测试实例后验证加载、资源作用域、Popup、Dialog/Snackbar、浅/深/高对比度、关闭透明与 DPI。

## 2026-07-31 0.6.22 共享主题令牌收口

- [x] Dashboard、设置页已无页面级颜色常量；环境光、信息/成功/警告/错误图标底色、安全提示、主按钮、悬停行、状态点和阴影都由 `DesignTokens.xaml` 提供。
- [x] 真实 Playnite 已加载 0.6.22：开发安装报告确认 `extension.yaml` 为 `0.6.22`、主 DLL 为 `0.6.22.0`；`playnite.log` 记录加载版本，Dashboard 侧栏显示 `v0.6.22`、Worker 正常。仅进行了浏览核验，未执行备份、恢复、删除或云端镜像操作。

## 2026-07-31 0.6.21 云端恢复队列与 WPF 输入/控件复审

- [x] 根目录新增 `AGENTS.md`，要求所有 WPF/Playnite UI 改动使用 `wpf-apple-desktop-ui` 并遵守已有 UI 门禁。
- [x] 云端备份在本地成功、Rclone copy 失败后持久化重试队列；首次失败后依次在 1、5、15、60、240、720 分钟重试。
- [x] 队列跨 Worker/SQLite 重启保留；上传成功自动清队列；六次自动尝试耗尽后标记 Failed 并审计；配置或目录不可用时不制造 30 秒失败风暴。
- [x] 备份策略分钟输入从 58 DIP、逐字符 `int` 回写改为 88 DIP 共享数值控件，完整输入后失焦/保存时提交并显示范围错误。
- [x] 设置页的备份间隔、轮询、刷新、保留数和压缩等级统一使用相同数值校验；共享按钮焦点样式和四个大列表的 Recycling 虚拟化一并复审。
- [x] Release build 0 警告/错误；Core 13、Worker 21、Playnite 11 项自动测试通过；源码和 UI Skill 静态门禁通过。新增测试确认旧 SQLite 数据库初始化时会保留原表并创建云端重试队列表/索引，且六次自动重试耗尽规则可直接验证。
- [x] Playnite 真机加载已核验 0.6.21：扩展日志确认加载版本，Dashboard 显示 Worker 正常且手动刷新后无新增 GameSaveCenter 跨线程/XAML 异常；在不保存真实游戏策略的前提下，将分钟框 `30` 临时编辑为 `1440`、失焦验证完整显示后恢复为 `30`。
- [x] 2026-07-31 0.6.22 真机复测：输入 `0` 后共享数值输入立即显示红色错误边框；恢复 `30` 后错误状态消失。测试只编辑未保存的 ViewModel 状态，未执行备份、恢复、删除或云端操作。
- [ ] 使用隔离测试游戏、测试目录和测试云端目标完成云端失败/恢复、100%–200% DPI、浅/深/跟随主题及完整键盘回归。

## 2026-07-31 0.6.20 Dashboard 跨线程崩溃修复

- [x] 根据 0.6.18 真机 `extensions.log` 调用栈确认：后台 `PropertyChanged` 进入 `DashboardView.OnViewModelPropertyChanged` 时访问 `IsLoaded` 导致跨线程异常。
- [x] View 事件处理器先以 `Dispatcher.CheckAccess()` 回到 UI 线程，再读取 WPF 控件、ViewModel 状态或执行动画。
- [x] 自动刷新改为 `RequestBackgroundRefreshAsync`；DispatcherTimer 与 Worker 任务事件均等待其受控 Task，不再让异常逃逸至 `async void`。
- [x] 初始化后的后台同步改为 `Task`，失败状态通过 UI Dispatcher 写入。
- [x] 新增源码门禁，要求 Dispatcher 检查位于 `IsLoaded` 之前，并禁止两个后台入口回退为 `async void`。
- [ ] 在 Playnite 中保持 Dashboard 打开，完成/取消任务、慢 Worker、关闭重开面板各循环至少十次；日志不得再出现跨线程 `InvalidOperationException`。

## 2026-07-31 0.6.19 媒体控制与来源管理

- [x] 设置页新增 Steam、Xbox Game Bar、Windows Screenshots、游戏相邻目录和自定义来源五项独立扫描开关，默认保持兼容旧行为。
- [x] 单游戏策略新增“启用当前游戏自动任务”“退出后归档媒体”“游玩中归档媒体”；关闭自动任务后，手动备份和手动媒体同步仍可用。
- [x] 游玩中媒体归档改为独立于游玩中备份的调度条件；两项任一启用都会按策略间隔执行，避免“只开媒体却永不扫描”。
- [x] 自定义媒体来源可在媒体页面暂停、恢复或移除；移除只删除扫描规则，绝不删除原始文件、收件箱项目或已归档媒体。
- [x] 将媒体来源命令和编辑状态拆分至 `DashboardViewModel.MediaSources.cs`，避免继续膨胀 Dashboard 主 ViewModel。
- [x] 保留策略继续只提供安全预览：当前 Ludusavi 集成没有稳定的“删除指定版本”契约，禁止直接猜测/篡改其 Vault 目录；待上游 API 支持后再接入真实清理任务。

## 2026-07-30 0.6.18 Worker 任务事件推送

- Worker 新增独立的当前用户命名管道 `GameSaveCenter.Worker.Events.v1`，专门向已打开的管理面板推送任务排队、运行、进度与结束状态。
- 事件订阅采用每客户端有界、丢弃最旧消息的缓冲区；慢 UI、断线或关闭面板绝不阻塞备份、恢复、媒体同步或 SQLite 持久化。
- Playnite 面板加载时订阅、卸载时取消；断线后以退避方式重连，不向用户显示无意义的错误提示。
- 原有 `tasks.changes`、`tasks.changes.wait` 与 SQLite 全量快照继续存在，确保 Worker 重启、事件积压或错过事件后能恢复正确状态。
- 新增 fan-out、快照隔离和取消订阅自动化测试；当前自动测试总数为 35。

## 2026-07-30 0.6.17 大数据量滚动滑块最小尺寸修复

- 修复内容数量极大时纵向 Thumb 被 WPF `Track` 压缩成尖点的问题。
- 在滚动 Track 的局部资源中覆盖 `VerticalScrollBarButtonHeightKey` 与 `HorizontalScrollBarButtonWidthKey`。
- WPF 使用上述系统参数的一半作为比例 Thumb 的最小长度，因此设置为 72 DIP 后，纵向和横向 Thumb 的最小可见长度均稳定为 36 DIP。
- 保留 0.6.16 的单一圆角 Rectangle，避免半透明端帽叠加、亮斑或上下端不对称。


## 2026-07-30 0.6.16 滚动滑块单形状修复

- [x] 移除由 Rectangle + 两个 Ellipse 叠加构成的滑块，避免半透明颜色叠加成白色端帽。
- [x] 纵向与横向 Thumb 均改为单一圆角 Rectangle，并在 Thumb 边界内保留安全边距。
- [x] 同时在模板根节点与 Thumb 上固定最小尺寸、布局取整和裁切边界。


状态定义：

- ✅ **已开发**：代码和文档已进入 Git；不依赖 Windows 专属环境即可验证的部分已完成结构检查。
- 🧪 **已开发待 Windows 验证**：源码已实现，但必须在 Windows、Playnite、真实 Ludusavi/Rclone/游戏数据上编译或验证。
- 🚧 **部分实现**：核心算法或基础链路已完成，仍缺真实平台数据、远端摄取或完整 UI 闭环。
- ⬜ **未开发**：没有可用实现。

> Windows 真机已确认 0.4.2 可以编译、安装并打开侧栏。0.4.3 修复 Worker/Ludusavi 路径混淆、重复启动和缩放布局问题；Release 编译与隔离 Worker 冒烟测试已通过，仍需完成 Playnite 交互与真实 Ludusavi 回归。

## 2026-07-30 0.6.15 滚动滑块双端圆弧修复

- [x] 以 0.6.14 为基线重新实现全局 ScrollBar Thumb；不依赖此前未应用的 0.6.15 补丁。
- [x] 纵向滑块使用顶部圆形端帽、中间矩形和底部圆形端帽组合绘制。
- [x] 横向滑块使用左右圆形端帽和中间矩形组合绘制。
- [x] 可见胶囊内缩于 Thumb 边界，避免 Playnite 宿主、高 DPI 和 DataGrid 视口裁切。
- [x] 正常状态不绘制可见轨道线，但保留拖动、滚轮和轨道分页点击行为。
- [ ] Windows/Playnite 下回归 100%、125%、150%、200% DPI，检查滑块位于首端、中部和末端时两端均为完整半圆。

## 2026-07-30 0.6.14 WPF 控件一致性与页面泄漏修复

- [x] 首页统计卡片统一图标、标题和数字对齐，移除“需要关注”多余箭头。
- [x] 动态任务筛选选项重建后强制恢复“全部”选中显示。
- [x] 任务进度列使用弹性进度条和固定百分比安全区。
- [x] 修改器导入按钮和设置迁移按钮统一高度、间距和垂直中心。
- [x] DataGrid 列宽调整热区改为透明模板，避免浅色主题出现醒目白色拉块。
- [x] 所有 DataGrid 保持列宽调整、Tooltip 与自动横向滚动能力。
- [x] 全局 ScrollBar Track 增加首尾安全内边距，修复 Thumb 底部/右侧被裁切。
- [x] 设备状态页签纳入维护中心可见性控制。
- [ ] Windows/Playnite 下回归 100%、125%、150%、200% DPI 与深浅主题渲染。

## 2026-07-29 0.6.13 远端备份隔离下载与受保护恢复

- [x] 从所选远端设备的 `Saves` 子树单向下载完整 Ludusavi 库到本机 `RemoteBackups` 隔离区。
- [x] 下载与哈希检查使用同一全局传输锁，避免本机上传任务并发干扰；不修改远端内容。
- [x] 使用隔离库运行 Ludusavi `backups --api`，确认所选游戏和 Backup ID 真实存在后才签发七天暂存句柄。
- [x] 设备名、暂存 ID 和本机根路径均执行路径穿越防护；失败暂存会尽力清理。
- [x] 远端恢复复用现有游戏关闭检查、PreRestore 锁定、本机回滚、云端暂停、恢复后预览校验和审计。
- [x] 设备状态页提供“下载并校验”与“创建快照并恢复”两个独立确认步骤。
- [x] Worker 路径防护自动测试已加入一键测试链路。
- [ ] 用两个真实设备目录和 Rclone 后端验证大库断线续传、远端变化、哈希不一致、过期暂存与低风险游戏恢复。

## 2026-07-29 0.6.12 虚拟化媒体缩略图与录像预览

- [x] 媒体 DataGrid 显式开启行/列虚拟化与 Recycling，只为可见行创建缩略图单元格。
- [x] 截图列表缩略图限制 96px，选中截图限制 480px；共同使用按文件版本键控的 96 项 LRU。
- [x] 图像采用 OnLoad、Freeze 和共享读取，转换后立即释放源文件句柄。
- [x] 选中录像仅创建一个静音内嵌播放器；系统默认播放器入口继续保留。
- [x] WPF 自动测试用 100 张 PNG 验证图像冻结、文件句柄释放和缓存上限。
- [ ] 在 Playnite 中验证 MP4/WMV/AVI/MOV 的本机 Media Foundation 支持、损坏录像、4K/8K 截图和 1000+ 媒体滚动内存。

## 2026-07-29 Playnite 官方更新发布准备

- [x] 确认 Playnite 插件不能运行中热重载，官方 Add-ons 数据库负责安装与自动更新提示。
- [x] 增加 `manifests/InstallerManifest.yaml`，绑定扩展 ID、0.6.12 PEXT 下载地址、最低 API 与变更说明。
- [x] 增加可提交到官方数据库 `addons/generic/` 的 add-on manifest。
- [x] 源码门禁校验扩展 ID、版本、PEXT 文件名与两份清单一致。
- [ ] 使用仓库所有者身份创建 `v0.6.13` GitHub Release 并上传 PEXT。
- [ ] 向 `JosefNemec/PlayniteAddonDatabase` 发起 PR；合并后验证 Playnite 内安装与下一版本更新。

## 2026-07-29 0.6.11 媒体域模块拆分

- [x] `SqliteStateStore` 改为 partial，并将媒体哈希、列表、摘要、批量元数据、收件箱、来源规则和归类状态迁到独立文件。
- [x] `DashboardViewModel` 改为 partial，并将媒体工作区加载、同步、来源、筛选、批量元数据、收件箱和文件打开迁到独立文件。
- [x] 所有 IPC 名称、公开方法、SQL、事务锁、绑定属性和命令保持不变。
- [x] 源码门禁聚合扫描所有 partial，模块拆分后继续保护原有媒体与设备安全约束。
- [x] Release 编译 0 警告/0 错误，Worker SQLite 2/2 与 Playnite 设置 5/5 测试通过。
- [ ] 继续按工作区拆分 Dashboard 与按领域拆分持久层；本批未改 XAML 结构。

## 2026-07-29 0.6.10 设置迁移自动化回归

- [x] 新增 net472 测试宿主直接引用 net462 Playnite 插件，插件运行时兼容目标不变。
- [x] 覆盖 SchemaVersion=1 导出导入往返与非敏感字段保持。
- [x] 覆盖旧设置包缺少新字段时采用当前安全默认值。
- [x] 覆盖未知架构、未知枚举、数值越界和超过 1 MiB 输入的拒绝。
- [x] 验证非法导入不修改当前编辑值，缺失路径报告不自动创建文件或目录。
- [x] 一键构建脚本与源码门禁要求运行并保留设置迁移测试。
- [ ] 在真实 Playnite 设置页回归“导入—取消—再次导入—保存—重启 Worker”完整宿主流程。

## 2026-07-29 0.6.9 多设备冲突人工决策记录

- [x] 设备比较可记录稍后处理、保留两者、优先本机或优先远端及备注。
- [x] 决策按游戏和远端设备持久化，刷新 sidecar 后重新附加到比较结果，并写入审计日志。
- [x] 决策仅表达用户意图，不触发 Rclone 下载、恢复、删除或覆盖。
- [x] Worker 集成测试覆盖决策持久化。
- [ ] 使用两台真实设备验证决策在刷新、Worker 重启和 sidecar 更新后的展示。

## 2026-07-29 0.6.8 媒体批量元数据与 Worker 集成测试

- [x] 当前游戏媒体列表支持 Extended 多选，并可批量收藏、取消收藏或将当前备注应用到所选项目。
- [x] 批量更新只修改 SQLite 收藏/备注字段，不移动、不覆盖、不删除媒体文件。
- [x] Worker 对 1–500 个去重 ID 执行单事务更新；任一记录不存在时整个事务回滚。
- [x] 新增独立 Windows Worker 测试项目，覆盖批量成功、未修改字段保留及部分无效 ID 的原子回滚；Core 测试继续保持跨平台。
- [x] 一键构建脚本同时运行 Core 与 Worker 测试。
- [ ] 在 Playnite 中用 Ctrl/Shift 多选媒体，验证搜索/筛选后选择、批量按钮、摘要计数和主题/DPI 布局。

## 2026-07-29 0.6.7 媒体页崩溃修复、检索与预览

- 0.6.6 真机日志确认打开媒体页时，WPF `Run.Text` 尝试 TwoWay 回写只读的 `MediaStorageSummaryDto.TotalSizeDisplay`，导致 Playnite 主线程未处理异常。
- 媒体统计的五个 `Run.Text` 数据绑定全部显式改为 `Mode=OneWay`，避免相同模板内其他统计字段以后改为只读属性时再次崩溃。
- 修复 `validate-source.py` 中被双重转义破坏的 `Run.Text` 正则；门禁现在会实际扫描所有 Playnite XAML 并拒绝缺少显式 OneWay 的数据绑定。
- `check-xaml.ps1` 的输出改为 ASCII，避免 Windows PowerShell 5.1 将 UTF-8 无 BOM 中文误解码后产生脚本解析错误。
- [ ] 安装 0.6.7 后连续切换媒体页、不同游戏和三种主题，确认不再出现扩展崩溃窗口或绑定错误。
- [x] 当前游戏媒体支持按文件名、备注和来源即时搜索。
- [x] 支持全部、截图、录像和收藏筛选，不触发 Worker 重扫。
- [x] 选中截图使用 480 像素解码上限预览，载入后释放文件句柄并冻结图像资源。
- [x] 录像与不支持格式继续使用系统默认应用打开，避免在 Playnite UI 中引入重量级播放器。
- [ ] 在 1000+ 媒体、4K/8K 截图、损坏图片和网络目录上验证选择切换响应。

## 2026-07-29 0.6.6 设置迁移与媒体管理补足

- [x] 设置页支持导出、导入带架构版本的可移植 JSON。
- [x] 导入前验证文件大小、主题/备份枚举和数值边界，失败时不写入当前设置。
- [x] 导入后报告新机器上缺失的程序和目录路径，仍需用户点击 Playnite 保存才应用。
- [x] 当前游戏媒体增加 SQLite 聚合的数量、类型、收藏和空间占用摘要。
- [x] 媒体支持收藏、备注、直接打开和资源管理器定位，操作不删除用户文件。
- [ ] 在 Playnite 设置页真实导出、取消编辑、重新导入并保存，验证 Worker 最终收到新设置。
- [ ] 使用真实图片与视频验证默认程序打开、文件缺失错误和 1000+ 媒体列表性能。

## 2026-07-29 0.6.5 任务事件、云端重试与修改器导入闭环

- Worker 新增带 25 秒上限的任务变化长轮询；任务状态写入后会主动唤醒等待客户端。Playnite 后台通知不再每 5 秒读取 200 条完整任务历史，SQLite 全量快照仍作为 Worker 重启后的可靠兜底。
- 本地备份成功但 Rclone 失败时，可执行仅重复 `rclone copy` 的 `CloudUpload` 任务，不会为了重试云端而再创建一个 Ludusavi 本地历史版本。上传开始、成功和失败分别持久化为待上传、已上传和上传失败。
- 导入 ZIP 或目录前由 Worker 安全检查候选入口；存在多个 EXE 时，Playnite 内显示主程序选择器，用户确认后才复制并绑定。
- 修改器 Inspector 可切换同一工具的活动版本，保存后启动、自动启动和打开目录均使用所选版本。
- Release 构建、13 项 Core 测试、源码/XAML 门禁和 `0.6.5` PEXT/ZIP 打包已通过；真实 Rclone 长传输、Playnite 后台通知、多 EXE 包和版本切换仍需真机回归。

## 2026-07-29 0.6.4 云端游戏状态

- 每游戏云端状态从 SQLite 读取；备份上传成功写入“已上传”，失败写入“上传失败”，不再按“是否配置 Rclone”伪造状态。
- 失败任务继续使用现有 Backup/MediaSync 安全重试入口；真实 Rclone 断网、恢复网络与状态刷新仍需 Windows 验证。

## 2026-07-29 0.6.3 未知进程人工学习

- 维护中心诊断页可将用户明确输入的 EXE 名称绑定到一个 Playnite 游戏，并可查看、删除持久化映射。
- 外部进程检测优先采用启用的人工映射；该映射只影响该 EXE 的游戏归属，绝不自动创建未知映射。
- MOD Loader、通用启动器的真实会话启动/结束语义仍需 Windows 真机验证。

## 2026-07-29 0.6.2 多设备只读状态摘要

- Worker 为每款有本地历史的游戏生成不含存档内容、文件路径或凭据的最新备份摘要，并原子写入本机 `DeviceState` 目录。
- 在启用 Rclone 云端后，维护中心可手动上传本机摘要、只读列出并读取其他设备摘要，调用仅限 `copy`、`lsf`、`cat`。
- 使用已有 `DeviceConflictDetector` 比较每游戏最新摘要；分叉只显示“需要人工决定”，绝不自动下载、恢复、删除或覆盖任一设备存档。
- 新增核心算法的单端摘要测试；Rclone 与多设备真实兼容性仍需 Windows 回归。

## 2026-07-29 0.6.1 关注项、云传输与任务增量收口

- 首页“需要关注”统计卡不再只是数字：点击会打开维护中心的异常与日志，选中首个关注项，并显示游戏名、问题详情及建议处理方式。
- Worker 任务状态新增有界增量变化馈送。管理面板空闲时只在任务变化时刷新完整快照，并每分钟做一次缓存校准，保留 SQLite 查询作为正确性兜底。
- 云端上传和恢复共享全局传输闸门；恢复会等待现有上传完成并阻止新上传，避免 rclone 复制共享备份根目录时与恢复交叠。
- 恢复在用户确认之外额外检查 Worker 活跃会话及仍存活的已记录游戏进程。
- FLiNG 下载增加 2 GiB 下载上限，ZIP 导入增加文件数量、单文件大小和总解压大小限制；失败安装会清理新建的版本目录。
- `dotnet build GameSaveCenter.sln -c Release --no-restore`、12 项 Core 测试和源码门禁通过；仍需真实 Playnite、Rclone、Ludusavi 与大型游戏库回归。

## 2026-07-29 0.5.10 页签布局热修复

- 修复页签内容被继承的居中对齐拉到页面中央。
- 隐藏页签头部 ScrollViewer 的滚动条轨道，并给四角圆角预留完整绘制空间。
- 不回退 0.5.9 已验证正常的表格复选框。

## 2026-07-29 0.5.9 WPF 控件几何与搜索交互收口

- 共享滚动条改为有限圆角、方向独立尺寸和无系统箭头模板，修复大型列表 Thumb 呈尖角/透镜形的问题。
- Dashboard 完全接管 TabControl 与 TabItem 模板，一级/二级页签四角统一，不再叠加 Playnite 默认标签线。
- DataGrid 首末表头分别使用上圆角，表格外框与表头几何一致；锁定列使用共享圆角复选框。
- 按钮、导航、状态和进度内容统一水平/垂直居中，图标与文字不再错位。
- 游戏与 FLiNG 搜索框使用焦点感知 Watermark 和可清除按钮；共享输入控件同步应用到设置页。
- Linux 源码/XAML 门禁通过；Windows WPF 编译、Playnite 多主题和大量数据滚动回归仍待真机。

## 2026-07-28 0.5.8 WPF 视觉系统与交互反馈重构

- 重写纵向和横向 ScrollBar/Thumb 模板，按方向分别设置最小长度，修复大型游戏库中滑块被宽高约束挤压成不规则形状的问题。
- 统一 DataGrid 表头、单元格、状态徽标、进度列和复选框的对齐与选中态，降低表格线和传统后台感。
- 路径、文件名、错误详情使用省略、完整 Tooltip、可调列宽和水平滚动；局部页签改为圆角 Pill。
- 标题栏增加 Playnite 宿主窗口按钮安全区，紧凑侧栏修复品牌图标裁切并保持 DPI 稳定。
- 新增插件内确认框、结果详情框和不抢焦点的 Toast；恢复、解绑、忽略媒体和后台任务结果接入统一反馈。
- 插件图标替换为高识别度的“手柄 + 存档”矢量方案，并提供 SVG 与多尺寸预览源。
- Linux 环境源码门禁通过；WPF Release 编译、Playnite 真机加载、窗口缩放和 DPI 回归仍需在 Windows 完成。

## 2026-07-28 0.5.7 大型游戏库加载优化

- 管理面板改为 SQLite 缓存优先，首次构造不再等待全库同步。
- 插件对五分钟内相同游戏库指纹的同步请求去重；Worker 只匹配新增、关键描述变化或超过七天冷却期的未匹配游戏。
- Dashboard 用一次聚合 SQL 读取全部游戏的备份、媒体和策略摘要，移除每游戏 N+1 查询。
- 详情按存档、媒体、修改器工作区懒加载，存档历史默认读缓存；Ludusavi 版本缓存六小时。
- Release 全解决方案编译、12 项 Core 测试、源码门禁、Worker 临时数据目录初始化和 0.5.7 开发安装均通过；约 1000 游戏库的真实首开耗时仍需 Playnite 真机记录。

## 2026-07-28 0.5.6 统一控件与信息密度收口

- 统一 ListBox、DataGrid、ComboBox 和导航项的选中前景色，使用低透明强调背景与左侧指示条，避免宿主主题把选中文字改成黑色。
- 用圆角紫色焦点环替换 WPF 默认虚线焦点框；设置页复选框也改为共享深浅主题模板。
- 首页下半区改为“需要处理”和最近八条任务，不再重复顶部统计数字；任务中心新增状态、游戏和任务类型筛选，并使用中文任务名称。
- 媒体中心拆分为“待归类 / 当前游戏媒体 / 来源与规则”，内部类型、来源和云端状态转换为用户语言，文件名和路径使用省略与 Tooltip。
- FLiNG 目录采用可读的游戏版本、功能数量、版本号、发布日期和大小；原始长文件名仅作为技术 Tooltip。
- 已安装修改器改为列表与设置 Inspector 双栏；维护中心默认显示摘要，原始诊断信息按需展开。
- 响应式断点统一为 1280 / 980 / 880 DIP；紧凑侧栏补齐 Logo、导航和状态 Tooltip。
- 滚动条 Thumb 使用圆角自定义模板和方向相关最小尺寸；后台刷新提示继续脱离主布局流，避免页面抖动。

## 2026-07-28 0.5.5 后台任务反馈与固定周期调度

- 任务通知从 Dashboard 轮询中解耦，改为插件整个运行期持续轻量监测；游戏在前台、管理面板关闭时，自动备份完成、无变化或失败仍会进入 Playnite 通知。
- 通知使用 Worker 的最终任务消息，不再把成功统一压缩为“任务已完成”；手动备份、游玩中定时备份、退出后备份、恢复、修改器下载和媒体同步均能显示实际结果。
- 所有任务按 TaskId 去重，首轮只建立快照，不补发旧任务；设置关闭期间仍记录已见任务，避免重新启用后通知风暴。
- 导入与解绑修改器、保存策略、路径候选和媒体归类等非队列操作补齐成功反馈；失败继续走统一错误通知。
- 游玩中调度以计划时间为锚点递推下一次时间，不再把每轮最多 5 秒的轮询延迟累加到以后各轮；运行中修改间隔或重新启用会从当前时间重新计时。
- 定时备份增加单会话重叠保护，上一轮仍在等待或执行时不会继续堆积同一游戏的周期任务。

## 2026-07-28 0.5.4 崩溃与一分钟定时备份修复

- 崩溃日志确认两次闪退均来自 WPF `Run.Text` 尝试 TwoWay 回写 `GameToolDto.TypeDisplay` 等只读属性；已统一显式指定 OneWay 并增加源码门禁。
- 每游戏游玩中备份此前界面可保存 1 分钟，但 Worker 静默最小化为 5 分钟；现已统一为 1–1440 分钟，计划检查周期为 5 秒。
- Worker 日志会记录会话的定时备份间隔和每次定时任务的开始，便于真机确认。
- Backup 任务消息会标明触发来源，因此“存档无变化，历史未新增”仍可明确识别为游玩中定时备份成功。
- 手动备份的无变化结果继续不新增版本，防止 ZIP 历史被完全相同的副本污染。

## 2026-07-28 0.5.3 紧凑模式、媒体工作区与滚动条修复

- 图标侧栏会收起 Worker/Ludusavi 文本并降低导航内边距，只保留可见状态灯和导航图标。
- 共享滚动条补齐 `Track` 范围/视口绑定并区分水平、垂直方向，避免短内容显示成错误的小方块 Thumb。
- 媒体中心改为页面内部纵向滚动，两个数据表分别保留约四行高度；媒体游戏选择器显式渲染游戏名称，不再暴露 DTO 类型名。
- FLiNG 搜索与选择结果会自动加载右侧可下载版本；后台刷新不再播放详情进入动画或插入工具栏进度控件。
- Release 构建、12 项 Core 测试与源码门禁通过；仍需 Playnite 真机验证滚动条 Thumb、紧凑导航和媒体滚动行为。

## 2026-07-28 0.5.2 模块化自适应 UI

- Dashboard 不再仅以一级导航切换同一个七标签详情页；导航会过滤为当前模块的最小标签集合，任务和维护中心使用完整工作区宽度。
- 主内容按 1320、1050、880 DIP 切换 Wide、Medium、Compact；紧凑模式收起导航文字与常驻游戏列，提供当前游戏选择器。
- 存档策略面板按需展开，正常 Worker 状态仅保留在侧栏；后台刷新提示不再改变主 Grid 行高。
- 设置页接入共享 ComboBox、Popup、滚动条及进度条模板，深浅主题均不依赖 WPF/Playnite 默认白色控件。
- Windows Release 构建、12 项 Core 测试、打包和开发安装已通过；仍需在正在运行的 Playnite 中手动执行 100%–200% DPI 与全部模块的视觉回归。

## 2026-07-28 0.5.0 修改器中心

- 新增 `game_tools`、`game_tool_versions`、`trainer_catalog` 和 `trainer_releases`，旧数据库可幂等增量升级。
- 一个 Playnite GameId 可绑定多个修改器、多个 Cheat Table 和多个工具版本。
- Worker 支持 EXE、ZIP、目录和 CT 导入、SHA-256、Zip Slip 防护、文件缺失检测、启动、提权和工作目录。
- 每项工具独立保存启用、随游戏启动、延迟、退出关闭和管理员权限；新导入和下载默认不自动启动。
- 只追踪当前游戏会话实际启动的 PID；退出时不会按进程名误杀其他程序。
- 检测到 Easy Anti-Cheat、BattlEye、Ricochet 或 Vanguard 线索时默认阻止自动启动并记录审计。
- 新增隔离的 FLiNG 目录适配器、SQLite 本地搜索、版本展开、后台下载、安全解压和自动绑定。
- 左侧导航调整为首页、存档中心、修改器中心、媒体中心、任务中心和维护中心；右侧内部标签不再反向改变一级导航。
- Release 全解决方案编译通过，Core 测试 12/12 通过；Playnite 真机和 FLiNG 实际下载仍待回归。

## 2026-07-28 0.5.1 构建恢复顺序热修复

- 修复 `GameSaveCenter-Run.cmd` 间接调用的一键安装流程：在 `dotnet clean` 前先执行 NuGet restore，避免随源码包带来的其他机器 `project.assets.json` 指向不存在的包缓存而导致 `NETSDK1064`。
- 发布 Worker 时恢复默认 restore 行为，确保 `win-x64` 发布能正确补齐 Runtime Identifier 资产。

## 2026-07-28 0.4.3 Worker 与响应式界面热修复

- Windows `.NET SDK 9.0.302` 完成 Release restore/build/test/publish/package，Core 9 项测试全部通过。
- 安装脚本确认 Playnite 扩展清单为 `0.4.3`、DLL 为 `0.4.3.0`。
- 使用原 0.4.2 错误配置启动后，`WorkerExecutable` 已自动恢复为打包 Worker，原 `ludusavi.exe` 路径迁移到 `LudusaviExecutable`。
- `worker-launch.log` 在进程启动前创建，记录了唯一 Worker 启动、SQLite 初始化和后续隐藏 CLI 调用。
- Playnite 1366×768 真机确认侧栏打开、Worker/Ludusavi 正常、搜索输入/过滤正常、深色 ComboBox Popup 可读、存档历史区域保持可见。
- 尚未对含真实历史数据的 DataGrid 滚动、125%/150%/200% DPI 和浅色主题逐项截图回归。

## 工程与治理

| 功能 | 状态 | 备注 |
|---|---|---|
| Git 仓库与分阶段提交 | ✅ | `main` 分支，完整 `.git`；历史提交已改为中文，作者统一为“Sable Drift” |
| 项目记忆文件 | ✅ | `PROJECT_MEMORY.md` |
| 需求、架构、安全与 UI 文档 | ✅ | 新增完整 Apple-inspired WPF 实施提示词与 UI 变更门禁；后续 UI 提交必须遵守 |
| Codex 延续开发提示词 | ✅ | `CODEX_CONTINUATION_PROMPT.md` |
| Windows 构建/测试/打包/安装脚本 | ✅ | 用户已在 Windows/.NET 9.0.302 完成 build、test、publish、package 与开发安装 |
| 含 `.git` 的源码打包脚本 | ✅ | `scripts/package-source.ps1` 使用 ZipFile，包含隐藏目录 |
| 跨平台源码结构校验 | ✅ | `scripts/validate-source.py` 已通过 |
| Core 单元测试源码 | ✅ | 6 组 xUnit 测试；当前环境未执行 |
| Windows 真机编译与 Playnite 加载 | 🧪 | 0.4.2 已真实编译、安装并打开侧栏；0.4.3 已 Release 编译，待安装后完成交互回归 |


## 0.4.1 全局媒体收件箱闭环

| 项目 | 状态 | 说明 |
|---|---|---|
| 公共目录单次扫描 | 🧪 | Game Bar、Windows Screenshots 与共享自定义目录由 `MediaInbox` 全局任务统一扫描，不再按游戏重复遍历 |
| 保守自动归类 | 🧪 | 仅文件名唯一命中，或明确 SessionId + 无重叠会话时间窗口时归类；多游戏歧义进入收件箱 |
| 待归类持久化 | 🧪 | SQLite 新增 `classification_state/reason`，支持旧库补列、规范化和分类索引 |
| 全局归类工作台 | 🧪 | 媒体页展示待归类时间、类型、来源、文件、大小和原因，可选择任意游戏确认归类 |
| 忽略但保留副本 | 🧪 | 忽略项移动到 `_Inbox/Ignored`，不删除原始文件或归档副本，并写入审计 |
| 文件级安全迁移 | 🧪 | 重新归类会校验目标哈希；跨盘时原子复制后再删除旧归档，归档丢失时只从原文件重建副本 |
| 首轮导入保护 | 🧪 | 每轮最多新增 200 个歧义历史媒体，后续借助 SHA-256 去重分批补齐 |
| 媒体收件箱安全重试 | 🧪 | `MediaInbox` Failed/Cancelled 任务可单独重试共享目录，不重扫所有游戏专属来源 |

## 0.4.0 自动候选、主题模式与任务操作

| 项目 | 状态 | 说明 |
|---|---|---|
| Apple-inspired WPF 实施规范落库 | ✅ | 用户提供的完整规范保存为 `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`，并新增 `UI_CHANGE_GATE.md` 强制门禁 |
| 三种主题模式 | 🧪 | 支持跟随 Playnite、固定浅色、固定深色；保存设置后管理面板即时重算局部色板 |
| 未匹配游戏会话前后快照 | 🧪 | 游戏启动时后台记录有界文件状态，退出后对比新增/修改文件并生成候选；只对未匹配游戏启用 |
| 候选持久化与解释 | 🧪 | 详情页重新加载可读取历史候选，显示可信度、状态和可解释依据；接受后生成规则草案，支持忽略候选 |
| 候选快照清理 | ✅ | Worker 启动时清理超过两天的孤立会话快照，避免长期积累 |
| 任务错误复制 | 🧪 | 选中任务可复制游戏、任务类型、真实详情和任务 ID |
| 失败任务安全重试 | 🧪 | 仅备份与媒体同步的 Failed/Cancelled 任务开放重试，不对恢复或未知任务盲目重放 |
| 公共媒体会话时间归类 | 🧪 | Game Bar/Windows 共享目录在文件名不匹配时，可使用明确且无重叠的游戏会话时间窗口归类 |

## 0.3.1 UI 与动态效果状态

| 项目 | 状态 | 说明 |
|---|---|---|
| 应用侧栏导航 | 🧪 | 左侧导航与详情标签双向同步，不添加不存在的窗口控制按钮 |
| 主题自适应毛玻璃 | 🧪 | 运行时根据 Playnite `TextBrush` 生成浅色/深色玻璃色板；环境光使用静态模糊色块 |
| 高对比度降级 | 🧪 | 高对比度下改为不透明主题表面并关闭环境光 |
| 页面与控件动画 | 🧪 | 页面、游戏、标签、任务、状态、指标卡、导航和按钮均有克制微动效 |
| 动效与玻璃设置 | 🧪 | 可关闭动画、关闭玻璃或调整强度；旧配置缺失字段时使用安全默认值 |
| 跨平台 XAML 语义检查 | ✅ | 检查 Trigger 父级、模板 TargetName 和 XAML 事件处理器，减少 Windows 上逐个暴露编译错误 |

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
| Xbox/Game Bar 媒体同步 | 🧪 | 公共 Captures 目录支持文件名匹配、无重叠会话时间窗口与未识别收件箱；待真机完善 |
| Epic/Ubisoft/EA/GOG 媒体来源 | 🧪 | 安装/Action 附近常见目录 + 每游戏自定义目录与匹配模式 |
| 误归类媒体修正 | 🧪 | UI 可把已归类媒体移动到另一游戏；全局收件箱可人工分配或忽略并保留副本 |

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
| 自定义媒体来源升级兼容 | 🧪 | `shared_directory` 与媒体 `classification_state/reason` 自动补列，分类索引在迁移后创建 |

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
| 文件变化候选扫描 | 🧪 | 限定目录即时扫描与默认未匹配游戏会话的启动前/退出后差分快照均已接入；待真实游戏调优 |
| 候选路径评分 | ✅ | 可解释评分、缓存降权、会话末/WGS/重复模式加权算法及测试源码 |
| Xbox WGS 辅助识别 | 🧪 | 扫描 Packages/SystemAppData/wgs 候选；不承诺所有游戏可恢复 |
| Ludusavi 自定义规则草案 | 🧪 | 用户确认后只生成草案，不静默改动 Ludusavi 配置 |
| 多设备冲突检测 | 🚧 | 核心判定算法与测试源码已实现；Rclone 远端元数据清单摄取和 UI 尚未完成 |
| 未知游戏/MOD 启动链识别 | 🚧 | 已知进程映射和多进程退出去重已实现；人工“学习并保存新映射”的 UI 尚未完成 |
| 公共截图会话归类 | 🧪 | 名称归类、无重叠会话窗口和全局未识别收件箱均已接入；重叠会话自动放弃时间推断，待 Windows 数据验证 |

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

## 2026-07-27 0.3.2 动画崩溃精准修复

- Playnite 主日志确认崩溃由导航项和指标卡悬停进入 `DashboardView.AnimateTranslate` 引发。
- 异常为对已冻结 `TranslateTransform` 调用 `BeginAnimation`，不是毛玻璃绘制或页面进入 Storyboard。
- Style Setter 不再提供共享 Transform；所有平移和缩放动画会创建或克隆当前元素专属的可变 Transform。
- 构建前检查新增 Style RenderTransform Freezable 风险检测，避免同类问题再次进入提交。
- Windows 仍需回归侧栏、指标卡、按钮和状态胶囊动画。


## 2026-07-27 0.3.3 开发安装可靠性

- 新增双击式一键构建、测试、打包、安装和启动入口。
- 自动发现标准或便携 Playnite 扩展目录；若发现已有安装，则优先更新实际存在的目录。
- 安装前停止 Playnite Desktop/Fullscreen 与 Worker，避免 DLL 锁定。
- 安装采用临时目录验证后原子替换，禁止静默保留旧版本。
- 打包文件名改为跟随 extension.yaml 版本。
- 安装完成后检查 extension.yaml 与 GameSaveCenter.Playnite.dll 文件版本，并生成 `artifacts/last-dev-install.txt`。


## 2026-07-27 0.3.4 Windows 一键入口编码修复

- 双击入口改为 ASCII-only + CRLF，避免中文 Windows 的 `cmd.exe` 将 UTF-8 字节拆成命令。
- 新增英文文件名 `GameSaveCenter-Run.cmd`；中文入口作为兼容快捷包装。
- `dev-install-run.ps1` 改为 UTF-8 BOM，并自动生成 `artifacts/one-click-install.log`。
- 源码校验增加批处理编码、换行和 PowerShell BOM 门禁。
- Windows 回归目标：双击后必须进入 PowerShell 构建流程，安装报告显示清单与 DLL 均为 0.3.4。

## 2026-07-27 0.3.5 历史同步、大型库检索与主题适配

- 修复 `DurationDisplay` 只读属性绑定方向，自动刷新不再因 WPF 绑定异常停用。
- `backup.list` 改为先与 Ludusavi 历史对账再返回 SQLite 索引；任务成功且磁盘已有 ZIP 时，历史页可自愈。
- 保护历史缓存：Ludusavi 报告存在版本但解析为零时，不再删除现有索引。
- 965 款游戏场景增加即时搜索、状态筛选、排序和结果数量。
- 任务页重排进度列，百分比不再覆盖进度轨道；空闲时彻底隐藏底部进度组件。
- 新增基于宿主实际背景和 Playnite 文字资源的自适应色板，覆盖第三方主题，不再仅按黑白模式判断。
- 全局启用像素对齐、Display/ClearType/Fixed hinting；移除正文控件透明度和按钮悬停缩放，改善 DPI 下文字锐度。
- 设置页同步使用派生输入框、文字、边框和玻璃表面色板。

## 2026-07-27 0.4.0 自动候选与 UI 规范门禁

- 将用户提供的完整 Apple-inspired WPF/Codex 规范原样保存到 `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`。
- 新增 `docs/design/UI_CHANGE_GATE.md`，后续新增控件、动画、主题色和材质前必须先通过门禁检查。
- 外观设置新增“跟随 Playnite / 浅色 / 深色”，第三方主题对比异常时可固定局部稳定色板。
- 对未匹配游戏接入会话前文件快照与退出后差异分析，候选按目录聚合并记录新增/修改数量、评分理由和 WGS 特征。
- 候选不会静默生效；用户可以接受生成规则草案或明确忽略，已接受路径不会被后续扫描重新降级为 Pending。
- 任务详情新增“复制详情”和仅针对备份/媒体任务的“安全重试”。
- Game Bar 与 Windows 公共截图目录在退出同步时可使用本次会话时间窗口归类；检测到其他游戏会话重叠时自动放弃时间推断，避免误归类。
- 仍需 Windows 验证：大型目录扫描耗时、会话结束候选准确率、主题模式即时切换、任务重试与候选按钮状态。

## 2026-07-28 0.4.1 全局媒体收件箱

- 完成公共目录单次扫描、保守归类、歧义原因、待归类持久化、人工归类和忽略闭环。
- 修复旧 SQLite 数据库升级顺序：分类字段必须先补列，再建立 `ix_media_classification`。
- 修复归档副本缺失时可能误移动原始截图的风险；现在只重建归档副本。
- 增加静态媒体收件箱门禁，防止 IPC、迁移顺序、源文件保护和 UI 命令在后续重构中丢失。
- 当前环境无 Windows/.NET/Playnite，未执行真实构建、安装或媒体目录端到端测试。

## 2026-07-28 0.4.1 Windows 构建热修复

- 修复 `DashboardView.xaml` 与 `GameSaveCenterSettingsView.xaml` 将 `ResourceDictionary.MergedDictionaries` 直接放在 `UserControl.Resources` 下导致的 `MC3074`。
- 两个资源区现在使用显式 `<ResourceDictionary>` 包裹合并字典和本地样式，符合 WPF XAML 属性语法。
- `validate-source.py` 新增资源字典父级门禁，后续出现同类结构会在交付前直接失败。
- 统一 Git 文本换行为 LF，`.cmd` 继续按二进制保留 CRLF；修复 Windows 编辑器按旧 `.editorconfig` 自动改写 Markdown/Python 文件导致工作区反复变脏的问题。
- 待 Windows 重新执行 `GameSaveCenter-Run.cmd`，确认 Playnite 工程继续进入下一编译阶段。
## 2026-07-28 0.4.2 Playnite 侧栏崩溃热修复

- 用户真机日志确认 Playnite 10.56 成功加载 GameSaveCenter 0.4.1，点击侧栏时在 `DashboardView.InitializeComponent()` 抛出 `XamlParseException`。
- 根因是媒体收件箱计数使用 `{StaticResource GscStatusPill}`，但资源字典中没有对应样式；静态资源区分大小写且加载时必须存在。
- 新增 `GscStatusPill` Border 样式，并将不存在的 `GscCardBrush`、`GscHairlineBrush` 替换为已有 `GscGlassStrongBrush`、`GscGlassStrokeBrush`。
- `validate-source.py` 新增所有 `Gsc*` StaticResource/DynamicResource 引用解析门禁，防止自有资源名缺失再次进入交付包。
- 版本提升为 0.4.2，便于在 Playnite 附加组件页区分崩溃版 0.4.1 与修复版。
- 当前环境仍无 Windows/.NET/WPF/Playnite，必须由真机重新执行一键构建安装并打开侧栏验证。

## 2026-08-04 0.6.22 UI-057 全局游戏入口与列表滚动收口

- 保持顶部唯一 GamePicker 与六个工作区架构；游戏级工作区不再依赖常驻重复游戏浏览栏，任务中心和维护中心保持全局视角。
- 修改器、FLiNG 搜索结果和可下载版本列表显式启用内部垂直滚动、关闭横向溢出，并继续使用 Recycling 虚拟化。
- 首页“需关注”入口补充无障碍名称；关注原因列表继续绑定 `AttentionFindings`，支持进入维护中心查看完整建议。
- `WpfUiResourceDictionaryTests` 新增列表滚动门禁和需关注入口可访问性门禁；Release 测试 103 项通过，Release 构建 0 警告、0 错误。
- 当前仍未声称完成 Playnite 宿主渲染、125/150% DPI、透明关闭、高对比度和拖动缩放的真机验证；这些仍需 Windows 回归。

## 2026-08-04 0.6.22 UI-058 共享表格视觉与低高度滚动收口

- `WpfUiProduction.xaml` 新增隐式 `DataGridColumnHeader`、`DataGridCell`、`DataGridRow` Apple-inspired 共享模板：统一表头、圆角行、悬停/选中状态、键盘焦点和动态主题色；列表行没有使用 BlurEffect。
- 概览、存档、媒体、任务和维护工作区的 DataGrid 继承共享模板，保留行/列虚拟化、内部横纵滚动，并将最小视口提高到 220 DIP、行高提高到 48–54 DIP，避免内容挤成一行。
- 修改器中心的工具设置改为有限高度 ScrollViewer；短窗口下设置区自身滚动，不再把主要工具列表或工作区标签推到可视区域之外。
- 增加共享表格模板、工作区 DataGrid 契约和修改器设置滚动的自动测试；Release 测试 105 项通过，Release 构建 0 警告、0 错误。
- 本次仍未声称完成 Playnite 宿主渲染、DPI、透明关闭、高对比度、键盘 Popup 和真实窗口拖动缩放的真机验证；这些仍需 Windows/Playnite 回归。

## 2026-08-04 0.6.22 UI-059 低高度检查器与系统偏好动态适配

- 媒体中心详情检查器改为独立有限高度滚动通道；WrapPanel 的元数据和操作控件不再把媒体表格推离可视区域。
- 维护中心诊断摘要限制为 90–220 DIP，并保留文本内部滚动，避免长诊断内容占用无限 Auto 行。
- Dashboard 与设置页监听 `SystemParameters.StaticPropertyChanged`；高对比度、透明度和系统动画偏好在 Playnite 运行期间变化时，动态重建局部色板/响应式布局，并在卸载时解除订阅。
- 新增 UI 门禁覆盖媒体检查器、诊断文本有限高度和系统偏好事件订阅；仍需 Windows/Playnite 验证系统设置动态切换和真实宿主渲染。

## 2026-08-04 0.6.22 UI-060 概览与任务详情滚动收口

- 任务中心详情卡改为独立有限高度滚动区，长错误详情和窄宽度操作换行不会挤走任务表。
- 概览窄宽度堆叠时，风险与提醒区域增加有限高度滚动；关注项增多时仍能访问最近活动表格。
- 新增对应 XAML/响应式门禁；Release 测试 105 项通过，Release 构建 0 警告、0 错误。
- 仍需 Windows/Playnite 在低高度、DPI 和真实数据量下验证宿主测量结果。

## 2026-08-04 0.6.22 UI-061 共享表格排序反馈

- 共享 `DataGridColumnHeader` 模板新增圆角主题化升序/降序箭头；提取工作区排序时不再只有行为没有视觉反馈。
- 排序箭头使用动态强调色，保留表头内容、键盘焦点、列宽调整和现有排序命令；没有对大型表格增加模糊效果。
- 新增模板门禁并通过 105 项 Release 测试、0 警告构建；仍需 Playnite 实机确认第三方主题下箭头对比度。

## 2026-08-04 0.6.22 UI-062 GamePicker 键盘入口收口

- 顶部全局 GamePicker 搜索框接入统一 Enter/Esc 预览键处理；搜索框获得焦点时也能确认当前选择或关闭抽屉。
- 保持本地搜索、防抖、虚拟化和现有选择状态不变；补充 XAML 门禁并通过 105 项测试和 Release 构建。

## 2026-08-04 0.6.22 UI-063 存档中心底部操作滚动收口

- 历史版本的备注、锁定、恢复、比较和撤销操作改为独立有限高度 ScrollViewer；窄宽度下 WrapPanel 换行不会再把历史 DataGrid 推离可视区域。
- 候选路径的判断依据和操作按钮分别拥有内部纵向滚动通道；长依据文本或窄窗口按钮换行时仍保持候选表格位于上方 `*` 行并可滚动。
- `SaveCenterView.ApplyResponsiveLayout` 根据窗口宽高动态限制三个次级区域高度，保留表格虚拟化和内部滚动；新增 XAML/响应式门禁。
- 当前环境仍未运行 Playnite 宿主、DPI、主题和拖动缩放真机回归，需在 Windows 继续验证 1600/1280/1024/850/700 DIP。

## 2026-08-04 0.6.22 UI-064 表格文本对齐收口

- 共享 `DataGridCell` 默认改为左对齐，长路径、任务详情、媒体来源和诊断文本不再在有限列宽中居中挤压。
- 状态点、进度条和操作按钮仍可通过各自模板显式使用居中或 Stretch，不改变现有命令、排序、焦点和虚拟化。
- 颜色、圆角和焦点仍由共享动态资源提供；新增共享样式门禁，待 Windows 实机确认第三方主题下列内容对齐和排序箭头共存效果。

## 2026-08-04 0.6.22 UI-065 维护中心设备操作滚动收口

- 设备状态页的人工决策和受保护远端恢复区域改为独立有限高度 ScrollViewer，窄窗口下的长判断依据不会再挤走设备对比表。
- 响应式代码按宽高动态限制两个次级操作区，保留表格 star 行、内部滚动和原有恢复/决策命令。
- 新增维护中心 XAML/响应式门禁；仍需 Windows 低高度、DPI、主题和真实设备摘要回归。

## 2026-08-04 0.6.22 UI-066 顶部操作栏窄宽度收口

- 顶部操作栏改为局部水平滚动通道；当第三方主题或高 DPI 下操作按钮超过可用宽度时，按钮不会被裁剪或覆盖标题。
- 窄模式仍隐藏非必要按钮文字，保留图标、Tooltip 和键盘可达性；页面主体不引入全局水平滚动。
- 新增 HeaderActions 的 XAML 门禁，待 Windows 700/850 DIP 和 125/150% DPI 实机确认滚动条与焦点效果。

## 2026-08-04 0.6.22 UI-067 修改器在线库版本联动修复

- 修复提取后的 `TrainerCenterView` 只更新 `SelectedTrainerCatalogItem`、却没有触发 `LoadTrainerReleasesCommand` 的工作区回归；现在选择 FLiNG 搜索结果会真实加载可下载版本列表。
- 事件处理位于修改器工作区自身，不再依赖已隐藏的旧 Dashboard 标签页事件，保持六个工作区提取后的行为闭环。
- 新增 XAML/代码门禁；Release 测试 109 项通过，Release 构建 0 警告、0 错误。
- 仍需 Windows/Playnite 验证 FLiNG 目录同步、版本列表加载、下载绑定和主题/DPI 下的可达性。

## 2026-08-04 0.6.22 UI-068 首页重复统计收口

- 移除 Dashboard 外壳中仍残留的旧六张统计卡片；摘要、需关注和最近活动统一由已提取的 `OverviewView` 负责，避免普通窗口高度下重复占用垂直空间。
- 保留 `OpenAttentionCenterCommand` 和 `AttentionFindings` 的具体原因入口，不改变业务数据或命令行为。
- 新增结构门禁；Release 测试 110 项通过，Release 构建 0 警告、0 错误。

## 2026-08-04 0.6.22 UI-069 Dashboard 旧工作区清理与媒体筛选补足

- 删除 Dashboard 中已由六个提取工作区替代的旧 Overview、存档、修改器、媒体、任务、诊断、设备、日志和开发探针 Tab，避免隐藏视觉树重复测量、旧事件入口和无效的响应式字段残留。
- 响应式协调收口为 Dashboard 外壳只切换六个工作区；存档、媒体、修改器、任务和维护的局部滚动/换行由各自 View 负责，保留虚拟化与键盘入口。
- 媒体中心“当前游戏媒体”补回本地搜索框和类型筛选 ComboBox，绑定 `MediaSearchText`、`MediaFilterOptions` 和 `MediaFilter`，不在输入时访问 Worker。
- 维护中心设备对比、进程映射和诊断文本列统一复用长文本省略/Tooltip 样式；同步更新结构校验器和 WPF 回归测试，使门禁检查提取后的真实归属。
- 源码校验、XAML 结构检查、Release 测试 110 项（Core 13、Worker 21、Playnite 76）和 Release 构建均通过，0 警告、0 错误。
- 当前仍未声称完成 Playnite 宿主渲染、DPI、主题切换、透明降级和真实窗口缩放回归；需在 Windows/Playnite 继续验证。

## 2026-08-04 0.6.22 UI-070 共享表格密度与滚动可读性收口

- 新增 `GscTableRowHeight`、`GscTableMinHeight` 和 `GscTableHeaderHeight` 设计令牌，统一六个工作区及 Dashboard 兼容样式的表格密度；默认行高 54 DIP、表头 44 DIP、最小可读视口 280 DIP。
- 概览、存档、媒体、任务和维护表格继续使用有限 Grid 测量、内部纵向/横向滚动和行列虚拟化；没有给 DataGrid 外层增加 ScrollViewer，避免破坏大库性能。
- 任务中心移除页面实例上的硬编码行高，避免共享主题与局部属性冲突；所有状态点、进度条和操作列仍保留各自模板对齐。
- 更新 WPF 结构测试，门禁现在验证共享表格令牌而不是散落的 220 DIP 硬编码；仍需 Windows/Playnite 在 1600/1280/1024/850/700 DIP、125%/150% DPI 和四种主题下确认真实测量。

## 2026-08-04 0.6.22 UI-071 紧凑窗口留白与共享行高一致性

- `DashboardView.ApplyResponsiveLayout` 现在按 Expanded / Standard / Compact / Narrow 动态调整外壳边距、详情卡片内边距以及工作区标签上间距；低高度窗口会回收装饰性留白，将有限空间优先交给工作区的星号行和内部滚动区。
- 共享隐式 `DataGridRow` 的最小高度改为使用 `GscTableRowHeight`，避免公共模板仍以旧的 48 DIP 覆盖各工作区的 54 DIP 可读表格密度。
- 这仍然不替代 Windows 宿主中的 DPI、主题、透明度和 Playnite 窗口实机回归。

## 2026-08-04 0.6.22 UI-072 表格交替行与主题层次

- 五个提取工作区和 Dashboard 兼容 DataGrid 样式现在统一使用 `GscTableAlternateRowBrush`，
  交替行、悬停行和选中行具有明确但克制的层次。
- 交替行颜色由 Dashboard 的自适应调色板按浅色/深色动态计算，高对比度下关闭额外填充，
  避免覆盖系统对比色；默认 DesignTokens 仍提供离线解析回退。
- 该改动只改变表面层，不改变 DataGrid 的内部滚动、排序、列宽和 Recycling 虚拟化。

## 2026-08-04 0.6.22 UI-073 低高度表格最小视口保护

- 正常高度仍使用 280 DIP 的表格最小可读视口；Dashboard 在低于 760/650 DIP 时分别将
  `GscTableMinHeight` 降为 220/180 DIP，避免表格的固定最小值把检查器和操作区推出窗口。
- 该资源通过 `DynamicResource` 更新，表格行仍由 DataGrid 内部滚动访问；没有给工作区增加页面级
  ScrollViewer，也没有关闭虚拟化。

## 2026-08-04 0.6.22 UI-074 工作区主题资源传播

- 抽出的六个工作区各自拥有本地资源字典；Dashboard 现在通过统一的
  `ApplyRuntimeThemeResources` 将浅色、深色、跟随 Playnite、高对比度和透明降级资源同步到每个工作区。
- 这避免子 UserControl 继续停留在 `DesignTokens.xaml` 的静态默认颜色，确保表格交替行、Popup、文字、状态色和滚动条在切换主题后保持一致。

## 2026-08-04 0.6.22 UI-075 列表项圆角与键盘焦点

- WPF-UI 生产字典新增统一的圆角 `ListBoxItem` 模板，提供悬停、选中、禁用和键盘焦点状态；FLiNG 结果与版本列表不再回退到宿主默认方形选中背景。
- 修改器已安装列表移除隐藏焦点视觉，继续保持虚拟化/ Recycling，同时使用共享焦点环和动态主题资源。

## 2026-08-04 0.6.22 UI-076 工作区入口焦点状态

- 顶部当前游戏入口和设置分类页签补齐共享键盘焦点环，避免自定义模板只支持鼠标悬停而无法通过 Tab 清晰定位。

## 2026-08-04 0.6.22 UI-077 诊断展开器共享圆角与滚动

- 维护中心的完整诊断区域改用共享 `GscExpander` 模板，标题、箭头、悬停、展开、禁用和键盘焦点均使用动态主题资源。
- 诊断正文继续由只读 `TextBox` 提供纵向/横向滚动；未给长文本或列表增加 BlurEffect，也不改变诊断加载和绑定逻辑。

## 2026-08-04 0.6.22 UI-078 列表滚动与虚拟化共享契约

- WPF-UI 生产资源新增隐式 `ListBox` 样式，统一本地纵向滚动、禁用页面级横向滚动、`CanContentScroll` 和 Recycling 虚拟化。
- 列表项继续由共享圆角模板负责视觉层，工作区特化列表可覆盖外观但不会丢失键盘访问和大列表滚动能力。

## 2026-08-04 0.6.22 UI-079 滚动条强调色跟随主题

- 滚动条 Thumb 的悬停色不再固定为旧紫色，改为由运行时 `AdaptiveThemePalette.AccentHover` 计算，兼容蓝色、紫色、自定义 Playnite 强调色和高对比度模式。


## 2026-08-02 UI-005 全功能视觉重构（源码阶段）

- 在不修改业务层的前提下建立 `Themes/Redesign.xaml`，统一最终页面的玻璃表面、圆角、指标卡、标题按钮、游戏选择器、状态卡和设置分类导航。
- Dashboard 六个工作区均进入新信息架构：首页采用关键指标/当前游戏/活动/风险分层；存档历史使用列表与安全详情检查器；修改器、媒体、任务和维护中心增加各自摘要与工作台层级；游戏级页面共享当前游戏选择器，并保留完整搜索/筛选/排序游戏库入口。
- 响应式不再依赖 WrapPanel 猜测：1280/980/880 DIP 四档显式调整侧栏、标题、操作栏、游戏选择器、游戏库和检查器；Compact/Narrow 导航固定 48×48，Worker/Ludusavi 固定 48×50 并由模板居中，侧栏裁切防止选中背景越界。
- Settings 改为左侧分类/右侧当前分类；低于 920 DIP 时分类移动到顶部并自动换行，输入分组按实际可读宽度降为两列或单列。保留所有原字段、校验、主题事件和导入导出行为，并明确由 Playnite 保存按钮提交。
- 自动比对确认 Dashboard 的 56 个唯一 Command、236 个原有唯一 Binding、Settings 的 25 个唯一 Binding 和原业务事件均未删除；大列表仍位于有限 Grid 测量路径并使用既有 Recycling 样式。
- 当前仅完成静态阶段：源码门禁、XML、UI Skill（0 errors）和 `git diff --check` 通过；新增 4 项响应式/资源/功能保留合约测试。Windows Release build、87 项测试、打包、Worker smoke 和真实 Playnite/DPI/主题验收因当前环境缺少工具而未执行，任务保持 `BLOCKED_ENVIRONMENT`。
### UI-080：统一列表局部滚动与焦点导航契约

- 生产 `ListBox` 隐式样式统一启用 `ScrollViewer.PanningMode=VerticalOnly`，让触控、触摸板和滚轮事件在列表内部消费，避免外层工作区发生意外滚动。
- 统一 `KeyboardNavigation.TabNavigation=Local` 与 Recycling 虚拟化，确保大库列表在 Tab 导航时保持局部焦点语义，不改变现有命令和选择绑定。
- 本次只修改共享资源，并以资源门禁锁定滚动、虚拟化和键盘导航属性。
### UI-081：修复游戏列表悬停色主题同步

- Dashboard 游戏选择器的悬停背景改为 `DynamicResource GscRowHoverStrongBrush`，不再把浅色默认悬停色冻结在 XAML。
- `AdaptiveThemePalette` 现在为普通、深色和高对比度模式生成共享悬停资源，切换蓝色/紫色/自定义 Playnite 强调色时保持一致。
- 新增资源门禁，避免生产页面重新引入静态悬停 Brush。
- 兼容列表模板中的悬停状态也同步切换为 DynamicResource，防止隐藏迁移模板重新引入旧主题颜色。
### UI-082：首页低高度次级面板局部滚动

- 首页在 Standard/Compact/Narrow 堆叠布局下，为“今日概览/风险与提醒”次级面板增加独立纵向 ScrollViewer，避免风险卡片把整个页面下方内容推出视口。
- DataGrid 仍留在主活动面板自己的星号行和内部滚动通道中，没有用外层无限测量破坏行虚拟化。
- 只在堆叠模式启用外层滚动；宽屏模式保持无额外滚动轨道。

### UI-083：DataGrid 共享滚动与虚拟化契约

- `WpfUiProduction.xaml` 新增隐式 `DataGrid` 滚动契约，统一 `CanContentScroll`、纵向触控板滚动、Auto 滚动条、局部键盘导航和 Recycling 虚拟化。
- 工作区现有 keyed DataGrid 样式继续保留各自的圆角表头、行高、交替行和列宽设置；共享契约只补齐行为，不改变命令、编辑权限或数据绑定。

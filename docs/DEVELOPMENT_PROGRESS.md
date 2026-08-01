# 开发实现进度

更新时间：2026-08-01
当前版本：`0.6.22-development-preview`

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

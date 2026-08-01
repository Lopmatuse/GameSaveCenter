# GameSaveCenter 全量 UI 信息架构与视觉设计基线

更新时间：2026-08-01  
状态：已批准为后续 WPF 重构的设计基线；不是运行结果或 Playnite 真机验收记录。

## 1. 目标、范围与不可退让项

本设计把 GameSaveCenter 从“在一个大型工作台中堆叠所有功能”重组为按用户工作目标划分的六个一级工作区。它重新安排入口、层级与版式，但**不删除、不隐藏、不弱化**现有备份、恢复、校验、云端、媒体、修改器、任务、诊断与设备恢复功能。

实现仍然是 Playnite 内嵌的 WPF `UserControl`：不创建自绘窗口、不修改宿主 Chrome、不使用 HTML/WebView，也不添加第三方 UI 框架。默认界面是清晰的近不透明阅读面；毛玻璃只属于导航、菜单、提示、抽屉与少量固定环境光，绝不落在游戏行、DataGrid 行、日志或滚动内容上。

| 保留的行为 | 设计约束 |
| --- | --- |
| 所有既有 `Command`、绑定、取消、错误与安全确认路径 | 页面迁移前逐项建立命令映射和自动化回归测试。 |
| 游戏、媒体、任务的大型列表 | `VirtualizingStackPanel`、Recycling、行/列虚拟化持续启用；不为样式包裹纵向 `StackPanel`。 |
| 备份/恢复/下载/文件扫描/云端操作 | 继续在后台执行，UI 只观察真实状态；不以动画或延迟伪造进度。 |
| 键盘、焦点、屏幕阅读器与长文本 | 保留/补齐 Automation Name、Focus Ring、Tooltip、复制和横向访问。 |
| Playnite 宿主兼容性 | 不使用 `ContentDialogHost`；确认继续使用插件本地遮罩，Toast 使用局部 Presenter。 |

## 2. 设计语言：克制的玻璃与平面阅读面

### 2.1 表面层级

视觉比例为 **70% 清晰阅读面 / 20% 低透明结构面 / 10% 浮层和强调**。

1. **阅读面**：游戏详情、策略表单、DataGrid、日志、媒体网格使用接近不透明的 `GscCardBackgroundBrush`。边框极浅，靠表面亮度和间距分组，而非粗线。
2. **结构玻璃**：左侧导航、顶部上下文条、Inspector 抽屉、Popup、Toast 使用 `GscSidebarMaterialBrush` 或 `GscFloatingSurfaceBrush`。它们在透明关闭时降级为同层级不透明色块。
3. **环境光**：根布局背后最多三枚固定、低不透明度的淡紫/蓝绿/暖白椭圆。仅这三枚元素可有静态 `BlurEffect`，不随滚动移动、不连续动画，并且在高对比度、透明关闭和紧凑性能模式隐藏。
4. **危险操作**：恢复、删除或解绑不以“漂亮”取代风险提示；在本地遮罩中显示游戏名、版本、后果、取消和日志/详情入口，默认焦点不落在危险按钮。

### 2.2 尺寸、字体与状态

- 圆角：控件 8 DIP、列表/输入 10–12 DIP、卡片 16 DIP、抽屉/对话框 18 DIP、顶级大面 22 DIP；不使用 999 DIP 伪胶囊。
- 间距：4/8/12/16/24/32 DIP；同一层级只用一套节奏。
- 文字：页标题 25–28、区块标题 16–18、正文 13–14、辅助信息 12–13 DIP；全局 `UseLayoutRounding` 和 `SnapsToDevicePixels`。
- 强调色：低饱和紫色只用于主操作、当前导航/页签、焦点环和小面积选中状态。成功、警告、错误均须同时有图标/状态点和文字。
- 动效：Hover 120ms，Pressed 90ms，导航/Tab 180–220ms，抽屉 220ms，Toast 220ms。仅改变 `Opacity`、元素独占的 `RenderTransform` 或颜色；禁止动画布局、滚动区域、模糊半径和每行计时器。

### 2.3 主题和可访问性降级

`Follow Playnite`、浅色、深色、高对比度、关闭透明、关闭动画必须均由共享 `DynamicResource` 令牌表达。关闭透明后玻璃表面改为实色，关闭动画后保留即时但完整的状态转换。焦点环绝不能被删除；长游戏名、路径、错误和版本号以省略号 + Tooltip + 详情/复制路径呈现。

## 3. 新的应用结构

```
Playnite 宿主
└─ GameSaveCenter
   ├─ 总览                         快速判断今天是否安全、有什么需要处理
   ├─ 游戏与存档                   围绕一个游戏完成备份、历史、路径和策略
   ├─ 修改器与工具                 管理本地工具、在线目录和可下载版本
   ├─ 媒体                         归类收件箱、整理媒体库、维护来源规则
   ├─ 任务                         处理运行、失败、历史和错误详情
   └─ 维护                         诊断、设备比较、进程映射、保留策略与日志

Playnite 设置页（独立入口）
└─ 常规、存档与恢复、云端、媒体、外观与可访问性、诊断
```

一级导航保持当前六个模块，避免与 Playnite 再复制一层“应用导航”。所有一级项目都有文字、轻量向量图标、Automation Name、Tooltip 和可见选中状态；中等/紧凑宽度改为图标优先而不是删除入口。

## 4. 每个页面的详细设计

### 4.1 总览：今天的状态，而不是第二个数据仓库

**目的**：进入插件后的 5 秒内知道是否有失败、可备份游戏、待归类媒体和等待云端的工作。

- 顶部为“今日状态”标题、最后刷新时间、`刷新`、`全部备份`、`同步媒体`。任务进行时按钮显示真实不可用原因/进度，不移动布局。
- 宽屏首屏为六张等高指标卡：已管理游戏、已匹配存档、正在运行、需要注意、云端队列、待归类媒体。卡片仅显示真实 ViewModel 值和一句可操作解释，点击进入对应工作区及已过滤状态。
- 第二行左侧为“需要处理”（失败任务、未匹配路径、云端重试到期、设备冲突），右侧为“当前/最近游戏”上下文卡。每项直接带安全操作，绝不把技术堆栈当标题。
- 底部是可虚拟化的“最近活动”轻表；成功、失败、取消和排队均有图标、文字、时间、游戏名，选中后在右侧或下方 Inspector 展开详情。
- 空状态说明下一步：未检测到 Ludusavi、没有匹配游戏、没有可同步媒体分别给出真实路径。

### 4.2 游戏与存档：主从结构，以一个游戏为中心

**目的**：将当前分散在存档、候选路径、历史、策略、恢复的操作收敛为一处，不让用户反复在大页中寻找选中游戏。

**宽屏布局**：左侧 320–360 DIP 虚拟化游戏列表；右侧为选中游戏详情。顶部搜索/筛选固定，内容分别滚动。左侧一行显示封面或图标、游戏名、平台、匹配状态、版本数量、媒体数量、运行状态；长名用 Tooltip。右侧固定标题区显示游戏名、识别来源、状态和三个优先级明确的操作：`立即备份`（主）、`校验`、`侦测路径`，其他操作在“更多”。

右侧局部页签如下，页签只切换当前游戏的内容：

| 页签 | 内容与完整功能 |
| --- | --- |
| 存档历史 | 版本表（时间、大小、备注、锁定、云端状态），Inspector 显示完整元数据；保留安全恢复、撤销最近恢复、与上一版本比较、保存备注/锁定、保留策略预览。恢复仍先走本地安全确认。 |
| 路径与校验 | 候选路径评分、来源、最后扫描结果、校验异常；保留立即扫描、接受并生成规则草案、忽略候选。不可用路径显示可复制详情与下一步。 |
| 策略 | 退出后备份、运行中周期备份、间隔、云端开关和保存策略。数值输入使用统一“编辑完成后提交”的数值控件，完整显示 1–1440，不在每次按键写回 `int`；有范围错误、全选编辑和可见单位。 |
| 比较与保留 | 只读比较摘要和保留策略预览。Ludusavi 没有安全的按版本删除 API 前，不提供删除落地动作。 |

**中等宽度**：列表 280–300 DIP，详情单栏；策略和比较从双列改为单列。  
**紧凑宽度**：不硬挤双栏。顶部显示游戏选择器/“游戏列表”抽屉入口；详情全宽，抽屉打开时保持返回和键盘焦点，表格保留横向滚动。

### 4.3 修改器与工具：区分“我已经有的”和“我准备下载的”

顶部显示选中游戏上下文和 `导入修改器` / `导入 CT` / `导入目录`。导入多入口选择仍以安全、可取消的待确认面板出现。

- **已绑定工具**：卡片式工具列表与右侧 Inspector。保留启动、保存设置、打开目录、解除绑定；版本可用性及入口候选明确显示。
- **在线目录**：固定搜索框、搜索、刷新目录、虚拟化结果列表。不会在输入每个字符时同步网络搜索；按现有搜索命令执行。
- **可下载版本**：只有选中在线结果后才按需加载。保留读取可下载版本和下载操作，显示真正进度、错误、取消路径。

宽屏为列表 + Inspector；紧凑时 Inspector 变为覆盖抽屉，下载/启动等主要动作仍固定可达。

### 4.4 媒体：三条明确工作流，而非混杂网格

| 局部页签 | 内容与操作 |
| --- | --- |
| 待归类 | 左侧虚拟收件箱、右侧目标游戏选择与媒体预览；保留确认归类、忽略并保留副本。空状态明确说明扫描来源。 |
| 媒体库 | 有搜索/筛选的虚拟化网格或表格，选中一项后 Inspector 显示预览、路径、元数据、收藏、备注；保留保存元数据、打开、目录中显示、移动并重新归类、批量收藏/取消收藏/应用备注。仅当前选中项加载预览。 |
| 来源与规则 | 可编辑来源规则列表；保留添加来源、启用、更新、移除。每行不使用 Effect 或长期动画。 |

媒体缩略图异步缓存、可取消并按需加载；大库仍然可滚动。紧凑布局让预览成为抽屉而不是与网格并排挤压。

### 4.5 任务：把失败的可恢复性放在最前面

顶部四个快速过滤：全部、运行中、需要重试、已完成，并显示真实数量。主区域是可回收的轻量 `DataGrid`：时间、类型、游戏、状态、进度、摘要；列具有最小宽度和横向滚动，不用所有列 `*` 压缩。

选中任务在右侧 Inspector（紧凑时抽屉）显示阶段、可取消性、完整错误、关联日志和重试条件。保留 `取消任务`、`安全重试`、`复制详情`；失败状态不会只染红一整行，也不自动隐藏。云端单向复制重试队列在此处显示重试次数和下一次时间；未启用云端/Rclone 不可用/本地源缺失时显示“等待配置恢复”，不制造重试风暴。

### 4.6 维护：技术能力集中，但不让普通用户先面对它

维护页首屏是一条健康摘要：Ludusavi、Rclone、Worker、数据目录、备份目录、媒体目录；每项显示状态、问题原因和安全的路径/日志动作。

下方局部页签：

| 页签 | 内容与保留功能 |
| --- | --- |
| 诊断与日志 | 刷新诊断、复制诊断、打开数据/存档/媒体目录、打开 Worker 日志；日志按需/增量读取，完整路径和错误可复制。 |
| 进程映射 | 可访问的 EXE → 游戏表和绑定表单；保留绑定与删除映射，避免通过 UI 误杀进程。 |
| 设备状态 | 仅在此页显示多设备比较和人工决策；保留刷新设备状态、保存人工决策、下载并校验、创建快照并恢复。恢复动作继续要求安全确认。 |
| 保留策略 | 只读预览：明确显示会保留/可能清理的候选及“不执行删除”的产品边界。 |

### 4.7 设置：减少视觉密度，不减少可配置项

设置继续通过 Playnite 的设置入口打开。页面使用一个垂直滚动阅读面，在宽屏最多两列、在中等和紧凑宽度一列；每张卡只放同类设置，底部保存/应用状态固定可见。

1. **常规与目录**：数据、备份、媒体目录与 Worker 行为。
2. **备份与恢复**：间隔、保留、压缩等级、恢复保护；所有数字字段复用数值输入样式。
3. **云端**：Rclone 配置、目标、重试/状态说明；配置异常为 Inline 错误。
4. **媒体**：收件箱、归档和扫描规则。
5. **外观与可访问性**：跟随 Playnite/浅/深、透明强度、减少动画、高对比度降级说明。
6. **诊断**：隐私安全的复制/导出信息，不承诺真实配置之外的成功。

## 5. 全功能映射门禁

下表是迁移时必须保留的命令集合。实现提交必须以测试或 XAML 绑定证明每一项仍可从鼠标和键盘到达。

| 当前功能族 | 新位置 | 必须保留的命令 |
| --- | --- | --- |
| 全局刷新与批处理 | 总览顶部 | `RefreshCommand`、`BackupAllCommand`、`SyncMediaCommand`、`OpenAttentionCenterCommand` |
| 单游戏存档 | 游戏与存档 | `BackupSelectedCommand`、`DetectPathsCommand`、`ValidateCommand`、`RestoreCommand`、`UndoRestoreCommand`、`LoadDetailsCommand`、`SavePolicyCommand`、`UpdateBackupMetadataCommand`、`CompareBackupCommand`、`PreviewRetentionCommand`、`AcceptCandidateCommand`、`RejectCandidateCommand` |
| 修改器与工具 | 修改器与工具 | `ImportTrainerCommand`、`ImportCheatTableCommand`、`ImportToolFolderCommand`、`ConfirmGameToolImportCommand`、`CancelGameToolImportCommand`、`SaveGameToolCommand`、`LaunchGameToolCommand`、`OpenGameToolDirectoryCommand`、`DeleteGameToolCommand`、`SyncTrainerCatalogCommand`、`SearchTrainerCatalogCommand`、`LoadTrainerReleasesCommand`、`DownloadTrainerCommand` |
| 媒体收件箱、库、来源 | 媒体 | `AddMediaSourceCommand`、`UpdateMediaSourceCommand`、`DeleteMediaSourceCommand`、`ReassignMediaCommand`、`UpdateMediaMetadataCommand`、`FavoriteSelectedMediaCommand`、`UnfavoriteSelectedMediaCommand`、`CommentSelectedMediaCommand`、`OpenSelectedMediaCommand`、`RevealSelectedMediaCommand`、`AssignInboxMediaCommand`、`IgnoreInboxMediaCommand` |
| 任务和错误 | 任务 | `CancelTaskCommand`、`RetryTaskCommand`、`CopyTaskErrorCommand` |
| 诊断、路径和日志 | 维护 / 诊断与日志 | `RefreshDiagnosticsCommand`、`CopyDiagnosticsCommand`、`OpenDataDirectoryCommand`、`OpenBackupDirectoryCommand`、`OpenMediaDirectoryCommand`、`OpenWorkerLogCommand` |
| 设备与远端安全恢复 | 维护 / 设备状态 | `SyncDeviceStatesCommand`、`SaveDeviceDecisionCommand`、`StageRemoteBackupCommand`、`RestoreStagedRemoteBackupCommand` |
| 进程关联 | 维护 / 进程映射 | `SaveProcessMappingCommand`、`DeleteProcessMappingCommand` |

## 6. 响应式和性能合同

| 模式 | 宿主内容宽度 | 导航与版式规则 |
| --- | --- | --- |
| Wide | ≥ 1280 DIP | 224 DIP 完整导航；游戏/工具/任务可使用列表 + Inspector；总览指标 3×2。 |
| Medium | 980–1279 DIP | 196 DIP 导航或图标优先；主从列表 270–300 DIP；指标 2×3；冗余副标题和次级操作收进更多菜单。 |
| Compact | < 980 DIP | 64 DIP 图标导航（Tooltip、Automation Name）；主从的列表和 Inspector 变为抽屉/选择器；内容一列；DataGrid 可水平滚动。 |
| Compact Height | < 760 DIP | 减少重复说明和空白，指标可横向滚动或折叠为摘要，不压缩正文/操作控件。 |

页面根使用 `Grid`：标题/工具栏为 `Auto`、内容为 `*`、操作栏为 `Auto`。不让整页有多余 `ScrollViewer`；导航、工具栏、表格、日志各自的滚动边界清楚。大列表保持回收虚拟化，筛选不重建复杂视觉树；转换器不得 I/O；缩略图、日志、文件扫描、压缩、哈希、网络和数据库操作不在 UI 线程执行。

## 7. 落地顺序和验收

1. **共享基础**：修复设计令牌、主题、焦点环、数值输入、Button/TextBox/ComboBox/Toggle/ScrollBar/DataGrid/ListBox/Tooltip/Popup/Toast 的共享模板及不透明回退。
2. **结构与导航**：引入响应式导航壳、页标题和局部 Inspector/Drawer，不改变命令语义。
3. **工作区迁移**：按总览、游戏与存档、修改器、媒体、任务、维护、设置逐个迁移，每个工作区一个独立提交和命令映射测试。
4. **安全与真机验证**：XAML 资源和绑定测试、数值输入测试、Dispatcher 边界、Release build、全部单元测试、打包和 Worker smoke。仅在有可证明独立数据根和 PID 边界的 Playnite 实例中运行主题/DPI/键盘/大库真机测试；当前用户 Playnite 绝不关闭、替换或作为测试目标。

每个 UI 提交还必须运行 `python scripts/validate-source.py`、`python C:\\Users\\lopmatuse\\.agents\\skills\\wpf-apple-desktop-ui\\scripts\\validate_wpf_ui.py .`、`git diff --check`、Release build 与所有测试。没有真实 Playnite 渲染证据时只能报告静态与自动化通过，不标记真机完成。

## 8. 明确不做的事情

- 不做自动更新发布或修改插件 ID、许可证、Git 历史。
- 不把表格/游戏列表/滚动内容玻璃化或高斯模糊化，也不以视觉效果关闭虚拟化。
- 不猜测 Ludusavi Vault 结构来实现按版本删除；保留策略继续是安全预览。
- 不对真实存档执行恢复，不对真实云端目录执行删除或镜像。
- 不使用 Window 全局 `ContentDialogHost`，以免与 Playnite 或其他扩展发生单例冲突。

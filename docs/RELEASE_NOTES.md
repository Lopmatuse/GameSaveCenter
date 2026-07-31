# 0.6.21 Development Preview

- 云端备份复制在本地备份已成功后失败，会将游戏、错误摘要、重试次数和下次执行时间持久化到 SQLite；Worker 重启后继续恢复。
- 自动重试使用 1、5、15、60、240、720 分钟退避，六次自动尝试均失败后停止并写入审计；Rclone 或备份目录未就绪时暂停扫描，避免失败任务风暴。
- 新增云端退避策略、SQLite 队列跨重启、成功清队列和暂停测试。
- 项目根目录接入 `wpf-apple-desktop-ui` 规则；共享数值输入支持完整值编辑、范围校验、可见错误环和键盘全选。
- 修复当前游戏备份策略的分钟输入框过窄且逐字符写回整数的问题；设置页的全部数值字段采用相同行为。
- 游戏、修改器和在线目录列表显式启用 Recycling 虚拟化，保持大库滚动性能。

# 0.6.20 Development Preview

- 修复 Worker 任务事件或自动刷新从后台线程触发 `PropertyChanged` 时，Dashboard View 先读取 `IsLoaded` 而导致 Playnite 崩溃的问题。
- `OnViewModelPropertyChanged` 现在首先检查并切回 WPF Dispatcher，随后才读取控件状态或安排动画。
- 自动刷新与后台同步由 `async void` 改为 `Task`；定时器与任务事件订阅会等待并观察该任务，异常通过受控路径处理。
- ViewModel 的后台刷新标志与状态文本只通过 UI Dispatcher 修改。

# 0.6.19 Development Preview

- Media collection can now be disabled by source: Steam, Xbox Game Bar, Windows Screenshots, game-adjacent directories, and custom rules.
- Each game can independently disable automatic tasks, exit-time media archiving, and during-play media archiving without disabling manual operations.
- Custom media rules now support pause/resume and safe removal; removing a rule never deletes source files or archived media.
- During-play media scheduling no longer depends on enabling timed backups.
- Backup retention remains a transparent preview only until Ludusavi exposes a stable single-version deletion API; the plugin will not guess its vault layout.

# 0.6.18 Development Preview

- Worker task changes now use a separate current-user event pipe for an open dashboard, so progress appears without waiting for the periodic refresh interval.
- Event delivery is best-effort and bounded; normal IPC, long-polling, and SQLite snapshots remain the authoritative recovery path after reconnects or Worker restarts.
- Added regression coverage for task-event fan-out, event snapshot isolation, subscription disposal, and source validation guards.

# 0.6.17 Development Preview

- 修复超大列表和长日志中滚动滑块缩成尖点的问题。
- 使用 WPF Track 官方最小 Thumb 系统资源机制，将纵向和横向滑块最小长度统一为 36 DIP。
- 保留单形状圆角胶囊绘制，避免端帽重叠和裁切。

# 0.6.16 Development Preview

- 滚动滑块改为单一圆角 Rectangle，不再使用重叠半透明端帽。
- 消除端帽叠色产生的白色椭圆与上下边缘不一致。
- Thumb 模板增加内部安全边距、布局取整与最小尺寸保护。

# 0.6.15 Development Preview

- 以 0.6.14 为基线重新实现全局滚动滑块。
- 使用独立圆形端帽和矩形主体绘制胶囊，修复一端圆弧、一端直线的问题。
- 可见滑块内缩于 Thumb 边界，避免 Playnite 宿主、DataGrid 和高 DPI 布局裁切。
- 正常状态隐藏可见轨道，仅保留滚动与分页点击行为。

# 0.6.14 Development Preview

- 统一修正首页统计卡片的内部对齐；“需要关注”移除多余箭头，图标固定在左侧，标题和数字在剩余区域居中。
- 任务筛选器在动态重建选项后会重新通知选中值，游戏和任务类型默认稳定显示“全部”。
- 任务进度列改为弹性宽度，进度条与百分比同一水平线，0%/100% 在普通与紧凑窗口下均保持完整。
- 修改器工具栏和设置迁移按钮使用统一 40 DIP 高度与显式水平间距，消除“导入 CT”等按钮高度/基线不一致。
- DataGrid 列宽拖动热区保持可用但不再绘制宿主主题的高亮白色拉块；长列继续支持列宽调整、Tooltip 和自动横向滚动。
- 全局滚动条改为带首尾安全内边距的 Track，纵向与横向 Thumb 到达边界时仍完整显示圆柱形圆角。
- “设备状态”页签只在维护中心显示，不再泄漏到首页、任务、存档、修改器和媒体页面。

# 0.6.13 Development Preview

- 多设备冲突页新增两阶段远端恢复：先下载完整远端 Ludusavi 库到本机隔离区，再单独确认恢复。
- Rclone 下载只读取 `<设备>/Saves`，哈希校验与下载共享传输锁；不调用 sync、delete、purge 或 move。
- 暂存标识、设备名和根目录均有路径穿越防护；校验失败会清除本次暂存，已验证暂存七天后过期。
- 恢复目标由 Ludusavi 直接读取隔离库；恢复前仍在本机备份库创建并锁定 PreRestore，失败时使用本机快照回滚。
- 用户点击恢复后会再次执行远端到隔离区的哈希与 Backup ID 校验，暂存后的本地或远端变化会阻止恢复。
- 新增 Worker 安全测试，覆盖设备名单路径、目录分隔符和不透明暂存 ID。

# 0.6.12 Development Preview

- 当前游戏媒体表格显式启用行/列回收虚拟化，并增加可见行 96px 缩略图。
- 缩略图按路径、大小、修改时间和解码宽度缓存，最多保留 96 项；图像冻结且读取后立即释放文件句柄。
- 选中录像使用单个静音 `MediaElement` 内嵌预览，完整声音与不支持格式继续使用系统默认播放器。
- 新增 WPF 自动测试，验证 100 张缩略图转换后文件可删除且 LRU 不超过上限。
- 增加官方 Playnite Add-ons installer/add-on 清单与版本门禁；自动更新需在 GitHub Release 上传 PEXT 并由官方数据库合并后生效。

# 0.6.11 Development Preview

- 将媒体持久化从单体 `SqliteStateStore.cs` 拆到独立 partial，保留原 SQL、锁和公开方法。
- 将媒体工作区加载、筛选、同步、元数据、收件箱与本地打开逻辑拆到 Dashboard 媒体 partial。
- 静态门禁改为聚合扫描全部持久层和 Dashboard partial，避免模块化后误报功能缺失。
- Release 构建、Worker SQLite 与 Playnite 设置测试验证拆分前后行为边界不变。

# 0.6.10 Development Preview

- 新增独立 Playnite 设置迁移测试项目，不改变插件本体的 net462 兼容目标。
- 覆盖可移植 JSON 往返、旧包缺失字段默认值、未知架构与枚举、数值越界、1 MiB 上限和缺失路径报告。
- 验证非法导入不会污染当前设置，缺失路径检查不会创建程序或目录。
- 一键构建现在同时运行 Core、Worker SQLite 和 Playnite 设置迁移测试。

# 0.6.9 Development Preview

- 多设备分叉支持记录可审计的人工决策与备注。
- 决策在 Worker 重启后仍保留，并在再次刷新设备摘要时显示。
- 决策不会执行任何远端下载、恢复、删除或覆盖。

# 0.6.8 Development Preview

- 当前游戏媒体支持多选后批量收藏、取消收藏和批量备注。
- Worker 使用有界 SQLite 事务更新所选元数据；任一记录无效时整体回滚。
- 新增 Worker SQLite 集成测试项目，一键构建会同时运行 Core 和 Worker 测试。

# 0.6.7 Development Preview

- 修复打开媒体页时 `Run.Text` 尝试回写只读空间占用属性、导致 Playnite 未处理异常的崩溃。
- 媒体统计的全部内联绑定显式使用 OneWay，并修复此前未实际匹配 XAML 的源码防回归规则。
- XAML 门禁脚本恢复 Windows PowerShell 5.1 编码兼容。
- 当前游戏媒体支持按文件名、备注和来源即时搜索。
- 新增全部、截图、录像和收藏筛选。
- 选中截图使用有界解码预览并立即释放文件句柄，避免为完整媒体库同时加载缩略图。
- 录像和不支持的图片格式继续交给系统默认应用打开。

# 0.6.6 Development Preview

- 设置页新增带架构版本的 JSON 导出、导入和缺失程序/目录迁移报告；只有点击 Playnite 保存后才应用。
- 导入校验文件大小、枚举和数值边界，导出不包含 Rclone 密码但仍可能包含本地路径与远端名称。
- 当前游戏媒体新增 SQLite 聚合的总量、截图、录像、收藏和空间占用摘要。
- 支持媒体收藏、备注、系统默认程序打开和资源管理器定位；这些操作不会移动或删除归档文件。

# 0.6.5 Development Preview

- Worker 任务变化支持信号唤醒的有界长轮询，后台成功/失败通知无需持续读取完整任务历史。
- 新增云端专用安全重试；Rclone 失败时只重复单向复制，不重复创建本地备份版本。
- ZIP 或目录包含多个 EXE 时，导入流程要求用户在插件内选择主程序。
- 已安装修改器可切换并保存活动版本，多版本下载不再只有后端数据而没有 UI 入口。

# 0.6.4 Development Preview

- 游戏列表云端列显示持久化的已上传、上传失败、待上传或未启用状态。

# 0.6.3 Development Preview

- 维护中心新增未知进程/MOD 启动器人工学习：可将 EXE 名称绑定到游戏并删除映射，检测器优先复用确认映射。

# 0.6.2 Development Preview

- 新增维护中心“设备状态”：生成并上传不含存档内容的本机备份摘要，读取其他设备摘要并显示需人工决定的分叉。
- 多设备状态只使用 Rclone `copy`、`lsf`、`cat`；不引入远端下载、自动恢复、覆盖或删除。

# 0.6.1 Development Preview

- 首页“需要关注”卡片可点击并直达异常与日志，展示关联游戏、详情与建议处理方式。
- 空闲 Dashboard 改用 Worker 任务增量变化馈送；没有任务变化时最多每分钟读取一次完整快照。
- 恢复新增已知会话/进程检查，并在整个恢复阶段独占云传输闸门。
- FLiNG 下载与 ZIP 解压新增安全大小、文件数量限制及失败目录清理。

# 0.5.11 Development Preview

- 修复 WPF `TabPanel` 在使用外部 `TabItem.Margin` 时对模板右边缘的裁切：页签间距移入模板内部固定透明列，确保选中与未选中页签的右上、右下圆角完整显示。
- 页签 Chrome 在自身布局槽内保留 1 DIP 安全边距并启用布局取整，避免高 DPI 下右侧描边被吸附成直线。
- 保留 0.5.10 的页签内容 Stretch、DataGrid CheckBox、搜索清除、滚动条和布局修复。

# 0.5.10 Development Preview

- 修复 0.5.9 `TabItem.HorizontalContentAlignment/VerticalContentAlignment=Center` 向选中内容传播，导致维护、媒体、修改器等页面整体居中的布局回归。
- 页签头部改为独立留白视口并隐藏内部横向滚动条轨道，避免底部圆角被裁切以及页签下方出现贯穿整行的伪分隔线。
- 保留 0.5.9 已确认正常的 Apple 风格 DataGrid CheckBox、搜索清除按钮、DataGrid 与滚动条基础样式。

# 0.5.9 Development Preview

- 重写共享 ScrollBar Track/Thumb 几何：纵向与横向使用固定厚度、方向独立最小长度和有限圆角，避免大量数据时滑块被归一化成尖角或透镜形。
- 完全接管 TabControl/TabItem 模板，移除 Playnite 宿主标签线与默认 Chrome；一级和二级页签现在四角一致、内容居中且具备完整 Hover/Selected/Focused 状态。
- 重构 DataGrid 表头与外框：首末列表头匹配表格上圆角，弱化分隔线；表格复选框改为居中的 Apple-inspired 圆角勾选框。
- 统一按钮 ContentPresenter 对齐，修复 Segoe MDL2 图标与文字不在同一水平线的问题；导航图标与文字同步垂直居中。
- 游戏搜索和 FLiNG 搜索改为真实 Watermark：获得焦点即隐藏占位文本，输入后显示垂直居中的清除按钮，点击后清空并保留焦点。
- 共享 TextBox、ComboBox、CheckBox、ScrollBar 与 DataGrid 模板应用到 Dashboard 和设置页，避免同类控件在其他页面继续回退为宿主默认样式。

# 0.5.8 Development Preview

- 重构全局 ScrollBar/Thumb 模板，修复大型游戏库中纵向滑块被最小宽度约束挤压变形的问题。
- 统一 DataGrid 表头、单元格、状态灯和进度条的水平/垂直对齐，降低表格线与选中背景强度。
- 长路径与详情列支持省略提示、列宽调整和横向滚动；局部标签改为明显可点击的圆角 Pill。
- 为 Playnite 宿主窗口按钮预留顶部安全区，修复右上操作按钮误触与重叠风险。
- 新增插件内确认弹窗、错误详情框和不抢焦点的静默 Toast；自动备份等后台任务可在面板内反馈。
- 修复紧凑侧栏品牌图标裁切，并替换为小尺寸更清晰的“手柄 + 存档”插件图标。
- 规范 Windows Git 元数据与换行策略：`validate-source.py` 改为普通文件模式，避免无文本差异的 mode-only 修改。

# 0.5.7 Development Preview

- 管理面板先展示 SQLite 持久化快照，后台再进行游戏库同步。
- 相同游戏库同步在五分钟内去重；Ludusavi 仅匹配新增、关键描述变化或到期重试的游戏。
- 首页游戏摘要改为聚合 SQL，移除每游戏备份、媒体和策略 N+1 查询。
- 存档历史默认读取缓存，详情按当前工作区懒加载，Ludusavi 版本缓存六小时。

# 0.5.6 Development Preview

- 统一选中态、键盘焦点、复选框、滚动条和轻量 DataGrid 视觉，修复选中文字变黑、系统虚线框与方块 Thumb。
- 首页删除重复统计，改为待处理摘要和最近八条任务；任务中心新增状态、游戏和任务类型筛选。
- 媒体中心拆为待归类、当前游戏媒体和来源规则三个页签，内部类型和长内容改为用户语言、截断与 Tooltip。
- 已安装修改器采用列表 + 设置 Inspector；FLiNG 版本列表显示语义版本、功能数量、发布时间和大小。
- 维护中心将原始诊断移入可展开技术详情；紧凑侧栏为 Logo、导航和状态补齐 Tooltip。
- 响应式边界统一为 1280 / 980 / 880 DIP，后台刷新提示不参与主布局测量。

# 0.5.5 Development Preview

- 新增插件生命周期级任务通知监测：即使 GameSaveCenter 管理面板已经关闭，也会每 5 秒读取一次轻量任务状态，并对新完成或失败的任务发送 Playnite 通知。
- 备份、恢复、修改器下载和媒体同步会显示 Worker 返回的真实结果；游玩中定时备份即使存档无变化，也会明确通知“存档无变化，历史未新增”。
- 任务通知按任务 ID 去重；插件启动时不会补发历史任务，关闭通知期间完成的任务也不会在重新启用后集中弹出。
- 修改器导入/解绑、策略保存、候选路径处理和媒体归类等即时操作补齐成功反馈；失败继续显示可理解的错误信息。
- 游玩中备份采用固定周期锚点，5 分钟策略不会因 5 秒轮询逐轮累积漂移；策略间隔或启停状态在游戏运行中变化时会重新开始倒计时。
- 同一游戏的上一轮定时备份尚未结束时不会再排入重叠备份，Worker 日志会记录到期、实际启动、跳过重叠和下一次计划时间。

# 0.5.4 Development Preview

- 修复修改器“已安装”等列表中 `Run.Text` 对只读 DTO 属性默认采用 TwoWay 绑定，点击后触发 WPF `XamlParseException` 并使 Playnite 闪退的问题。
- 所有项目内 `Run.Text` 数据绑定现在显式使用 OneWay；源码门禁会阻止同类只读属性回写错误再次进入包。
- 修复游玩中定时备份的最低间隔不一致：界面、Worker 配置和每游戏策略均支持 1–1440 分钟；到期检查改为 5 秒一次，并在 Worker 日志中记录计划和实际开始。
- 任务消息现在会明确标明“手动备份”“游玩中定时备份”或“退出后自动备份”，不再让无变化的定时任务看起来像没有触发。
- 手动、退出后和游玩中备份继续由 Ludusavi 判断内容是否变化；`Same` 将显示“存档无变化，历史未新增”，不会制造重复历史版本。

# 0.5.3 Development Preview

- 修复图标侧栏仍保留文字导致导航图标、Worker/Ludusavi 状态被裁切；紧凑模式只保留居中的 Worker 状态灯。
- 修复共享滚动条模板未绑定 `ViewportSize` 且把横向滚动条固定成窄方块的问题；横向与纵向轨道、页面命令和最小 Thumb 尺寸分别适配。
- 媒体中心改为内部纵向滚动工作区；待归类和当前游戏媒体表格各保留约四行可见空间，不再被前置控件挤成单行或裁掉底边。
- FLiNG 搜索后自动读取首个结果的可下载版本；选择其他目录结果时也会立即更新右侧版本列表。
- 后台轮询不再触发游戏/任务详情的进入动画，也不会在顶部工具栏动态插入进度控件造成页面抖动。

# 0.5.2 Development Preview

- 重构 Dashboard 为模块化工作区：首页、存档、修改器、媒体、任务和维护中心只显示本模块相关标签；任务与维护中心不再占用游戏浏览器列。
- 宽度采用 1320 / 1050 / 880 DIP 断点：普通窗口自动收起导航文字，紧凑窗口改用顶部游戏选择器而非继续挤压三栏。
- 备份策略改为按“策略”按钮展开；自动恢复说明仅在存档中心且有足够高度时显示；移除重复的底部正常状态栏。
- 后台更新进度不再插入底部布局流，避免自动刷新时页面上下震动。
- 设置页及共享资源新增完整深浅主题 ComboBox/Popup、滚动条和进度条模板，避免宿主默认白色控件重新出现。

# 0.5.1 Development Preview

- 修复一键构建在 NuGet 还原前执行 clean，遇到跨机器旧 `project.assets.json` 时可能报 `NETSDK1064` 缺包的问题。
- 修复跳过构建时 Worker 发布可能缺少 `win-x64` Runtime Identifier 资产的问题。

# 0.5.0 Development Preview

- 新增原生 WPF 修改器中心：每游戏多个修改器、多个 CT、独立启用与自动启动策略。
- 支持本地 EXE、ZIP、目录和 `.ct` 导入，保存版本、哈希、入口与工作目录。
- 新增应用内 FLiNG 目录搜索、版本列表、下载任务、安全解压和自动绑定，全程不打开网页或 WebView。
- 新增游戏会话 PID 跟踪和可选退出关闭；检测常见反作弊线索时默认阻止自动启动。
- 新增 SQLite 增量表、Zip Slip 单元测试和一级工作区导航。

# 0.4.3 Development Preview

- 修复 0.4.2 配置可能把 `ludusavi.exe` 误存为 Worker 路径，导致刷新时反复打开 Ludusavi 窗口的问题。
- 启动时自动迁移误填路径：恢复打包内 `GameSaveCenter.Worker.exe`，并在 Ludusavi 路径为空时保留用户原有的有效路径。
- Worker 启动增加目标身份校验、并发启动锁、隐藏窗口和启动/退出日志；错误程序即使退出码为 0 也不会再被当作 Worker 启动。
- 搜索框改为完整宽度独占行，状态和排序筛选放到第二行，解决高 DPI 下搜索框只剩图标的问题。
- ComboBox 主体、箭头、Popup 和选项全部改为主题自适应的 Apple-inspired WPF 模板，深色主题不再出现系统白色下拉框。
- Tab 内容改为双向拉伸；矮窗口自动收起统计卡片，将剩余高度优先交给存档历史和其他详情列表。
- 主工作区按宽度调整侧栏、栏间距与游戏列表宽度，避免缩放时右侧详情被固定内容挤到不可见。
- Windows Release 编译、源码/XAML 门禁和隔离 Worker 存活冒烟验证已通过；Playnite 真实交互回归记录在本版本交付说明中。

# 0.4.2 Development Preview

- 修复 Playnite 10.56 中点击 GameSaveCenter 侧栏时因缺失 `GscStatusPill` 静态资源导致的 `XamlParseException` 崩溃。
- 新增媒体收件箱数量胶囊样式，继续使用共享玻璃表面、细描边和克制圆角设计令牌。
- 移除不存在的 `GscCardBrush`、`GscHairlineBrush` 引用，改用已有 `GscGlassStrongBrush`、`GscGlassStrokeBrush`。
- 静态校验新增全部 `Gsc*` StaticResource/DynamicResource 可解析性检查。
- 0.4.1 已由 Windows 真机确认可编译、安装和被 Playnite 加载；0.4.2 仍需重新打开侧栏并完成主题、标签与媒体页回归。

# 0.4.1 Development Preview

- 公共 Game Bar、Windows Screenshots 与共享自定义目录改为全局 `MediaInbox` 任务单次扫描。
- 仅文件名唯一匹配或明确且无重叠的会话时间窗口自动归类；多游戏歧义和无法确认项进入 `_Inbox/Pending`。
- SQLite 新增媒体 `Assigned/Inbox/Ignored` 状态与可解释原因，旧库按“先补列、后建索引”的顺序安全升级。
- Playnite 媒体页新增全局待归类列表、目标游戏选择、确认归类和“忽略并保留副本”。
- `media.reassign` 从只改数据库升级为真实移动归档、目标哈希校验与审计；归档缺失时只从原文件重建副本，不移动或删除源文件。
- 首轮每次最多导入 200 个歧义历史媒体，依靠 SHA-256 在后续扫描中分批补齐。
- `MediaInbox` 失败或取消任务支持只重试共享目录；静态校验新增媒体收件箱迁移与源文件安全门禁。
- 本版本未在当前环境执行 Windows build/test/package 或 Playnite 真机加载。

# 0.4.0 Development Preview

- 将用户提供的完整 Apple-inspired WPF/Codex 设计规范保存到仓库，并新增强制 UI 变更门禁。
- 外观设置新增“跟随 Playnite / 浅色 / 深色”，保存后管理面板即时重算主题色板。
- 未匹配游戏会话开始时异步记录有界文件快照，退出后比较新增和修改文件并生成可解释存档路径候选。
- 候选路径持久化到 SQLite，支持查看依据、接受生成 Ludusavi 规则草案以及忽略候选。
- Worker 启动时清理过期检测快照，避免异常退出后长期积累。
- 任务详情新增复制错误和任务 ID；失败/取消的备份与媒体同步任务支持安全重试。
- Game Bar 与 Windows 公共媒体目录新增无重叠会话时间窗口归类；同时运行多个游戏时自动退回文件名匹配，避免猜测。
- 共享主题敏感资源开始集中到 `Themes/DesignTokens.xaml`，Dashboard 与设置页复用同一色板键。

# 0.3.5 Development Preview

- 修复任务耗时只读属性绑定错误，恢复管理面板自动刷新。
- 历史查询会主动与 Ludusavi 对账，备份 ZIP 已生成但索引缺失时能够自愈。
- 新增大型游戏库搜索、状态筛选、排序和结果计数。
- 重构任务进度列与底部状态区，空闲时不再显示空进度框。
- 新增面向第三方 Playnite 主题的对比度派生色板，修复浅色主题黑块和深色主题低对比。
- 启用像素对齐与 ClearType 渲染，移除正文透明度和按钮悬停缩放，改善 DPI 下文字锐度。

# 0.3.4 Development Preview

- 修复中文 Windows 双击一键批处理时乱码、命令截断和 PowerShell 未启动的问题。
- 新增 ASCII-only 的 `GameSaveCenter-Run.cmd`，中文入口保留为兼容包装。
- PowerShell 安装脚本使用 UTF-8 BOM，并持续记录 `artifacts/one-click-install.log`。
- 构建前新增批处理 ASCII/CRLF 与 PowerShell BOM 检查。

# 0.3.3 Development Preview

- 修复开发安装后 Playnite 仍加载旧版扩展的问题。
- 新增双击式一键构建、测试、打包、原子安装和启动流程。
- 打包文件名改为动态版本，安装后强制核验清单与 DLL 版本。
- 包含 0.3.2 的悬停动画 Freezable 精准修复。

# 0.3.1 Development Preview

- 管理面板增加左侧应用导航，功能入口与详情标签保持同步。
- 新增主题自适应拟态毛玻璃：半透明渐变表面、模糊环境光、细高光边框与柔和阴影。
- 浅色、深色和 Windows 高对比度模式使用不同色板；高对比度自动关闭环境光和透明表面。
- 新增页面进入、游戏切换、标签切换、任务选择、状态胶囊、卡片悬停、导航悬停和按钮悬停动画。
- 动画只使用 Opacity 与 RenderTransform，并遵循 Windows 客户区动画设置。
- 设置页新增“启用界面动画”“启用毛玻璃”和毛玻璃强度 20–100%，支持实时预览。
- 设置页同步采用玻璃卡片、环境光和进入动画。
- 跨平台源码校验新增 WPF Trigger 层级、TargetName 和 XAML 事件处理器检查。

# 0.3.0 Development Preview

- 管理面板支持可配置的轻量自动刷新，手动长任务执行期间仍能看到实时进度。
- 任务页新增进度条、耗时、任务 ID、完整详情和 Queued/Running 任务取消入口。
- 修复排队阶段取消可能无法写入 Cancelled 状态及清理任务 Token 的问题；运行中取消会终止对应外部工具进程，避免孤儿进程。
- Worker 启动时自动把异常退出遗留的 Queued/Running 任务标记为 `WORKER_RESTARTED`。
- 自动任务成功、失败或取消时可显示 Playnite 通知；设置页可关闭通知。
- 新增诊断中心：查看有效 Worker/Ludusavi/备份策略，复制诊断摘要并打开数据、存档、媒体和 Worker 日志目录。
- `settings.get` 使用稳定的非敏感 DTO，Rclone 远端只暴露是否已配置。

# 0.2.0 Development Preview

- Worker 运行设置持久化到本地文件，重启后不再丢失 Ludusavi 路径。
- Playnite 启动、游戏事件和刷新都会可靠发送设置；刷新会重新导出游戏库并匹配 Ludusavi。
- Worker 启动等待提升至 30 秒，记录启动输出并处理同路径失效残留进程。
- 默认使用 ZIP 多版本：完整版本 3、每组差异版本 5，可在设置页调整格式、压缩和数量。
- 备份任务区分新版本、不同内容、无变化和 Simple 当前副本更新，并保留真实错误码与诊断。
- 修复备份历史复合主键、同一版本更新时间、刷新后历史消失和旧记录清理。
- 任务、存档、媒体与审计时间统一转换为 Windows 本地时间显示。
- 仪表盘与设置页按 Apple HIG 启发重构：主题资源、圆角卡片、弱边框、紫蓝强调、状态点、空状态和浅色/深色兼容。
- 新增 `KNOWN_ISSUES.md`，持续记录 GSC-001 至 GSC-019 的修复与回归状态。

# 0.1.1 Development Preview

- 修复选中游戏后操作按钮仍保持禁用的问题。
- 命令接入 WPF `CommandManager`，选择变化时重新计算可执行状态。
- 刷新列表后保留已选游戏，首次加载自动选择第一款游戏。
- 记录 Windows 真机构建、Playnite 加载、Ludusavi 匹配与手动备份验证结果。

# 0.1.0 Development Preview

## 已实现源码

- Playnite 10 GenericPlugin 与 Apple HIG 启发的统一面板；
- Playnite 游戏库导出、Game Action/MOD loader 识别；
- Worker 命名管道、SQLite、任务、日志和审计；
- Ludusavi 匹配、单游戏/全部/定时/退出备份；
- 版本备注、锁定、历史索引、差异和保留预览；
- PreRestore、安全恢复、回滚和撤销流程；
- 外部游戏进程与多进程 MOD 启动链兜底；
- Steam、Game Bar、Windows 和自定义平台媒体增量同步；
- SHA-256 去重、稳定写入检测、原子复制和误归类修正；
- Rclone `copy/check` 安全适配；
- 候选存档路径评分、WGS 辅助扫描和规则草案；
- Windows 构建/测试/打包/安装脚本；
- Core xUnit 测试源码与跨平台结构校验。

## 尚未声明完成

- Windows 编译、Playnite 真机加载和真实游戏端到端验证；
- Worker 主动推送后台通知；
- 公共截图目录的完整会话时间归类；
- 默认会话的启动前/退出后文件快照差异；
- Rclone 远端设备摘要摄取及完整多设备冲突 UI；
- 未知进程映射学习 UI。

详见 `IMPLEMENTATION_LIMITATIONS.md`。

## 0.1.0 开发预览修订 1

- `global.json` 不再锁死不存在的 `8.0.420`，现在允许 .NET 8 或更高稳定版 SDK；用户现有的 .NET 9.0.302 可被解析使用。
- `build.ps1` 对每一个 `dotnet` 原生命令检查退出码，失败立即终止，不再显示虚假的“构建完成”。
- `package.ps1` 只有在编译和测试成功后才创建打包目录，并对 Worker 发布退出码进行检查。
- 新增 GitHub Actions Windows 构建、测试、打包工作流。
- 插件作者和完整 Git 历史统一改为英文笔名“Sable Drift”，提交信息统一使用中文。

- 修复 Playnite 插件日志初始化：使用 `LogManager.GetLogger()`，兼容当前 PlayniteSDK 6.16.0。
- 清理 Windows 首次真实编译暴露的主要可空引用警告。
- Git 作者统一改为英文笔名 `Sable Drift`。

## 构建热修复

- 修复 Apple 风格按钮按压模板的 WPF 名称作用域错误（`MC4111`）。
- 按压反馈仍保留 0.97 倍缩放和轻微透明度变化。

## 0.4.1 Development Preview — Windows Build Hotfix

- 修复管理面板和设置页资源字典的 WPF XAML 结构错误，解决 `MC3074` 编译失败。
- 源码门禁新增 `UserControl.Resources` 下非法直挂 `ResourceDictionary.MergedDictionaries` 检测。
- 对齐 `.editorconfig` 与 `.gitattributes`：普通文本固定 LF，Windows 批处理继续保留 CRLF，减少覆盖源码包后 Markdown/Python 文件反复显示修改的问题。

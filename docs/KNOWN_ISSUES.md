# 已知缺陷与回归状态

更新时间：2026-08-01
目标版本：`0.6.22-development-preview`

本文档是持续缺陷台账。任何修复必须同步更新 `DEVELOPMENT_PROGRESS.md` 与 `PROJECT_MEMORY.md`。

| 编号 | 问题 | 当前状态 | 回归要求 |
|---|---|---|---|
| GSC-001 | Worker 冷启动等待过短 | 已修复待验证 | 首次启动允许最多 30 秒，Defender 扫描时仍能就绪 |
| GSC-002 | 残留同路径 Worker 阻止重启 | 已修复待验证 | 健康检查失败时只终止同一安装路径的旧进程 |
| GSC-003 | 刷新只读缓存，不重发设置/游戏/匹配 | 已修复待验证 | 点击刷新后重新发送设置、导出全部游戏并匹配 |
| GSC-004 | CLI 可匹配但插件显示 Unmatched | 已修复待验证 | Worker 当前路径正确，测试游戏和 Bongo Cat 均显示已就绪 |
| GSC-005 | Ludusavi 失败只显示“执行失败” | 已修复待验证 | 任务详情显示稳定错误码和真实诊断 |
| GSC-006 | Worker 重启后丢失 Ludusavi 路径 | 已修复待验证 | `worker-settings.json` 持久化；重启后无需 PowerShell 注入 |
| GSC-007 | UTC 时间直接显示 | 已修复待验证 | 任务、历史、媒体和审计按 Windows 本地时区显示 |
| GSC-008 | 深色主题文字对比度过低 | 已重构待视觉回归 | 浅色、深色及至少两个 Playnite 主题文字清晰 |
| GSC-009 | 按钮为默认 WPF 样式 | 共享模板已收口，待视觉回归 | 普通/主按钮均使用共享模板；圆角、克制强调色、悬停/按压/焦点反馈一致 |
| GSC-010 | 空状态和错误状态缺少引导 | 已改善待验证 | 无游戏、无历史、未配置 Ludusavi 均有明确提示 |
| GSC-011 | 外部管道连接曾超时 | 已排除误判 | 当时 Worker 被 Ctrl+C 终止；不作为稳定缺陷 |
| GSC-012 | 选中游戏后按钮仍禁用 | 已修复并真机验证 | 依赖选择的命令会即时重算 |
| GSC-013 | 刷新后存档历史消失 | 已修复待验证 | 刷新结束后显式加载当前游戏详情 |
| GSC-014 | 多次 Backup Failed 原因不明 | 根因确认并已修复待验证 | 原因是 `LUDUSAVI_NOT_CONFIGURED`；重启后不得复现 |
| GSC-015 | 任务状态与校验提示看似矛盾 | 已改善待验证 | 任务详情展示执行阶段真实失败，校验结果独立呈现 |
| GSC-016 | 无变化备份可能被误解为失败 | 已改善待验证 | Same 显示“存档无变化，历史未新增” |
| GSC-017 | 备份保留策略依赖 Ludusavi 隐藏全局配置 | 已修复待验证 | 设置页显式控制格式、完整/差异数量和压缩 |
| GSC-018 | Simple 备份 ID `.` 更新时间不刷新 | 已修复待验证 | UPSERT 更新 `created_utc`，历史显示最近时间 |
| GSC-019 | Simple 模式无法提供多历史恢复点 | 已解决设计缺口待验证 | 默认 ZIP，完整 3、差异 5；Simple 明确提示单副本 |
| GSC-024 | Worker 路径误指向 Ludusavi，刷新时反复打开窗口 | 已修复待 Playnite 回归 | 自动迁移错误配置；启动器拒绝非 Worker 文件并串行化并发启动 |
| GSC-025 | 高 DPI 下搜索框被两个筛选框挤压 | 已修复待视觉回归 | 搜索独占一行，筛选在第二行等宽排列 |
| GSC-026 | 深色主题 ComboBox/Popup 使用系统白色模板 | 已修复待视觉回归 | 主体、Chevron、Popup 和 ComboBoxItem 使用共享动态主题资源 |
| GSC-027 | 窗口变矮后存档历史表格高度变为零 | 已修复待视觉回归 | Tab 内容拉伸；矮窗口收起统计卡片并优先保留详情列表空间 |
| GSC-046 | 一级导航只切换同一七标签详情页，普通窗口持续拥挤 | 已重构待 Playnite 回归 | 当前模块只显示相关标签；任务/维护移除游戏列；1320/1050/880 DIP 响应式布局 |
| GSC-047 | 后台刷新进度插入底部状态行导致页面震动 | 已修复待 Playnite 回归 | 正常状态只保留侧栏；后台刷新提示不再参与 Dashboard 主布局测量 |
| GSC-048 | 设置页下拉框和滚动条回退为宿主默认白色控件 | 已修复待多主题回归 | 共享 ComboBox/Popup/ScrollBar/ProgressBar 模板，设置页显式复用 |
| GSC-049 | 0.6.6 媒体统计只读属性被 Run.Text 回写导致崩溃 | 已修复为 0.6.7 待真机回归 | 打开媒体页并切换游戏/主题，不出现未处理绑定异常 |
| GSC-054 | 保留策略尚不能安全删除指定 Ludusavi 历史版本 | 受上游契约限制，明确保留预览模式 | 不得通过猜测 Vault 文件布局删除任何备份；仅显示可清理预览和手动目录入口 |

## GSC-049：0.6.6 媒体页因只读统计绑定崩溃

- **状态**：已修复为 0.6.7，待 Windows/Playnite 真机回归。
- **真机证据**：Playnite 10.56 日志记录加载 `GameSaveCenter, version 0.6.6`，随后在 2026-07-29 19:47:53 抛出未处理的 `System.InvalidOperationException`。
- **异常**：无法对 `MediaStorageSummaryDto.TotalSizeDisplay` 只读属性进行 TwoWay 或 OneWayToSource 绑定。
- **根因**：新增媒体统计使用了未显式指定模式的 `Run.Text` 绑定；既有防回归正则多写了一层反斜杠，验证脚本实际没有匹配任何 `Run`。
- **修复**：媒体统计五个数据绑定全部显式设为 OneWay，并修正门禁正则。
- **回归**：安装 0.6.7 后反复打开媒体页、切换有/无媒体的游戏与三种主题；Playnite 日志不得再出现该绑定异常。
| GSC-049 | 图标侧栏仍保留 Worker/Ludusavi 文本，缩小时图标与状态被裁切 | 已修复待 Playnite 回归 | Compact 模式仅显示居中状态灯并减小导航内边距 |
| GSC-050 | 自定义横向滚动条 Thumb 显示为错误的小方块 | 已修复待 Playnite 回归 | `PART_Track` 绑定视口和范围；水平/垂直方向使用独立尺寸与翻页命令 |
| GSC-051 | 媒体页的自动行挤压两个 DataGrid，导致仅显示一行或底边裁切 | 已修复待 Playnite 回归 | 局部纵向滚动；两个表格各保留约四行高度 |
| GSC-052 | FLiNG 搜索结果与可下载版本列表未自动联动 | 已修复待 Playnite 回归 | 搜索完成和选中目录项后自动请求版本列表 |
| GSC-053 | 点击修改器已安装列表触发只读属性 TwoWay 绑定闪退 | 已修复待 Playnite 回归 | `Run.Text` 全部显式 OneWay；点击已安装、FLiNG 搜索和版本列表均无 `XamlParseException` |
| GSC-054 | 游玩中备份输入 1 分钟却被 Worker 静默延长为 5 分钟 | 已修复待真机回归 | 1 分钟策略在游戏持续运行约 60–65 秒后创建或报告 Same；Worker 日志记录调度 |
| GSC-055 | Dashboard 关闭后自动任务没有用户可见反馈 | 已修复待真机回归 | 插件生命周期每 5 秒监测新终态任务；Same、成功和失败各通知一次，启动时不补发旧任务 |
| GSC-056 | 定时备份轮询延迟可能逐轮累积且允许重叠排队 | 已修复待真机回归 | 下一时间从原计划锚点递推；间隔/启停变化重新计时；上一轮未结束时跳过重叠 |
| GSC-057 | 列表和表格选中后文字被宿主主题改为黑色，焦点显示系统虚线框 | 已修复待多主题回归 | 所有 Selected 状态保持主题前景；Tab 键显示圆角紫色焦点环，鼠标点击不出现默认虚线 |
| GSC-058 | 首页重复统计、媒体双表单同屏和技术类型名造成信息过载 | 已重构待视觉回归 | 首页仅显示待处理与最近任务；媒体拆为三个局部页签；DTO 类型和来源映射为用户语言 |
| GSC-059 | 已安装修改器配置固定沉底且 FLiNG 版本以长文件名为主 | 已重构待视觉回归 | 已安装页采用列表 + Inspector；FLiNG 显示语义版本、功能数、日期和大小，原始名称仅作 Tooltip |
| GSC-060 | 1000 游戏库启动和首次打开会重复全量 Ludusavi 匹配 | 已修复待大库回归 | 首开先显示 SQLite；相同同步去重；仅新增、变化或到期未匹配游戏重新匹配 |
| GSC-061 | Dashboard 每款游戏分别读取完整历史、媒体和策略 | 已修复待性能回归 | 使用聚合 SQL 一次返回全部游戏摘要，移除每游戏 N+1 |
| GSC-062 | 选择游戏会加载全部模块并强制刷新 Ludusavi 历史 | 已修复待交互回归 | 仅加载当前工作区；历史默认读缓存，显式刷新或缓存为空才校准 |
| GSC-063 | 大型列表滚动条 Thumb 呈尖角/透镜形且轨道按钮变形 | 已修复待多主题回归 | 纵横模板使用固定厚度、有限圆角、方向独立最小长度；游戏/媒体/任务列表均可拖动与轨道翻页 |
| GSC-064 | 页签仅左上角圆角并叠加宿主直线 Chrome | 已修复待 Playnite 回归 | 完整接管 TabControl/TabItem 模板；一级与二级页签四角一致且内容居中 |
| GSC-065 | DataGrid 表头直角覆盖圆角外框且复选框回退为原生样式 | 已修复待视觉回归 | 首末列 Header 匹配外框上圆角；所有表格勾选列复用 GscDataGridCheckBox |
| GSC-066 | 搜索框聚焦后占位文字仍显示且无法一键清除 | 已修复待交互回归 | Watermark 仅空且未聚焦时可见；有文本时显示居中清除按钮并在清除后保留焦点 |
| GSC-067 | 首页“需要关注”只显示数量，无法得知具体原因 | 已修复待 Playnite 回归 | 卡片可打开异常与日志，显示游戏、原因与建议处理方式并选中首项 |
| GSC-068 | Dashboard 空闲时仍按固定间隔重建完整快照 | 已修复待大库回归 | Worker 提供有界任务增量变化馈送；无变化时最多每分钟完整校准 |
| GSC-069 | 恢复可能与共享备份根目录的 rclone 上传交叠 | 已修复待 Rclone 回归 | 上传与恢复共享全局传输闸门；恢复等待正在执行的上传完成 |
| GSC-070 | FLiNG 下载和 ZIP 解压缺少体积资源上限 | 已修复待安全回归 | 下载、条目数、单条目与总解压大小均有限制，失败版本目录会清理 |
| GSC-071 | 多设备云端历史无法识别分叉 | 已开发待 Rclone 回归 | 维护中心只读比较各设备 sidecar；任何冲突均要求人工处理，不自动覆盖 |
| GSC-072 | MOD Loader 或未知 EXE 无法稳定归属游戏 | 已开发待进程回归 | 用户确认 EXE→游戏映射后持久化复用，绝不自动猜测绑定 |
| GSC-073 | 云端复制失败只能重跑完整 Backup | 已修复待 Rclone 回归 | `CloudUpload` 重试只重复安全的单向复制，不新增本地历史 |
| GSC-074 | 后台通知固定读取完整任务历史 | 已修复待 Playnite 回归 | Worker 状态写入后唤醒长轮询；重启/超时回退 SQLite 快照 |
| GSC-075 | 多 EXE 修改器包静默选择最大文件且无法切换版本 | 已修复待修改器回归 | 导入时用户选择主程序；Inspector 可选择并保存活动版本 |
| GSC-076 | Worker 任务刷新在后台线程触发 Dashboard PropertyChanged 后访问 WPF 控件而导致插件崩溃 | 已修复待 Playnite 回归 | View 先检查 Dispatcher 后再读取 IsLoaded；定时器和事件回调等待 Task；循环任务完成/取消/慢 Worker 时日志无跨线程异常 |
| GSC-077 | 备份策略分钟输入框过窄且逐字符回写整数，导致多位数输入看似丢失 | 已修复，Playnite 基础回归通过 | 共享数值输入宽度、完整值提交和范围校验；1600×900 真机完成 `30`→`1440`→`30` 未保存编辑验证；隔离 DPI/主题回归仍按测试计划执行 |
| GSC-078 | Settings 引用 Dashboard 局部按钮资源、输入错误填充资源缺失，且数值静态门禁漏检嵌套路径 | 源码已修复待 Playnite 回归 | UI-001 已将通用无 EventSetter Button 和错误色令牌加入共享资源，门禁已识别嵌套路径；Release 与 66 项测试通过，仍需隔离 Playnite 加载验证 |
| GSC-079 | WPF-UI POC 尚未在隔离 Playnite 宿主中实际加载 | 环境阻塞 | 现有 Playnite/Worker 是用户实例，不能关闭或覆盖。需要独立插件目录和测试实例后检查资源作用域、Dialog/Snackbar、主题、DPI 与宿主未污染 |
| GSC-080 | 共享 WPF 控件尚未在隔离 Playnite 中完成多 DPI/主题视觉验证 | 源码与自动化已验收，环境待验证 | 全量 UI 源码重构已通过资源门禁、Release 构建、66 项测试与包 smoke；仍需在独立数据根验证按钮、输入、滑块、提示、表格、页签、滚动条和键盘焦点 |
| GSC-081 | Dashboard/Settings 的紧凑布局尚未在独立 Playnite 中完成真实缩放验证 | 源码收口，环境阻塞 | 侧栏滚动、图标工具栏、设置页横向访问与可见焦点均已实现；`ENV-001` 证明独立数据根前不得启动 `.tmp` 副本或用户实例 |
| GSC-082 | 生产 WPF-UI 局部主题与控件尚未经过隔离 Playnite 宿主加载验证 | 自动化已通过，真机环境阻塞 | Card/Button/ToggleSwitch/普通输入、Dialog/Snackbar 已接入局部适配层并保留原生回退；Windows Release build 与 66 项测试通过，仍需以独立实例检查资源加载、Light/Dark/HighContrast、DPI、键盘和宿主无全局污染 |
| GSC-093 | 数值框获得键盘焦点时，宿主关闭可能向已停止 Dispatcher 投递全选操作 | 源码已修复待 Playnite 回归 | 关闭前不再投递；守卫与投递之间的关闭竞态被捕获，保留原有编辑值和业务绑定 |
| GSC-094 | Dashboard 卸载或 Toast 超出容量后，自动关闭计时器仍可能保持页面引用并延迟投递 | 源码已修复待 Playnite 回归 | 所有 Toast 计时器由页面集中跟踪；淘汰和卸载立即停止并移除动画 |
| GSC-095 | 品牌图标前景固定为白色，宿主强调色与高对比度下可能失去对比 | 源码已修复待多主题回归 | Dashboard 和 Settings 均使用按宿主强调色计算的 `GscOnAccentTextBrush` |
| GSC-096 | 语义状态色在运行时高对比度切换后仍引用初始静态资源 | 源码已修复待多主题回归 | 状态点、文本和图标填充均改为页面局部动态资源，并为高对比度计算可读回退 |
| GSC-097 | Settings 异步导入/导出在页面卸载后仍可能尝试显示反馈或保留入场动画 | 源码已修复待 Playnite 生命周期回归 | 业务操作保持完成，视觉反馈受页面/Dispatcher 可用性保护，卸载取消动画 |
| GSC-098 | 连续窗口缩放会对 Dashboard/Settings 重复执行大量响应式布局赋值 | 源码已修复待 DPI/缩放回归 | 同一渲染帧只应用最后一个尺寸，卸载页面不会执行延迟布局 |
| GSC-099 | Dashboard 卸载后仍保留 ViewModel 事件订阅，可能持有页面并触发无效 UI 调度 | 源码已修复待 Playnite 生命周期回归 | ViewModel 事件改为严格 Loaded/Unloaded 对称订阅 |
| GSC-100 | Toast 在高对比度或关闭毛玻璃时仍无条件创建黑色阴影 | 源码已修复待多主题回归 | 柔和阴影仅在玻璃效果可用时创建，其他模式使用无阴影实体回退 |
| GSC-101 | Worker 任务回调同步更新 Dashboard 绑定集合时未保护 Dispatcher 关闭竞态 | 源码已修复待 Playnite 生命周期回归 | 集合更新保留顺序，但关闭中的 Dispatcher 会被守卫并写入真实日志 |
| GSC-102 | 插件级通知与安全确认直接 Invoke Playnite Dispatcher，关闭期间可能向宿主回抛异常 | 源码已修复待 Playnite 生命周期回归 | 统一调度守卫；无法显示的确认默认取消，通知安全回退 |

## GSC-093：数值输入焦点全选的 Dispatcher 关闭竞态

- **状态**：源码已修复，待隔离 Playnite 生命周期回归。
- **根因**：共享 `SelectAllOnKeyboardFocus` 为避免鼠标光标跳动而延后到 Dispatcher 输入队列执行全选；嵌入式页面卸载期间，Dispatcher 可能已开始关闭，原先的投递会走向未处理 UI 异常路径。
- **修复**：投递前检查 `HasShutdownStarted` / `HasShutdownFinished`，并仅对守卫后的 `InvalidOperationException` 做无副作用降级。全选只是键盘编辑便利功能，无法安全调度时保留当前光标；数值校验、失焦提交和真实错误反馈未改变。
- **回归**：源码测试锁定关闭守卫和竞态捕获；隔离 Playnite 中在设置页和备份策略分钟输入框聚焦后立即关闭/切换页面，不得记录 Dispatcher、绑定或未处理异常。

## GSC-094：Toast 自动关闭计时器的页面卸载边界

- **状态**：源码已修复，待隔离 Playnite 生命周期回归。
- **根因**：Dashboard 的通知卡拥有各自的 `DispatcherTimer`。原实现只在自然关闭时停止计时器；关闭页面或为新通知淘汰最旧卡片时，计时器仍会暂时保留卡片与页面引用，并继续在 Dispatcher 排队。
- **修复**：页面以卡片为键集中追踪 Toast 计时器；容量淘汰、显式关闭、动画结束和卸载均会停止并移除相应计时器。卸载同时取消卡片动画并清空容器，不改通知文本、错误详情入口或真实任务反馈。
- **回归**：自动化门禁锁定集中追踪、容量淘汰和卸载清理；隔离 Playnite 中连续产生超过四条通知、打开错误详情后关闭 Dashboard，后续日志不得出现卸载页面的回调或未处理异常。

## GSC-095：品牌强调色图标的多主题前景

- **状态**：源码已修复，待隔离 Playnite 多主题/高对比度回归。
- **根因**：Dashboard 品牌图形和 Settings 标题图标直接写死为白色；用户选择的 Playnite 强调色可能更适合深色前景，高对比度也要求使用系统选中前景。
- **修复**：三个品牌图形均改用局部动态 `GscOnAccentTextBrush`。该令牌由共享调色板根据当前强调色计算可读前景，并在高对比度时采用 Windows `HighlightText`；未改动画、布局或功能入口。
- **回归**：自动化测试禁止这些页面重新写死白色品牌前景；隔离 Playnite 中依次验证 Follow、浅色、深色、自定义宿主色和高对比度下图标可读。

## GSC-096：语义状态色的高对比度动态更新

- **状态**：源码已修复，待隔离 Playnite 多主题/高对比度回归。
- **根因**：成功、警告、失败和信息色的定义虽位于共享资源，但 Dashboard/Settings 的多处状态点与图标用 `StaticResource` 捕获初始 Brush；运行时本地调色板无法覆盖已捕获的对象。
- **修复**：状态色与图标填充进入 `AdaptiveThemePaletteFactory` 的页面局部资源更新路径，所有实际 Dashboard/Settings 使用点改为 `DynamicResource`。普通主题维持既有语义色；高对比度下采用经过对比度保护的系统色或主前景，并继续依赖状态文字而非颜色单独表达。
- **回归**：自动化测试禁止两个页面重新使用静态语义色并检查四类动态资源更新；隔离 Playnite 中切换 Follow、浅/深、高对比度时检查任务、健康、图标与错误文字可读。

## GSC-097：Settings 异步反馈与页面卸载边界

- **状态**：源码已修复，待隔离 Playnite 生命周期回归。
- **根因**：设置导入/导出把文件读写移到后台后，完成续体仍会调用 Snackbar、MessageBox、DataContext 刷新和入场动画对象；用户关闭 Playnite 设置页时，这些视觉操作缺少页面可用性边界。
- **修复**：导入/导出本身仍会完成，随后仅在页面已加载且 Dispatcher 未关闭时刷新设置或显示真实结果/错误；反馈显示自身也有异常边界。`Unloaded` 会取消 Settings 入口动画，避免脱离视觉树后保留动画时钟。
- **回归**：自动化测试锁定卸载订阅、反馈可用性检查、动画清理与观察型 `Task` 边界；隔离 Playnite 中启动设置导入/导出后立即关闭/切换页面，确认文件操作真实完成但日志无未处理 Dispatcher、Snackbar 或绑定异常。

## GSC-098：缩放事件的响应式布局合并

- **状态**：源码已修复，待隔离 Playnite DPI/窗口缩放回归。
- **根因**：拖动或 DPI 重排会连续触发 `SizeChanged`。Dashboard 的响应式逻辑会同时重排侧栏、工具栏、列表、媒体、修改器和维护工作区；逐事件同步执行会造成不必要的 UI 线程布局压力。
- **修复**：Dashboard 与 Settings 记录最近尺寸并以 Dispatcher `Render` 优先级合并同一帧中的重复事件；只执行一次最终布局。页面卸载后跳过延迟布局，保持已有紧凑模式、滚动通道、命令与焦点逻辑。
- **回归**：自动化测试锁定合并标记、最近尺寸、Render 优先级和已卸载视图保护；隔离 Playnite 中在 100%–200% DPI 与 980×640–1600×900 连续拖动窗口，检查无重叠、裁切、明显掉帧或卸载回调。

## GSC-099：Dashboard ViewModel 事件订阅生命周期

- **状态**：源码已修复，待隔离 Playnite 生命周期回归。
- **根因**：Dashboard 在构造时订阅 `PropertyChanged` 和关注中心请求事件，但页面卸载时未解除；后台任务更新即使被 `IsLoaded` 过滤，仍会持有视图并产生无效回调/调度。
- **修复**：事件订阅移至 `Loaded`，并在 `Unloaded` 与任务订阅同步解除；重复 Loaded 不会重复订阅。重新打开 Dashboard 时会恢复真实状态动画与关注中心导航，未改变命令、绑定或 Worker 任务。
- **回归**：自动化测试锁定订阅守卫、解除逻辑和两个事件处理器；隔离 Playnite 中反复打开/关闭 Dashboard、完成后台任务和打开关注中心，日志不得出现重复回调、页面泄漏或 Dispatcher 异常。

## GSC-100：Toast 材质在辅助功能模式的回退

- **状态**：源码已修复，待隔离 Playnite 多主题/高对比度回归。
- **根因**：Toast 卡片无条件创建黑色 `DropShadowEffect`；同一共享材质系统中的 Surface、主按钮、侧栏、Popup、Dialog 和 Slider Thumb 也会在关闭毛玻璃或高对比度时保留 Effect visual。阴影既不符合实体材质降级，也增加不必要的合成开销。
- **修复**：`ApplyMaterialResources` 仅在用户启用玻璃效果且未开启高对比度时创建冻结的轻量阴影；其他模式为所有共享材质键提供真正的 `null` Effect。通知内容、真实错误详情、关闭按钮、计时器和主题背景资源不变；列表和滚动内容没有新增阴影或模糊。
- **回归**：自动化测试锁定关闭态为 null、启用态为 Effect 以及 Dashboard/Settings 的局部资源应用；隔离 Playnite 中分别检查浅/深、关闭玻璃和高对比度的成功/错误 Toast、主按钮、侧栏、Popup 与 Dialog 可读、可关闭且无阴影残影。

## GSC-101：后台 Worker 回调的 Dashboard 集合更新边界

- **状态**：源码已修复，待隔离 Playnite 生命周期回归。
- **根因**：Dashboard 长轮询/Worker 回调使用同步 Dispatcher 更新 `ObservableCollection`、筛选与选中项，保证 UI 可见状态的一致顺序；原入口未检查宿主 Dispatcher 已关闭或在检查后关闭的竞态。
- **修复**：`ApplyOnUi` 现在先检查关闭态，并对同步 `Invoke` 的 `InvalidOperationException` 记录真实日志。正常场景仍使用 DataBind 优先级同步更新，未将集合改为后台线程访问，也未伪造或丢失真实任务状态。
- **回归**：自动化测试锁定关闭守卫、DataBind 调度和异常日志；隔离 Playnite 中模拟慢 Worker/任务结束后关闭 Dashboard 或 Playnite，日志不得出现跨线程、已关闭 Dispatcher 或未处理集合更新异常。

## GSC-102：插件级通知和确认的 Dispatcher 关闭边界

- **状态**：源码已修复，待隔离 Playnite 生命周期回归。
- **根因**：`ShowError`、`ShowInfo`、任务通知和安全恢复确认均可能从 Worker/后台续体进入 Playnite UI；原先各自直接 `UIDispatcher.Invoke`，在宿主开始关闭时会抛出未处理异常。
- **修复**：集中 `TryInvokeUi` 检查 Dispatcher 关闭态并记录不可用竞态；仅当 Dispatcher 已确认关闭时才拦截调度异常，处理器自身的真实异常继续进入既有错误边界。通知在安全调度失败时保留既有宿主回退路径；无法显示的确认返回取消，绝不执行恢复或其他需要用户确认的操作。
- **回归**：自动化测试锁定确认、通知和宿主通知均通过统一守卫；隔离 Playnite 中在后台任务完成、错误通知和恢复确认期间关闭宿主，确认日志无未处理 Dispatcher 异常且危险操作不会越过确认。
| GSC-083 | 0.6.22 Dashboard 解析 WPF-UI Button 类型样式时崩溃 | 源码已修复，待隔离 Playnite 回归 | Production 字典必须先合并 WpfUiBase；STA 资源解析测试通过后，在隔离 Playnite 打开 Dashboard/Settings，日志不得再出现 `Wpf.Ui.Controls.Button` 或 `XamlParseException` |
| GSC-084 | 0.6.22 Dashboard 在修复类型样式后仍解析不到主题阴影资源而崩溃 | 源码已修复，待隔离 Playnite 回归 | Production 适配器中的 GameSaveCenter 令牌必须使用 `DynamicResource` 从父级 UserControl 作用域解析；打开 Dashboard/Settings 后日志不得再出现 `GscSoftShadowColor`、`StaticResourceHolder` 或 `XamlParseException` |
| GSC-085 | 0.6.22 动画冻结 Transform 与 WPF-UI `ContentDialogHost` 在 Playnite 共用窗口中导致崩溃 | 源码已修复，待隔离 Playnite 回归 | 回归测试验证冻结的 Translate/Scale Transform 会先克隆为元素私有实例；全插件 XAML/C# 禁止 `ContentDialogHost` 和 `new ContentDialog(...)`；确认使用插件内浮层、设置报告使用 MessageBox，日志不得再出现相应异常 |
| GSC-086 | 媒体摘要只读 `TotalSizeDisplay` 曾被 TwoWay 绑定而在打开媒体页时崩溃 | 源码已修复，待隔离 Playnite 回归 | 媒体概览固定使用 `Mode=OneWay`，回归测试禁止该属性恢复为 TwoWay；仍需在独立 Playnite 实例打开媒体工作区确认无绑定异常 |
| GSC-087 | WPF 定时刷新或取消任务的 async-void/RelayCommand 边界可能把宿主异常传播到 Playnite Dispatcher | 源码已修复，待隔离 Playnite 回归 | 定时刷新事件增加最终异常边界；取消任务改为受保护的 `Task`，确认、Worker IPC 与刷新均在同一 try/catch 内，避免后台故障导致宿主未处理异常 |
| GSC-088 | Dashboard 共用命令执行器曾以 `async void` 承载所有业务命令；错误通知层再次失败时可能造成未观察异常 | 源码已修复，待隔离 Playnite 回归 | 命令入口仅观察 `RunAsync`；所有业务异常统一落入真实状态/通知路径，通知层失败时记录原始异常与通知异常，不再传播到 Playnite Dispatcher |
| GSC-089 | 跟随 Playnite 切换多种主题色时，局部强调色可能静态保留初始紫色，造成按钮、焦点环与选中态不一致 | 源码已修复，待隔离 Playnite 回归 | Dashboard/Settings 必须从宿主 `HighlightGlyphBrush` 派生动态 Accent/主按钮令牌；在隔离实例切换浅色、深色和两种不同强调色主题，所有按钮、焦点环、选中态与图标容器同步更新且文字仍可读 |
| GSC-090 | 高对比度下半透明 Accent Tint 或 Accent 前景可能在系统 Highlight 背景上不可读 | 源码已修复，待隔离 Playnite 回归 | 高对比度必须使用不透明 Windows Highlight/HighlightText，验证导航、页签、游戏行和下拉选中项均可见且键盘焦点仍明确 |
| GSC-091 | WPF-UI 框架控件可能沿用其默认 Fluent 调色板，与动态 GameSaveCenter Accent 产生视觉断层 | 源码已修复，待隔离 Playnite 回归 | 页面局部覆盖已验证的 WPF-UI Accent/Text/Control/Card/Focus 资源键；在隔离 Playnite 切换多色主题时，原生与 WPF-UI 按钮、开关、输入、下拉和 Card 必须同步更新且不污染宿主 |
| GSC-092 | 插件生命周期、设置同步或后台通知中的 `async void` 在错误反馈再次失败时可能把异常送入宿主 | 源码已修复，待隔离 Playnite 回归 | 生命周期工作必须是可观测 Task；故障及错误呈现故障都写入日志。反复更新设置、导入库、启动/退出游戏和断开 Worker 时不得出现未处理异常 |

### GSC-083：WPF-UI Button 同级资源字典作用域导致 Dashboard 崩溃

- **状态**：源码已修复，待隔离 Playnite 回归。
- **真机证据**：2026-08-01 的 Playnite `0.6.22` 日志在打开 GameSaveCenter 侧栏时记录 `DashboardView.InitializeComponent()` 的未处理 `XamlParseException`：找不到资源 `Wpf.Ui.Controls.Button`。
- **根因**：`WpfUiProduction.xaml` 的按钮适配样式使用 WPF-UI 的类型键作为 `BasedOn`，但默认样式位于 Dashboard/Settings 的同级 `WpfUiBase.xaml`。Playnite 的 BAML 加载在解析 Production 字典时不向该同级字典查找类型键。
- **修复**：Production 字典直接合并 WpfUiBase，两个页面只保留 DesignTokens + Production 的合并顺序；新增 STA XAML 资源字典测试，确保 `GscWpfUiButton` 的类型样式可实际解析。
- **回归**：使用独立 Playnite 数据根打开 Dashboard 与 Settings，并显示一次 Dialog/Snackbar；`playnite.log` 不得新增 `Wpf.Ui.Controls.Button`、`StaticResourceHolder` 或 `XamlParseException`。

### GSC-084：WPF-UI 适配器静态主题令牌导致 Dashboard 二次崩溃

- **状态**：源码已修复，待隔离 Playnite 回归。
- **真机证据**：同一份 2026-08-01 崩溃日志显示，在修复 `Wpf.Ui.Controls.Button` 后再次打开侧栏，`DashboardView.InitializeComponent()` 抛出未处理 `XamlParseException`，内部 `StaticResourceHolder` 找不到 `GscSoftShadowColor`。
- **根因**：`WpfUiProduction.xaml` 在自身解析期间用 `StaticResource` 查找 `GscSoftShadowColor` 和 `GscSharedFocusVisual`；这两项令牌由宿主 UserControl 的兄弟字典 `DesignTokens.xaml` 提供。Playnite 的 BAML 解析不保证在该时点向父级兄弟字典回溯。
- **修复**：适配器把上述 GameSaveCenter 令牌改为 `DynamicResource`，延后到控件实际位于 Dashboard/Settings 父级资源树时解析；只保留 WPF-UI 类型默认样式的本字典 `StaticResource`。新增 STA 资源树布局测试与源码门禁，防止把这些令牌恢复为静态查找。
- **回归**：以独立 Playnite 数据根打开 Dashboard 与 Settings；`playnite.log` 不得新增 `GscSoftShadowColor`、`StaticResourceHolder` 或 `XamlParseException`。该验证不能使用用户日常 Playnite 或其扩展目录。

### GSC-085：WPF-UI Window 级 ContentDialogHost 与 Playnite 共享宿主冲突

- **状态**：源码已修复，待隔离 Playnite 回归。
- **真机证据**：2026-08-01 14:16:47，`0.6.22` 在插件加载后抛出未处理 `InvalidOperationException`：`Only one ContentDialogHost instance is allowed per Window.`，堆栈位于 `Wpf.Ui.Controls.ContentDialogHost.RegisterHost(Window window)`。
- **根因**：Dashboard、Settings 和惰性界面探针都在 Playnite 的同一个 Window 内各自声明了 `ContentDialogHost`。WPF-UI 将该宿主注册为窗口级单例，无法在嵌入式页面中安全重复使用，也不能假设其他扩展未注册它。
- **修复**：移除所有 Playnite 页面中的 `ContentDialogHost` 与 `ContentDialog` 构造。Dashboard 的普通/危险确认继续使用已有插件内半透明对话层；设置导入报告改为可靠的 `MessageBox`；Snackbar 与本地 Toast 保留。新增单元测试和源码门禁，禁止重新注册 Host。
- **回归**：独立 Playnite 中依次打开 Dashboard、Settings 和维护中心探针，普通/危险确认使用 Enter、Esc、取消和确认均能完成原有 `TaskCompletionSource`；导入报告可关闭；`playnite.log` 不得出现 `ContentDialogHost`、`RegisterHost` 或未处理异常。

## 当前安全边界

- 未通过多版本与恢复回归前，不对重要存档执行恢复。
- 云端仍只允许 `rclone copy/check`，不默认启用镜像删除。
- 从 `0.1.x` 升级后，第一次刷新应自动迁移 SQLite 主键并重新同步设置。

### GSC-020：Apple 风格按钮模板无法通过 XAML 编译

- 状态：已修复，待 Windows 回归。
- 现象：`DashboardView.xaml` 报 `MC4111`，模板触发器无法找到 `ButtonScale`。
- 原因：触发器尝试跨模板名称作用域直接定位 `ScaleTransform`。
- 第一次修复遗漏了 `GscPrimaryButton` 中的第二处 `ButtonScale`，Windows 构建随后在第 119 行再次报同类错误。
- 最终修复：基础按钮与主按钮模板均只定位模板根元素 `Chrome`，触发器整体替换其 `RenderTransform`；源码中不再存在任何 `ButtonScale` 引用，并保留按下时 0.97 缩放效果。

### GSC-021：任务状态模板的触发器层级错误

- **状态：已修复，待 Windows 构建回归**
- **现象：** `DashboardView.xaml` 编译时报 `MC3015`，提示 `StackPanel` 上未定义附加属性 `DataTemplate.Triggers`。
- **根因：** 任务状态列为了压缩成单行，将 `<DataTemplate.Triggers>` 错误嵌入了 `<StackPanel>` 内容中，而它必须是 `<DataTemplate>` 的直属属性元素。
- **修复：** 将任务状态模板展开为标准 XAML 结构，触发器移到 `StackPanel` 结束标签之后；新增 `scripts/check-xaml.ps1`，在正式构建前检查触发器父级、模板 TargetName 和 Transform 目标等常见 WPF 名称作用域错误。


### GSC-022：长任务执行期间管理面板无法看到实时进度

- **状态：已修复，待 Windows 回归**
- **现象：** 手动备份请求等待 Worker 完成期间，旧版 `IsBusy` 会阻止所有刷新，任务页无法持续看到 Running 状态和进度。
- **修复：** Dashboard 使用独立轻量轮询，不受手动操作 Busy 状态阻断；只刷新仪表盘和任务，任务完成后按需重载当前游戏历史。

### GSC-023：排队任务取消时可能永久停留在 Queued

- **状态：已修复，待 Windows 回归**
- **根因：** `TaskCoordinator` 在进入 `try/finally` 前等待每游戏锁；若等待期间取消，状态、Token 清理和锁释放逻辑均不会执行。
- **修复：** 将锁等待纳入统一状态机，通过 `gateEntered` 只释放实际获得的锁，并确保取消状态写入 SQLite。

### GSC-024：缺少用户可直接复制的诊断信息

- **状态：已开发，待 Windows 回归**
- **实现：** 新增诊断页，显示有效 Worker 设置、版本、目录、备份策略和最近失败任务；支持复制摘要以及打开数据、存档、媒体和 Worker 日志位置。

### GSC-025：Worker 异常退出后任务可能永久停留在 Running

- **状态：已修复，待 Windows 回归**
- **现象：** Worker 被强制结束或升级重启时，SQLite 中已排队/执行中的任务没有机会进入 finally，管理面板会长期显示旧的 Queued/Running。
- **修复：** Worker 初始化数据库后将遗留活动任务标记为 Failed，错误码为 `WORKER_RESTARTED`，提醒用户检查目标文件后重新执行。


### GSC-026：界面仍缺少完整的 Apple 风格层级与微动效

- **状态：已重构，待 Windows 构建与视觉回归**
- **原现象：** 虽已更换圆角与主题资源，但页面仍缺少应用侧栏、玻璃层级和连续的页面/选择动画。
- **修复：** 增加侧栏导航、主题自适应拟态毛玻璃、环境光、状态胶囊，以及页面、游戏、标签、任务、卡片、导航和按钮动画。
- **回归：** 分别在浅色、深色、两个不同 Playnite 主题、100%/150% DPI 下检查文字、透明度、滚动与动画。

### GSC-027：毛玻璃和动画需要可关闭并兼容高对比度

- **状态：已开发，待 Windows 回归**
- **风险：** 模糊环境光可能在低性能设备上增加 GPU 负担，半透明表面在高对比度下可能降低可读性。
- **修复：** 设置页增加动画、毛玻璃开关和强度；遵循 Windows 客户区动画设置；高对比度模式自动使用不透明背景并禁用环境光。

### GSC-028：悬停动画尝试修改被冻结的 Style Transform 导致 Playnite 崩溃

- **根因**：`GscNavItem`、`GscMetricCard` 和按钮样式通过 `Style.Setter` 共享 `TranslateTransform`/`ScaleTransform`。WPF 会冻结这些 `Freezable`，随后 `BeginAnimation` 抛出 `InvalidOperationException`。
- **日志证据**：Playnite 主日志多次指向 `DashboardView.AnimateTranslate`，由 `OnNavigationMouseEnter` 和 `OnMetricCardMouseEnter` 触发。
- **修复**：移除 Style 中的动画 Transform；动画入口对已有冻结 Transform 使用 `CloneCurrentValue()`，再把独立可变实例回写到当前元素。
- **状态**：已精确修复，待 Windows 悬停回归。
- **门禁**：反复经过侧栏、指标卡和按钮至少 2 分钟，不出现扩展崩溃，动画持续可用。


## GSC-029：开发安装完成后 Playnite 仍加载旧版本

- **状态**：已修复，待 Windows 回归。
- **现象**：源码和补丁已升级，但 Playnite 扩展管理仍显示 0.3.1，动画崩溃也继续出现。
- **根因**：旧开发安装流程没有验证实际安装目录、清单版本和 DLL 文件版本；打包文件名长期写死为 0.2.0，且删除旧扩展时忽略错误，容易误以为已替换。
- **修复**：新增一键构建安装运行入口；自动关闭 Playnite/Worker、清理构建、动态读取版本打包、原子替换扩展，并在启动前验证 extension.yaml 与 DLL 文件版本。


## GSC-030：双击一键脚本出现乱码并把参数片段当成命令

- **状态**：已修复，待 Windows 双击回归。
- **现象**：`cmd.exe` 输出中文乱码，并报告 `rofile`、`se` 等不是内部或外部命令，PowerShell 安装流程没有真正启动。
- **根因**：入口 `.cmd` 使用无 BOM UTF-8 中文内容和 LF 换行；传统 `cmd.exe` 在解析批处理文件时受系统代码页和换行格式影响，导致字节被误解析。
- **修复**：新增纯 ASCII、CRLF 的 `GameSaveCenter-Run.cmd`；中文文件名入口只调用该脚本；PowerShell 主脚本使用 UTF-8 BOM 并记录 `artifacts/one-click-install.log`。
- **门禁**：源码检查强制所有 `.cmd` 入口仅含 ASCII 且使用 CRLF，`dev-install-run.ps1` 必须带 UTF-8 BOM。

## GSC-031：只读 DurationDisplay 被 TwoWay 绑定导致自动刷新停用

- **状态**：已修复，待 Windows 回归。
- **现象**：底部持续提示无法对 `TaskStatusDto.DurationDisplay` 进行 TwoWay/OneWayToSource 绑定，后台自动刷新被异常中断。
- **根因**：任务详情中的 `Run.Text` 未显式声明绑定方向，WPF 按目标属性元数据尝试回写只读计算属性。
- **修复**：`DurationDisplay` 与任务 ID 明确使用 `Mode=OneWay`；源码门禁阻止该绑定再次退化。

## GSC-032：备份任务成功且 ZIP 已生成，但历史面板为空

- **状态**：已修复，待 Windows 回归。
- **证据**：任务显示“已创建新的历史版本”，磁盘存在 `backup-*.zip` 和 `mapping.yaml`，但 `Backups` 集合仍为空。
- **根因**：`backup.list` 只读取 SQLite 缓存，不会在历史面板打开时主动与 Ludusavi `backups --api` 对账；一次索引失败会长期表现为无历史。
- **修复**：每次读取历史时先尝试与 Ludusavi 对账，再返回 SQLite 索引；对账失败保留旧索引并写入诊断。官方输出报告存在版本但解析结果为零时，禁止清空缓存并返回稳定错误码。

## GSC-033：第三方 Playnite 主题下文字和输入区域对比失效

- **状态**：已重构，待多主题视觉回归。
- **现象**：浅色主题出现黑色输入块，深色主题出现近黑文字，单纯按“浅色/深色”切换无法覆盖社区主题。
- **修复**：从宿主背景、`TextBrush`、`TextBrushDark` 等资源推导局部高对比色板；文字、输入框、边框、卡片和侧栏统一使用派生资源，不再直接复用不确定的 `ControlBackgroundBrush`。

## GSC-034：文字在 DPI 与动画环境下发虚

- **状态**：已改善，待 100%/125%/150% DPI 回归。
- **原因**：正文大量使用控件整体 `Opacity`，按钮悬停缩放文字，同时缺少统一像素对齐和 ClearType 提示。
- **修复**：文字透明度改为带 Alpha 的专用前景色；启用 `UseLayoutRounding`、`SnapsToDevicePixels`、Display/ClearType/Fixed hinting；按钮悬停改为整数像素位移，不再缩放正文。

## GSC-035：大型游戏库缺少搜索、筛选和排序

- **状态**：已开发，待 Windows 回归。
- **修复**：增加按游戏名、Ludusavi 名称、平台和状态搜索；增加已就绪、未匹配、运行中、需关注、有历史筛选；增加名称、运行优先、匹配优先和最近备份排序，并显示过滤结果数量。

## GSC-036：任务表格和底部进度条布局失控

- **状态**：已重构，待视觉回归。
- **现象**：任务行拥挤、百分比压在进度条上；底部空进度框在空闲时仍显示并贴住状态提示。
- **修复**：任务进度使用固定宽度轨道与独立百分比列，详情限制并省略；底部空闲进度框移除，只有实际忙碌或后台刷新时显示独立进度胶囊。

## GSC-037：UI 设计规范未形成长期工程约束

- **状态**：已修复。
- **风险**：聊天中的 Apple-inspired 设计要求容易在后续会话丢失，新增控件又回到硬编码颜色、过度玻璃或默认 WPF 风格。
- **修复**：完整规范保存为 `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`，新增 `docs/design/UI_CHANGE_GATE.md`；README 与 Codex 交接文档均要求先阅读。

## GSC-038：候选存档路径缺少默认会话前后差异闭环

- **状态**：已开发，待 Windows 真机调优。
- **原状**：只能手动扫描最近变化目录，无法明确证明文件变化发生在本次游戏会话中。
- **修复**：仅对未匹配游戏在会话开始时异步记录有界快照，退出后对比新增/修改文件并生成可解释候选；Xbox WGS、会话末写入和重复模式继续参与评分。
- **回归**：未匹配测试游戏启动不能明显阻塞；退出后候选应在详情页持久显示，缓存/日志目录不能获得高分。

## GSC-039：候选路径状态与重复扫描管理不完整

- **状态**：已修复待回归。
- **问题**：候选详情重新打开后曾被清空；已接受路径可能被新扫描重新插入为 Pending；用户没有明确忽略入口。
- **修复**：新增候选列表 IPC；详情加载读取 SQLite；更新同一路径 Pending 记录时保留 Accepted；增加“忽略候选”及审计；Worker 启动清理过期会话快照。

## GSC-040：失败任务缺少可操作的下一步

- **状态**：已开发待回归。
- **问题**：任务虽然显示真实错误，但用户仍需手工复制和重新发起操作。
- **修复**：任务详情增加“复制详情”；只对 Backup、MediaSync 的 Failed/Cancelled 状态开放安全重试。恢复、撤销恢复及未知任务禁止通用重放。

## GSC-041：第三方主题异常时缺少稳定外观兜底

- **状态**：已开发待多主题回归。
- **修复**：设置新增“跟随 Playnite / 浅色 / 深色”。跟随模式继续从宿主资源派生色板，固定模式使用稳定局部背景与文字组合；主题设置保存后管理面板重算色板，不修改 Playnite 窗口外壳。

## GSC-042：公共截图目录只靠文件名导致漏归类

- **状态**：已开发待真机回归。
- **问题**：Game Bar 和 Windows Screenshots 的文件名不一定包含完整游戏名，旧版会直接跳过。
- **修复**：退出媒体同步带有明确 SessionId 时，文件名不匹配仍可按会话开始前 2 分钟至结束后 10 分钟归类；数据库检测到其他游戏会话重叠时，自动关闭时间推断并退回文件名匹配。
- **安全边界**：不做“时间最新就一定属于该游戏”的无条件猜测；无法唯一判断的文件由 0.4.1 收件箱保留并等待人工处理。


## GSC-043：公共媒体未识别统计为空壳，歧义文件被跳过

- **状态**：已开发待 Windows 真机回归。
- **问题**：0.4.0 仪表盘存在未归类计数，但公共目录扫描无法唯一判断游戏时直接跳过文件，UI 没有真实待处理记录，也无法人工分配。
- **修复**：新增媒体分类状态/原因、全局 `MediaInbox` 任务、`_Inbox/Pending` 归档、待归类列表、人工归类、忽略保留副本与安全重试。共享目录只扫描一次，文件名多游戏歧义不再伪装成唯一匹配。
- **升级修复**：旧数据库先补 `classification_state/reason` 再创建索引，避免启动迁移报 `no such column`。
- **源文件边界**：重新归类或忽略只移动 GameSaveCenter 归档副本；归档缺失时从原始文件重新复制，禁止删除或移动原始截图/录像。
- **回归**：验证旧库升级、首轮 200 项保护、重启持久化、归类/忽略后的真实文件位置、SHA-256 去重、不同盘符移动以及多主题/DPI 布局。

## GSC-044：0.4.1 Playnite 工程因资源字典结构错误无法编译

- **状态**：已修复，待 Windows 重新构建。
- **现象**：`DashboardView.xaml` 和 `GameSaveCenterSettingsView.xaml` 报 `MC3074`，提示 Presentation 命名空间中不存在 `ResourceDictionary.MergedDictionaries`。
- **根因**：`MergedDictionaries` 是 `ResourceDictionary` 的属性元素，不能直接作为 `UserControl.Resources` 的资源条目；同时存在合并字典和本地样式时必须显式创建 `ResourceDictionary`。
- **修复**：在两处 `UserControl.Resources` 内增加显式 `<ResourceDictionary>`，并新增跨平台 XAML 语义门禁。
- **伴随修复**：仓库统一普通文本使用 LF，避免 Windows 上 `APPLE_UI_GUIDE.md`、`validate-source.py` 因编辑器 CRLF 自动转换持续显示为修改；`.cmd` 仍保留 CRLF。
## GSC-045：0.4.1 打开 Playnite 侧栏时因缺失静态资源崩溃

- **状态**：已修复为 0.4.2，待 Windows 真机回归。
- **真机证据**：Playnite 10.56 日志先记录 `Loaded plugin: GameSaveCenter, version 0.4.1`，随后点击侧栏时出现未处理的 `System.Windows.Markup.XamlParseException`。
- **异常**：`无法找到名为“GscStatusPill”的资源。资源名称区分大小写。`
- **根因**：0.4.1 媒体收件箱计数 Border 引用了未定义的 `{StaticResource GscStatusPill}`。此前门禁只检查 XML 结构、MergedDictionaries、Trigger 和 TargetName，未校验项目自有资源键是否真实存在。
- **修复**：新增 `GscStatusPill` 样式；将不存在的 `GscCardBrush`、`GscHairlineBrush` 替换为已存在的玻璃表面和描边令牌；新增全部 `Gsc*` 静态/动态资源引用门禁。
- **回归**：安装 0.4.2 后确认附加组件页版本、连续打开/关闭侧栏、切换全部标签与三种主题，不得再出现扩展崩溃窗口或资源解析异常。
## 0.5.0 修改器中心

- **待验证**：FLiNG 是未提供稳定公开 API 的网页来源。当前解析器已隔离并带最小目录数量保护，但官网结构变化仍可能使目录或版本解析失败。
- **待验证**：修改器容易触发安全软件告警或隔离。GameSaveCenter 不会关闭 Defender、建立白名单或自动重试被拦截的文件。
- **待完善**：ZIP 或目录含多个可执行文件时，当前使用排除卸载器/更新器后体积最大的 EXE；后续需要在 Playnite 中增加显式主程序选择器。
- **待验证**：提权修改器的 PID 生命周期、CT 文件关联、多 CT 同时启动以及游戏退出关闭行为需要 Windows 真机回归。

## 0.5.1 构建恢复顺序修复

- **已修复待验证**：源码包若包含由其他用户配置文件生成的 `obj/project.assets.json`，旧的一键脚本会在 NuGet 恢复前执行 `dotnet clean`，进而报 `NETSDK1064` 缺少包。现在一键入口会先恢复依赖并重写资产路径，再执行清理、构建和打包。

# 已知缺陷与回归状态

更新时间：2026-07-27
目标版本：`0.3.5-development-preview`

本文档是持续缺陷台账。任何修复必须同步更新 `DEVELOPMENT_PROGRESS.md` 与 `PROJECT_MEMORY.md`。

| 编号 | 问题 | 0.2.0 状态 | 回归要求 |
|---|---|---|---|
| GSC-001 | Worker 冷启动等待过短 | 已修复待验证 | 首次启动允许最多 30 秒，Defender 扫描时仍能就绪 |
| GSC-002 | 残留同路径 Worker 阻止重启 | 已修复待验证 | 健康检查失败时只终止同一安装路径的旧进程 |
| GSC-003 | 刷新只读缓存，不重发设置/游戏/匹配 | 已修复待验证 | 点击刷新后重新发送设置、导出全部游戏并匹配 |
| GSC-004 | CLI 可匹配但插件显示 Unmatched | 已修复待验证 | Worker 当前路径正确，测试游戏和 Bongo Cat 均显示已就绪 |
| GSC-005 | Ludusavi 失败只显示“执行失败” | 已修复待验证 | 任务详情显示稳定错误码和真实诊断 |
| GSC-006 | Worker 重启后丢失 Ludusavi 路径 | 已修复待验证 | `worker-settings.json` 持久化；重启后无需 PowerShell 注入 |
| GSC-007 | UTC 时间直接显示 | 已修复待验证 | 任务、历史、媒体和审计按 Windows 本地时区显示 |
| GSC-008 | 深色主题文字对比度过低 | 已重构待视觉回归 | 浅色、深色及至少两个 Playnite 主题文字清晰 |
| GSC-009 | 按钮为默认 WPF 样式 | 已重构待视觉回归 | 圆角、克制强调色、悬停/按压反馈一致 |
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

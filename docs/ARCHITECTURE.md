# 系统架构

## 组件

```text
Playnite 10
  └─ GameSaveCenter.Playnite (net462/WPF)
       ├─ Apple HIG 启发的 Dashboard / Settings / Restore UI
       ├─ Playnite game events, library metadata and Game Actions
       └─ Named-pipe IPC client
                │
                ▼
GameSaveCenter.Worker (net8.0-windows)
  ├─ Named-pipe IPC server
  ├─ Game session coordinator
  ├─ Ludusavi process adapter
  ├─ Rclone copy/check adapter
  ├─ Save validation / retention / restore orchestration
  ├─ Process and MOD launch-chain detection
  ├─ Media source adapters and incremental synchronizer
  ├─ Save path / Xbox WGS candidate detector
  └─ SQLite state, tasks, findings and audit logs
```

## 关键边界

- `GameSaveCenter.Contracts` 只保存 IPC DTO、枚举和协议常量，不依赖 Playnite、WPF、数据库或外部工具。
- `GameSaveCenter.Core` 保存纯算法和领域模型，可独立单元测试。
- `GameSaveCenter.Worker` 负责文件 I/O、数据库、进程侦测、定时任务和外部程序调用。
- `GameSaveCenter.Playnite` 只负责 Playnite SDK 适配、UI、用户确认和短生命周期 IPC 调用。
- Ludusavi 仍是存档复制/版本/恢复引擎；GameSaveCenter 不重写其底层格式。
- 媒体同步不进入 Ludusavi 历史，避免截图和录像在每个存档版本中重复。

## IPC

使用当前用户范围内的命名管道：

- 管道名：`GameSaveCenter.Worker.v1`；
- 一行一个 JSON envelope；
- 每条请求包含 `requestId`、`type`、`timestampUtc`、`payload`；
- 响应复用相同 `requestId`；
- 协议版本和最大消息大小由 Contracts 固定；
- 当前实现以请求/响应为主；`tasks.changes` 提供 Worker 内存中的增量任务变化拉取，避免面板每次轮询重建完整首页。Worker 主动持续推送尚未完成。

长任务由 Worker 写入 SQLite 任务表；Playnite 以 `tasks.changes` 获取立即增量，并以 `tasks.changes.wait` 建立最长 25 秒的信号唤醒长轮询。Worker 重启、事件窗口溢出、管道中断或面板首次打开时回退到 SQLite 全量快照；近实时事件只增强体验，不能成为任务正确性的唯一依赖。

## 数据存储

SQLite 保存：

- Playnite 游戏映射、平台 ID、安装路径、已知 EXE 和 Game Actions；
- 每游戏备份/媒体/云端策略；
- 游戏会话和定时备份状态；
- Worker 任务、异常 finding 和审计事件；
- Ludusavi 历史摘要与可用 manifest；
- 媒体来源、文件哈希、归档路径、`Assigned/Inbox/Ignored` 分类状态、可解释原因和同步状态；
- 存档候选路径和确认状态。

配置文件只保存工具路径、目录和无敏感策略。Rclone 凭据由 Rclone 自己管理，不复制到数据库或插件设置。

## 游戏会话识别

优先级：

1. Playnite `OnGameStarted/OnGameStopped` 事件；
2. Playnite Game Actions、已知安装 EXE 和平台 ID；
3. Worker 进程路径、父子关系和 MOD loader 规则；
4. 后续人工学习映射。

同一游戏可能同时存在 loader、启动器、反作弊和主游戏进程。Worker 将它们合并为一个逻辑会话，并只在所有已映射游戏进程退出后结束会话。

## 媒体同步

```text
游戏专属目录 ───────────────→ 稳定写入检测
共享 Captures/Screenshots ─→ 单次全局扫描
                              → 唯一名称 / 无重叠会话归类
                              → 无法确认则 `_Inbox/Pending`
                              → SHA-256 去重与原子复制
                              → 人工归类 / 忽略保留副本
                              → 已归类媒体可选 rclone copy
```

规则：

- 只处理新增文件；
- 同图改名仍去重；
- 源文件删除不传播到归档；
- 共享目录由全局 `MediaInbox` 任务扫描一次，不按每个游戏重复遍历；
- 只在文件名唯一匹配，或明确 SessionId 且会话时间窗口无重叠时自动归类；
- 无法确认的媒体复制到 `_Inbox/Pending` 并记录原因，而不是静默猜测；
- 人工归类和忽略只移动归档副本，原始截图/录像不被删除；
- 游戏存档恢复不触碰媒体库。

## 备份可靠性

每次备份后可比较前后 manifest 或摘要：

- 文件数量；
- 总体积；
- 零字节文件；
- 异常下降；
- 长游戏会话却无变化；
- 路径消失或工具错误。

异常写入 finding，不因云端失败而撤销本地成功备份。

## 安全恢复状态机

```text
Requested
→ GameClosedVerified
→ PreRestoreBackupCreatedAndLocked
→ RestorePreviewed
→ RestoreExecuted
→ PostRestoreValidated
→ Completed
```

任一步失败：

```text
Failed
→ RollbackAttempted
→ RolledBack / ManualInterventionRequired
```

恢复会先验证该游戏不存在活跃会话或仍存活的记录进程，再取得全局云传输闸门：它会等待已有 `rclone copy` 结束，并在恢复完成前阻止新的备份和媒体上传启动。它不会强制终止已经运行的安全上传。

## 云端边界

- 默认只调用 `rclone copy`；
- 校验使用 `rclone check --one-way` 等只读校验；
- 多设备状态使用每设备独立的、无存档内容 sidecar；读取只调用 `rclone lsf/cat`，分叉仅提示人工选择；
- 不调用 `sync`、`delete`、`purge`；
- 本地副本是主副本，云端失败只记录任务错误；
- 多设备默认使用不同设备子目录；
- 完整多设备冲突闭环仍需远端设备摘要 sidecar 和摄取逻辑。

# 系统架构

## 组件

```text
Playnite 10
  └─ GameSaveCenter.Playnite (net462/WPF)
       ├─ Dashboard / Settings / Restore Wizard
       ├─ Playnite game events and menus
       └─ Named-pipe IPC client
                │
                ▼
GameSaveCenter.Worker (net8.0-windows)
  ├─ Named-pipe IPC server
  ├─ Game session coordinator
  ├─ Ludusavi process adapter
  ├─ Rclone process adapter
  ├─ Save validation / retention / restore orchestration
  ├─ Process and MOD launch-chain detection
  ├─ Media source adapters and incremental synchronizer
  ├─ Save path / Xbox WGS candidate detector
  └─ SQLite state and structured logs
```

## 关键边界

- `Contracts` 只保存 IPC DTO 与枚举，不依赖 Playnite、WPF 或数据库。
- `Core` 保存纯算法和领域模型，可独立测试。
- `Worker` 负责 I/O 和外部进程。
- `Playnite` 项目只负责适配 Playnite SDK、UI 和通知。

## IPC

使用当前用户范围内的命名管道：

- 管道名：`GameSaveCenter.Worker.v1`
- 一行一个 JSON envelope。
- 每条请求包含 `requestId`、`type`、`timestampUtc`、`payload`。
- 响应使用相同 `requestId`。
- Worker 可主动推送任务状态事件。

## 数据存储

- SQLite 保存游戏映射、策略、任务、备份摘要、媒体索引、会话、候选路径和冲突记录。
- 配置文件只保存工具路径、目录和无敏感策略。
- Rclone 凭据由 Rclone 自己管理，不复制到数据库。

## 安全恢复状态机

```text
Requested
→ GameClosedVerified
→ PreRestoreBackupCreated
→ CloudJobsPaused
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

# GameSaveCenter

GameSaveCenter 是一个面向 Windows PC 游戏的本地优先存档与媒体管理系统：

- **Playnite 插件**：统一 UI、游戏菜单、启动/退出事件与通知。
- **后台 Worker**：执行 Ludusavi、Rclone、进程侦测、截图增量同步、校验和恢复编排。
- **Ludusavi**：作为成熟的存档扫描、版本备份和恢复引擎。
- **Rclone**：只读安全策略优先的云端单向复制与校验通道。

主要兼容 Steam、Xbox PC / Game Pass、Epic、Ubisoft Connect、EA App、GOG，以及通过 MOD 加载器启动的游戏。

> 当前仓库是 Windows 集成开发版本。请先阅读 `docs/DEVELOPMENT_PROGRESS.md` 和 `docs/PROJECT_MEMORY.md`。

## 核心原则

1. 自动备份可以积极，自动恢复必须保守。
2. 恢复前始终创建 `PreRestore` 快照。
3. 截图/录像采用增量同步与哈希去重，不跟随每个存档版本重复打包。
4. 从 Playnite 启动时优先使用精确事件；从平台客户端、快捷方式或 MOD 管理器启动时由 Worker 进程侦测兜底。
5. 云端默认使用 `rclone copy`，不默认使用会删除目标文件的 `sync`。
6. 所有可破坏性操作必须可审计、可取消、可回滚。

## 仓库结构

```text
src/GameSaveCenter.Playnite   Playnite 10 插件（.NET Framework 4.6.2 / WPF）
src/GameSaveCenter.Worker     Windows 后台助手（.NET 8）
src/GameSaveCenter.Core       领域逻辑与算法
src/GameSaveCenter.Contracts  插件与 Worker 的 IPC 契约
tests/                        单元与集成测试
scripts/                      Windows 构建、打包、安装和验证脚本
docs/                         需求、进度、架构、记忆与操作文档
design/                       Apple HIG 启发的 UI 规范和原型资料
```

## 快速开始（开发机）

要求：

- Windows 10/11 x64
- Visual Studio 2022 或 Rider
- .NET Framework 4.6.2 Developer Pack
- .NET 8 SDK
- Playnite 10.56+
- Ludusavi 0.30+
- Rclone（云同步需要）

在 PowerShell 中执行：

```powershell
./scripts/build.ps1
./scripts/package.ps1
./scripts/install-dev.ps1 -PlayniteExtensionsPath "$env:APPDATA\Playnite\Extensions"
```

## 安全状态

在完成 Windows 真机恢复测试前，本项目应视为 **开发预览版**。默认配置禁用自动恢复和云端反向覆盖。

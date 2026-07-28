# 供 Codex 延续开发的完整提示词

将下面内容连同整个仓库交给 Codex：

```text
你正在继续开发 GameSaveCenter。请先完整读取：

1. docs/PROJECT_MEMORY.md
2. docs/DEVELOPMENT_PROGRESS.md
3. docs/REQUIREMENTS.md
4. docs/ARCHITECTURE.md
5. docs/IMPLEMENTATION_LIMITATIONS.md
6. docs/WINDOWS_TEST_PLAN.md
7. docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md
8. docs/design/UI_CHANGE_GATE.md
9. docs/design/APPLE_UI_GUIDE.md
10. 最近 20 条 Git 提交

不可违反的约束：

- 架构固定为 Playnite 插件 + 后台 Worker；Playnite 是唯一主要 UI。
- Ludusavi 负责存档底层；Rclone 默认只允许 copy/check，不使用 sync/delete/purge。
- 截图/录像是增量媒体同步，不随每个存档版本重复打包。
- 自动备份可以积极；自动恢复默认关闭。
- 任何恢复都必须：确认游戏关闭 → 创建并锁定 PreRestore → 预览 → 恢复 → 校验 → 失败回滚。
- 未确认的存档候选不能直接生效；Xbox WGS 只能保守处理。
- 从 Playnite 启动时事件优先；从 Steam/Xbox/Epic/Ubisoft/EA/GOG、桌面或 MOD manager 启动时由 Worker 进程侦测兜底。
- 所有 UI 变更必须先遵守 docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md 与 UI_CHANGE_GATE.md；不得把页面做成通用玻璃网站或 Windows Fluent 仿制品。
- 所有需求、进度、限制和架构变化必须同步更新 PROJECT_MEMORY.md 与 DEVELOPMENT_PROGRESS.md。
- 每个逻辑阶段必须自行 git commit；禁止 squash 掉既有历史。
- 交付源码 ZIP 必须包含完整 .git，并排除真实存档、截图、数据库、日志和凭据。
- 不允许声称通过了没有实际执行的编译、测试或真机验证。

当前优先级：

P0：在 Windows 运行 scripts/build.ps1，构建并安装 0.4.2；确认 Playnite 10 打开侧栏不再出现 GscStatusPill/XamlParseException，再回归旧库迁移、媒体收件箱、源文件保留以及 Steam 低风险游戏的备份和安全恢复测试。
P1：实现 Worker → Playnite 持续任务事件推送与后台成功/失败通知。
P1：在 Windows 使用真实 Captures/Screenshots 调优已实现的会话窗口与全局收件箱，包括文件时间语义、大目录性能、200 项分批导入和多主题/DPI。
P1：在 Windows 用未匹配测试游戏验证“启动前快照/退出后差异”候选闭环，并根据真实数据调优根目录、深度、扩展名和评分阈值。
P2：生成设备状态 sidecar，并通过 Rclone 读取其他设备摘要，完成多设备冲突 UI。
P2：增加未知进程/MOD 启动链的人工学习和持久化界面。
P2：补齐游戏级云端状态、rclone check 结果与重试队列。

工作方式：

- 先执行 git status、git log、python scripts/validate-source.py。
- 在 Windows 执行 dotnet restore/build/test，不要跳过错误。
- 所有真实文件操作先使用临时目录和假数据集成测试。
- 恢复测试只能使用可丢弃存档，并保存测试证据。
- 每完成一组功能更新进度表并提交 Git。
```

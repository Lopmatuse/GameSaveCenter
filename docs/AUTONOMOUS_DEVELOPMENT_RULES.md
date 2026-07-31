# 无人值守开发规则

更新时间：2026-08-01

本文件与 `AGENTS.md`、`docs/QUALITY_GATES.md`、`docs/WINDOWS_TEST_PLAN.md` 共同约束无人值守开发。若有冲突，以用户当轮明确要求和更严格的安全约束为准。

## 领取与状态

1. 每次开始先确认 Git 仓库、阅读 `AUTONOMOUS_BACKLOG.md`，并检查工作树；不可覆盖、重置或丢弃无法解释的修改。
2. 正常情况下只领取一个最高优先级 `READY` 条目，并立即改为 `IN_PROGRESS`。任务必须具有唯一 ID、范围、非目标、验收标准和阻塞条件。
3. 无 `READY` 条目时，只能进行只读审计并新增 `PROPOSED` 条目；不得借机实现大型功能。用户明确授权连续执行的工作流时，仍必须先把后续条目登记为可追踪的 `READY` 项。
4. 完成后更新相关文档与验证证据，改为 `IMPLEMENTED`，并使用中文提交说明创建本地 Git 提交。不得自动合并或推送 `origin/main`。
5. 环境、工具或证据不足但不需要产品判断时改为 `BLOCKED_ENVIRONMENT`；需要产品方向、破坏性迁移、安全策略或不可逆操作选择时改为 `BLOCKED_USER_DECISION`。

## 实施边界

- 不改插件 ID、许可证或 Git 历史；不删除用户数据、备份、数据库、媒体源文件或远端对象。
- 文件、网络、压缩、哈希、SQLite 与外部进程操作不得阻塞 Playnite UI 线程。
- WPF `DependencyObject`、`ObservableCollection` 与绑定属性只能在 Dispatcher 规则允许的线程更新；事件回调先检查 Dispatcher，再读取 UI 控件。
- 不使用假进度、假成功、掩盖异常的 `Task.Delay` 或吞异常逻辑。
- 行为变化必须配套自动化测试；旧 SQLite 迁移必须保持幂等、数据保留与路径安全。
- UI 改动必须先阅读 `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md` 和 `docs/design/UI_CHANGE_GATE.md`，并使用 `wpf-apple-desktop-ui` Skill。不得以视觉重构替换真实命令、取消、错误或虚拟化。

## 提交与记录

- 每个提交只包含已验证的相邻变更；提交说明使用中文。
- `DEVELOPMENT_PROGRESS.md`、`PROJECT_MEMORY.md` 与 `KNOWN_ISSUES.md` 必须区分“源码已实现”“自动化已验证”和“Windows/Playnite 真机已验证”。
- 不把真实设备、Rclone、Ludusavi、用户存档或 DPI 条件未实际执行的结果写成通过。

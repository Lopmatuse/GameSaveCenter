# 无人值守开发积压清单

本清单是无人值守开发代理选择单一工作项的唯一来源。任务状态只能使用：`PROPOSED`、`READY`、`IN_PROGRESS`、`IMPLEMENTED`、`BLOCKED_ENVIRONMENT`、`BLOCKED_USER_DECISION`。

## PROPOSED

### GOV-001：补齐无人值守开发治理文档

- **优先级**：P0
- **状态**：PROPOSED
- **发现日期**：2026-07-31
- **根因与影响**：仓库缺少 `docs/AUTONOMOUS_DEVELOPMENT_RULES.md`、`docs/QUALITY_GATES.md`，且此前没有此积压清单。虽然现有 `AGENTS.md`、设计门禁和 Windows 测试计划包含部分约束，但代理无法从单一来源确定 READY 工作项、状态流转与最低验收证据，容易导致无人值守运行越过范围或重复领取工作。
- **建议范围**：建立两份缺失的治理文档；将任务状态、领取规则、最小验证集、环境阻塞记录格式和提交要求集中化；引用既有 `AGENTS.md`、`WINDOWS_TEST_PLAN.md` 与 UI 门禁，避免复制并漂移安全约束；把当前仍需真实 Windows/Playnite 验证的事项按优先级拆分为后续候选任务。
- **非目标**：不修改插件业务行为、WPF/XAML、版本、安装脚本或发布流程；不把需要真实设备、Rclone 或 Playnite 证据的事项标记为已验证。
- **验收标准**：三份治理文档均存在且互相链接；每个可领取条目有唯一 ID、优先级、状态、范围、验收证据和阻塞条件；至少一个经过审查的非破坏性工作项可标记为 `READY`。

## 本次审计证据

- 2026-07-31：仓库根目录为 Git 工作树，启动时 `main` 无未提交修改；`rg --files` 未找到 GOV-001 所列三份治理文档。
- 2026-07-31：`scripts/build.ps1 -Configuration Release` 通过：Release 构建 0 警告/0 错误，Core 13、Worker 20、Playnite 设置迁移 11 项测试均通过。
- 2026-07-31：`scripts/package.ps1 -Configuration Release -SkipBuild` 成功生成 `0.6.21` ZIP 与 PEXT；`scripts/verify.ps1` 确认打包 Worker 文件版本为 `0.6.21.0`。该 smoke 检查未启动、停止或替换已安装的 Playnite/Worker。
- 2026-07-31：`python scripts/validate-source.py` 未能运行，退出码 9009；PATH 中的 `python.exe` 是 Microsoft Store 占位符，系统未安装 Python 解释器。此项必须在配置 Python 3 后重跑，不能以 XAML 结构检查替代。

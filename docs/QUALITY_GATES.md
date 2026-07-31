# 质量门禁

更新时间：2026-08-01

本门禁适用于 `docs/AUTONOMOUS_BACKLOG.md` 中每个 `IN_PROGRESS` 工作项；任务领取、状态转换与验收记录以该清单为准。它补充但不替代 `AGENTS.md`、`docs/AUTONOMOUS_DEVELOPMENT_RULES.md`、`docs/design/UI_CHANGE_GATE.md` 与 `docs/WINDOWS_TEST_PLAN.md`。

## 最低自动化门禁

每次代码或 XAML 修改至少执行并记录实际结果：

1. `python scripts/validate-source.py`
2. `git diff --check`
3. `git fsck --full`
4. `dotnet restore GameSaveCenter.sln`
5. `dotnet build GameSaveCenter.sln -c Release`
6. 项目定义的全部测试命令。

Windows 环境可用时，还须执行非破坏性的打包完整性检查和适用的隔离 smoke test。构建、测试或安装失败不得被忽略，也不得以旧日志替代本次结果。

## 审查重点

- 编译错误、XAML 资源、绑定方向、`async void`、Dispatcher 与资源释放；
- SQLite 旧库迁移、数据丢失、路径穿越、误杀进程、N+1 查询与大库虚拟化；
- DPI、窄窗口、主题、高对比度、关闭透明和减少动画；
- 文档、清单、程序集与安装包版本一致性；
- 真实备份、恢复、云端、媒体操作只可在隔离测试目录中验证，禁止触及真实存档或远端镜像删除。

## UI 专项门禁

- 共享令牌、控件模板和动态主题资源必须在局部资源作用域内解析，不能污染 Playnite 宿主。
- 动画只修改渲染属性；大型列表、表格和文本禁止 `BlurEffect`，并保持虚拟化。
- 所有可交互控件必须具备 Normal、Hover、Pressed、Disabled 与键盘焦点状态；长内容必须有省略和 Tooltip。
- 所有结论均需明确区分静态检查、自动化测试和实际 Playnite 验证。

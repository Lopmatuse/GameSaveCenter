# GameSaveCenter 0.5.8 UI 实施报告

## 源码基线

- 输入：GitHub `main` 下载归档（用户上传的 `GameSaveCenter-main.zip`）。
- 技术栈：Playnite 10 GenericPlugin、.NET Framework 4.6.2、WPF/XAML、MVVM。
- 设计门禁：`APPLE_WPF_IMPLEMENTATION_PROMPT.md` 与 `UI_CHANGE_GATE.md`。

## 本轮真实代码修改

1. 重新实现方向独立的 ScrollBar/Thumb 模板，修复纵向 Thumb 的最小宽度冲突。
2. 重构 Dashboard DataGrid 的表头、单元格、行、状态列、进度列和长文本列。
3. 顶部增加 Playnite WindowChrome 安全列，普通和紧凑布局动态调整。
4. 局部 TabItem 改为 Pill，保留键盘焦点与主题资源。
5. 紧凑侧栏 Logo 使用固定矢量安全盒，修复裁切。
6. 新增插件内确认框、结果详情框和 ToastHost；面板关闭时保留 Playnite 通知回退。
7. 恢复、撤销、取消任务、解绑修改器、忽略媒体接入统一确认服务。
8. 替换 `icon.png`，并保留 SVG 与尺寸预览源。

## 自动校验

- `git diff --check`：通过。
- `python3 scripts/validate-source.py`：通过。
- XML/XAML 结构和项目版本一致性门禁：通过。

## 当前环境限制

当前 Linux 容器没有 .NET SDK/WindowsDesktop MSBuild，也没有 Playnite，因此不能诚实声称 WPF Release 编译与真机加载已经完成。必须在 Windows 机器按 `WINDOWS_TEST_PLAN.md` 执行最终回归。

## Windows Git 状态修复

- 保留用户上传仓库原有 `.git`、`main` 分支、`origin` 和完整提交历史。
- 将 `scripts/validate-source.py` 从 Git 模式 `100755` 规范为 `100644`，修复 Windows 下只有模式变化、文本无差异的假修改。
- `.gitattributes` 明确固定源码 LF，并让 Windows `.cmd` 保持字节不变。
- 新增 `GameSaveCenter-Repair-Git-State.cmd` 与 `docs/WINDOWS_GIT_STATE.md`。

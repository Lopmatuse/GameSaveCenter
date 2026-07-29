# Windows Git 状态与换行说明

## `validate-source.py` 无内容差异但显示已修改的原因

旧仓库把 `scripts/validate-source.py` 记录为 Unix 可执行文件模式 `100755`。Windows/NTFS 不稳定保留该可执行位；当 Git 的 `core.filemode` 配置不合适时，会产生只有文件模式变化、没有文本差异的 `modified` 状态。

这不是 `validate-source.py` 在执行时改写了自身。当前版本已经：

- 将 `scripts/validate-source.py` 正规化为普通文件模式 `100644`；
- 在 `.gitattributes` 中显式固定 Python、XAML、C#、Markdown 等文本为 LF；
- 保留 `.cmd` 为二进制字节，以免 Git 反复改写 Windows 启动脚本；
- 忽略 `__pycache__` 和 `.pyc`。

## 推荐的仓库本地配置

在项目根目录双击：

```text
GameSaveCenter-Repair-Git-State.cmd
```

它只修改当前仓库的 Git 配置，不影响其他项目：

```bash
git config --local core.filemode false
git config --local core.autocrlf false
git update-index --refresh
```

## 验证

```bash
python scripts/validate-source.py
git status --short
```

验证脚本执行后不应使 `scripts/validate-source.py` 变为已修改。

不要对整个项目随意执行编辑器的“转换全部文件为 CRLF”。源码格式由 `.gitattributes` 管理，Windows 启动脚本本身继续保留 CRLF。

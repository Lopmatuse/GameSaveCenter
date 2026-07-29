# GameSaveCenter 0.5.10 页签与布局热修复

## 根因

0.5.9 为了让页签头文字居中，在 `GscTabItem` 上设置了 `HorizontalContentAlignment=Center` 与 `VerticalContentAlignment=Center`。这两个对齐属性会传播到选中页签的内容树，导致完整页面 Grid 按自身期望尺寸居中，而不是占满 TabControl 的内容区域。维护、媒体来源、修改器等页面因此出现大面积空白和内容漂浮。

页签头部还使用了 `HorizontalScrollBarVisibility=Auto`。在宿主主题及统一 ScrollBar 样式共同作用下，Header ScrollViewer 的横向轨道会显示成页签下方的贯穿线，并可能裁切页签下半部分，使四角圆角看起来只剩上半部。

## 修复

- `GscTabItem` 内容对齐改为 Stretch；页签 Header 的 `ContentPresenter` 单独居中。
- `GscTabControl` 的 SelectedContentHost 强制 Stretch。
- Header ScrollViewer 隐藏滚动条轨道，并由外层透明容器提供 10 DIP 下方安全留白。
- 页签间距只保留右侧间距，避免底部 Margin 被 Header viewport 裁切。
- 保留 0.5.9 的 DataGrid CheckBox、搜索清除与统一表格样式。

当前 Linux 环境无法渲染 WPF 或运行 Playnite；已执行 XML/XAML 解析及 `scripts/validate-source.py`，Windows 真机需按测试计划回归。

# GameSaveCenter 0.5.11 页签右圆角修复

## 根因

0.5.10 已经修复页签底部裁切与内容居中，但页签之间仍通过 `TabItem.Margin=0,0,8,0` 提供外部间距。WPF `TabPanel` 在部分宿主主题、DPI 和布局取整组合下，会按子项安排槽裁切模板右边缘，使 `Border` 的右上、右下圆角被截成垂直直线；左侧圆角因为远离槽的末端而正常。

## 修复

- 移除 `TabItem` 外部 Margin。
- 在 `ControlTemplate` 根 Grid 内增加 8 DIP 透明间距列。
- `TabChrome` 完全位于 TabItem 的安排槽内部，并保留 1 DIP 内部安全边距。
- 启用 `UseLayoutRounding`，同时关闭 Chrome 的像素吸附，防止 1 DIP 描边在高 DPI 下吞掉右圆角。
- 不修改 0.5.10 已确认正常的内容 Stretch 和 CheckBox 样式。

当前环境只能执行源码/XAML 静态验证，最终像素效果需在 Windows Playnite 中按测试计划回归。

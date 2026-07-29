# GameSaveCenter 0.5.9 WPF 控件修复说明

## 本轮根因

- 旧滚动条使用超大 `CornerRadius`，WPF 会按实际短边归一化圆角；8 DIP 宽的纵向 Thumb 因此呈尖角/透镜形。部分页面仍可能继承宿主按钮模板。
- Dashboard 只改了 `TabItem`，没有完全接管 `TabControl`，Playnite 默认标签线与 Chrome 继续叠加，导致局部页签看起来只有一个圆角。
- DataGrid 外框虽有圆角，但列头背景属于独立视觉树，首末列头没有对应上圆角；锁定列仍使用原生 CheckBox。
- 字体图标和普通文字依赖各自字体基线，未使用固定图标列和显式垂直居中。
- 搜索占位文本只判断空字符串，没有判断键盘焦点，也没有统一清除入口。

## 实现

- `Themes/DesignTokens.xaml`
  - 共享 ScrollBar/Thumb 模板改为固定 8 DIP Thumb 厚度、12 DIP 轨道和 4 DIP 有限圆角。
  - 新增 `GscDataGridCheckBox`、`GscTextBox`、`GscSearchClearButton`。
  - ComboBox、ComboBoxItem、ProgressBar 和 RepeatButton 模板显式脱离宿主默认样式。
- `Views/DashboardView.xaml`
  - `GscButtonBase` 和主按钮显式接管模板、内容双向居中。
  - 新增完整 `GscTabControl` 模板，`GscTabItem` 改为四角一致的 Pill。
  - DataGrid 首末列 Header 使用对应上圆角，分隔线减弱，锁定列使用共享主题 CheckBox。
  - 游戏与 FLiNG 搜索框使用焦点感知 Watermark 和清除按钮。
- `Views/DashboardView.xaml.cs`
  - 新增统一清除搜索处理，清空后保留键盘焦点。
- `Settings/GameSaveCenterSettingsView.xaml`
  - 复用共享 TextBox/ComboBox/CheckBox/ScrollBar 资源，防止同类控件回退。

## 未在当前环境完成的验证

当前环境没有 WindowsDesktop MSBuild、Playnite 和真实 WPF 渲染器，因此没有声称真机编译或视觉回归通过。必须按 `docs/WINDOWS_TEST_PLAN.md` 在 Windows 上完成 0.5.9 回归。

# GameSaveCenter 0.6.17 滚动滑块最小尺寸修复

## 根因

WPF `Track` 在 `ViewportSize` 有效时按内容比例计算 Thumb 长度。比例 Thumb 的最小值并不由普通 `Thumb.MinHeight` / `MinWidth` 单独决定，而由系统资源决定：

- 纵向最小长度：`VerticalScrollBarButtonHeightKey / 2`
- 横向最小长度：`HorizontalScrollBarButtonWidthKey / 2`

内容极多时，旧模板虽然声明了 `MinHeight=36`，Track 仍可能按默认系统最小值把 Thumb 压缩成接近尖点。

## 修复

在 `PART_Track.Resources` 内局部覆盖：

```xml
<sys:Double x:Key="{x:Static SystemParameters.VerticalScrollBarButtonHeightKey}">72</sys:Double>
<sys:Double x:Key="{x:Static SystemParameters.HorizontalScrollBarButtonWidthKey}">72</sys:Double>
```

因此 WPF Track 使用 36 DIP 作为最小 Thumb 长度。修复只作用于 GameSaveCenter 的滚动条模板，不修改系统或 Playnite 的全局设置。

## 回归点

- 数千个游戏的游戏列表。
- 数千条任务、异常和日志记录。
- 媒体中心大量待归类文件。
- 存档历史大量版本。
- ComboBox 大型下拉列表。
- 横向超宽表格。
- 100%、125%、150%、200% DPI。

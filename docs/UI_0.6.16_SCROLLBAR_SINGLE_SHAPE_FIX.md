# GameSaveCenter 0.6.16 滚动滑块单形状修复

0.6.15 v2 使用半透明矩形与两个圆形端帽叠加。重叠区域会重复进行 Alpha 混合，因此在深色主题中出现更亮的白灰色端帽；部分宿主裁切下仍可能产生一端平直。

0.6.16 改为一个位于 Thumb 内部安全边距中的圆角 `Rectangle`：

- 纵向：内部宽度 8 DIP，`RadiusX=4`、`RadiusY=4`。
- 横向：内部高度 8 DIP，`RadiusX=4`、`RadiusY=4`。
- 不再叠加任何 `Ellipse`，因此没有端帽重复着色。
- Track 两端保留 4 DIP 安全空间，Thumb 内部再保留 1～2 DIP。
- `SnapsToDevicePixels` 与 `UseLayoutRounding` 启用，降低高 DPI 半像素差异。

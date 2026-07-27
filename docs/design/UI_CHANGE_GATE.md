# UI 变更强制门禁

更新时间：2026-07-27

所有新增或修改 GameSaveCenter UI 的提交，必须先阅读：

- `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md`
- `docs/design/APPLE_UI_GUIDE.md`
- `docs/KNOWN_ISSUES.md` 中与主题、DPI、动画和安装有关的条目

## 不可违反的原则

1. 真实 WPF/XAML 实现，不引入 HTML、WebView、Electron、Avalonia、WinUI 3 或 MAUI 外壳。
2. 不改插件 ID，不重写备份、恢复、Ludusavi、媒体、云端和任务编排业务层。
3. Playnite 内嵌页面不伪造 macOS 红黄绿窗口按钮，不修改宿主窗口 Chrome。
4. Apple-inspired 指的是层级、留白、材质、圆角、克制状态色和自然动效，不是复制 Apple 资产。
5. 不分散硬编码主题色。可切换颜色必须使用共享设计令牌和 `DynamicResource`。
6. 普通内容卡片以清晰、近不透明表面为主；毛玻璃只用于侧栏、浮层、提示和少量结构层。
7. 文字、列表行和大型滚动区域禁止应用 `BlurEffect`。
8. 动画优先使用 `Opacity`、`TranslateTransform`、`ScaleTransform` 和颜色；避免动画 Width/Height/Margin。
9. Style 中禁止放置会被代码动画修改的共享 `Freezable` Transform；动画对象必须是元素独占且可变的实例。
10. 文字必须启用像素对齐与高 DPI 友好渲染；禁止通过整个 TextBlock 的 `Opacity` 制造次级文本。
11. 主题至少支持“跟随 Playnite、浅色、深色”，并在高对比度、关闭透明和关闭动画时安全降级。
12. 状态不能只依赖颜色，必须同时有文本、图标或状态点。
13. 所有 Loading、Succeeded、Failed、Cancelled 状态必须来自真实任务，不用 `Task.Delay` 模拟业务成功。
14. 新增按钮必须有正常、Hover、Pressed、Disabled、Keyboard Focus 状态。
15. 大型游戏库列表必须保持虚拟化，不能让动画和容器模板关闭虚拟化。
16. 修改 XAML 后必须运行 `scripts/validate-source.py`；Windows 上还必须通过 `GameSaveCenter-Run.cmd` 的真实构建、安装和版本核验。

## UI 提交检查表

- [ ] 颜色、圆角、间距和阴影是否来自共享令牌或局部可解释资源？
- [ ] 跟随 Playnite、强制浅色、强制深色下是否均可读？
- [ ] 100%、125%、150%、175%、200% DPI 是否无裁剪、发虚和错位？
- [ ] 透明效果关闭、高对比度开启时是否仍完整可用？
- [ ] 空状态、错误状态、禁用原因和下一步操作是否清楚？
- [ ] 长名称是否使用省略和 Tooltip？
- [ ] 任务和日志是否按需加载，不在 UI 线程读取大文件？
- [ ] 动画是否只改变渲染属性，且关闭页面后停止计时器和订阅？
- [ ] 是否保留真实命令、错误传播、取消和业务状态？
- [ ] `extension.yaml`、程序集版本、安装包名和已安装 DLL 是否一致？

违反本门禁的 UI 改动不应进入 `main`。

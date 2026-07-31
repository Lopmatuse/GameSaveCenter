# GameSaveCenter agent rules

## WPF / XAML UI changes

For any WPF, XAML, Playnite UI, theming, layout, controls, animation, dialog, toast,
DataGrid, ScrollBar, TabControl, responsive sizing, DPI, accessibility, or visual-regression work:

1. Use the `wpf-apple-desktop-ui` skill and inspect the shared resources before editing.
2. Read `docs/design/APPLE_WPF_IMPLEMENTATION_PROMPT.md` and `docs/design/UI_CHANGE_GATE.md`.
3. Repair shared styles/templates rather than a single visual instance.
4. Preserve commands, bindings, business behavior, virtualization, keyboard access, UI Automation, and Playnite compatibility.
5. Validate all affected controls in every theme and never claim Playnite rendering was verified unless it was actually run.

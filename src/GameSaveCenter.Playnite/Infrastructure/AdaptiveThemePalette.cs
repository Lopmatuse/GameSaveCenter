using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GameSaveCenter.Playnite.Settings;

namespace GameSaveCenter.Playnite.Infrastructure
{
    /// <summary>
    /// Derives a readable local palette from the active Playnite theme. Playnite requires
    /// TextBrush/TextBrushDark, but community themes can expose very different background brushes,
    /// so GameSaveCenter validates contrast instead of assuming a simple light/dark pair.
    /// </summary>
    internal sealed class AdaptiveThemePalette
    {
        public bool IsDark { get; set; }
        public Color Background { get; set; }
        public Color PrimaryText { get; set; }
        public Color SecondaryText { get; set; }
        public Color MutedText { get; set; }
        public Color DisabledText { get; set; }
        public Color ControlFill { get; set; }
        public Color ControlStroke { get; set; }
        public Color Divider { get; set; }
        public Color SurfaceTop { get; set; }
        public Color SurfaceBottom { get; set; }
        public Color StrongSurfaceTop { get; set; }
        public Color StrongSurfaceBottom { get; set; }
        public Color SidebarTop { get; set; }
        public Color SidebarBottom { get; set; }
        public Color Backdrop { get; set; }
        public Color Highlight { get; set; }
        public Color Accent { get; set; }
        public Color AccentHover { get; set; }
        public Color AccentPressed { get; set; }
        public Color AccentTint { get; set; }
        public Color AccentTintStrong { get; set; }
        public Color AccentIconFill { get; set; }
        public Color OnAccentText { get; set; }
        public Color Info { get; set; }
        public Color Success { get; set; }
        public Color Warning { get; set; }
        public Color Error { get; set; }
        public Color InfoIconFill { get; set; }
        public Color SuccessIconFill { get; set; }
        public Color WarningIconFill { get; set; }
        public Color ErrorIconFill { get; set; }
    }

    internal static class AdaptiveThemePaletteFactory
    {
        private static readonly string[] BackgroundResourceKeys =
        {
            "WindowBackgroundBrush",
            "MainWindowBackgroundBrush",
            "ControlBackgroundBrush",
            "BackgroundBrush"
        };

        private static readonly string[] AccentResourceKeys =
        {
            "HighlightGlyphBrush",
            "AccentBrush",
            "HoverBrush"
        };

        public static AdaptiveThemePalette Create(FrameworkElement host, bool glassEnabled, int strengthPercent, GameSaveCenterThemeMode themeMode = GameSaveCenterThemeMode.FollowPlaynite)
        {
            var forcedLight = themeMode == GameSaveCenterThemeMode.Light;
            var forcedDark = themeMode == GameSaveCenterThemeMode.Dark;
            var highContrast = SystemParameters.HighContrast;
            var rawBackground = highContrast
                ? SystemColors.WindowColor
                : forcedLight
                    ? Color.FromRgb(243, 244, 248)
                    : forcedDark
                        ? Color.FromRgb(23, 24, 31)
                        : ResolveHostBackground(host)
                            ?? ResolveFirstResourceColor(host, BackgroundResourceKeys)
                            ?? Color.FromRgb(18, 20, 30);

            var text = highContrast ? SystemColors.WindowTextColor : forcedLight ? Colors.Black : forcedDark ? Colors.White : ResolveResourceColor(host, "TextBrush");
            var inverseText = highContrast ? SystemColors.WindowTextColor : forcedLight ? Colors.White : forcedDark ? Colors.Black : ResolveResourceColor(host, "TextBrushDark");

            // If a theme uses a transparent/image background, infer a stable local surface from the
            // required text brushes. Otherwise preserve some of the theme's own color character.
            var initialDark = RelativeLuminance(rawBackground) < 0.48;
            var fallbackBackground = initialDark
                ? Color.FromRgb(15, 18, 29)
                : Color.FromRgb(246, 247, 250);

            var primaryText = ChooseBestText(rawBackground, text, inverseText, initialDark);
            if (ContrastRatio(primaryText, rawBackground) < 4.5)
            {
                rawBackground = fallbackBackground;
                primaryText = ChooseBestText(rawBackground, text, inverseText, initialDark);
            }

            var isDark = RelativeLuminance(rawBackground) < 0.5;
            // Keep the embedded page in the demo's quiet navy family even when the host theme
            // exposes a vivid blue/black background. We still retain a small amount of the host
            // character, but do not let a Playnite accent turn every card into saturated blue.
            var stableBase = Blend(rawBackground, isDark ? Color.FromRgb(15, 23, 36) : Color.FromRgb(248, 249, 252), isDark ? 0.56 : 0.34);
            primaryText = ChooseBestText(stableBase, text, inverseText, isDark);
            if (ContrastRatio(primaryText, stableBase) < 7)
                primaryText = isDark ? Colors.White : Colors.Black;

            var strength = Math.Max(20, Math.Min(100, strengthPercent)) / 100.0;
            var controlFill = isDark
                ? Blend(stableBase, Colors.White, 0.075)
                : Blend(stableBase, Colors.Black, 0.035);
            var strongControl = isDark
                ? Blend(stableBase, Colors.White, 0.105)
                : Blend(stableBase, Colors.Black, 0.02);
            // Keep the standalone demo and the embedded Playnite view on the same blue
            // interaction language. FollowPlaynite still honors a host accent when one is
            // exposed; these values are the deterministic Light/Dark fallbacks used by the
            // demo and by themes that do not publish an accent resource.
            var fallbackAccent = isDark ? Color.FromRgb(120, 151, 255) : Color.FromRgb(91, 116, 224);
            var hostAccent = !forcedLight && !forcedDark && !highContrast
                ? ResolveFirstResourceColor(host, AccentResourceKeys)
                : null;
            // FollowPlaynite keeps an explicitly published host accent intact. The local
            // fallback is the quieter demo violet-blue used when the host has no accent.
            var requestedAccent = hostAccent ?? fallbackAccent;
            var accent = EnsureContrast(highContrast ? SystemColors.HighlightColor : requestedAccent, stableBase, isDark);
            var accentHover = Blend(accent, isDark ? Colors.White : Colors.Black, 0.1);
            var accentPressed = Blend(accent, Colors.Black, isDark ? 0.16 : 0.2);
            // Keep the host accent for actions and focus rings, but soften large selected
            // surfaces. Playnite themes often publish a saturated blue/purple accent; using
            // that color at 24–34% opacity on every card makes the page look like a blue admin
            // dashboard instead of the demo's restrained navy surface hierarchy.
            var tintAccent = hostAccent.HasValue
                ? Blend(accent, fallbackAccent, 0.42)
                : accent;
            var onAccentText = highContrast
                ? SystemColors.HighlightTextColor
                : ChooseBestText(accent, Colors.White, Colors.Black, RelativeLuminance(accent) < 0.5);
            var info = highContrast
                ? EnsureContrast(SystemColors.HighlightColor, stableBase, isDark)
                : Color.FromRgb(120, 169, 255);
            var success = highContrast
                ? EnsureContrast(SystemColors.HotTrackColor, stableBase, isDark)
                : Color.FromRgb(75, 211, 155);
            var warning = highContrast ? primaryText : Color.FromRgb(245, 178, 77);
            var error = highContrast ? primaryText : Color.FromRgb(255, 107, 128);

            var surfaceTop = glassEnabled
                ? WithAlpha(strongControl, 0.86 * strength)
                : WithAlpha(strongControl, 1);
            var surfaceBottom = glassEnabled
                ? WithAlpha(controlFill, 0.78 * strength)
                : WithAlpha(controlFill, 1);
            var strongTop = glassEnabled
                ? WithAlpha(Blend(strongControl, primaryText, isDark ? 0.018 : 0.006), 0.94 * strength)
                : WithAlpha(strongControl, 1);
            var strongBottom = glassEnabled
                ? WithAlpha(controlFill, 0.88 * strength)
                : WithAlpha(controlFill, 1);

            return new AdaptiveThemePalette
            {
                IsDark = isDark,
                Background = stableBase,
                PrimaryText = primaryText,
                SecondaryText = WithAlpha(primaryText, 0.74),
                MutedText = WithAlpha(primaryText, 0.56),
                DisabledText = WithAlpha(primaryText, 0.38),
                ControlFill = WithAlpha(controlFill, glassEnabled ? Math.Max(0.76, 0.9 * strength) : 1),
                ControlStroke = WithAlpha(primaryText, isDark ? 0.15 : 0.13),
                Divider = WithAlpha(primaryText, isDark ? 0.13 : 0.11),
                SurfaceTop = surfaceTop,
                SurfaceBottom = surfaceBottom,
                StrongSurfaceTop = strongTop,
                StrongSurfaceBottom = strongBottom,
                SidebarTop = glassEnabled ? WithAlpha(strongControl, 0.9 * strength) : WithAlpha(strongControl, 1),
                SidebarBottom = glassEnabled ? WithAlpha(stableBase, 0.82 * strength) : WithAlpha(stableBase, 1),
                Backdrop = WithAlpha(stableBase, glassEnabled ? 0.26 : 1),
                Highlight = WithAlpha(primaryText, isDark ? 0.075 : 0.24),
                Accent = accent,
                AccentHover = accentHover,
                AccentPressed = accentPressed,
                AccentTint = highContrast ? accent : WithAlpha(tintAccent, isDark ? 0.20 : 0.12),
                AccentTintStrong = highContrast ? accent : WithAlpha(tintAccent, isDark ? 0.28 : 0.18),
                AccentIconFill = highContrast ? accent : WithAlpha(tintAccent, isDark ? 0.18 : 0.12),
                OnAccentText = onAccentText,
                Info = info,
                Success = success,
                Warning = warning,
                Error = error,
                InfoIconFill = highContrast ? info : WithAlpha(info, 0.13),
                SuccessIconFill = highContrast ? success : WithAlpha(success, 0.13),
                WarningIconFill = highContrast ? warning : WithAlpha(warning, 0.13),
                ErrorIconFill = highContrast ? error : WithAlpha(error, 0.13)
            };
        }

        public static void ApplyAccentResources(ResourceDictionary resources, AdaptiveThemePalette palette)
        {
            resources["GscAccentBrush"] = Brush(palette.Accent);
            resources["GscAccentHoverBrush"] = Brush(palette.AccentHover);
            resources["GscAccentPressedBrush"] = Brush(palette.AccentPressed);
            resources["GscAccentTintBrush"] = Brush(palette.AccentTint);
            resources["GscAccentTintStrongBrush"] = Brush(palette.AccentTintStrong);
            resources["GscAccentIconFillBrush"] = Brush(palette.AccentIconFill);
            resources["GscOnAccentTextBrush"] = Brush(palette.OnAccentText);
            resources["GscSelectionTextBrush"] = Brush(SystemParameters.HighContrast ? SystemColors.HighlightTextColor : palette.PrimaryText);
            resources["GscPrimaryButtonBrush"] = Gradient(palette.Accent, palette.AccentPressed);
            resources["GscPrimaryButtonBorderBrush"] = Brush(palette.AccentHover);
            resources["GscAmbientAccentBrush"] = Brush(WithAlpha(palette.Accent, palette.IsDark ? 0.18 : 0.15));
            resources["GscAccentShadowColor"] = WithAlpha(palette.Accent, palette.IsDark ? 0.34 : 0.28);
            resources["GscInfoBrush"] = Brush(palette.Info);
            resources["GscSuccessBrush"] = Brush(palette.Success);
            resources["GscWarningBrush"] = Brush(palette.Warning);
            resources["GscErrorBrush"] = Brush(palette.Error);
            // These semantic surfaces are used by both the extracted workspaces and the
            // settings page. Keep them in the same palette as their status strokes instead
            // of leaving the static DesignTokens fallback active after a theme switch.
            resources["GscErrorTintBrush"] = Brush(SemanticTint(palette.Error, palette.IsDark ? 0.20 : 0.12));
            resources["GscRestoreInfoFillBrush"] = Brush(SemanticTint(palette.Info, palette.IsDark ? 0.20 : 0.11));
            resources["GscRestoreInfoStrokeBrush"] = Brush(SemanticTint(palette.Info, palette.IsDark ? 0.46 : 0.32));
            resources["GscSafetyFillBrush"] = Brush(SemanticTint(palette.Warning, palette.IsDark ? 0.20 : 0.12));
            resources["GscSafetyStrokeBrush"] = Brush(SemanticTint(palette.Warning, palette.IsDark ? 0.48 : 0.34));
            resources["GscAmbientInfoBrush"] = Brush(SemanticTint(palette.Info, palette.IsDark ? 0.13 : 0.09));
            resources["GscAmbientSuccessBrush"] = Brush(SemanticTint(palette.Success, palette.IsDark ? 0.12 : 0.08));
            resources["GscMutedStatusBrush"] = Brush(SemanticTint(palette.PrimaryText, palette.IsDark ? 0.54 : 0.46));
            resources["GscInfoIconFillBrush"] = Brush(palette.InfoIconFill);
            resources["GscSuccessIconFillBrush"] = Brush(palette.SuccessIconFill);
            resources["GscWarningIconFillBrush"] = Brush(palette.WarningIconFill);
            resources["GscErrorIconFillBrush"] = Brush(palette.ErrorIconFill);
        }

        /// <summary>
        /// Keeps elevation effects local to an embedded plugin page and removes them entirely
        /// when transparent material is unavailable. An Effect with Opacity=0 still retains an
        /// effect visual, so accessibility and low-cost fallback paths must use a real null.
        /// </summary>
        public static void ApplyMaterialResources(ResourceDictionary resources, AdaptiveThemePalette palette, bool glassEnabled, bool motionEnabled)
        {
            resources["GscSurfaceEffect"] = CreateShadowEffect(glassEnabled, Colors.Black, 14, 2, palette.IsDark ? 0.34 : 0.24);
            resources["GscPrimaryButtonEffect"] = CreateShadowEffect(glassEnabled, palette.Accent, 12, 2, palette.IsDark ? 0.32 : 0.28);
            resources["GscSidebarEffect"] = CreateShadowEffect(glassEnabled, Colors.Black, 24, 3, palette.IsDark ? 0.42 : 0.30);
            resources["GscPopupEffect"] = CreateShadowEffect(glassEnabled, Colors.Black, 20, 5, palette.IsDark ? 0.46 : 0.38);
            resources["GscDialogEffect"] = CreateShadowEffect(glassEnabled, Colors.Black, 34, 8, palette.IsDark ? 0.52 : 0.44);
            resources["GscSliderThumbEffect"] = CreateShadowEffect(glassEnabled, Colors.Black, 6, 1, 0.26);
            resources["GscPopupAllowsTransparency"] = glassEnabled;
            resources["GscPopupAnimation"] = motionEnabled ? PopupAnimation.Fade : PopupAnimation.None;
        }

        /// <summary>
        /// WPF-UI resolves these Fluent token names through dynamic resources. Keep the overrides
        /// local to an embedded GameSaveCenter view so a Playnite theme (or another extension)
        /// is never mutated, while WPF-UI controls still share the same palette as native controls.
        /// </summary>
        public static void ApplyWpfUiResources(ResourceDictionary resources, AdaptiveThemePalette palette)
        {
            var secondaryFill = palette.IsDark
                ? Blend(palette.ControlFill, Colors.White, 0.045)
                : Blend(palette.ControlFill, Colors.Black, 0.025);
            var tertiaryFill = palette.IsDark
                ? Blend(palette.ControlFill, Colors.White, 0.085)
                : Blend(palette.ControlFill, Colors.Black, 0.055);

            resources["AccentFillColorDefaultBrush"] = Brush(palette.Accent);
            resources["AccentFillColorSecondaryBrush"] = Brush(palette.AccentHover);
            resources["AccentFillColorTertiaryBrush"] = Brush(palette.AccentPressed);
            resources["AccentFillColorDisabledBrush"] = Brush(WithAlpha(palette.Accent, 0.38));
            resources["TextOnAccentFillColorPrimaryBrush"] = Brush(palette.OnAccentText);
            resources["TextOnAccentFillColorSelectedTextBrush"] = Brush(palette.OnAccentText);
            resources["TextFillColorPrimaryBrush"] = Brush(palette.PrimaryText);
            resources["TextFillColorSecondaryBrush"] = Brush(palette.SecondaryText);
            resources["TextFillColorTertiaryBrush"] = Brush(palette.MutedText);
            resources["TextFillColorDisabledBrush"] = Brush(palette.DisabledText);
            resources["ControlFillColorDefaultBrush"] = Brush(palette.ControlFill);
            resources["ControlFillColorSecondaryBrush"] = Brush(secondaryFill);
            resources["ControlFillColorTertiaryBrush"] = Brush(tertiaryFill);
            resources["ControlFillColorInputActiveBrush"] = Brush(tertiaryFill);
            resources["ControlFillColorDisabledBrush"] = Brush(WithAlpha(palette.ControlFill, 0.5));
            resources["ControlSolidFillColorDefaultBrush"] = Brush(palette.Accent);
            resources["ControlStrokeColorDefaultBrush"] = Brush(palette.ControlStroke);
            resources["ControlStrokeColorSecondaryBrush"] = Brush(palette.Divider);
            resources["CardBackgroundFillColorDefaultBrush"] = Brush(palette.StrongSurfaceTop);
            resources["CardStrokeColorDefaultBrush"] = Brush(palette.ControlStroke);
            resources["FocusStrokeColorOuterBrush"] = Brush(palette.Accent);
            resources["FocusStrokeColorInnerBrush"] = Brush(palette.OnAccentText);
        }

        /// <summary>
        /// Applies every runtime theme resource used by the Dashboard shell and its extracted
        /// workspace views.  Workspace UserControls own local dictionaries for their styles, so
        /// copying only the shell resources would leave their DynamicResource lookups on the
        /// static DesignTokens fallback after a theme switch.
        /// </summary>
        public static void ApplyRuntimeThemeResources(ResourceDictionary resources, AdaptiveThemePalette palette, bool glassEnabled, bool motionEnabled)
        {
            ApplyAccentResources(resources, palette);
            ApplyMaterialResources(resources, palette, glassEnabled, motionEnabled);
            ApplyWpfUiResources(resources, palette);
            resources["GscPrimaryTextBrush"] = Brush(palette.PrimaryText);
            resources["GscSecondaryTextBrush"] = Brush(palette.SecondaryText);
            resources["GscMutedTextBrush"] = Brush(palette.MutedText);
            resources["GscDisabledTextBrush"] = Brush(palette.DisabledText);
            resources["GscControlFillBrush"] = Brush(palette.ControlFill);
            resources["GscControlStrokeBrush"] = Brush(palette.ControlStroke);
            resources["GscDividerBrush"] = Brush(palette.Divider);
            resources["GscTableDividerBrush"] = Brush(Color.FromArgb(
                palette.IsDark ? (byte)24 : (byte)18, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            resources["GscPopupBrush"] = Brush(Color.FromArgb(
                250, palette.StrongSurfaceTop.R, palette.StrongSurfaceTop.G, palette.StrongSurfaceTop.B));
            resources["GscGlassFillBrush"] = Gradient(palette.SurfaceTop, palette.SurfaceBottom);
            resources["GscGlassStrongBrush"] = Gradient(palette.StrongSurfaceTop, palette.StrongSurfaceBottom);
            resources["GscSidebarBrush"] = Gradient(palette.SidebarTop, palette.SidebarBottom);
            resources["GscGlassStrokeBrush"] = Brush(palette.ControlStroke);
            resources["GscGlassHighlightBrush"] = Brush(palette.Highlight);
            resources["GscBackdropBrush"] = Brush(palette.Backdrop);
            // Table headers are a real surface, not a translucent white wash. An opaque
            // header keeps the last column from falling back to a host white brush and gives
            // every workspace the same readable demo hierarchy.
            resources["GscTableHeaderBrush"] = Brush(palette.IsDark
                ? Color.FromRgb(32, 42, 57)
                : Color.FromRgb(236, 240, 248));
            resources["GscTableAlternateRowBrush"] = Brush(Color.FromArgb(
                SystemParameters.HighContrast ? (byte)0 : palette.IsDark ? (byte)14 : (byte)7,
                palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            resources["GscRowHoverBrush"] = Brush(Color.FromArgb(
                palette.IsDark ? (byte)18 : (byte)10, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            resources["GscRowHoverStrongBrush"] = Brush(Color.FromArgb(
                SystemParameters.HighContrast ? (byte)0 : palette.IsDark ? (byte)32 : (byte)18,
                palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            resources["GscScrollTrackBrush"] = Brush(Color.FromArgb(
                palette.IsDark ? (byte)28 : (byte)20, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            resources["GscScrollThumbBrush"] = Brush(Color.FromArgb(
                palette.IsDark ? (byte)88 : (byte)68, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            // Scrollbar hover must follow the active Playnite accent (blue, purple, or a
            // custom high-contrast highlight) instead of retaining the old purple fallback.
            resources["GscScrollThumbHoverBrush"] = Brush(WithAlpha(palette.AccentHover, palette.IsDark ? 0.78 : 0.66));
            resources["GscOverlayBrush"] = Brush(Color.FromArgb(
                palette.IsDark ? (byte)138 : (byte)72, 0, 0, 0));
            resources["GscPickerScrimBrush"] = Brush(Color.FromArgb(
                palette.IsDark ? (byte)54 : (byte)34, 0, 0, 0));
            WpfUiThemeScope.Apply(resources, palette.IsDark);
        }

        public static SolidColorBrush Brush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static LinearGradientBrush Gradient(Color top, Color bottom)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            brush.GradientStops.Add(new GradientStop(top, 0));
            brush.GradientStops.Add(new GradientStop(bottom, 1));
            brush.Freeze();
            return brush;
        }

        private static DropShadowEffect? CreateShadowEffect(bool enabled, Color color, double blurRadius, double shadowDepth, double opacity)
        {
            if (!enabled) return null;
            var effect = new DropShadowEffect
            {
                Color = color,
                BlurRadius = blurRadius,
                ShadowDepth = shadowDepth,
                Opacity = opacity
            };
            effect.Freeze();
            return effect;
        }

        private static Color ChooseBestText(Color background, Color? first, Color? second, bool darkBackground)
        {
            var candidates = new List<Color>();
            if (first.HasValue) candidates.Add(Opaque(first.Value));
            if (second.HasValue) candidates.Add(Opaque(second.Value));
            candidates.Add(darkBackground ? Colors.White : Colors.Black);
            var best = candidates[0];
            var bestRatio = ContrastRatio(best, background);
            for (var i = 1; i < candidates.Count; i++)
            {
                var ratio = ContrastRatio(candidates[i], background);
                if (ratio <= bestRatio) continue;
                best = candidates[i];
                bestRatio = ratio;
            }
            return best;
        }

        private static Color? ResolveHostBackground(FrameworkElement host)
        {
            DependencyObject? current = host;
            while (current != null)
            {
                var brush = current switch
                {
                    Border border => border.Background,
                    Panel panel => panel.Background,
                    Control control => control.Background,
                    _ => null
                };
                var color = ExtractUsableColor(brush);
                if (color.HasValue) return color;
                current = VisualTreeHelper.GetParent(current);
            }

            var window = Window.GetWindow(host);
            return ExtractUsableColor(window?.Background);
        }

        private static Color? ResolveFirstResourceColor(FrameworkElement host, IEnumerable<string> keys)
        {
            foreach (var key in keys)
            {
                var color = ResolveResourceColor(host, key);
                if (color.HasValue) return color;
            }
            return null;
        }

        private static Color? ResolveResourceColor(FrameworkElement host, string key)
            => ExtractUsableColor(host.TryFindResource(key) as Brush);

        private static Color? ExtractUsableColor(Brush? brush)
        {
            if (brush == null || brush.Opacity <= 0.08) return null;
            if (brush is SolidColorBrush solid && solid.Color.A > 24)
                return Opaque(solid.Color);

            if (brush is GradientBrush gradient && gradient.GradientStops.Count > 0)
            {
                double red = 0;
                double green = 0;
                double blue = 0;
                double totalWeight = 0;
                foreach (var stop in gradient.GradientStops)
                {
                    var weight = Math.Max(0.01, stop.Color.A / 255d);
                    red += stop.Color.R * weight;
                    green += stop.Color.G * weight;
                    blue += stop.Color.B * weight;
                    totalWeight += weight;
                }
                if (totalWeight > 0)
                    return Color.FromRgb((byte)(red / totalWeight), (byte)(green / totalWeight), (byte)(blue / totalWeight));
            }

            return null;
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromRgb(
                (byte)Math.Round(source.R + (target.R - source.R) * amount),
                (byte)Math.Round(source.G + (target.G - source.G) * amount),
                (byte)Math.Round(source.B + (target.B - source.B) * amount));
        }

        private static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);

        private static Color EnsureContrast(Color candidate, Color background, bool darkBackground)
        {
            candidate = Opaque(candidate);
            var target = darkBackground ? Colors.White : Colors.Black;
            for (var attempt = 0; attempt < 6 && ContrastRatio(candidate, background) < 3; attempt++)
                candidate = Blend(candidate, target, 0.18);
            return candidate;
        }

        private static Color WithAlpha(Color color, double opacity)
        {
            var alpha = (byte)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255);
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static Color SemanticTint(Color color, double opacity)
        {
            // High-contrast themes do not reliably render translucent fills. Use the solid
            // semantic color there so the associated border/status remains visible.
            return SystemParameters.HighContrast ? color : WithAlpha(color, opacity);
        }

        private static double ContrastRatio(Color first, Color second)
        {
            var firstLuminance = RelativeLuminance(first);
            var secondLuminance = RelativeLuminance(second);
            var lighter = Math.Max(firstLuminance, secondLuminance);
            var darker = Math.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double RelativeLuminance(Color color)
        {
            double Convert(byte channel)
            {
                var value = channel / 255.0;
                return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Convert(color.R) + 0.7152 * Convert(color.G) + 0.0722 * Convert(color.B);
        }
    }
}

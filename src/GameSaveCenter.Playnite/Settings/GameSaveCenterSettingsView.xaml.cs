using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameSaveCenter.Playnite.Infrastructure;

namespace GameSaveCenter.Playnite.Settings
{
    public partial class GameSaveCenterSettingsView : UserControl
    {
        private bool entrancePlayed;

        public GameSaveCenterSettingsView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private GameSaveCenterSettings? CurrentSettings => DataContext as GameSaveCenterSettings;

        private bool MotionEnabled => (CurrentSettings?.EnableUiAnimations ?? true) && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyAdaptiveTheme();
            if (entrancePlayed)
            {
                SettingsShell.Opacity = 1;
                return;
            }

            entrancePlayed = true;
            Dispatcher.BeginInvoke(new Action(PlayEntranceAnimation), DispatcherPriority.Loaded);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) ApplyAdaptiveTheme();
        }

        private void OnVisualSettingChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) Dispatcher.BeginInvoke(new Action(ApplyAdaptiveTheme), DispatcherPriority.Background);
        }

        private void OnGlassStrengthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded) Dispatcher.BeginInvoke(new Action(ApplyAdaptiveTheme), DispatcherPriority.Background);
        }

        private void PlayEntranceAnimation()
        {
            if (!MotionEnabled)
            {
                SettingsShell.Opacity = 1;
                SettingsShell.RenderTransform = Transform.Identity;
                return;
            }

            var translate = new TranslateTransform(0, 14);
            SettingsShell.RenderTransform = translate;
            SettingsShell.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(270))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            translate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(310))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void ApplyAdaptiveTheme()
        {
            var glassEnabled = CurrentSettings?.EnableGlassEffects ?? true;
            var strength = CurrentSettings?.GlassEffectStrength ?? 78;
            var palette = AdaptiveThemePaletteFactory.Create(this, glassEnabled, strength);

            Resources["SettingsPrimaryText"] = AdaptiveThemePaletteFactory.Brush(palette.PrimaryText);
            Resources["SettingsSecondaryText"] = AdaptiveThemePaletteFactory.Brush(palette.SecondaryText);
            Resources["SettingsMutedText"] = AdaptiveThemePaletteFactory.Brush(palette.MutedText);
            Resources["SettingsInputFill"] = AdaptiveThemePaletteFactory.Brush(palette.ControlFill);
            Resources["SettingsInputStroke"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["SettingsDivider"] = AdaptiveThemePaletteFactory.Brush(palette.Divider);
            Resources["SettingsGlassFill"] = AdaptiveThemePaletteFactory.Gradient(palette.SurfaceTop, palette.SurfaceBottom);
            Resources["SettingsGlassStroke"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["SettingsBackdrop"] = AdaptiveThemePaletteFactory.Brush(palette.Backdrop);

            SettingsAmbientLayer.Opacity = SystemParameters.HighContrast || !glassEnabled
                ? 0
                : (palette.IsDark ? 0.42 : 0.3) * Math.Max(0.2, Math.Min(1, strength / 100.0));
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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
            var textColor = ResolveColor("TextBrush", Colors.Black);
            var darkTheme = RelativeLuminance(textColor) > 0.55;
            var controlColor = ResolveColor("ControlBackgroundBrush", darkTheme ? Color.FromRgb(30, 32, 42) : Colors.White);

            if (SystemParameters.HighContrast)
            {
                Resources["SettingsGlassFill"] = new SolidColorBrush(controlColor);
                Resources["SettingsGlassStroke"] = new SolidColorBrush(textColor);
                Resources["SettingsBackdrop"] = new SolidColorBrush(Colors.Transparent);
                SettingsAmbientLayer.Opacity = 0;
                return;
            }

            if (!(CurrentSettings?.EnableGlassEffects ?? true))
            {
                Resources["SettingsGlassFill"] = new SolidColorBrush(controlColor);
                Resources["SettingsGlassStroke"] = new SolidColorBrush(darkTheme ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(38, 0, 0, 0));
                Resources["SettingsBackdrop"] = new SolidColorBrush(Colors.Transparent);
                SettingsAmbientLayer.Opacity = 0;
                return;
            }

            var strength = Math.Max(20, Math.Min(100, CurrentSettings?.GlassEffectStrength ?? 78)) / 100.0;
            SettingsAmbientLayer.Opacity = (darkTheme ? 0.68 : 0.84) * strength;
            Resources["SettingsGlassFill"] = CreateGlassGradient(
                WithAlpha(darkTheme ? Color.FromArgb(230, 29, 31, 42) : Color.FromArgb(230, 255, 255, 255), strength),
                WithAlpha(darkTheme ? Color.FromArgb(197, 18, 20, 30) : Color.FromArgb(201, 240, 242, 248), strength));
            Resources["SettingsGlassStroke"] = new SolidColorBrush(
                darkTheme ? Color.FromArgb(43, 255, 255, 255) : Color.FromArgb(35, 0, 0, 0));
            Resources["SettingsBackdrop"] = new SolidColorBrush(
                darkTheme ? Color.FromArgb(28, 8, 10, 18) : Color.FromArgb(24, 241, 243, 249));
        }

        private Color ResolveColor(string key, Color fallback)
        {
            var brush = TryFindResource(key) as SolidColorBrush;
            return brush?.Color ?? fallback;
        }

        private static Color WithAlpha(Color color, double strength)
        {
            var alpha = (byte)Math.Max(0, Math.Min(255, Math.Round(color.A * strength)));
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static LinearGradientBrush CreateGlassGradient(Color top, Color bottom)
        {
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            brush.GradientStops.Add(new GradientStop(top, 0));
            brush.GradientStops.Add(new GradientStop(bottom, 1));
            brush.Freeze();
            return brush;
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

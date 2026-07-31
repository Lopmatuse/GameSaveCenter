using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameSaveCenter.Playnite.Infrastructure;
using Microsoft.Win32;

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
            SizeChanged += OnSizeChanged;
        }

        private GameSaveCenterSettings? CurrentSettings => DataContext as GameSaveCenterSettings;

        private bool MotionEnabled => (CurrentSettings?.EnableUiAnimations ?? true) && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyAdaptiveTheme();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
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
            if (!IsVisible) return;
            ApplyAdaptiveTheme();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            => ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);

        private void OnThemeModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) Dispatcher.BeginInvoke(new Action(ApplyAdaptiveTheme), DispatcherPriority.Background);
        }

        private void OnVisualSettingChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) Dispatcher.BeginInvoke(new Action(ApplyAdaptiveTheme), DispatcherPriority.Background);
        }

        private void OnGlassStrengthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded) Dispatcher.BeginInvoke(new Action(ApplyAdaptiveTheme), DispatcherPriority.Background);
        }

        private void OnExportSettingsClick(object sender, RoutedEventArgs e)
        {
            if (CurrentSettings == null) return;
            var dialog = new SaveFileDialog
            {
                Title = "导出 GameSaveCenter 设置",
                Filter = "GameSaveCenter 设置 (*.json)|*.json",
                FileName = $"GameSaveCenter-settings-{DateTime.Now:yyyyMMdd}.json",
                AddExtension = true,
                DefaultExt = ".json"
            };
            if (dialog.ShowDialog() != true) return;
            File.WriteAllText(dialog.FileName, CurrentSettings.ExportPortableJson(), new System.Text.UTF8Encoding(false));
            MessageBox.Show("设置已导出。文件不包含 Rclone 密码，但会包含你填写的本地路径和云端目标名称。",
                "GameSaveCenter", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnImportSettingsClick(object sender, RoutedEventArgs e)
        {
            var settings = CurrentSettings;
            if (settings == null) return;
            var dialog = new OpenFileDialog
            {
                Title = "导入 GameSaveCenter 设置",
                Filter = "GameSaveCenter 设置 (*.json)|*.json|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var info = new FileInfo(dialog.FileName);
                if (info.Length > 1024 * 1024) throw new InvalidDataException("设置文件超过 1 MiB 安全上限。");
                var report = settings.ImportPortableJson(File.ReadAllText(dialog.FileName));
                DataContext = null;
                DataContext = settings;
                ApplyAdaptiveTheme();
                ApplyResponsiveLayout(ActualWidth, ActualHeight);
                MessageBox.Show(report.Summary, "GameSaveCenter 设置迁移报告",
                    MessageBoxButton.OK, report.MissingPaths.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法导入设置：" + ex.Message, "GameSaveCenter",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            var palette = AdaptiveThemePaletteFactory.Create(this, glassEnabled, strength, CurrentSettings?.ThemeMode ?? GameSaveCenterThemeMode.FollowPlaynite);

            Resources["GscPrimaryTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.PrimaryText);
            Resources["GscSecondaryTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.SecondaryText);
            Resources["GscMutedTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.MutedText);
            Resources["GscControlFillBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlFill);
            Resources["GscControlStrokeBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["GscDividerBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Divider);
            Resources["GscGlassFillBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.SurfaceTop, palette.SurfaceBottom);
            Resources["GscGlassStrokeBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["GscBackdropBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Backdrop);
            WpfUiThemeScope.Apply(Resources, palette.IsDark);

            SettingsAmbientLayer.Opacity = SystemParameters.HighContrast || !glassEnabled
                ? 0
                : (palette.IsDark ? 0.42 : 0.3) * Math.Max(0.2, Math.Min(1, strength / 100.0));
        }

        private void ApplyResponsiveLayout(double width, double height)
        {
            if (SettingsShell == null || SettingsHeaderSubtitle == null) return;

            var compact = width < 720;
            SettingsShell.Margin = compact
                ? new Thickness(16, 16, 20, 24)
                : new Thickness(28, 22, 32, 30);
            SettingsShell.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
            SettingsShell.MaxWidth = compact ? double.PositiveInfinity : 980;
            SettingsHeaderSubtitle.Visibility = height < 680 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}

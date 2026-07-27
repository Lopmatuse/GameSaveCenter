using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameSaveCenter.Playnite.ViewModels;

namespace GameSaveCenter.Playnite.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly GameSaveCenterPlugin plugin;
        private readonly DispatcherTimer refreshTimer;
        private readonly Dictionary<int, RadioButton> navigationItems = new Dictionary<int, RadioButton>();
        private DashboardViewModel viewModel;
        private bool syncingNavigation;
        private bool hasPlayedEntrance;

        public DashboardView(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();

            viewModel = new DashboardViewModel(plugin);
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = viewModel;

            navigationItems[0] = NavOverview;
            navigationItems[1] = NavMedia;
            navigationItems[2] = NavPaths;
            navigationItems[3] = NavTasks;
            navigationItems[4] = NavDiagnostics;
            navigationItems[5] = NavLogs;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
            refreshTimer.Tick += OnRefreshTimerTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private bool MotionEnabled => plugin.Settings.EnableUiAnimations && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var version = typeof(DashboardView).Assembly.GetName().Version;
            SidebarVersionText.Text = version == null ? "开发预览" : "v" + version.ToString(3);
            ApplyAdaptiveTheme();
            refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, Math.Min(300, plugin.Settings.DashboardRefreshSeconds)));
            if (plugin.Settings.EnableDashboardAutoRefresh) refreshTimer.Start();

            if (!hasPlayedEntrance)
            {
                hasPlayedEntrance = true;
                Dispatcher.BeginInvoke(new Action(PlayEntranceAnimation), DispatcherPriority.Loaded);
            }
            else
            {
                MainShell.Opacity = 1;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => refreshTimer.Stop();

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) ApplyAdaptiveTheme();
        }

        private void OnRefreshTimerTick(object sender, EventArgs e) => viewModel?.RequestBackgroundRefresh();

        private void OnNavigationChecked(object sender, RoutedEventArgs e)
        {
            if (syncingNavigation || DetailsTabControl == null) return;
            var item = sender as RadioButton;
            if (item == null || item.Tag == null) return;
            if (!int.TryParse(item.Tag.ToString(), out var index)) return;
            if (index < 0 || index >= DetailsTabControl.Items.Count) return;
            DetailsTabControl.SelectedIndex = index;
        }

        private void OnDetailsTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, DetailsTabControl)) return;
            SyncNavigationFromTab();
            AnimateElement(DetailsTabControl, 10, 0, 0.2);
        }

        private void SyncNavigationFromTab()
        {
            if (DetailsTabControl == null) return;
            if (!navigationItems.TryGetValue(DetailsTabControl.SelectedIndex, out var item)) return;
            syncingNavigation = true;
            try { item.IsChecked = true; }
            finally { syncingNavigation = false; }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (e.PropertyName == nameof(DashboardViewModel.SelectedGame))
            {
                Dispatcher.BeginInvoke(new Action(() => AnimateElement(GameDetailCard, 13, 0, 0.23)), DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(DashboardViewModel.SelectedTask))
            {
                Dispatcher.BeginInvoke(new Action(() => AnimateElement(TaskDetailCard, 8, 0, 0.2)), DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(DashboardViewModel.StatusMessage))
            {
                Dispatcher.BeginInvoke(new Action(() => AnimateStatusPill()), DispatcherPriority.Background);
            }
        }

        private void OnMetricCardMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateTranslate(sender as FrameworkElement, 0, -3, 160);

        private void OnMetricCardMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateTranslate(sender as FrameworkElement, 0, 0, 180);

        private void OnNavigationMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateTranslate(sender as FrameworkElement, 3, 0, 140);

        private void OnNavigationMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateTranslate(sender as FrameworkElement, 0, 0, 160);

        private void OnButtonMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateScale(sender as FrameworkElement, 1.025, 130);

        private void OnButtonMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateScale(sender as FrameworkElement, 1, 160);

        private void AnimateTranslate(FrameworkElement element, double x, double y, int milliseconds)
        {
            if (element == null || !MotionEnabled) return;
            var translate = GetMutableTranslateTransform(element);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
        }

        private void AnimateScale(FrameworkElement element, double scaleValue, int milliseconds)
        {
            if (element == null || !MotionEnabled) return;
            var scale = GetMutableScaleTransform(element);
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scaleValue, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scaleValue, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
        }

        private void PlayEntranceAnimation()
        {
            if (!MotionEnabled)
            {
                MainShell.Opacity = 1;
                MainShell.RenderTransform = Transform.Identity;
                return;
            }

            MainShell.RenderTransform = new TranslateTransform(0, 16);
            var storyboard = new Storyboard();
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var move = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fade, MainShell);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(move, MainShell);
            Storyboard.SetTargetProperty(move, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            storyboard.Children.Add(fade);
            storyboard.Children.Add(move);
            storyboard.Begin(this, HandoffBehavior.SnapshotAndReplace, true);
        }

        private void AnimateElement(FrameworkElement element, double offsetX, double offsetY, double seconds)
        {
            if (element == null) return;
            if (!MotionEnabled)
            {
                element.Opacity = 1;
                return;
            }

            var translate = GetMutableTranslateTransform(element);

            translate.X = offsetX;
            translate.Y = offsetY;
            element.Opacity = 0.72;
            var duration = TimeSpan.FromSeconds(seconds);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.72, 1, duration) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offsetX, 0, duration) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0, duration) { EasingFunction = easing });
        }

        private void AnimateStatusPill()
        {
            if (StatusPill == null || !MotionEnabled) return;
            var scale = GetMutableScaleTransform(StatusPill);
            StatusPill.RenderTransformOrigin = new Point(0, 0.5);

            var duration = TimeSpan.FromMilliseconds(180);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            StatusPill.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.58, 1, duration) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, duration) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, duration) { EasingFunction = easing });
        }

        private static TranslateTransform GetMutableTranslateTransform(FrameworkElement element)
        {
            var translate = element.RenderTransform as TranslateTransform;
            if (translate == null)
            {
                translate = new TranslateTransform();
                element.RenderTransform = translate;
                return translate;
            }

            // Freezables declared in a Style setter are shared and frozen by WPF. They cannot be
            // animated directly, so every element must receive its own mutable clone first.
            if (translate.IsFrozen)
            {
                translate = (TranslateTransform)translate.CloneCurrentValue();
                element.RenderTransform = translate;
            }

            return translate;
        }

        private static ScaleTransform GetMutableScaleTransform(FrameworkElement element)
        {
            var scale = element.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1);
                element.RenderTransform = scale;
                return scale;
            }

            if (scale.IsFrozen)
            {
                scale = (ScaleTransform)scale.CloneCurrentValue();
                element.RenderTransform = scale;
            }

            return scale;
        }

        private void ApplyAdaptiveTheme()
        {
            var textColor = ResolveSolidColor("TextBrush", Colors.Black);
            var darkTheme = GetRelativeLuminance(textColor) > 0.55;
            var controlColor = ResolveSolidColor("ControlBackgroundBrush", darkTheme ? Color.FromRgb(30, 32, 42) : Colors.White);

            if (SystemParameters.HighContrast)
            {
                Resources["GscGlassFillBrush"] = new SolidColorBrush(controlColor);
                Resources["GscGlassStrongBrush"] = new SolidColorBrush(controlColor);
                Resources["GscSidebarBrush"] = new SolidColorBrush(controlColor);
                Resources["GscGlassStrokeBrush"] = new SolidColorBrush(textColor);
                Resources["GscGlassHighlightBrush"] = new SolidColorBrush(Colors.Transparent);
                Resources["GscBackdropBrush"] = new SolidColorBrush(Colors.Transparent);
                AmbientGlowLayer.Opacity = 0;
                return;
            }

            if (!plugin.Settings.EnableGlassEffects)
            {
                Resources["GscGlassFillBrush"] = new SolidColorBrush(controlColor);
                Resources["GscGlassStrongBrush"] = new SolidColorBrush(controlColor);
                Resources["GscSidebarBrush"] = new SolidColorBrush(controlColor);
                Resources["GscGlassStrokeBrush"] = new SolidColorBrush(darkTheme ? Color.FromArgb(45, 255, 255, 255) : Color.FromArgb(38, 0, 0, 0));
                Resources["GscGlassHighlightBrush"] = new SolidColorBrush(Colors.Transparent);
                Resources["GscBackdropBrush"] = new SolidColorBrush(Colors.Transparent);
                AmbientGlowLayer.Opacity = 0;
                return;
            }

            var strength = Math.Max(20, Math.Min(100, plugin.Settings.GlassEffectStrength)) / 100.0;
            AmbientGlowLayer.Opacity = (darkTheme ? 0.72 : 0.88) * strength;
            Resources["GscGlassFillBrush"] = CreateGlassGradient(
                WithAlpha(darkTheme ? Color.FromArgb(218, 28, 30, 41) : Color.FromArgb(221, 255, 255, 255), strength),
                WithAlpha(darkTheme ? Color.FromArgb(188, 17, 19, 29) : Color.FromArgb(194, 238, 240, 247), strength));
            Resources["GscGlassStrongBrush"] = CreateGlassGradient(
                WithAlpha(darkTheme ? Color.FromArgb(240, 35, 37, 49) : Color.FromArgb(244, 255, 255, 255), strength),
                WithAlpha(darkTheme ? Color.FromArgb(218, 24, 26, 37) : Color.FromArgb(226, 246, 247, 251), strength));
            Resources["GscSidebarBrush"] = CreateGlassGradient(
                WithAlpha(darkTheme ? Color.FromArgb(230, 25, 27, 38) : Color.FromArgb(232, 255, 255, 255), strength),
                WithAlpha(darkTheme ? Color.FromArgb(200, 15, 17, 26) : Color.FromArgb(205, 244, 245, 250), strength));
            Resources["GscGlassStrokeBrush"] = new SolidColorBrush(darkTheme ? Color.FromArgb(42, 255, 255, 255) : Color.FromArgb(34, 0, 0, 0));
            Resources["GscGlassHighlightBrush"] = new SolidColorBrush(darkTheme ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(145, 255, 255, 255));
            Resources["GscBackdropBrush"] = new SolidColorBrush(darkTheme ? Color.FromArgb(30, 7, 9, 17) : Color.FromArgb(25, 240, 242, 248));
        }

        private Color ResolveSolidColor(string resourceKey, Color fallback)
        {
            var resource = TryFindResource(resourceKey);
            var solid = resource as SolidColorBrush;
            return solid?.Color ?? fallback;
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

        private static double GetRelativeLuminance(Color color)
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

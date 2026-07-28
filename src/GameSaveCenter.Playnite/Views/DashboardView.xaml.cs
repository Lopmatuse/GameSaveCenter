using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameSaveCenter.Playnite.Infrastructure;
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
        private bool visualSettingsSubscribed;

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
            SizeChanged += OnSizeChanged;
        }

        private bool MotionEnabled => plugin.Settings.EnableUiAnimations && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!visualSettingsSubscribed)
            {
                plugin.VisualSettingsChanged += OnVisualSettingsChanged;
                visualSettingsSubscribed = true;
            }
            var version = typeof(DashboardView).Assembly.GetName().Version;
            SidebarVersionText.Text = version == null ? "开发预览" : "v" + version.ToString(3);
            ApplyAdaptiveTheme();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Stop();
            if (visualSettingsSubscribed)
            {
                plugin.VisualSettingsChanged -= OnVisualSettingsChanged;
                visualSettingsSubscribed = false;
            }
        }

        private void OnVisualSettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyAdaptiveTheme();
                refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, Math.Min(300, plugin.Settings.DashboardRefreshSeconds)));
                if (plugin.Settings.EnableDashboardAutoRefresh) refreshTimer.Start(); else refreshTimer.Stop();
            }), DispatcherPriority.Background);
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                ApplyAdaptiveTheme();
                ApplyResponsiveLayout(ActualWidth, ActualHeight);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            => ApplyResponsiveLayout(e.NewSize.Width, e.NewSize.Height);

        private void ApplyResponsiveLayout(double width, double height)
        {
            if (SidebarColumn == null || MetricsPanel == null || GameListColumn == null) return;

            var compactWidth = width < 1100;
            SidebarColumn.Width = new GridLength(compactWidth ? 172 : 194);
            SidebarGutterColumn.Width = new GridLength(compactWidth ? 12 : 18);
            WorkspaceGutterColumn.Width = new GridLength(compactWidth ? 12 : 16);

            if (compactWidth)
            {
                GameListColumn.Width = new GridLength(width < 920 ? 250 : 285);
                GameDetailColumn.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                GameListColumn.Width = new GridLength(0.95, GridUnitType.Star);
                GameDetailColumn.Width = new GridLength(2.05, GridUnitType.Star);
            }

            // At short Playnite client heights the metric strip previously consumed the space
            // required by the detail tabs, reducing the history grid to zero pixels.
            var showMetrics = height >= 800 && width >= 1180;
            MetricsPanel.Visibility = showMetrics ? Visibility.Visible : Visibility.Collapsed;
            MetricsPanel.Columns = width >= 1450 ? 6 : 3;
            MetricsPanel.Margin = showMetrics ? new Thickness(0, 0, 0, 18) : new Thickness(0);
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
            => AnimateTranslate(sender as FrameworkElement, 0, -1, 120);

        private void OnButtonMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            => AnimateTranslate(sender as FrameworkElement, 0, 0, 150);

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
            var glassEnabled = plugin.Settings.EnableGlassEffects && !SystemParameters.HighContrast;
            var palette = AdaptiveThemePaletteFactory.Create(this, glassEnabled, plugin.Settings.GlassEffectStrength, plugin.Settings.ThemeMode);

            Resources["GscPrimaryTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.PrimaryText);
            Resources["GscSecondaryTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.SecondaryText);
            Resources["GscMutedTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.MutedText);
            Resources["GscDisabledTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.DisabledText);
            Resources["GscControlFillBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlFill);
            Resources["GscControlStrokeBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["GscDividerBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Divider);
            Resources["GscPopupBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                250, palette.StrongSurfaceTop.R, palette.StrongSurfaceTop.G, palette.StrongSurfaceTop.B));
            Resources["GscGlassFillBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.SurfaceTop, palette.SurfaceBottom);
            Resources["GscGlassStrongBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.StrongSurfaceTop, palette.StrongSurfaceBottom);
            Resources["GscSidebarBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.SidebarTop, palette.SidebarBottom);
            Resources["GscGlassStrokeBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["GscGlassHighlightBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Highlight);
            Resources["GscBackdropBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Backdrop);

            AmbientGlowLayer.Opacity = glassEnabled
                ? (palette.IsDark ? 0.46 : 0.56) * Math.Max(0.2, Math.Min(1, plugin.Settings.GlassEffectStrength / 100d))
                : 0;
        }
    }
}

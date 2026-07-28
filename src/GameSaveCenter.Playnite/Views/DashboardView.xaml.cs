using System;
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
        private DashboardViewModel viewModel;
        private bool hasPlayedEntrance;
        private bool visualSettingsSubscribed;

        public DashboardView(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();

            viewModel = new DashboardViewModel(plugin);
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            DataContext = viewModel;

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
            UpdateWorkspacePresentation();
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

            var mode = width >= 1320 ? LayoutMode.Expanded
                : width >= 1050 ? LayoutMode.Standard
                : width >= 880 ? LayoutMode.Compact
                : LayoutMode.Narrow;
            viewModel.LayoutMode = mode;
            var iconSidebar = mode != LayoutMode.Expanded;
            var gameScopedWorkspace = viewModel.CurrentWorkspace == WorkspaceKind.Saves
                || viewModel.CurrentWorkspace == WorkspaceKind.Trainers
                || viewModel.CurrentWorkspace == WorkspaceKind.Media;
            var showGameBrowser = gameScopedWorkspace && (mode == LayoutMode.Expanded || mode == LayoutMode.Standard);

            SidebarColumn.Width = new GridLength(iconSidebar ? 72 : 220);
            SidebarGutterColumn.Width = new GridLength(iconSidebar ? 10 : 18);
            SetSidebarLabelsVisible(!iconSidebar);

            GameBrowserPanel.Visibility = showGameBrowser ? Visibility.Visible : Visibility.Collapsed;
            WorkspaceGutterColumn.Width = new GridLength(showGameBrowser ? 14 : 0);
            GameListColumn.Width = showGameBrowser
                ? new GridLength(mode == LayoutMode.Expanded ? 340 : 290)
                : new GridLength(0);
            GameDetailColumn.Width = new GridLength(1, GridUnitType.Star);
            CompactGameSelector.Visibility = gameScopedWorkspace && !showGameBrowser ? Visibility.Visible : Visibility.Collapsed;

            PageSubtitleText.Visibility = height >= 760 ? Visibility.Visible : Visibility.Collapsed;
            var showMetrics = viewModel.CurrentWorkspace == WorkspaceKind.Overview && height >= 760 && width >= 1180;
            MetricsPanel.Visibility = showMetrics ? Visibility.Visible : Visibility.Collapsed;
            MetricsPanel.Columns = width >= 1450 ? 6 : 3;
            MetricsPanel.Margin = showMetrics ? new Thickness(0, 0, 0, 18) : new Thickness(0);

            RestoreSafetyBanner.Visibility = viewModel.CurrentWorkspace == WorkspaceKind.Saves && height >= 700
                ? Visibility.Visible : Visibility.Collapsed;
            if (viewModel.CurrentWorkspace != WorkspaceKind.Saves)
            {
                BackupPolicyPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnRefreshTimerTick(object sender, EventArgs e) => viewModel?.RequestBackgroundRefresh();

        private void OnNavigationChecked(object sender, RoutedEventArgs e)
        {
            if (viewModel == null || DetailsTabControl == null) return;
            var item = sender as RadioButton;
            if (item == null || item.Tag == null) return;
            if (!Enum.TryParse(item.Tag.ToString(), out WorkspaceKind workspace)) return;
            viewModel.CurrentWorkspace = workspace;
            switch (workspace)
            {
                case WorkspaceKind.Saves:
                    PageTitleText.Text = "存档中心"; PageSubtitleText.Text = "历史版本、备份策略与安全恢复"; break;
                case WorkspaceKind.Trainers:
                    PageTitleText.Text = "修改器中心"; PageSubtitleText.Text = "管理本地修改器、Cheat Table 与 FLiNG 在线目录"; break;
                case WorkspaceKind.Media:
                    PageTitleText.Text = "媒体中心"; PageSubtitleText.Text = "截图、录像与待归类媒体"; break;
                case WorkspaceKind.Tasks:
                    PageTitleText.Text = "任务中心"; PageSubtitleText.Text = "查看后台任务、进度与失败详情"; break;
                case WorkspaceKind.Maintenance:
                    PageTitleText.Text = "维护中心"; PageSubtitleText.Text = "Worker、Ludusavi、目录与诊断"; break;
                default:
                    PageTitleText.Text = "首页"; PageSubtitleText.Text = "存档、修改器、媒体与任务的一体化工作台"; break;
            }
            UpdateWorkspacePresentation();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
            AnimateElement(DetailsTabControl, 10, 0, 0.2);
        }

        private void UpdateWorkspacePresentation()
        {
            var workspace = viewModel.CurrentWorkspace;
            SetVisibility(OverviewTab, workspace == WorkspaceKind.Overview);
            SetVisibility(SaveHistoryTab, workspace == WorkspaceKind.Saves);
            SetVisibility(CandidateTab, workspace == WorkspaceKind.Saves);
            SetVisibility(TrainerTab, workspace == WorkspaceKind.Trainers);
            SetVisibility(MediaTab, workspace == WorkspaceKind.Media);
            SetVisibility(TaskTab, workspace == WorkspaceKind.Tasks);
            SetVisibility(DiagnosticTab, workspace == WorkspaceKind.Maintenance);
            SetVisibility(LogsTab, workspace == WorkspaceKind.Maintenance);

            var saves = workspace == WorkspaceKind.Saves;
            SetVisibility(SelectedGameHeader, workspace != WorkspaceKind.Tasks && workspace != WorkspaceKind.Maintenance && workspace != WorkspaceKind.Overview);
            SetVisibility(BackupSelectedButton, saves);
            SetVisibility(ValidateButton, saves);
            SetVisibility(DetectPathsButton, saves);
            SetVisibility(PolicyToggleButton, saves);
            SetVisibility(RestoreSafetyBanner, saves);
            if (!saves) BackupPolicyPanel.Visibility = Visibility.Collapsed;

            SetVisibility(TopRefreshButton, workspace != WorkspaceKind.Trainers && workspace != WorkspaceKind.Maintenance);
            SetVisibility(TopBackupAllButton, saves);
            SetVisibility(TopMediaSyncButton, workspace == WorkspaceKind.Media);
            SetVisibility(TopTrainerImportButton, workspace == WorkspaceKind.Trainers);
            SetVisibility(TopTrainerCatalogButton, workspace == WorkspaceKind.Trainers);
            SetVisibility(TopDiagnosticsButton, workspace == WorkspaceKind.Maintenance);

            TabItem? firstVisible = null;
            foreach (var item in DetailsTabControl.Items)
            {
                var tab = item as TabItem;
                if (tab != null && tab.Visibility == Visibility.Visible)
                {
                    firstVisible = tab;
                    break;
                }
            }
            if (firstVisible != null) DetailsTabControl.SelectedItem = firstVisible;
        }

        private void OnTogglePolicy(object sender, RoutedEventArgs e)
        {
            if (viewModel.CurrentWorkspace != WorkspaceKind.Saves) return;
            BackupPolicyPanel.Visibility = BackupPolicyPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void SetSidebarLabelsVisible(bool visible)
        {
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            SidebarBrandText.Visibility = visibility;
            NavOverviewLabel.Visibility = visibility;
            NavSavesLabel.Visibility = visibility;
            NavTrainersLabel.Visibility = visibility;
            NavMediaLabel.Visibility = visibility;
            NavTasksLabel.Visibility = visibility;
            NavMaintenanceLabel.Visibility = visibility;
        }

        private static void SetVisibility(UIElement element, bool visible)
            => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        private void OnDetailsTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, DetailsTabControl)) return;
            AnimateElement(DetailsTabControl, 10, 0, 0.2);
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

        private void AnimateTranslate(FrameworkElement? element, double x, double y, int milliseconds)
        {
            if (element == null || !MotionEnabled) return;
            var translate = GetMutableTranslateTransform(element);
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
        }

        private void AnimateScale(FrameworkElement? element, double scaleValue, int milliseconds)
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

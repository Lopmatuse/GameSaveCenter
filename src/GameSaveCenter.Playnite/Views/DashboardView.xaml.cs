using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameSaveCenter.Playnite.Infrastructure;
using GameSaveCenter.Playnite.ViewModels;
using Playnite.SDK;
using Snackbar = Wpf.Ui.Controls.Snackbar;

namespace GameSaveCenter.Playnite.Views
{
    public partial class DashboardView : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly GameSaveCenterPlugin plugin;
        private readonly DispatcherTimer refreshTimer;
        private readonly Dictionary<Border, DispatcherTimer> toastTimers = new Dictionary<Border, DispatcherTimer>();
        private readonly UiFrameworkProbeLoader uiFrameworkProbeLoader;
        private DashboardViewModel viewModel;
        private bool hasPlayedEntrance;
        private bool viewModelSubscribed;
        private bool visualSettingsSubscribed;
        private bool systemParametersSubscribed;
        private bool uiFeedbackSubscribed;
        private UiConfirmationEventArgs? activeConfirmation;
        private bool dialogShowsResult;
        private bool confirmationOpen;
        private bool responsiveLayoutPending;
        private bool compactGameBrowserOpen;
        private Size pendingResponsiveSize;

        public DashboardView(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            uiFrameworkProbeLoader = new UiFrameworkProbeLoader(exception => Logger.Error(exception, "GameSaveCenter WPF-UI probe could not be loaded."));

            viewModel = new DashboardViewModel(plugin);
            DataContext = viewModel;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background);
            refreshTimer.Tick += OnRefreshTimerTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
            SizeChanged += OnSizeChanged;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private bool MotionEnabled => plugin.Settings.EnableUiAnimations && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SubscribeViewModel();
            if (!visualSettingsSubscribed)
            {
                plugin.VisualSettingsChanged += OnVisualSettingsChanged;
                visualSettingsSubscribed = true;
            }
            if (!systemParametersSubscribed)
            {
                SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
                systemParametersSubscribed = true;
            }
            if (!uiFeedbackSubscribed)
            {
                plugin.UiNotificationRequested += OnUiNotificationRequested;
                plugin.UiConfirmationRequested += OnUiConfirmationRequested;
                uiFeedbackSubscribed = true;
            }
            var version = typeof(DashboardView).Assembly.GetName().Version;
            SidebarVersionText.Text = version == null ? "开发预览" : "v" + version.ToString(3);
            ApplyAdaptiveTheme();
            UpdateWorkspacePresentation();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
            refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, Math.Min(300, plugin.Settings.DashboardRefreshSeconds)));
            if (plugin.Settings.EnableDashboardAutoRefresh) refreshTimer.Start();
            viewModel.StartTaskEventSubscription();

            if (!hasPlayedEntrance)
            {
                hasPlayedEntrance = true;
                BeginUiSafely(PlayEntranceAnimation, DispatcherPriority.Loaded);
            }
            else
            {
                MainShell.Opacity = 1;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Stop();
            viewModel.CancelDeferredUiWork();
            viewModel.StopTaskEventSubscription();
            UnsubscribeViewModel();
            if (visualSettingsSubscribed)
            {
                plugin.VisualSettingsChanged -= OnVisualSettingsChanged;
                visualSettingsSubscribed = false;
            }
            if (systemParametersSubscribed)
            {
                SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
                systemParametersSubscribed = false;
            }
            if (uiFeedbackSubscribed)
            {
                plugin.UiNotificationRequested -= OnUiNotificationRequested;
                plugin.UiConfirmationRequested -= OnUiConfirmationRequested;
                uiFeedbackSubscribed = false;
            }
            activeConfirmation?.Completion.TrySetResult(false);
            activeConfirmation = null;
            confirmationOpen = false;
            responsiveLayoutPending = false;
            DialogOverlay.Visibility = Visibility.Collapsed;
            ClearToasts();
        }

        private void SubscribeViewModel()
        {
            if (viewModelSubscribed) return;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.AttentionCenterRequested += OnAttentionCenterRequested;
            viewModelSubscribed = true;
        }

        private void UnsubscribeViewModel()
        {
            if (!viewModelSubscribed) return;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.AttentionCenterRequested -= OnAttentionCenterRequested;
            viewModelSubscribed = false;
        }

        private void OnAttentionCenterRequested(object? sender, EventArgs e)
        {
            BeginUiSafely(() =>
            {
                if (!IsLoaded) return;
                NavMaintenance.IsChecked = true;
                UpdateWorkspacePresentation();
                DetailsTabControl.SelectedItem = MaintenanceWorkspaceTab;
                MaintenanceWorkspaceView.FindingsGridElement.ScrollIntoView(viewModel.SelectedFinding);
                MaintenanceWorkspaceView.FindingsGridElement.Focus();
                AnimateElement(DetailsTabControl, 10, 0, 0.2);
            }, DispatcherPriority.Background);
        }

        private void OnVisualSettingsChanged(object sender, EventArgs e)
        {
            BeginUiSafely(() =>
            {
                if (!IsLoaded) return;
                ApplyAdaptiveTheme();
                refreshTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, Math.Min(300, plugin.Settings.DashboardRefreshSeconds)));
                if (plugin.Settings.EnableDashboardAutoRefresh) refreshTimer.Start(); else refreshTimer.Stop();
            }, DispatcherPriority.Background);
        }

        private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
        {
            // High contrast, client-area animation and transparency preferences can change
            // while Playnite remains open. Rebuild the local palette without touching the host.
            BeginUiSafely(() =>
            {
                if (!IsLoaded) return;
                ApplyAdaptiveTheme();
                ApplyResponsiveLayout(ActualWidth, ActualHeight);
            }, DispatcherPriority.Background);
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
            => QueueResponsiveLayout(e.NewSize);

        private void QueueResponsiveLayout(Size size)
        {
            pendingResponsiveSize = size;
            if (responsiveLayoutPending) return;
            responsiveLayoutPending = true;
            BeginUiSafely(() =>
            {
                responsiveLayoutPending = false;
                if (!IsLoaded) return;
                ApplyResponsiveLayout(pendingResponsiveSize.Width, pendingResponsiveSize.Height);
            }, DispatcherPriority.Render);
        }

        private void BeginUiSafely(Action action, DispatcherPriority priority)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            try
            {
                Dispatcher.BeginInvoke(action, priority);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error(ex, "GameSaveCenter skipped a deferred Dashboard UI callback because the dispatcher is unavailable.");
            }
        }

        private void ApplyResponsiveLayout(double width, double height)
        {
            if (SidebarColumn == null || MetricsPanel == null || GameListColumn == null) return;

            var mode = width >= 1260 ? LayoutMode.Expanded
                : width >= 980 ? LayoutMode.Standard
                : width >= 760 ? LayoutMode.Compact
                : LayoutMode.Narrow;
            viewModel.LayoutMode = mode;

            var iconSidebar = mode == LayoutMode.Compact || mode == LayoutMode.Narrow;
            // The game picker is a single global context entry. It is never a permanent
            // third column: all widths use the same top button and the same finite drawer.
            // Tasks and maintenance remain global and intentionally have no game picker.
            var gameScopedWorkspace = viewModel.CurrentWorkspace != WorkspaceKind.Tasks
                && viewModel.CurrentWorkspace != WorkspaceKind.Maintenance;
            var showPersistentGameBrowser = false;
            var showCompactGameBrowser = gameScopedWorkspace && compactGameBrowserOpen;

            SidebarColumn.Width = new GridLength(mode == LayoutMode.Expanded ? 228
                : mode == LayoutMode.Standard ? 204
                : mode == LayoutMode.Compact ? 78
                : 72);
            SidebarGutterColumn.Width = new GridLength(iconSidebar ? 10 : 16);
            TopChromeSafetyColumn.Width = new GridLength(0);
            ToastHost.Margin = new Thickness(0, height < 760 ? 66 : 78, width < 980 ? 12 : 22, 0);
            SetSidebarLabelsVisible(!iconSidebar);
            SetToolbarLabelsVisible(mode == LayoutMode.Expanded);

            // Header layout is explicit at every breakpoint.  It never relies on wrapping the
            // title and action bar into the same measure slot, which prevents overlap at 125–200% DPI.
            Grid.SetRow(HeaderTitlePanel, 0);
            Grid.SetColumn(HeaderTitlePanel, 0);
            Grid.SetColumnSpan(HeaderTitlePanel, mode == LayoutMode.Expanded ? 1 : 2);
            GameSwitcherHost.Visibility = gameScopedWorkspace && !showPersistentGameBrowser
                ? Visibility.Visible
                : Visibility.Collapsed;
            Grid.SetRow(GameSwitcherHost, 1);
            Grid.SetColumn(GameSwitcherHost, 0);
            Grid.SetColumnSpan(GameSwitcherHost, mode == LayoutMode.Standard ? 1 : 2);
            GameSwitcherHost.MaxWidth = mode == LayoutMode.Narrow ? double.PositiveInfinity : 640;
            GameSwitcherHost.HorizontalAlignment = mode == LayoutMode.Narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

            if (mode == LayoutMode.Expanded)
            {
                HeaderCompactActionsRow.Height = new GridLength(0);
                Grid.SetRow(TopActionsScroller, 0);
                Grid.SetColumn(TopActionsScroller, 1);
                Grid.SetColumnSpan(TopActionsScroller, 1);
                TopActionsScroller.HorizontalAlignment = HorizontalAlignment.Right;
                TopActionsScroller.Margin = new Thickness(14, 0, 0, 0);
            }
            else if (mode == LayoutMode.Standard)
            {
                HeaderCompactActionsRow.Height = new GridLength(0);
                Grid.SetRow(TopActionsScroller, 1);
                Grid.SetColumn(TopActionsScroller, 1);
                Grid.SetColumnSpan(TopActionsScroller, 1);
                TopActionsScroller.HorizontalAlignment = HorizontalAlignment.Right;
                TopActionsScroller.Margin = new Thickness(14, 12, 0, 0);
            }
            else
            {
                HeaderCompactActionsRow.Height = GridLength.Auto;
                Grid.SetRow(TopActionsScroller, 2);
                Grid.SetColumn(TopActionsScroller, 0);
                Grid.SetColumnSpan(TopActionsScroller, 2);
                TopActionsScroller.HorizontalAlignment = HorizontalAlignment.Stretch;
                TopActionsScroller.Margin = new Thickness(0, 10, 0, 0);
            }

            // The selected-game context button is the only selector entry at every breakpoint.
            // It opens the same virtualized game browser drawer.
            ToggleGameBrowserButton.Visibility = Visibility.Collapsed;

            // The complete game search/filter/sort surface is an explicit, finite-height drawer;
            // it is never left permanently beside the details view.
            GameBrowserPanel.Visibility = showPersistentGameBrowser || showCompactGameBrowser
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (showPersistentGameBrowser)
            {
                WorkspaceCompactBrowserRow.Height = new GridLength(0);
                WorkspaceDetailRow.Height = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(GameBrowserPanel, 0);
                Grid.SetRowSpan(GameBrowserPanel, 2);
                Grid.SetColumn(GameBrowserPanel, 0);
                Grid.SetColumnSpan(GameBrowserPanel, 1);
                GameBrowserPanel.MaxHeight = double.PositiveInfinity;
                GameBrowserPanel.Margin = new Thickness(0);
                WorkspaceGutterColumn.Width = new GridLength(14);
                GameListColumn.Width = new GridLength(310);
                GameDetailColumn.Width = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(GameDetailCard, 0);
                Grid.SetRowSpan(GameDetailCard, 2);
                Grid.SetColumn(GameDetailCard, 2);
                Grid.SetColumnSpan(GameDetailCard, 1);
                GameDetailCard.Margin = new Thickness(0);
            }
            else
            {
                WorkspaceCompactBrowserRow.Height = showCompactGameBrowser ? GridLength.Auto : new GridLength(0);
                WorkspaceDetailRow.Height = new GridLength(1, GridUnitType.Star);
                WorkspaceGutterColumn.Width = new GridLength(0);
                GameListColumn.Width = new GridLength(1, GridUnitType.Star);
                GameDetailColumn.Width = new GridLength(0);
                Grid.SetRow(GameBrowserPanel, 0);
                Grid.SetRowSpan(GameBrowserPanel, 1);
                Grid.SetColumn(GameBrowserPanel, 0);
                Grid.SetColumnSpan(GameBrowserPanel, 3);
                GameBrowserPanel.MaxHeight = showCompactGameBrowser ? Math.Max(240, Math.Min(360, height * 0.42)) : 0;
                GameBrowserPanel.Margin = showCompactGameBrowser ? new Thickness(0, 0, 0, 12) : new Thickness(0);
                Grid.SetRow(GameDetailCard, 1);
                Grid.SetRowSpan(GameDetailCard, 1);
                Grid.SetColumn(GameDetailCard, 0);
                Grid.SetColumnSpan(GameDetailCard, 3);
                GameDetailCard.Margin = new Thickness(0);
            }

            PageSubtitleText.Visibility = height >= 760 ? Visibility.Visible : Visibility.Collapsed;
            var showMetrics = viewModel.CurrentWorkspace == WorkspaceKind.Overview && height >= 760 && width >= 980;
            MetricsPanel.Visibility = showMetrics ? Visibility.Visible : Visibility.Collapsed;
            MetricsPanel.Columns = width >= 1480 ? 6 : width >= 1120 ? 3 : 2;
            MetricsPanel.Margin = showMetrics ? new Thickness(0, 0, 0, 16) : new Thickness(0);

            if (OverviewWorkspaceView != null)
            {
                var stackOverview = width < 1180;
                OverviewWorkspaceView.OverviewCompactSecondaryRowHeight = stackOverview ? GridLength.Auto : new GridLength(0);
                OverviewWorkspaceView.ApplyResponsiveColumns(stackOverview);
                OverviewWorkspaceView.ApplyResponsiveHeight(height, stackOverview);
            }

            if (SelectedGameMetricPanel != null)
            {
                SelectedGameMetricPanel.Visibility = width >= 1180 ? Visibility.Visible : Visibility.Collapsed;
                GameHeaderActions.Margin = width >= 1180
                    ? new Thickness(62, 12, 0, 0)
                    : new Thickness(0, 12, 0, 0);
            }

            if (TrainerSummaryPanel != null)
            {
                TrainerSummaryPanel.Columns = width >= 1120 ? 3 : 1;
                TrainerSummaryPanel.Visibility = height >= 720 ? Visibility.Visible : Visibility.Collapsed;
            }

            if (MediaWorkspaceView != null)
            {
                MediaWorkspaceView.ApplyResponsiveLayout(width, height);
            }

            if (TaskWorkspaceView != null)
            {
                TaskWorkspaceView.ApplyResponsiveLayout(width, height);
            }

            if (DiagnosticHealthPanel != null)
            {
                DiagnosticHealthPanel.Columns = width >= 1320 ? 4 : width >= 980 ? 2 : 1;
            }
            if (MaintenanceWorkspaceView != null)
            {
                MaintenanceWorkspaceView.ApplyResponsiveLayout(width, height);
            }
            SaveWorkspaceView?.ApplyResponsiveLayout(width, height);
            TrainerWorkspaceView?.ApplyResponsiveLayout(width, height);

            if (SaveHistoryCompactInspectorRow != null && SaveHistoryListPanel != null
                && SaveHistoryInspectorPanel != null && SaveHistoryListColumn != null
                && SaveHistoryGutterColumn != null && SaveHistoryInspectorColumn != null)
            {
                var stackSaveHistory = width < 1180;
                SaveHistoryCompactInspectorRow.Height = stackSaveHistory ? GridLength.Auto : new GridLength(0);
                SaveHistoryListColumn.Width = new GridLength(1.25, GridUnitType.Star);
                SaveHistoryGutterColumn.Width = new GridLength(stackSaveHistory ? 0 : 14);
                SaveHistoryInspectorColumn.Width = stackSaveHistory ? new GridLength(0) : new GridLength(0.75, GridUnitType.Star);
                Grid.SetRow(SaveHistoryListPanel, 0);
                Grid.SetColumn(SaveHistoryListPanel, 0);
                Grid.SetColumnSpan(SaveHistoryListPanel, stackSaveHistory ? 3 : 1);
                Grid.SetRow(SaveHistoryInspectorPanel, stackSaveHistory ? 1 : 0);
                Grid.SetColumn(SaveHistoryInspectorPanel, stackSaveHistory ? 0 : 2);
                Grid.SetColumnSpan(SaveHistoryInspectorPanel, stackSaveHistory ? 3 : 1);
                SaveHistoryInspectorPanel.Margin = stackSaveHistory
                    ? new Thickness(0, 14, 0, 0)
                    : new Thickness(0);
                SaveHistoryInspectorPanel.MaxHeight = stackSaveHistory
                    ? Math.Max(360, Math.Min(620, height * 0.7))
                    : double.PositiveInfinity;
            }

            if (MediaInspectorPanel != null && MediaInspectorCompactRow != null
                && MediaPreviewPanel != null && MediaMetadataPanel != null)
            {
                var stackMediaInspector = width < 1180;
                MediaInspectorCompactRow.Height = stackMediaInspector ? GridLength.Auto : new GridLength(0);
                Grid.SetColumnSpan(MediaPreviewPanel, stackMediaInspector ? 2 : 1);
                MediaPreviewPanel.Margin = stackMediaInspector
                    ? new Thickness(0, 0, 0, 12)
                    : new Thickness(0, 0, 12, 0);
                Grid.SetRow(MediaMetadataPanel, stackMediaInspector ? 1 : 0);
                Grid.SetColumn(MediaMetadataPanel, stackMediaInspector ? 0 : 1);
                Grid.SetColumnSpan(MediaMetadataPanel, stackMediaInspector ? 2 : 1);
            }

            if (TrainerToolsCompactRow != null && TrainerToolsListPanel != null
                && TrainerToolsInspectorPanel != null && TrainerToolsListColumn != null
                && TrainerToolsGutterColumn != null && TrainerToolsInspectorColumn != null)
            {
                var stackTrainerTools = width < 1180;
                TrainerToolsCompactRow.Height = stackTrainerTools ? GridLength.Auto : new GridLength(0);
                TrainerToolsListColumn.Width = stackTrainerTools
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(1.25, GridUnitType.Star);
                TrainerToolsGutterColumn.Width = new GridLength(stackTrainerTools ? 0 : 14);
                TrainerToolsInspectorColumn.Width = stackTrainerTools ? new GridLength(0) : new GridLength(0.9, GridUnitType.Star);
                Grid.SetRow(TrainerToolsListPanel, 2);
                Grid.SetColumn(TrainerToolsListPanel, 0);
                Grid.SetColumnSpan(TrainerToolsListPanel, stackTrainerTools ? 3 : 1);
                Grid.SetRow(TrainerToolsInspectorPanel, stackTrainerTools ? 3 : 2);
                Grid.SetColumn(TrainerToolsInspectorPanel, stackTrainerTools ? 0 : 2);
                Grid.SetColumnSpan(TrainerToolsInspectorPanel, stackTrainerTools ? 3 : 1);
            }

            if (TrainerCatalogCompactRow != null && TrainerCatalogResultsPanel != null
                && TrainerCatalogReleasesPanel != null && TrainerCatalogResultsColumn != null
                && TrainerCatalogGutterColumn != null && TrainerCatalogReleasesColumn != null)
            {
                var stackTrainerCatalog = width < 1180;
                TrainerCatalogCompactRow.Height = stackTrainerCatalog ? GridLength.Auto : new GridLength(0);
                TrainerCatalogResultsColumn.Width = new GridLength(1, GridUnitType.Star);
                TrainerCatalogGutterColumn.Width = new GridLength(stackTrainerCatalog ? 0 : 14);
                TrainerCatalogReleasesColumn.Width = stackTrainerCatalog ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
                Grid.SetRow(TrainerCatalogResultsPanel, 0);
                Grid.SetColumn(TrainerCatalogResultsPanel, 0);
                Grid.SetColumnSpan(TrainerCatalogResultsPanel, stackTrainerCatalog ? 3 : 1);
                Grid.SetRow(TrainerCatalogReleasesPanel, stackTrainerCatalog ? 1 : 0);
                Grid.SetColumn(TrainerCatalogReleasesPanel, stackTrainerCatalog ? 0 : 2);
                Grid.SetColumnSpan(TrainerCatalogReleasesPanel, stackTrainerCatalog ? 3 : 1);
                TrainerCatalogReleasesPanel.Margin = stackTrainerCatalog
                    ? new Thickness(0, 12, 0, 0)
                    : new Thickness(0);
            }

            if (MediaSourceFields != null)
            {
                MediaSourceFields.Columns = width < 980 ? 1 : 2;
            }

            if (DeviceDecisionFields != null)
            {
                DeviceDecisionFields.Columns = width < 980 ? 1 : width < 1280 ? 2 : 3;
            }

            RestoreSafetyBanner.Visibility = viewModel.CurrentWorkspace == WorkspaceKind.Saves && height >= 680
                ? Visibility.Visible : Visibility.Collapsed;
            if (viewModel.CurrentWorkspace != WorkspaceKind.Saves)
            {
                BackupPolicyPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SetToolbarLabelsVisible(bool visible)
        {
            if (TopRefreshLabel == null || TopBackupAllLabel == null || TopMediaSyncLabel == null
                || TopTrainerImportLabel == null || TopTrainerCatalogLabel == null
                || TopDiagnosticsLabel == null || ToggleGameBrowserLabel == null) return;

            var labelVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
            TopRefreshLabel.Visibility = labelVisibility;
            TopBackupAllLabel.Visibility = labelVisibility;
            TopMediaSyncLabel.Visibility = labelVisibility;
            TopTrainerImportLabel.Visibility = labelVisibility;
            TopTrainerCatalogLabel.Visibility = labelVisibility;
            TopDiagnosticsLabel.Visibility = labelVisibility;
            ToggleGameBrowserLabel.Visibility = labelVisibility;

            var width = visible ? double.NaN : 44;
            foreach (var button in new[]
            {
                TopRefreshButton, TopBackupAllButton, TopMediaSyncButton,
                TopTrainerImportButton, TopTrainerCatalogButton, TopDiagnosticsButton,
                ToggleGameBrowserButton
            })
            {
                button.Width = width;
                button.Padding = visible ? new Thickness(13, 7, 13, 7) : new Thickness(0);
            }
        }

        private async void OnRefreshTimerTick(object sender, EventArgs e)
        {
            if (viewModel == null) return;
            try
            {
                // DispatcherTimer invokes an async-void event boundary. The view-model normally
                // converts refresh failures into status text, but keep this final boundary
                // guarded so a future refresh path cannot tear down Playnite's Dispatcher.
                await viewModel.RequestBackgroundRefreshAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameSaveCenter background refresh timer failed.");
            }
        }


        private void OnClearTextBoxClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement source) || !(source.Tag is TextBox textBox)) return;
            textBox.Clear();
            textBox.Focus();
            Keyboard.Focus(textBox);
        }

        private void OnLoadUiFrameworkProbeClick(object sender, RoutedEventArgs e)
        {
            if (UiFrameworkProbeHost.Content != null)
            {
                UiFrameworkProbeHost.Visibility = Visibility.Visible;
                UiFrameworkProbeRecoveryPanel.Visibility = Visibility.Collapsed;
                return;
            }

            if (!uiFrameworkProbeLoader.TryCreate(CreateUiFrameworkProbe, out var probe, out var failure))
            {
                UiFrameworkProbeLoadFailureText.Text = failure;
                UiFrameworkProbeLoadFailurePanel.Visibility = Visibility.Visible;
                UiFrameworkProbeRecoveryPanel.Visibility = Visibility.Visible;
                UiFrameworkProbeHost.Visibility = Visibility.Collapsed;
                return;
            }

            UiFrameworkProbeHost.Content = probe;
            UiFrameworkProbeHost.Visibility = Visibility.Visible;
            UiFrameworkProbeRecoveryPanel.Visibility = Visibility.Collapsed;
        }

        private static UIElement CreateUiFrameworkProbe()
        {
            var probeType = typeof(DashboardView).Assembly.GetType(
                "GameSaveCenter.Playnite.Views.Development.UiFrameworkProbeView",
                throwOnError: true);
            var probe = Activator.CreateInstance(probeType!);
            return probe as UIElement
                ?? throw new InvalidOperationException("WPF-UI 探针未创建可显示的 WPF 控件。");
        }

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
            compactGameBrowserOpen = false;
            UpdateWorkspacePresentation();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
            viewModel.RequestWorkspaceLoad();
            AnimateElement(DetailsTabControl, 10, 0, 0.2);
        }

        private void UpdateWorkspacePresentation()
        {
            var workspace = viewModel.CurrentWorkspace;
            // Overview is now rendered by the extracted physical workspace. Keep the old
            // tab collapsed as a migration fallback until the remaining workspaces move out.
            SetVisibility(OverviewWorkspaceTab, workspace == WorkspaceKind.Overview);
            SetVisibility(OverviewTab, false);
            SetVisibility(SaveHistoryTab, workspace == WorkspaceKind.Saves);
            SetVisibility(CandidateTab, workspace == WorkspaceKind.Saves);
            SetVisibility(TrainerTab, workspace == WorkspaceKind.Trainers);
            SetVisibility(MediaWorkspaceTab, workspace == WorkspaceKind.Media);
            SetVisibility(MediaTab, false);
            SetVisibility(TaskWorkspaceTab, workspace == WorkspaceKind.Tasks);
            SetVisibility(TaskTab, false);
            SetVisibility(MaintenanceWorkspaceTab, workspace == WorkspaceKind.Maintenance);
            SetVisibility(SaveWorkspaceTab, workspace == WorkspaceKind.Saves);
            SetVisibility(TrainerWorkspaceTab, workspace == WorkspaceKind.Trainers);
            SetVisibility(SaveHistoryTab, false);
            SetVisibility(CandidateTab, false);
            SetVisibility(TrainerTab, false);
            SetVisibility(DiagnosticTab, false);
            SetVisibility(DeviceStatusTab, false);
            SetVisibility(LogsTab, false);
            SetVisibility(UiFrameworkProbeTab, false);

            var saves = workspace == WorkspaceKind.Saves;
            // Game-scoped pages need breathing room between the selected-game identity and
            // the first module pill. Save pages already have their safety banner in between.
            DetailsTabControl.Margin = workspace == WorkspaceKind.Trainers || workspace == WorkspaceKind.Media
                ? new Thickness(0, 12, 0, 0)
                : new Thickness(0);
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
            SidebarWorkerStatusText.Visibility = visibility;
            SidebarLudusaviStatusText.Visibility = visibility;
            SidebarWorkerCompactLabel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
            SidebarLudusaviCompactLabel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;

            SidebarStatusPanel.HorizontalAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            SidebarChrome.Padding = visible ? new Thickness(16) : new Thickness(11);
            SidebarBrandContainer.HorizontalAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            SidebarBrandIcon.Width = visible ? 44 : 46;
            SidebarBrandIcon.Height = visible ? 44 : 46;

            var navigationPadding = visible ? new Thickness(13, 10, 13, 10) : new Thickness(0);
            foreach (var item in new[] { NavOverview, NavSaves, NavTrainers, NavMedia, NavTasks, NavMaintenance })
            {
                item.Padding = navigationPadding;
                item.HorizontalAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
                item.HorizontalContentAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
                item.Width = visible ? double.NaN : 48;
                item.Height = visible ? double.NaN : 48;
                item.Margin = visible ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 0, 8);
            }

            ConfigureCompactStatusCard(SidebarWorkerStatusCard, SidebarWorkerStatusDot, visible);
            ConfigureCompactStatusCard(SidebarLudusaviStatusCard, SidebarLudusaviStatusDot, visible);
        }

        private static void ConfigureCompactStatusCard(Border card, Border dot, bool expanded)
        {
            card.Width = expanded ? double.NaN : 48;
            card.Height = expanded ? double.NaN : 50;
            card.MinHeight = expanded ? 58 : 50;
            card.Padding = expanded ? new Thickness(12, 10, 12, 10) : new Thickness(0);
            card.HorizontalAlignment = expanded ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

            if (expanded)
            {
                Grid.SetColumn(dot, 0);
                dot.HorizontalAlignment = HorizontalAlignment.Left;
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Margin = new Thickness(0, 0, 9, 0);
            }
            else
            {
                Grid.SetColumn(dot, 1);
                dot.HorizontalAlignment = HorizontalAlignment.Right;
                dot.VerticalAlignment = VerticalAlignment.Top;
                dot.Margin = new Thickness(0, 7, 7, 0);
            }
        }

        private void OnToggleGameBrowserClick(object sender, RoutedEventArgs e)
        {
            if (viewModel == null || viewModel.CurrentWorkspace == WorkspaceKind.Tasks || viewModel.CurrentWorkspace == WorkspaceKind.Maintenance) return;
            compactGameBrowserOpen = !compactGameBrowserOpen;
            var tooltip = compactGameBrowserOpen
                ? "关闭游戏搜索、状态筛选和排序"
                : "打开游戏搜索、状态筛选和排序";
            ToggleGameBrowserButton.ToolTip = tooltip;
            CompactGameSelector.ToolTip = tooltip;
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
            if (compactGameBrowserOpen && MotionEnabled)
                AnimateElement(GameBrowserPanel, 10, 0, 0.18);
            if (compactGameBrowserOpen && GameSearchTextBox != null)
            {
                BeginUiSafely(() => GameSearchTextBox.Focus(), DispatcherPriority.Background);
            }
        }

        private void OnGameSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && viewModel != null && e.AddedItems[0] is GamePickerItem pickerItem)
                viewModel.SelectedGame = pickerItem.Game;
        }

        private void OnGamePickerMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!compactGameBrowserOpen || viewModel == null || viewModel.SelectedGame == null) return;
            compactGameBrowserOpen = false;
            CompactGameSelector.ToolTip = "打开游戏搜索、状态筛选和排序";
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
        }

        private void OnGamePickerPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!compactGameBrowserOpen || viewModel == null) return;
            if (e.Key == Key.Escape || (e.Key == Key.Enter && viewModel.SelectedGame != null))
            {
                compactGameBrowserOpen = false;
                CompactGameSelector.ToolTip = "打开游戏搜索、状态筛选和排序";
                ApplyResponsiveLayout(ActualWidth, ActualHeight);
                e.Handled = true;
            }
        }

        private void OnInspectorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null || scrollViewer.ScrollableHeight <= 0) return;

            // Playnite themes can route the wheel to an outer host before nested inspectors
            // consume it. Move the finite inspector explicitly and mark the event handled.
            for (var index = 0; index < 3; index++)
            {
                if (e.Delta < 0) scrollViewer.LineDown();
                else scrollViewer.LineUp();
            }
            e.Handled = true;
        }

        private static void SetVisibility(UIElement element, bool visible)
            => element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        private void OnDetailsTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, DetailsTabControl)) return;
            AnimateElement(DetailsTabControl, 10, 0, 0.2);
        }

        private void OnTrainerCatalogSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || viewModel == null) return;
            if (viewModel.LoadTrainerReleasesCommand.CanExecute(null))
            {
                viewModel.LoadTrainerReleasesCommand.Execute(null);
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // PropertyChanged may originate from the Worker event pipe or another background
            // continuation. Do not read any View state until this View is back on its owner thread.
            if (!Dispatcher.CheckAccess())
            {
                BeginUiSafely(() => OnViewModelPropertyChanged(sender, e), DispatcherPriority.Background);
                return;
            }
            if (!IsLoaded) return;
            if (e.PropertyName == nameof(DashboardViewModel.SelectedGame) && !viewModel.IsBackgroundRefreshing)
            {
                BeginUiSafely(() => AnimateElement(GameDetailCard, 13, 0, 0.23), DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(DashboardViewModel.SelectedTask) && !viewModel.IsBackgroundRefreshing)
            {
                BeginUiSafely(() => AnimateElement(TaskWorkspaceView.TaskDetailCardElement, 8, 0, 0.2), DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(DashboardViewModel.StatusMessage))
            {
                BeginUiSafely(AnimateStatusPill, DispatcherPriority.Background);
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

        private void OnUiNotificationRequested(object? sender, UiNotificationEventArgs e)
        {
            if (!IsLoaded || !IsVisible) return;
            e.Handled = true;

            // Error notifications retain the existing details action and recovery path. Other
            // feedback uses WPF-UI first, with the local toast host as a non-destructive fallback.
            if (e.Kind == UiNotificationKind.Error || !TryShowFrameworkSnackbar(e))
                ShowToast(e.Title, e.Message, e.Kind);
        }

        private bool TryShowFrameworkSnackbar(UiNotificationEventArgs notification)
        {
            try
            {
                var snackbar = new Snackbar(SnackbarHost)
                {
                    Title = notification.Title,
                    Content = notification.Message,
                    Timeout = TimeSpan.FromSeconds(notification.Kind == UiNotificationKind.Warning ? 5 : 3.8),
                    IsCloseButtonEnabled = true
                };
                snackbar.Show();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameSaveCenter WPF-UI snackbar failed; using the local toast fallback.");
                return false;
            }
        }

        private void OnUiConfirmationRequested(object? sender, UiConfirmationEventArgs e)
        {
            if (!IsLoaded || !IsVisible) return;
            e.Handled = true;

            if (confirmationOpen)
            {
                // Never stack modal prompts inside Playnite. The caller receives a safe cancellation
                // and can expose the action again after the current decision is complete.
                e.Completion.TrySetResult(false);
                return;
            }

            _ = ShowFrameworkConfirmationAsync(e);
        }

        private Task ShowFrameworkConfirmationAsync(UiConfirmationEventArgs request)
        {
            confirmationOpen = true;
            activeConfirmation?.Completion.TrySetResult(false);
            activeConfirmation = request;
            try
            {
                // ContentDialogHost is a Window-wide singleton in WPF-UI. Dashboard and Settings
                // are both embedded in Playnite's shared Window, so use the existing in-plugin
                // modal surface rather than registering a competing WPF-UI host.
                ShowFallbackConfirmation(request);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GameSaveCenter embedded confirmation failed.");
                activeConfirmation = null;
                confirmationOpen = false;
                request.Completion.TrySetResult(false);
            }

            return Task.CompletedTask;
        }

        private void ShowFallbackConfirmation(UiConfirmationEventArgs request)
        {
            activeConfirmation = request;
            dialogShowsResult = false;
            DialogTitleText.Text = request.Title;
            DialogMessageText.Text = request.Message;
            DialogCancelButton.Content = request.CancelText;
            DialogCancelButton.Visibility = Visibility.Visible;
            DialogConfirmButton.Content = request.ConfirmText;
            DialogConfirmButton.SetResourceReference(Control.BackgroundProperty, request.IsDangerous ? "GscErrorBrush" : "GscAccentBrush");
            DialogConfirmButton.SetResourceReference(Control.BorderBrushProperty, request.IsDangerous ? "GscErrorBrush" : "GscAccentBrush");
            OpenDialog(request.IsDangerous ? DialogCancelButton : DialogConfirmButton);
        }

        private void ShowResultDialog(string title, string message)
        {
            activeConfirmation?.Completion.TrySetResult(false);
            activeConfirmation = null;
            confirmationOpen = true;
            dialogShowsResult = true;
            DialogTitleText.Text = title;
            DialogMessageText.Text = message;
            DialogCancelButton.Visibility = Visibility.Collapsed;
            DialogConfirmButton.Content = "关闭";
            DialogConfirmButton.SetResourceReference(Control.BackgroundProperty, "GscAccentBrush");
            DialogConfirmButton.SetResourceReference(Control.BorderBrushProperty, "GscAccentBrush");
            OpenDialog(DialogConfirmButton);
        }

        private void OpenDialog(Control initialFocus)
        {
            DialogOverlay.Visibility = Visibility.Visible;
            DialogCard.Opacity = MotionEnabled ? 0 : 1;
            var translate = GetMutableTranslateTransform(DialogCard);
            translate.Y = MotionEnabled ? 14 : 0;
            if (MotionEnabled)
            {
                var duration = TimeSpan.FromMilliseconds(210);
                var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
                DialogCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, duration) { EasingFunction = easing });
            }
            BeginUiSafely(() =>
            {
                if (IsLoaded && DialogOverlay.Visibility == Visibility.Visible) initialFocus.Focus();
            }, DispatcherPriority.Input);
        }

        private void OnDialogCancelClick(object sender, RoutedEventArgs e) => CompleteDialog(false);

        private void OnDialogConfirmClick(object sender, RoutedEventArgs e)
        {
            if (dialogShowsResult)
            {
                CloseDialog();
                return;
            }
            CompleteDialog(true);
        }

        private void CompleteDialog(bool result)
        {
            var completion = activeConfirmation?.Completion;
            activeConfirmation = null;
            CloseDialog();
            completion?.TrySetResult(result);
        }

        private void CloseDialog()
        {
            confirmationOpen = false;
            dialogShowsResult = false;
            DialogOverlay.Visibility = Visibility.Collapsed;
            DialogCard.BeginAnimation(OpacityProperty, null);
            DialogCard.Opacity = 0;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && compactGameBrowserOpen)
            {
                compactGameBrowserOpen = false;
                ApplyResponsiveLayout(ActualWidth, ActualHeight);
                e.Handled = true;
                return;
            }
            if (DialogOverlay.Visibility != Visibility.Visible) return;
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (dialogShowsResult) CloseDialog(); else CompleteDialog(false);
            }
        }

        private void ShowToast(string title, string message, UiNotificationKind kind)
        {
            var accentKey = kind == UiNotificationKind.Error ? "GscErrorBrush"
                : kind == UiNotificationKind.Warning ? "GscWarningBrush"
                : kind == UiNotificationKind.Success ? "GscSuccessBrush"
                : "GscInfoBrush";

            var card = new Border
            {
                CornerRadius = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 10, 12),
                Margin = new Thickness(0, 0, 0, 10),
                MaxWidth = 360,
                Opacity = MotionEnabled ? 0 : 1,
                RenderTransform = new TranslateTransform(MotionEnabled ? 18 : 0, 0)
            };
            card.SetResourceReference(Border.BackgroundProperty, "GscGlassStrongBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "GscGlassStrokeBrush");
            if (plugin.Settings.EnableGlassEffects && !SystemParameters.HighContrast)
            {
                card.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 5,
                    Opacity = 0.24
                };
            }

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var indicator = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 5, 0, 0) };
            indicator.SetResourceReference(Border.BackgroundProperty, accentKey);
            layout.Children.Add(indicator);

            var textPanel = new StackPanel { Margin = new Thickness(10, 0, 8, 0) };
            Grid.SetColumn(textPanel, 1);
            var titleText = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var messageText = new TextBlock { Text = message, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 72, TextTrimming = TextTrimming.CharacterEllipsis };
            messageText.SetResourceReference(TextBlock.ForegroundProperty, "GscSecondaryTextBrush");
            messageText.ToolTip = message;
            textPanel.Children.Add(titleText);
            textPanel.Children.Add(messageText);
            if (kind == UiNotificationKind.Error)
            {
                var details = new Button { Content = "查看详情", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 0), Padding = new Thickness(8, 4, 8, 4), MinHeight = 28 };
                details.Style = (Style)Resources["GscButtonBase"];
                details.Click += (_, __) => ShowResultDialog(title, message);
                textPanel.Children.Add(details);
            }
            layout.Children.Add(textPanel);

            var close = new Button { Content = "×", Width = 28, Height = 28, MinHeight = 28, Padding = new Thickness(0), Margin = new Thickness(2, -3, -2, 0), VerticalAlignment = VerticalAlignment.Top };
            close.Style = (Style)Resources["GscButtonBase"];
            Grid.SetColumn(close, 2);
            layout.Children.Add(close);
            card.Child = layout;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(kind == UiNotificationKind.Error ? 7 : 3.8) };
            toastTimers[card] = timer;
            Action dismiss = () => DismissToast(card, timer);
            timer.Tick += (_, __) => dismiss();
            close.Click += (_, __) => dismiss();
            card.MouseEnter += (_, __) => timer.Stop();
            card.MouseLeave += (_, __) => timer.Start();
            ToastHost.Children.Insert(0, card);
            while (ToastHost.Children.Count > 4 && ToastHost.Children[ToastHost.Children.Count - 1] is Border expired)
            {
                RemoveToast(expired);
            }
            timer.Start();

            if (MotionEnabled)
            {
                var duration = TimeSpan.FromMilliseconds(230);
                var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
                card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
                ((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(18, 0, duration) { EasingFunction = easing });
            }
        }

        private void DismissToast(Border card, DispatcherTimer timer)
        {
            StopToastTimer(card, timer);
            if (!ToastHost.Children.Contains(card)) return;
            if (!MotionEnabled)
            {
                RemoveToast(card);
                return;
            }

            var duration = TimeSpan.FromMilliseconds(180);
            var fade = new DoubleAnimation(card.Opacity, 0, duration);
            fade.Completed += (_, __) => RemoveToast(card);
            card.BeginAnimation(OpacityProperty, fade);
            var translate = card.RenderTransform as TranslateTransform ?? new TranslateTransform();
            card.RenderTransform = translate;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 16, duration));
        }

        private void ClearToasts()
        {
            foreach (var timer in toastTimers.Values) timer.Stop();
            toastTimers.Clear();
            ToastHost.Children.Clear();
        }

        private void StopToastTimer(Border card, DispatcherTimer? expectedTimer = null)
        {
            if (!toastTimers.TryGetValue(card, out var timer))
            {
                expectedTimer?.Stop();
                return;
            }

            if (expectedTimer != null && !ReferenceEquals(timer, expectedTimer)) return;
            timer.Stop();
            toastTimers.Remove(card);
        }

        private void RemoveToast(Border card)
        {
            StopToastTimer(card);
            card.BeginAnimation(OpacityProperty, null);
            if (card.RenderTransform is TranslateTransform translate)
            {
                translate.BeginAnimation(TranslateTransform.XProperty, null);
            }
            ToastHost.Children.Remove(card);
        }

        private void ApplyAdaptiveTheme()
        {
            var glassEnabled = plugin.Settings.EnableGlassEffects && !SystemParameters.HighContrast;
            var palette = AdaptiveThemePaletteFactory.Create(this, glassEnabled, plugin.Settings.GlassEffectStrength, plugin.Settings.ThemeMode);

            AdaptiveThemePaletteFactory.ApplyAccentResources(Resources, palette);
            AdaptiveThemePaletteFactory.ApplyMaterialResources(Resources, palette, glassEnabled, MotionEnabled);
            AdaptiveThemePaletteFactory.ApplyWpfUiResources(Resources, palette);
            Resources["GscPrimaryTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.PrimaryText);
            Resources["GscSecondaryTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.SecondaryText);
            Resources["GscMutedTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.MutedText);
            Resources["GscDisabledTextBrush"] = AdaptiveThemePaletteFactory.Brush(palette.DisabledText);
            Resources["GscControlFillBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlFill);
            Resources["GscControlStrokeBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["GscDividerBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Divider);
            Resources["GscTableDividerBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                palette.IsDark ? (byte)24 : (byte)18, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            Resources["GscPopupBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                250, palette.StrongSurfaceTop.R, palette.StrongSurfaceTop.G, palette.StrongSurfaceTop.B));
            Resources["GscGlassFillBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.SurfaceTop, palette.SurfaceBottom);
            Resources["GscGlassStrongBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.StrongSurfaceTop, palette.StrongSurfaceBottom);
            Resources["GscSidebarBrush"] = AdaptiveThemePaletteFactory.Gradient(palette.SidebarTop, palette.SidebarBottom);
            Resources["GscGlassStrokeBrush"] = AdaptiveThemePaletteFactory.Brush(palette.ControlStroke);
            Resources["GscGlassHighlightBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Highlight);
            Resources["GscBackdropBrush"] = AdaptiveThemePaletteFactory.Brush(palette.Backdrop);
            Resources["GscTableHeaderBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                palette.IsDark ? (byte)22 : (byte)12, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            Resources["GscRowHoverBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                palette.IsDark ? (byte)18 : (byte)10, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            Resources["GscScrollTrackBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                palette.IsDark ? (byte)28 : (byte)20, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            Resources["GscScrollThumbBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                palette.IsDark ? (byte)88 : (byte)68, palette.PrimaryText.R, palette.PrimaryText.G, palette.PrimaryText.B));
            Resources["GscScrollThumbHoverBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                166, 124, 92, 252));
            Resources["GscOverlayBrush"] = AdaptiveThemePaletteFactory.Brush(Color.FromArgb(
                palette.IsDark ? (byte)138 : (byte)72, 0, 0, 0));
            WpfUiThemeScope.Apply(Resources, palette.IsDark);

            // The ambient ellipses are the only fixed BlurEffect surfaces in the dashboard.
            // Collapse them instead of merely making them transparent so reduced-transparency
            // and high-contrast modes do not retain an unnecessary effect visual tree.
            AmbientGlowLayer.Visibility = glassEnabled ? Visibility.Visible : Visibility.Collapsed;
            AmbientGlowLayer.Opacity = glassEnabled
                ? (palette.IsDark ? 0.46 : 0.56) * Math.Max(0.2, Math.Min(1, plugin.Settings.GlassEffectStrength / 100d))
                : 0;
        }
    }
}

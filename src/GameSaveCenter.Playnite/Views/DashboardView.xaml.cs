using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GameSaveCenter.Playnite.Infrastructure;
using GameSaveCenter.Playnite.ViewModels;
using Playnite.SDK;

namespace GameSaveCenter.Playnite.Views
{
    public partial class DashboardView : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly GameSaveCenterPlugin plugin;
        private readonly DispatcherTimer refreshTimer;
        private readonly UiFrameworkProbeLoader uiFrameworkProbeLoader;
        private DashboardViewModel viewModel;
        private bool hasPlayedEntrance;
        private bool visualSettingsSubscribed;
        private bool uiFeedbackSubscribed;
        private UiConfirmationEventArgs? activeConfirmation;
        private bool dialogShowsResult;

        public DashboardView(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            InitializeComponent();
            uiFrameworkProbeLoader = new UiFrameworkProbeLoader(exception => Logger.Error(exception, "GameSaveCenter WPF-UI probe could not be loaded."));

            viewModel = new DashboardViewModel(plugin);
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.AttentionCenterRequested += OnAttentionCenterRequested;
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
            if (!visualSettingsSubscribed)
            {
                plugin.VisualSettingsChanged += OnVisualSettingsChanged;
                visualSettingsSubscribed = true;
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
            viewModel.StopTaskEventSubscription();
            if (visualSettingsSubscribed)
            {
                plugin.VisualSettingsChanged -= OnVisualSettingsChanged;
                visualSettingsSubscribed = false;
            }
            if (uiFeedbackSubscribed)
            {
                plugin.UiNotificationRequested -= OnUiNotificationRequested;
                plugin.UiConfirmationRequested -= OnUiConfirmationRequested;
                uiFeedbackSubscribed = false;
            }
            activeConfirmation?.Completion.TrySetResult(false);
            activeConfirmation = null;
            DialogOverlay.Visibility = Visibility.Collapsed;
            ToastHost.Children.Clear();
        }

        private void OnAttentionCenterRequested(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                NavMaintenance.IsChecked = true;
                UpdateWorkspacePresentation();
                DetailsTabControl.SelectedItem = LogsTab;
                FindingsGrid.ScrollIntoView(viewModel.SelectedFinding);
                FindingsGrid.Focus();
                AnimateElement(DetailsTabControl, 10, 0, 0.2);
            }), DispatcherPriority.Background);
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

            var mode = width >= 1280 ? LayoutMode.Expanded
                : width >= 980 ? LayoutMode.Standard
                : width >= 880 ? LayoutMode.Compact
                : LayoutMode.Narrow;
            viewModel.LayoutMode = mode;
            var iconSidebar = mode != LayoutMode.Expanded;
            var gameScopedWorkspace = viewModel.CurrentWorkspace == WorkspaceKind.Saves
                || viewModel.CurrentWorkspace == WorkspaceKind.Trainers
                || viewModel.CurrentWorkspace == WorkspaceKind.Media;
            var showGameBrowser = gameScopedWorkspace && (mode == LayoutMode.Expanded || mode == LayoutMode.Standard);

            SidebarColumn.Width = new GridLength(iconSidebar ? (mode == LayoutMode.Narrow ? 68 : 72) : 220);
            SidebarGutterColumn.Width = new GridLength(iconSidebar ? 10 : 18);
            TopChromeSafetyColumn.Width = new GridLength(width < 980 ? 76 : 96);
            ToastHost.Margin = new Thickness(0, height < 760 ? 66 : 78, width < 980 ? 12 : 22, 0);
            SetSidebarLabelsVisible(!iconSidebar);
            SetToolbarLabelsVisible(mode == LayoutMode.Expanded);

            GameBrowserPanel.Visibility = showGameBrowser ? Visibility.Visible : Visibility.Collapsed;
            WorkspaceGutterColumn.Width = new GridLength(showGameBrowser ? 14 : 0);
            GameListColumn.Width = showGameBrowser
                ? new GridLength(mode == LayoutMode.Expanded ? 330 : 280)
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

        private void SetToolbarLabelsVisible(bool visible)
        {
            if (TopRefreshLabel == null || TopBackupAllLabel == null || TopMediaSyncLabel == null) return;
            var labelVisibility = visible ? Visibility.Visible : Visibility.Collapsed;
            TopRefreshLabel.Visibility = labelVisibility;
            TopBackupAllLabel.Visibility = labelVisibility;
            TopMediaSyncLabel.Visibility = labelVisibility;
        }

        private async void OnRefreshTimerTick(object sender, EventArgs e)
        {
            if (viewModel == null) return;
            await viewModel.RequestBackgroundRefreshAsync();
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
            UpdateWorkspacePresentation();
            ApplyResponsiveLayout(ActualWidth, ActualHeight);
            viewModel.RequestWorkspaceLoad();
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
            SetVisibility(DeviceStatusTab, workspace == WorkspaceKind.Maintenance);
            SetVisibility(LogsTab, workspace == WorkspaceKind.Maintenance);
            SetVisibility(UiFrameworkProbeTab, workspace == WorkspaceKind.Maintenance);

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
            SidebarStatusPanel.HorizontalAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            SidebarChrome.Padding = visible ? new Thickness(15) : new Thickness(10);
            SidebarBrandContainer.HorizontalAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            SidebarBrandIcon.Width = visible ? 42 : 44;
            SidebarBrandIcon.Height = visible ? 42 : 44;

            var navigationPadding = visible ? new Thickness(13, 10, 13, 10) : new Thickness(7, 10, 7, 10);
            NavOverview.Padding = navigationPadding;
            NavSaves.Padding = navigationPadding;
            NavTrainers.Padding = navigationPadding;
            NavMedia.Padding = navigationPadding;
            NavTasks.Padding = navigationPadding;
            NavMaintenance.Padding = navigationPadding;
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
                Dispatcher.BeginInvoke(new Action(() => OnViewModelPropertyChanged(sender, e)), DispatcherPriority.Background);
                return;
            }
            if (!IsLoaded) return;
            if (e.PropertyName == nameof(DashboardViewModel.SelectedGame) && !viewModel.IsBackgroundRefreshing)
            {
                Dispatcher.BeginInvoke(new Action(() => AnimateElement(GameDetailCard, 13, 0, 0.23)), DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(DashboardViewModel.SelectedTask) && !viewModel.IsBackgroundRefreshing)
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

        private void OnUiNotificationRequested(object? sender, UiNotificationEventArgs e)
        {
            if (!IsLoaded || !IsVisible) return;
            e.Handled = true;
            ShowToast(e.Title, e.Message, e.Kind);
        }

        private void OnUiConfirmationRequested(object? sender, UiConfirmationEventArgs e)
        {
            if (!IsLoaded || !IsVisible) return;
            e.Handled = true;
            ShowConfirmation(e);
        }

        private void ShowConfirmation(UiConfirmationEventArgs request)
        {
            activeConfirmation?.Completion.TrySetResult(false);
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
            Dispatcher.BeginInvoke(new Action(() => initialFocus.Focus()), DispatcherPriority.Input);
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
            dialogShowsResult = false;
            DialogOverlay.Visibility = Visibility.Collapsed;
            DialogCard.BeginAnimation(OpacityProperty, null);
            DialogCard.Opacity = 0;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
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
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 22,
                ShadowDepth = 5,
                Opacity = 0.24
            };

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
            ToastHost.Children.Insert(0, card);
            while (ToastHost.Children.Count > 4) ToastHost.Children.RemoveAt(ToastHost.Children.Count - 1);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(kind == UiNotificationKind.Error ? 7 : 3.8) };
            Action dismiss = () => DismissToast(card, timer);
            timer.Tick += (_, __) => dismiss();
            close.Click += (_, __) => dismiss();
            card.MouseEnter += (_, __) => timer.Stop();
            card.MouseLeave += (_, __) => timer.Start();
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
            timer.Stop();
            if (!ToastHost.Children.Contains(card)) return;
            if (!MotionEnabled)
            {
                ToastHost.Children.Remove(card);
                return;
            }

            var duration = TimeSpan.FromMilliseconds(180);
            var fade = new DoubleAnimation(card.Opacity, 0, duration);
            fade.Completed += (_, __) => ToastHost.Children.Remove(card);
            card.BeginAnimation(OpacityProperty, fade);
            var translate = card.RenderTransform as TranslateTransform ?? new TranslateTransform();
            card.RenderTransform = translate;
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 16, duration));
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

            AmbientGlowLayer.Opacity = glassEnabled
                ? (palette.IsDark ? 0.46 : 0.56) * Math.Max(0.2, Math.Min(1, plugin.Settings.GlassEffectStrength / 100d))
                : 0;
        }
    }
}

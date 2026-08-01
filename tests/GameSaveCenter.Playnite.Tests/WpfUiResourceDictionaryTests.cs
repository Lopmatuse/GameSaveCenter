using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using GameSaveCenter.Playnite.Settings;
using GameSaveCenter.Playnite.Views;
using Wpf.Ui.Controls;
using Xunit;

namespace GameSaveCenter.Playnite.Tests;

public sealed class WpfUiResourceDictionaryTests
{
    [Fact]
    public void LocalAccentTokensFollowTheHostPaletteWithoutStaticThemeCapture()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var host = new System.Windows.Controls.Border();
                var hostAccent = Color.FromRgb(84, 61, 190);
                host.Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(248, 249, 252));
                host.Resources["TextBrush"] = new SolidColorBrush(Colors.Black);
                host.Resources["TextBrushDark"] = new SolidColorBrush(Colors.White);
                host.Resources["HighlightGlyphBrush"] = new SolidColorBrush(hostAccent);

                var factoryType = typeof(DashboardView).Assembly.GetType(
                    "GameSaveCenter.Playnite.Infrastructure.AdaptiveThemePaletteFactory",
                    throwOnError: true)!;
                var palette = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!.Invoke(
                    null,
                    new object[] { host, true, 78, GameSaveCenterThemeMode.FollowPlaynite })!;

                Assert.Equal(hostAccent, (Color)palette.GetType().GetProperty("Accent")!.GetValue(palette)!);
                Assert.Equal(Colors.White, (Color)palette.GetType().GetProperty("OnAccentText")!.GetValue(palette)!);
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);

        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));
        Assert.Contains("AdaptiveThemePaletteFactory.ApplyAccentResources(Resources, palette)", dashboardCode);
        Assert.Contains("AdaptiveThemePaletteFactory.ApplyAccentResources(Resources, palette)", settingsCode);

        foreach (var xamlPath in new[]
                 {
                     Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"),
                     Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"),
                     Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml")
                 })
        {
            var xaml = File.ReadAllText(xamlPath);
            Assert.DoesNotContain("{StaticResource GscAccentBrush}", xaml);
            Assert.DoesNotContain("{StaticResource GscAccentTintBrush}", xaml);
            Assert.DoesNotContain("{StaticResource GscAccentTintStrongBrush}", xaml);
            Assert.DoesNotContain("{StaticResource GscPrimaryButtonBrush}", xaml);
            Assert.Contains("{DynamicResource GscAccentBrush}", xaml);
        }

        var paletteSource = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "AdaptiveThemePalette.cs"));
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));
        Assert.Contains("resources[\"GscAmbientAccentBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscAccentShadowColor\"]", paletteSource);
        Assert.Contains("resources[\"GscSelectionTextBrush\"]", paletteSource);
        Assert.Contains("highContrast ? accent", paletteSource);
        Assert.DoesNotContain("{StaticResource GscAccentShadowColor}", dashboard);
        Assert.DoesNotContain("{StaticResource GscAccentShadowColor}", tokens);
        Assert.Contains("{DynamicResource GscAmbientAccentBrush}", dashboard);
        Assert.Contains("{DynamicResource GscSelectionTextBrush}", dashboard);
        Assert.Contains("{DynamicResource GscSelectionTextBrush}", tokens);
    }

    [Fact]
    public void ProductionAdaptersResolveWpfUiButtonDefaultsInsideTheirOwnParseScope()
    {
        Exception? exception = null;
        ResourceDictionary? resources = null;

        var thread = new Thread(() =>
        {
            try
            {
                resources = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/DesignTokens.xaml""/>
        <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/WpfUiProduction.xaml""/>
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>");
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
        var buttonStyle = Assert.IsType<Style>(resources!["GscWpfUiButton"]);
        Assert.Equal(typeof(Button), buttonStyle.TargetType);
    }

    [Fact]
    public void ProductionAdaptersResolveGameSaveCenterTokensFromTheUserControlScope()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var host = (System.Windows.Controls.UserControl)XamlReader.Parse(@"
<UserControl xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
             xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
             xmlns:ui=""http://schemas.lepo.co/wpfui/2022/xaml"">
    <UserControl.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/DesignTokens.xaml""/>
                <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/WpfUiProduction.xaml""/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </UserControl.Resources>
    <StackPanel>
        <ui:Card Style=""{StaticResource GscWpfUiCard}""/>
        <ui:Button Style=""{StaticResource GscWpfUiButton}"" Content=""测试""/>
        <ui:ToggleSwitch Style=""{StaticResource GscWpfUiToggleSwitch}"" Content=""测试""/>
        <TextBox Style=""{StaticResource GscWpfUiTextBox}"" Text=""测试""/>
        <ComboBox Style=""{StaticResource GscWpfUiComboBox}""/>
    </StackPanel>
</UserControl>");

                host.Measure(new Size(1024, 768));
                host.Arrange(new Rect(0, 0, 1024, 768));
                host.UpdateLayout();
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void EmbeddedPlayniteViewsDoNotRegisterWindowScopedContentDialogHosts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginSourceDirectory = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite");
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var probe = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "Development", "UiFrameworkProbeView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.DoesNotContain("<ui:ContentDialogHost", dashboard);
        Assert.DoesNotContain("<ui:ContentDialogHost", settings);
        Assert.DoesNotContain("<ui:ContentDialogHost", probe);
        Assert.DoesNotContain("new ContentDialog(", dashboardCode);
        Assert.DoesNotContain("new ContentDialog(", settingsCode);
        Assert.Contains("ShowFallbackConfirmation", dashboardCode);
        Assert.Contains("MessageBox.Show", settingsCode);

        foreach (var xamlPath in Directory.GetFiles(pluginSourceDirectory, "*.xaml", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("<ui:ContentDialogHost", File.ReadAllText(xamlPath));
        }

        foreach (var sourcePath in Directory.GetFiles(pluginSourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("new ContentDialog(", File.ReadAllText(sourcePath));
        }
    }

    [Fact]
    public void FixedAmbientBlurLayersUseTheOpaqueAccessibilityFallback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("AmbientGlowLayer.Visibility = glassEnabled ? Visibility.Visible : Visibility.Collapsed", dashboardCode);
        Assert.Contains("SettingsAmbientLayer.Visibility = glassEnabled ? Visibility.Visible : Visibility.Collapsed", settingsCode);
        Assert.Contains("&& !SystemParameters.HighContrast", settingsCode);
    }

    [Fact]
    public void DashboardAnimationsCloneFrozenTransformsBeforeTheyAreAnimated()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var translateElement = new System.Windows.Controls.Border { RenderTransform = new TranslateTransform(2, 3) };
                var frozenTranslate = (TranslateTransform)translateElement.RenderTransform;
                frozenTranslate.Freeze();

                var scaleElement = new System.Windows.Controls.Border { RenderTransform = new ScaleTransform(1, 1) };
                var frozenScale = (ScaleTransform)scaleElement.RenderTransform;
                frozenScale.Freeze();

                var translateMethod = typeof(DashboardView).GetMethod(
                    "GetMutableTranslateTransform",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var scaleMethod = typeof(DashboardView).GetMethod(
                    "GetMutableScaleTransform",
                    BindingFlags.Static | BindingFlags.NonPublic);

                var mutableTranslate = Assert.IsType<TranslateTransform>(translateMethod!.Invoke(null, new object[] { translateElement }));
                var mutableScale = Assert.IsType<ScaleTransform>(scaleMethod!.Invoke(null, new object[] { scaleElement }));

                Assert.NotSame(frozenTranslate, mutableTranslate);
                Assert.NotSame(frozenScale, mutableScale);
                Assert.False(mutableTranslate.IsFrozen);
                Assert.False(mutableScale.IsFrozen);
                Assert.Same(mutableTranslate, translateElement.RenderTransform);
                Assert.Same(mutableScale, scaleElement.RenderTransform);
            }
            catch (Exception caught)
            {
                exception = caught;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    [Fact]
    public void SaveWorkspaceKeepsAllPrimaryCommandsReachableAtHighDpi()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));

        Assert.Contains("x:Name=\"GameHeaderActions\"", dashboard);
        Assert.Contains("<WrapPanel x:Name=\"GameHeaderActions\"", dashboard);
        Assert.Contains("Command=\"{Binding BackupSelectedCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding ValidateCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding DetectPathsCommand}\"", dashboard);
        Assert.Contains("Click=\"OnTogglePolicy\"", dashboard);
        Assert.Contains("Header=\"时间\" Binding=\"{Binding CreatedLocal", dashboard);
    }

    [Fact]
    public void TaskWorkspaceKeepsRecoveryActionsReachableWhenDetailsWrap()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));

        Assert.Contains("x:Name=\"TaskFiltersPanel\"", dashboard);
        Assert.Contains("<WrapPanel x:Name=\"TaskDetailActions\"", dashboard);
        Assert.Contains("Command=\"{Binding CopyTaskErrorCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding RetryTaskCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding CancelTaskCommand}\"", dashboard);
        Assert.Contains("筛选不会取消、重排或重新执行后台任务", dashboard);
    }

    [Fact]
    public void MaintenanceWorkspaceReflowsHealthCardsAndMappingEditor()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"DiagnosticHealthPanel\"", dashboard);
        Assert.Contains("x:Name=\"ProcessMappingEditor\"", dashboard);
        Assert.Contains("MinWidth=\"220\"", dashboard);
        Assert.Contains("Command=\"{Binding SaveProcessMappingCommand}\"", dashboard);
        Assert.Contains("DiagnosticHealthPanel.Columns = width >= 1280 ? 3 : width >= 980 ? 2 : 1", dashboardCode);
    }

    [Fact]
    public void MediaInspectorStacksBeforeItsEditingControlsAreCompressed()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"MediaInspectorPanel\"", dashboard);
        Assert.Contains("x:Name=\"MediaPreviewPanel\"", dashboard);
        Assert.Contains("x:Name=\"MediaMetadataPanel\"", dashboard);
        Assert.Contains("EnableRowVirtualization=\"True\"", dashboard);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", dashboard);
        Assert.Contains("Text=\"{Binding MediaSummary.TotalSizeDisplay, Mode=OneWay}\"", dashboard);
        Assert.DoesNotContain("MediaSummary.TotalSizeDisplay, Mode=TwoWay", dashboard);
        Assert.Contains("var stackMediaInspector = width < 1180", dashboardCode);
        Assert.Contains("Grid.SetRow(MediaMetadataPanel, stackMediaInspector ? 1 : 0)", dashboardCode);
    }

    [Fact]
    public void TrainerWorkspaceStacksVirtualizedPanesBeforeTheirControlsBecomeUnreadable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"TrainerToolsPanel\"", dashboard);
        Assert.Contains("x:Name=\"TrainerToolsListPanel\"", dashboard);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", dashboard);
        Assert.Contains("x:Name=\"TrainerCatalogPanel\"", dashboard);
        Assert.Contains("x:Name=\"TrainerCatalogResultsPanel\"", dashboard);
        Assert.Contains("x:Name=\"TrainerCatalogReleasesPanel\"", dashboard);
        Assert.Contains("var stackTrainerTools = width < 1180", codeBehind);
        Assert.Contains("var stackTrainerCatalog = width < 1180", codeBehind);
        Assert.Contains("Grid.SetRow(TrainerCatalogReleasesPanel, stackTrainerCatalog ? 1 : 0)", codeBehind);
    }

    [Fact]
    public void DashboardLargeScrollableControlsStayInsideFiniteGridLayouts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = XDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml")));
        var largeControls = dashboard.Descendants()
            .Where(element => element.Name.LocalName is "DataGrid" or "ListBox")
            .ToList();

        Assert.NotEmpty(largeControls);
        foreach (var control in largeControls)
        {
            // Item templates may legitimately use a StackPanel for two lines of text, but a
            // scrolling control itself must not inherit infinite height from a StackPanel or an
            // outer ScrollViewer. Its direct layout path must retain a finite Grid measurement.
            Assert.DoesNotContain(control.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
            Assert.DoesNotContain(control.Ancestors(), ancestor => ancestor.Name.LocalName == "ScrollViewer");
            Assert.Contains(control.Ancestors(), ancestor => ancestor.Name.LocalName == "Grid");

            if (control.Name.LocalName == "DataGrid")
            {
                Assert.Equal("{StaticResource GscDataGrid}", control.Attribute("Style")?.Value);
                continue;
            }

            Assert.Equal("True", control.Attribute("VirtualizingPanel.IsVirtualizing")?.Value);
            Assert.Equal("Recycling", control.Attribute("VirtualizingPanel.VirtualizationMode")?.Value);
            Assert.Equal("True", control.Attribute("ScrollViewer.CanContentScroll")?.Value);
        }
    }

    [Fact]
    public void MediaSourcesKeepPathEditingAndSafetyCommandsReachableInCompactLayouts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"MediaSourceFields\" Columns=\"2\"", dashboard);
        Assert.Contains("Command=\"{Binding AddMediaSourceCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding DataContext.UpdateMediaSourceCommand", dashboard);
        Assert.Contains("Command=\"{Binding DataContext.DeleteMediaSourceCommand", dashboard);
        Assert.Contains("<ItemsPanelTemplate><StackPanel/></ItemsPanelTemplate>", dashboard);
        Assert.Contains("MediaSourceFields.Columns = width < 980 ? 1 : 2", codeBehind);
    }

    [Fact]
    public void DeviceDecisionsPreserveProtectedRecoveryAndReadableCompactFields()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"DeviceDecisionFields\" Columns=\"3\"", dashboard);
        Assert.Contains("Command=\"{Binding SaveDeviceDecisionCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding StageRemoteBackupCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding RestoreStagedRemoteBackupCommand}\"", dashboard);
        Assert.Contains("不会执行远端操作或删除远端内容", dashboard);
        Assert.Contains("DeviceDecisionFields.Columns = width < 980 ? 1 : width < 1280 ? 2 : 3", codeBehind);
    }

    [Fact]
    public void EveryDashboardViewModelCommandRemainsReachableFromTheRedesignedDashboard()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var commands = Regex.Matches(viewModel, @"public ICommand (?<name>[A-Za-z0-9_]+Command) \{ get; \}");

        Assert.NotEmpty(commands);
        foreach (Match match in commands)
        {
            var command = match.Groups["name"].Value;
            Assert.True(
                dashboard.Contains("Command=\"{Binding " + command)
                || dashboard.Contains("Command=\"{Binding DataContext." + command),
                "重构后的 Dashboard 缺少可访问命令入口：" + command);
        }
    }

    [Fact]
    public void DeferredDashboardCallbacksAreProtectedDuringPlayniteUnload()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("private void BeginUiSafely(Action action, DispatcherPriority priority)", dashboardCode);
        Assert.Contains("Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished", dashboardCode);
        Assert.Contains("catch (InvalidOperationException ex)", dashboardCode);
        Assert.Contains("BeginUiSafely(() => OnViewModelPropertyChanged(sender, e)", dashboardCode);
        Assert.Contains("BeginUiSafely(PlayEntranceAnimation, DispatcherPriority.Loaded)", dashboardCode);
        Assert.Contains("if (!IsLoaded) return;", dashboardCode);
        Assert.Contains("private void BeginUiSafely(Action action, DispatcherPriority priority)", settingsCode);
        Assert.Contains("Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished", settingsCode);
        Assert.Contains("BeginUiSafely(ApplyAdaptiveTheme, DispatcherPriority.Background)", settingsCode);
    }

    [Fact]
    public void AsyncUiEventBoundariesDoNotLeakFailuresIntoThePlayniteDispatcher()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));

        // DispatcherTimer is an async-void WPF event boundary, so it must have a final catch even
        // though the current view-model refresh implementation also reports its own failures.
        Assert.Contains("private async void OnRefreshTimerTick", dashboardCode);
        Assert.Contains("background refresh timer failed", dashboardCode);

        // RelayCommand accepts an Action, therefore cancellation must be fire-and-forget only
        // through a Task that guards confirmation, Worker IPC, and the final refresh.
        Assert.Contains("_ = CancelSelectedTaskAsync()", viewModelCode);
        Assert.Contains("private async Task CancelSelectedTaskAsync()", viewModelCode);
        Assert.DoesNotContain("private async void CancelSelectedTask()", viewModelCode);
        Assert.Contains("catch (Exception ex)", viewModelCode);
        Assert.DoesNotContain("private async void Run(Func<Task> action)", viewModelCode);
        Assert.Contains("private async Task RunAsync(Func<Task> action)", viewModelCode);
        Assert.Contains("Observe(RunAsync(action))", viewModelCode);
        Assert.Contains("TaskContinuationOptions.OnlyOnFaulted", viewModelCode);
        Assert.Contains("failed to present dashboard command error", viewModelCode);
    }

    [Fact]
    public void SettingsStoragePolicyFieldsUseASafeCompactSingleColumnLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("x:Name=\"StoragePolicyFields\" Columns=\"2\"", settings);
        Assert.Contains("Path=\"FullBackupLimit\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"DifferentialBackupLimit\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"CompressionLevel\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("StoragePolicyFields.Columns = compact ? 1 : 2", settingsCode);
    }

    [Fact]
    public void SettingsUsesSharedResponsiveFieldGroupsWithoutShrinkingNumericInputs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("x:Name=\"CoreToolFields\" Columns=\"2\"", settings);
        Assert.Contains("x:Name=\"AppearanceFields\" Columns=\"2\"", settings);
        Assert.Contains("x:Name=\"AutomationIntervalFields\" Columns=\"3\"", settings);
        Assert.Contains("Path=\"DefaultBackupIntervalMinutes\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"ProcessPollingSeconds\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"DashboardRefreshSeconds\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("CoreToolFields.Columns = compact ? 1 : 2", settingsCode);
        Assert.Contains("AppearanceFields.Columns = compact ? 1 : 2", settingsCode);
        Assert.Contains("AutomationIntervalFields.Columns = compact ? 1 : width < 950 ? 2 : 3", settingsCode);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var initialDirectory in new[]
                 {
                     new DirectoryInfo(Directory.GetCurrentDirectory()),
                     new DirectoryInfo(AppContext.BaseDirectory)
                 })
        {
            for (DirectoryInfo? directory = initialDirectory; directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "GameSaveCenter.sln")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GameSaveCenter repository root for the WPF host regression test.");
    }
}

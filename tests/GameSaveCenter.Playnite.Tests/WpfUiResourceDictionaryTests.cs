using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using GameSaveCenter.Playnite.Settings;
using GameSaveCenter.Playnite.Views;
using GameSaveCenter.Playnite.Controls;
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

                var localResources = new ResourceDictionary();
                factoryType.GetMethod("ApplyAccentResources", BindingFlags.Public | BindingFlags.Static)!.Invoke(
                    null,
                    new object[] { localResources, palette });
                Assert.IsType<SolidColorBrush>(localResources["GscErrorTintBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscRestoreInfoFillBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscRestoreInfoStrokeBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscSafetyFillBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscSafetyStrokeBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscAmbientInfoBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscAmbientSuccessBrush"]);
                Assert.IsType<SolidColorBrush>(localResources["GscMutedStatusBrush"]);
                factoryType.GetMethod("ApplyWpfUiResources", BindingFlags.Public | BindingFlags.Static)!.Invoke(
                    null,
                    new object[] { localResources, palette });
                var wpfUiAccent = Assert.IsType<SolidColorBrush>(localResources["AccentFillColorDefaultBrush"]);
                var wpfUiText = Assert.IsType<SolidColorBrush>(localResources["TextOnAccentFillColorPrimaryBrush"]);
                Assert.Equal(hostAccent, wpfUiAccent.Color);
                Assert.Equal(Colors.White, wpfUiText.Color);

                var materialResources = factoryType.GetMethod("ApplyMaterialResources", BindingFlags.Public | BindingFlags.Static)!;
                materialResources.Invoke(null, new object[] { localResources, palette, false, false });
                Assert.Null(localResources["GscSurfaceEffect"]);
                Assert.Null(localResources["GscPrimaryButtonEffect"]);
                Assert.Null(localResources["GscSidebarEffect"]);
                Assert.Null(localResources["GscPopupEffect"]);
                Assert.Null(localResources["GscDialogEffect"]);
                Assert.Null(localResources["GscSliderThumbEffect"]);
                Assert.False(Assert.IsType<bool>(localResources["GscPopupAllowsTransparency"]));
                Assert.Equal(PopupAnimation.None, Assert.IsType<PopupAnimation>(localResources["GscPopupAnimation"]));

                materialResources.Invoke(null, new object[] { localResources, palette, true, true });
                Assert.IsType<DropShadowEffect>(localResources["GscSurfaceEffect"]);
                Assert.IsType<DropShadowEffect>(localResources["GscPrimaryButtonEffect"]);
                Assert.IsType<DropShadowEffect>(localResources["GscSidebarEffect"]);
                Assert.IsType<DropShadowEffect>(localResources["GscPopupEffect"]);
                Assert.IsType<DropShadowEffect>(localResources["GscDialogEffect"]);
                Assert.IsType<DropShadowEffect>(localResources["GscSliderThumbEffect"]);
                Assert.True(Assert.IsType<bool>(localResources["GscPopupAllowsTransparency"]));
                Assert.Equal(PopupAnimation.Fade, Assert.IsType<PopupAnimation>(localResources["GscPopupAnimation"]));
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
        Assert.Contains("AdaptiveThemePaletteFactory.ApplyRuntimeThemeResources(Resources, palette", dashboardCode);
        Assert.Contains("AdaptiveThemePaletteFactory.ApplyAccentResources(Resources, palette)", settingsCode);
        Assert.Contains("AdaptiveThemePaletteFactory.ApplyMaterialResources(Resources, palette, glassEnabled, MotionEnabled)", settingsCode);
        Assert.Contains("AdaptiveThemePaletteFactory.ApplyWpfUiResources(Resources, palette)", settingsCode);

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
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));
        var production = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "WpfUiProduction.xaml"));
        Assert.Contains("resources[\"GscAmbientAccentBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscErrorTintBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscRestoreInfoFillBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscSafetyFillBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscAmbientInfoBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscAmbientSuccessBrush\"]", paletteSource);
        Assert.Contains("SemanticTint", paletteSource);
        Assert.Contains("resources[\"GscAccentShadowColor\"]", paletteSource);
        Assert.Contains("resources[\"GscSelectionTextBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscSurfaceEffect\"]", paletteSource);
        Assert.Contains("resources[\"GscPrimaryButtonEffect\"]", paletteSource);
        Assert.Contains("resources[\"GscPickerScrimBrush\"]", paletteSource);
        Assert.Contains("resources[\"GscPopupAllowsTransparency\"] = glassEnabled", paletteSource);
        Assert.Contains("resources[\"GscPopupAnimation\"] = motionEnabled ? PopupAnimation.Fade : PopupAnimation.None", paletteSource);
        Assert.Contains("if (!enabled) return null;", paletteSource);
        Assert.Contains("highContrast ? accent", paletteSource);
        Assert.DoesNotContain("{StaticResource GscAccentShadowColor}", dashboard);
        Assert.DoesNotContain("{StaticResource GscAccentShadowColor}", tokens);
        Assert.Contains("{DynamicResource GscAmbientAccentBrush}", dashboard);
        Assert.Contains("{DynamicResource GscSelectionTextBrush}", dashboard);
        Assert.Contains("{DynamicResource GscSelectionTextBrush}", tokens);
        Assert.Contains("x:Key=\"GscPickerScrimBrush\"", tokens);
        Assert.Contains("{DynamicResource GscSurfaceEffect}", dashboard);
        Assert.Contains("{DynamicResource GscPrimaryButtonEffect}", dashboard);
        Assert.Contains("{DynamicResource GscSidebarEffect}", redesign);
        Assert.Contains("{DynamicResource GscDialogEffect}", dashboard);
        Assert.Contains("{DynamicResource GscPopupEffect}", tokens);
        Assert.Contains("{DynamicResource GscSliderThumbEffect}", tokens);
        Assert.Contains("AllowsTransparency=\"{DynamicResource GscPopupAllowsTransparency}\"", tokens);
        Assert.Contains("PopupAnimation=\"{DynamicResource GscPopupAnimation}\"", tokens);
        Assert.Contains("HorizontalScrollBarVisibility=\"{TemplateBinding HorizontalScrollBarVisibility}\"", tokens);
        Assert.Contains("VerticalScrollBarVisibility=\"{TemplateBinding VerticalScrollBarVisibility}\"", tokens);
        Assert.Contains("HorizontalScrollBarVisibility=\"{TemplateBinding HorizontalScrollBarVisibility}\"", production);
        Assert.Contains("x:Key=\"GscElevatedSurface\"", tokens);
        Assert.Contains("x:Key=\"GscElevatedSurface\"", dashboard);
        Assert.Contains("x:Name=\"GameBrowserScrim\"", dashboard);
        Assert.Contains("Background=\"{DynamicResource GscPickerScrimBrush}\"", dashboard);
        Assert.Contains("x:Name=\"GameBrowserPanel\"", dashboard);
        Assert.Contains("Style=\"{StaticResource GscRedesignFloatingPickerCard}\"", dashboard);
        Assert.Contains("x:Name=\"GameDetailCard\" Grid.Row=\"0\" Grid.RowSpan=\"2\" Grid.Column=\"2\" Style=\"{StaticResource GscWorkspaceHostSurface}\"", dashboard);
        Assert.Contains("x:Key=\"GscWorkspaceHostSurface\"", dashboard);
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
        Assert.Equal(typeof(GameSaveCenter.Playnite.Controls.Button), buttonStyle.TargetType);
        Assert.IsAssignableFrom<Brush>(resources["AccentFillColorDefaultBrush"]);
        Assert.IsAssignableFrom<Brush>(resources["TextOnAccentFillColorPrimaryBrush"]);
    }

    [Fact]
    public void ProductionAdaptersOwnRoundedInputTemplatesAndPopupItems()
    {
        Exception? exception = null;
        ResourceDictionary? resources = null;

        var thread = new Thread(() =>
        {
            try
            {
                resources = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""><ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/DesignTokens.xaml""/>
    <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/WpfUiProduction.xaml""/>
</ResourceDictionary.MergedDictionaries></ResourceDictionary>");
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
        Assert.IsType<ControlTemplate>(resources!["GscWpfUiTextBoxTemplate"]);
        Assert.IsType<ControlTemplate>(resources["GscWpfUiComboBoxTemplate"]);
        Assert.True(resources.Contains(typeof(ComboBoxItem)), "The local ComboBoxItem style must win over a bright host popup style.");

        var buttonStyle = Assert.IsType<Style>(resources["GscWpfUiButton"]);
        Assert.Contains(buttonStyle.Setters.OfType<Setter>(), setter => setter.Property.Name == "CornerRadius");
        var comboStyle = Assert.IsType<Style>(resources["GscWpfUiComboBox"]);
        Assert.Contains(comboStyle.Setters.OfType<Setter>(), setter => setter.Property.Name == "Template");
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
             xmlns:ui=""clr-namespace:GameSaveCenter.Playnite.Controls;assembly=GameSaveCenter.Playnite"">
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
        var saves = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml"));

        Assert.Contains("x:Name=\"GameHeaderActions\"", dashboard);
        Assert.Contains("<WrapPanel x:Name=\"GameHeaderActions\"", dashboard);
        Assert.Contains("Command=\"{Binding BackupSelectedCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding ValidateCommand}\"", dashboard);
        Assert.Contains("Command=\"{Binding DetectPathsCommand}\"", dashboard);
        Assert.Contains("Click=\"OnTogglePolicy\"", dashboard);
        Assert.Contains("Header=\"时间\" Binding=\"{Binding CreatedLocal", saves);
    }

    [Fact]
    public void TaskWorkspaceKeepsRecoveryActionsReachableWhenDetailsWrap()
    {
        var repositoryRoot = FindRepositoryRoot();
        var task = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml"));

        Assert.Contains("x:Name=\"TaskFiltersPanel\"", task);
        Assert.Contains("<WrapPanel x:Name=\"TaskDetailActions\"", task);
        Assert.Contains("Command=\"{Binding CopyTaskErrorCommand}\"", task);
        Assert.Contains("Command=\"{Binding RetryTaskCommand}\"", task);
        Assert.Contains("Command=\"{Binding CancelTaskCommand}\"", task);
        Assert.Contains("筛选不会取消、重排或重新执行后台任务", task);
    }

    [Fact]
    public void GlobalWorkspaceViewsHaveOneVisibleMigrationEntryAndKeepVirtualization()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var media = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));
        var saves = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml"));
        var trainers = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));

        foreach (var marker in new[] { "MediaWorkspaceTab", "MaintenanceWorkspaceTab", "SaveWorkspaceTab", "TrainerWorkspaceTab" })
            Assert.Contains($"x:Name=\"{marker}\"", dashboard);
        foreach (var legacy in new[] { "OverviewTab", "SaveHistoryTab", "CandidateTab", "TrainerTab", "MediaTab", "TaskTab", "DiagnosticTab", "DeviceStatusTab", "LogsTab", "UiFrameworkProbeTab" })
            Assert.DoesNotContain($"x:Name=\"{legacy}\"", dashboard);
        Assert.DoesNotContain("SetVisibility(MediaTab, false)", dashboardCode);
        Assert.DoesNotContain("SetVisibility(DiagnosticTab, false)", dashboardCode);
        Assert.DoesNotContain("SetVisibility(SaveHistoryTab, false)", dashboardCode);
        Assert.DoesNotContain("SetVisibility(TrainerTab, false)", dashboardCode);
        foreach (var view in new[] { media, maintenance, saves, trainers })
        {
            Assert.True(view.Contains("VirtualizingPanel.IsVirtualizing=\"True\"") || view.Contains("EnableRowVirtualization\" Value=\"True\""));
            Assert.True(view.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"") || view.Contains("EnableColumnVirtualization\" Value=\"True\""));
            Assert.Contains("DynamicResource GscPrimaryTextBrush", view);
        }
        Assert.Contains("AssignInboxMediaCommand", media);
        Assert.Contains("RefreshDiagnosticsCommand", maintenance);
        Assert.Contains("RestoreCommand", saves);
        Assert.Contains("DownloadTrainerCommand", trainers);
    }

    [Fact]
    public void SharedDataGridChromeCoversHeadersCellsAndRowsAcrossExtractedWorkspaces()
    {
        var repositoryRoot = FindRepositoryRoot();
        var production = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "WpfUiProduction.xaml"));
        var designTokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));

        Assert.Contains("<Style TargetType=\"DataGridColumnHeader\">", production);
        Assert.Contains("<Style TargetType=\"DataGridCell\">", production);
        Assert.Contains("<Style TargetType=\"DataGridRow\">", production);
        Assert.Contains("<Style TargetType=\"DataGrid\">", production);
        Assert.Contains("AlternatingRowBackground\" Value=\"{DynamicResource GscTableAlternateRowBrush}\"", production);
        Assert.Contains("RowHeight\" Value=\"{DynamicResource GscTableRowHeight}\"", production);
        Assert.Contains("ColumnHeaderHeight\" Value=\"{DynamicResource GscTableHeaderHeight}\"", production);
        Assert.Contains("HorizontalGridLinesBrush\" Value=\"{DynamicResource GscTableDividerBrush}\"", production);
        Assert.Contains("VerticalContentAlignment\" Value=\"Top\"", production);
        Assert.Contains("ScrollViewer.VerticalContentAlignment\" Value=\"Top\"", production);
        Assert.Contains("<Style TargetType=\"DataGridColumnHeadersPresenter\">", production);
        Assert.Contains("OverridesDefaultStyle\" Value=\"True\"", production);
        // Keep the shared DataGrid on WPF's stable default panning contract.  Both-axis
        // panning combined with the custom row template can produce phantom empty rows.
        Assert.Contains("KeyboardNavigation.TabNavigation\" Value=\"Local\"", production);
        Assert.Contains("KeyboardNavigation.DirectionalNavigation\" Value=\"Contained\"", production);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"", production);
        Assert.Contains("GscTableHeaderBrush", production);
        Assert.Contains("GscRowHoverBrush", production);
        Assert.Contains("GscAccentTintBrush", production);
        Assert.Contains("CornerRadius=\"10\"", production);
        Assert.Contains("Property=\"MinHeight\" Value=\"{DynamicResource GscTableRowHeight}\"", production);
        Assert.Contains("Text columns read naturally from the leading edge", production);
        Assert.Contains("<SelectiveScrollingGrid>", production);
        Assert.Contains("x:Name=\"PART_CellsPresenter\"", production);
        Assert.Contains("<DataGridDetailsPresenter", production);
        Assert.Contains("SelectiveScrollingGrid.SelectiveScrollingOrientation=\"Horizontal\"", production);
        Assert.Contains("SelectiveScrollingGrid.SelectiveScrollingOrientation=\"Vertical\"", production);
        Assert.Contains("<Style TargetType=\"DataGridCell\">", production);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Left\"/>", production);
        Assert.Contains("x:Name=\"SortGlyph\"", production);
        Assert.Contains("Property=\"SortDirection\" Value=\"Ascending\"", production);
        Assert.Contains("Property=\"SortDirection\" Value=\"Descending\"", production);
        Assert.Contains("x:Key=\"GscTableRowHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableMinHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableViewportHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableRowHeight\">48</sys:Double>", designTokens);
        Assert.Contains("x:Key=\"GscTableMinHeight\">0</sys:Double>", designTokens);
        Assert.Contains("x:Key=\"GscTableViewportHeight\">720</sys:Double>", designTokens);
        Assert.Contains("x:Key=\"GscTableHeaderHeight\">42</sys:Double>", designTokens);
        Assert.Contains("<Setter Property=\"ClipToBounds\" Value=\"True\"/>", redesign);
        Assert.DoesNotContain("Property=\"Height\" Value=\"{DynamicResource GscTableViewportHeight}\"", production);
        Assert.DoesNotContain("Property=\"Height\" Value=\"{DynamicResource GscTableViewportHeight}\"", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml")));
        Assert.Contains("x:Key=\"GscTableHeaderHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableAlternateRowBrush\"", designTokens);
        Assert.Contains("x:Key=\"GscPageScrollViewer\"", designTokens);
        Assert.Contains("x:Key=\"GscInspectorScrollViewer\"", designTokens);

        Assert.Contains("x:Key=\"GscRedesignTableFrame\"", redesign);
        Assert.Contains("CornerRadius\" Value=\"16\"", redesign);

        // Dashboard keeps a compatibility-scope row style while the extracted workspaces use
        // the shared dictionary. Both templates must retain the same WPF selective-scrolling
        // contract so a host theme cannot silently break horizontal scrolling in one scope.
        Assert.Contains("<SelectiveScrollingGrid>", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml")));
        Assert.Contains("<DataGridDetailsPresenter", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml")));

        // OverviewView hosts the recent-activity tile list instead of a DataGrid table now,
        // so the shared table chrome contract is verified on the remaining table workspaces.
        foreach (var workspace in new[] { "SaveCenterView.xaml", "MediaCenterView.xaml", "TaskCenterView.xaml", "MaintenanceView.xaml" })
        {
            var text = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", workspace));
            Assert.Contains("BasedOn=\"{StaticResource {x:Type DataGrid}}\"", text);
            Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\"", text);
            Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", text);
            Assert.Contains("EnableRowVirtualization\" Value=\"True\"", text);
            Assert.Contains("EnableColumnVirtualization\" Value=\"True\"", text);
            Assert.Contains("Property=\"MinHeight\" Value=\"{DynamicResource GscWorkspaceTableMinHeight}\"", text);
            Assert.DoesNotContain("Property=\"Height\" Value=\"Auto\"", text);
            Assert.Contains("Property=\"RowHeight\" Value=\"{DynamicResource GscTableRowHeight}\"", text);
            Assert.Contains("Property=\"ColumnHeaderHeight\" Value=\"{DynamicResource GscTableHeaderHeight}\"", text);
            Assert.Contains("Property=\"AlternatingRowBackground\" Value=\"{DynamicResource GscTableAlternateRowBrush}\"", text);
            Assert.DoesNotContain("PageScrollViewer\" Style=\"{DynamicResource GscPageScrollViewer}", text);
            Assert.DoesNotContain("x:Name=\"SavePageScrollViewer\"", text);
            Assert.DoesNotContain("x:Name=\"TrainerPageScrollViewer\"", text);
            Assert.DoesNotContain("x:Name=\"MediaPageScrollViewer\"", text);
            Assert.DoesNotContain("x:Name=\"TaskPageScrollViewer\"", text);
            Assert.DoesNotContain("x:Name=\"MaintenancePageScrollViewer\"", text);
            Assert.DoesNotContain("BlurEffect", text);
        }
    }

    [Fact]
    public void MediaInboxUsesStableItemScrollingAndMaintenanceHeadersOwnTheirTheme()
    {
        var repositoryRoot = FindRepositoryRoot();
        var media = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));
        var maintenanceCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml.cs"));

        Assert.Contains("VirtualizingPanel.ScrollUnit\" Value=\"Item\"", media);
        Assert.Contains("x:Name=\"MediaInboxGrid\"", media);
        Assert.Contains("EnableColumnVirtualization=\"False\"", media);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Standard\"", media);
        Assert.Contains("x:Key=\"MediaInboxStableRowStyle\"", media);
        Assert.Contains("RowStyle=\"{StaticResource MediaInboxStableRowStyle}\"", media);
        Assert.Contains("HeaderStyle=\"{StaticResource MediaMiddleColumnHeader}\" Header=\"类型\"", media);
        Assert.Contains("HeaderStyle=\"{StaticResource MediaMiddleColumnHeader}\" Header=\"来源\"", media);
        Assert.Contains("HeaderStyle=\"{StaticResource MediaMiddleColumnHeader}\" Header=\"文件\"", media);
        var mediaCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml.cs"));
        Assert.DoesNotContain("MediaInboxGrid.Loaded += InboxGridLoaded", mediaCode);
        Assert.DoesNotContain("ConfigureInboxGrid", mediaCode);
        Assert.DoesNotContain("InboxGridSizeChanged", mediaCode);
        Assert.DoesNotContain("FindVisualChild", mediaCode);
        Assert.DoesNotContain("ScrollViewer.Background=\"{DynamicResource GscGlassStrongBrush}\"", media);
        Assert.Contains("ColumnHeaderStyle\" Value=\"{StaticResource MediaInboxColumnHeaderStyle}\"", media);
        Assert.Contains("HeaderStyle=\"{StaticResource MaintenanceLastColumnHeader}\" Header=\"建议处理\"", maintenance);
        Assert.Contains("DataGridLoaded", maintenanceCode);
        Assert.DoesNotContain("AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler(ApplyHeaderTheme), true)", maintenanceCode);
        Assert.Contains("UnassignedMedia = new System.Collections.ObjectModel.ObservableCollection<MediaItemDto>(inbox)", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.Media.cs")));
        Assert.Contains("Limit = 5000", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.Media.cs")));
        Assert.DoesNotContain("if (loadInbox) await LoadInboxAsync();", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs")));
    }

    [Fact]
    public void MediaInboxActionsStayOutsideTheGridScrollSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mediaPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml");
        var document = XDocument.Parse(File.ReadAllText(mediaPath));
        var tabItem = document.Descendants()
            .Single(element => element.Name.LocalName == "TabItem" && element.Attribute("Header")?.Value == "待归类");
        var tabGrid = tabItem.Elements().First();
        Assert.Equal("Grid", tabGrid.Name.LocalName);

        var rowHeights = tabGrid.Descendants()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .Select(element => element.Attribute("Height")?.Value)
            .ToList();
        Assert.Equal(new[] { "Auto", "*", "Auto" }, rowHeights);

        var xamlName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var inboxGrid = document.Descendants()
            .Single(element => element.Name.LocalName == "DataGrid" && element.Attribute(xamlName)?.Value == "MediaInboxGrid");
        var tableFrame = inboxGrid.Ancestors()
            .FirstOrDefault(element => element.Name.LocalName == "Border"
                && (element.Attribute("Style")?.Value ?? "").Contains("MediaTableFrame"));
        Assert.NotNull(tableFrame);
        Assert.Equal("1", tableFrame.Attribute("Grid.Row")?.Value);

        var actionBar = tabGrid.Descendants()
            .Single(element => element.Name.LocalName == "WrapPanel"
                && element.DescendantsAndSelf().Any(descendant => descendant.Attribute("Command")?.Value == "{Binding AssignInboxMediaCommand}"));
        Assert.Equal("2", actionBar.Attribute("Grid.Row")?.Value);
        Assert.False(actionBar.Ancestors().Contains(tableFrame),
            "待归类确认/忽略按钮与归类目标不能放进 MediaInboxGrid 的滚动面。");
        Assert.Contains(actionBar.Descendants(), element => element.Attribute("Command")?.Value == "{Binding IgnoreInboxMediaCommand}");
        Assert.Contains(actionBar.Descendants(), element => element.Attribute("SelectedItem")?.Value == "{Binding InboxTargetGame}");
    }

    [Fact]
    public void ExtractedWorkspacesUseGridRootsAndInternalTableScrolling()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewDirectory = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views");
        foreach (var file in new[]
        {
            "OverviewView.xaml",
            "SaveCenterView.xaml",
            "MediaCenterView.xaml",
            "TaskCenterView.xaml",
            "TrainerCenterView.xaml",
            "MaintenanceView.xaml"
        })
        {
            var text = File.ReadAllText(Path.Combine(viewDirectory, file));
            var root = XDocument.Parse(text);
            var directScrollViewers = root.Root?.Elements()
                .Where(element => element.Name.LocalName == "ScrollViewer")
                .ToList() ?? new List<XElement>();
            Assert.Empty(directScrollViewers);
            Assert.True(root.Root?.Elements().Any(element => element.Name.LocalName == "Grid") == true, $"{file} must expose a Grid root instead of a page ScrollViewer.");
            Assert.DoesNotContain("PageScrollViewer\" Style=\"{DynamicResource GscPageScrollViewer}", text);
        }

        var trainer = File.ReadAllText(Path.Combine(viewDirectory, "TrainerCenterView.xaml"));
        var trainerCode = File.ReadAllText(Path.Combine(viewDirectory, "TrainerCenterView.xaml.cs"));
        Assert.Contains("MinHeight\" Value=\"{DynamicResource GscWorkspaceTableMinHeight}\"", trainer);
        Assert.DoesNotContain("Height\" Value=\"{DynamicResource GscListViewportHeight}\"", trainer);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", trainer);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\"/>", trainer);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Stretch\"/>", trainer);
        Assert.Contains("x:Name=\"InstalledToolsLayout\"", trainer);
        Assert.Contains("Grid.Column=\"2\" Grid.RowSpan=\"4\"", trainer);
        Assert.Contains("InstalledToolsLayout.ColumnDefinitions[2].Width", trainerCode);
        Assert.Contains("Grid.SetRowSpan(TrainerToolsSettingsScrollViewer", trainerCode);
    }

    [Fact]
    public void NestedWorkspaceScrollChannelsUseSharedPageOrInspectorStyles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewDirectory = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views");
        var overview = File.ReadAllText(Path.Combine(viewDirectory, "OverviewView.xaml"));
        var save = File.ReadAllText(Path.Combine(viewDirectory, "SaveCenterView.xaml"));
        var media = File.ReadAllText(Path.Combine(viewDirectory, "MediaCenterView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(viewDirectory, "MaintenanceView.xaml"));

        Assert.Contains("x:Name=\"OverviewSecondaryScrollViewer\"", overview);
        Assert.Contains("x:Name=\"OverviewSecondaryScrollViewer\"\n                      Style=\"{DynamicResource GscPageScrollViewer}\"", overview);
        Assert.Contains("x:Name=\"OverviewRiskScrollViewer\" Style=\"{DynamicResource GscPageScrollViewer}\"", overview);
        Assert.Contains("<ScrollViewer Style=\"{DynamicResource GscPageScrollViewer}\" VerticalScrollBarVisibility=\"Hidden\"", save);
        Assert.Contains("<ScrollViewer Style=\"{DynamicResource GscPageScrollViewer}\" Grid.Row=\"0\"", media);
        Assert.DoesNotContain("Grid.Row=\"0\" MaxHeight=\"190\"", media);
        Assert.Contains("x:Name=\"MaintenanceDeviceInspectorScrollViewer\" Grid.Row=\"2\" Grid.Column=\"2\" Style=\"{DynamicResource GscInspectorScrollViewer}\"", maintenance);
        Assert.DoesNotContain("MaintenanceDeviceDecisionScrollViewer", maintenance);
        Assert.DoesNotContain("MaintenanceRemoteRestoreScrollViewer", maintenance);
        Assert.DoesNotContain("x:Name=\"MaintenanceAuditScrollViewer\"", maintenance);
    }

    [Fact]
    public void SettingsAndSidebarUseTheSharedPageScrollChannel()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));

        Assert.Contains("x:Name=\"SettingsScroller\" Style=\"{DynamicResource GscPageScrollViewer}\"", settings);
        Assert.Contains("x:Name=\"SidebarNavigationScrollViewer\"", dashboard);
        Assert.Contains("Style=\"{DynamicResource GscPageScrollViewer}\"", dashboard);
        Assert.DoesNotContain("SettingsScroller\" VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\" KeyboardNavigation", settings);
    }

    [Fact]
    public void DashboardUsesSidebarAsTheOnlyWorkspaceSwitcher()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));

        Assert.Contains("x:Name=\"DetailsTabControl\"", dashboard);
        Assert.Contains("Tag=\"HideHeaders\"", dashboard);
        Assert.Contains("Property=\"Tag\" Value=\"HideHeaders\"", dashboard);
        Assert.DoesNotContain("TabStripPlacement=\"None\"", dashboard);
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Local\"", dashboard);
        Assert.DoesNotContain("DetailsTabControl\" Grid.Row=\"3\" MinHeight=\"0\"\n                                Style=\"{StaticResource GscTabControl}\"\n                                SelectionChanged", dashboard);
    }

    [Fact]
    public void CompactLayoutsKeepSummaryInformationAndUseThePageScroller()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var mediaCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml.cs"));
        var tasksCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml.cs"));
        var maintenanceCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml.cs"));

        Assert.Contains("PageSubtitleText.Visibility = Visibility.Visible", dashboardCode);
        // The duplicate metric strip is intentionally collapsed; metrics are rendered by
        // the workspace-specific overview cards so compact layouts do not reserve a
        // second header row.
        Assert.Contains("SelectedGameMetricPanel.Visibility = Visibility.Collapsed", dashboardCode);
        Assert.Contains("MediaSummaryPanel.Visibility = Visibility.Visible", mediaCode);
        Assert.Contains("TaskSummaryPanel.Visibility = Visibility.Visible", tasksCode);
        Assert.Contains("DiagnosticHealthPanel.Visibility = Visibility.Visible", maintenanceCode);
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));
        Assert.Contains("SettingsHeaderSubtitle.Visibility = Visibility.Visible", settingsCode);
        Assert.Contains("SettingsSaveHint.Visibility = Visibility.Visible", settingsCode);
        Assert.Contains("RestoreSafetyBanner.Visibility = viewModel.CurrentWorkspace == WorkspaceKind.Saves", dashboardCode);
    }

    [Fact]
    public void SafeFallbackUsesSystemThemeResourcesInsteadOfHardCodedDarkColors()
    {
        var repositoryRoot = FindRepositoryRoot();
        var safeView = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SafeViewFactory.cs"));

        Assert.Contains("SystemColors.WindowTextBrush", safeView);
        Assert.Contains("SystemColors.GrayTextBrush", safeView);
        Assert.Contains("SystemColors.WindowBrush", safeView);
        Assert.DoesNotContain("Brushes.White", safeView);
        Assert.DoesNotContain("Color.FromRgb(28, 30, 38)", safeView);
    }

    [Fact]
    public void ContextActionsRemainInLayoutWhenDisabled()
    {
        var repositoryRoot = FindRepositoryRoot();
        var production = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "WpfUiProduction.xaml"));
        var contextStyleStart = production.IndexOf("x:Key=\"GscWpfUiContextButton\"", StringComparison.Ordinal);
        Assert.True(contextStyleStart >= 0);
        var contextStyle = production.Substring(contextStyleStart, Math.Min(900, production.Length - contextStyleStart));
        Assert.Contains("<Setter Property=\"Opacity\" Value=\"0.48\"/>", contextStyle);
        Assert.DoesNotContain("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", contextStyle);
    }

    [Fact]
    public void DiagnosticInspectorKeepsLongContentScrollableAndOwnsNoDeadRightColumn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);

        // The diagnostics detail inspector mirrors the approved audit reading card:
        // title / detail / suggested action with an info band, collapsed until a
        // finding is selected so no fixed empty right column remains.
        Assert.DoesNotContain(maintenance.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.DoesNotContain("x:Name=\"MaintenanceDiagnosticDetails\"", maintenanceText);
        var inspector = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceDiagnosticsInspector");
        Assert.Equal("Auto", inspector.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", inspector.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Contains("Text=\"诊断详情\" Style=\"{DynamicResource GscSectionTitleStyle}\"", maintenanceText);
        Assert.Contains("SelectedFinding.SuggestedAction, TargetNullValue=暂无建议处理方式", maintenanceText);

        // The full diagnostic summary left the inspector and lives in a full-width
        // strip with both scrollbars enabled for long worker output.
        var summary = maintenance.Descendants().Single(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceDiagnosticSummaryGrid");
        var textBox = summary.Descendants().Single(element => element.Name.LocalName == "TextBox");
        Assert.Equal("Auto", textBox.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", textBox.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Consolas", textBox.Attribute("FontFamily")?.Value);
        Assert.Equal("NoWrap", textBox.Attribute("TextWrapping")?.Value);
    }

    [Fact]
    public void MaintenanceAuditUsesDemoInspectorAndInternalAuditViewport()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);
        Assert.DoesNotContain(maintenance.Descendants(), element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceAuditScrollViewer");
        Assert.Contains("x:Name=\"MaintenanceAuditLayout\"", maintenanceText);
        Assert.Contains("x:Name=\"MaintenanceAuditInspector\"", maintenanceText);
        Assert.Contains("x:Name=\"MaintenanceAuditFindingsGrid\" Style=\"{StaticResource MaintenanceDataGrid}\"", maintenanceText);
        Assert.Contains("x:Name=\"MaintenanceAuditLogGrid\" Style=\"{StaticResource MaintenanceDataGrid}\"", maintenanceText);
        var auditInspector = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceAuditInspector");
        Assert.Equal("Auto", auditInspector.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", auditInspector.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Null(auditInspector.Attribute("MaxHeight"));
        Assert.Contains("MaintenanceAuditInspector.MaxHeight = stackAudit ? Math.Max(150, height * 0.34) : double.PositiveInfinity", File.ReadAllText(maintenancePath + ".cs"));
        Assert.DoesNotContain("Height=\"{DynamicResource GscTableViewportHeight}\"", maintenanceText);
    }

    [Fact]
    public void MaintenanceAuditTableSpansFullWidthUntilAFindingIsSelected()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);

        // The findings table expands over the inspector column while nothing is selected,
        // and the detail inspector collapses so no fixed empty right column remains.
        Assert.Contains("x:Name=\"MaintenanceAuditFindingsTable\"", maintenanceText);
        Assert.Contains("<DataTrigger Binding=\"{Binding SelectedFinding}\" Value=\"{x:Null}\">", maintenanceText);
        Assert.Contains("<Setter Property=\"Grid.ColumnSpan\" Value=\"3\"/>", maintenanceText);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", maintenanceText);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Visible\"/>", maintenanceText);

        // The recent audit log left the detail inspector and owns a full-width strip
        // below the findings table instead of being squeezed into the 360-DIP column.
        var auditInspector = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceAuditInspector");
        Assert.DoesNotContain(auditInspector.Descendants(), element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceAuditLogGrid");
        var auditLog = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "DataGrid" && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceAuditLogGrid");
        Assert.Contains(auditLog.Ancestors(), ancestor =>
            ancestor.Name.LocalName == "Border" && ancestor.Attribute("Grid.Row")?.Value == "2" && ancestor.Attribute("Grid.ColumnSpan")?.Value == "3");
        Assert.Contains("MaintenanceAuditLogGrid.MinHeight = stackAudit ? 96 : 140", File.ReadAllText(maintenancePath + ".cs"));
    }

    [Fact]
    public void MaintenanceDiagnosticsTableSpansFullWidthUntilAFindingIsSelected()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);

        // The findings table expands over the inspector column while nothing is
        // selected, and the detail inspector collapses so no fixed empty right
        // column remains on the diagnostics page.
        Assert.Contains("x:Name=\"MaintenanceDiagnosticsTable\"", maintenanceText);
        Assert.Contains("<DataTrigger Binding=\"{Binding SelectedFinding}\" Value=\"{x:Null}\">", maintenanceText);
        Assert.Contains("<Setter Property=\"Grid.ColumnSpan\" Value=\"3\"/>", maintenanceText);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\"/>", maintenanceText);
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Visible\"/>", maintenanceText);

        // The full diagnostic summary left the detail inspector and owns a full-width
        // strip below the findings table instead of being squeezed into the 360-DIP column.
        var summary = maintenance.Descendants().Single(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceDiagnosticSummaryGrid");
        Assert.Equal("1", summary.Attribute("Grid.Row")?.Value);
        Assert.Equal("3", summary.Attribute("Grid.ColumnSpan")?.Value);
        Assert.Contains("MaintenanceDiagnosticsInspector.MaxHeight = stackDiagnostics ? Math.Max(150, height * 0.34) : double.PositiveInfinity", File.ReadAllText(maintenancePath + ".cs"));
    }

    [Fact]
    public void MaintenanceProcessTableSpansFullWidthUntilAMappingIsSelected()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenanceText = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));

        Assert.Contains("x:Name=\"MaintenanceProcessTable\"", maintenanceText);
        Assert.Contains("x:Name=\"MaintenanceProcessInspector\"", maintenanceText);
        Assert.Contains("<DataTrigger Binding=\"{Binding SelectedProcessMapping}\" Value=\"{x:Null}\">", maintenanceText);
        Assert.Contains("Command=\"{Binding DeleteProcessMappingCommand}\" CommandParameter=\"{Binding SelectedProcessMapping}\"", maintenanceText);
        Assert.Contains("Command=\"{Binding SaveProcessMappingCommand}\"", maintenanceText);
        Assert.Contains("SelectedItem=\"{Binding ProcessMappingTargetGame}\"", maintenanceText);
    }

    [Fact]
    public void MaintenanceDeviceStateUsesAStarTableRowAndInternalScrolling()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);
        Assert.DoesNotContain(maintenance.Descendants(), element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceDeviceScrollViewer");
        Assert.Contains("<RowDefinition Height=\"Auto\"/><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"*\"/>", maintenanceText);
        Assert.Contains("x:Name=\"MaintenanceDeviceGrid\" Style=\"{StaticResource MaintenanceDataGrid}\"", maintenanceText);
        Assert.DoesNotContain("x:Name=\"MaintenanceDeviceGrid\" Height=\"{DynamicResource GscTableViewportHeight}\"", maintenanceText);
        Assert.Contains("ItemsSource=\"{Binding DeviceComparisons}\"", maintenanceText);
    }

    [Fact]
    public void TrainerInspectorFillsTheWorkspaceAndOnlyCapsWhenStacked()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trainerPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml");
        var trainerCodePath = trainerPath + ".cs";
        var trainerText = File.ReadAllText(trainerPath);
        var trainer = XDocument.Parse(trainerText);
        var trainerCode = File.ReadAllText(trainerCodePath);
        var scrollViewer = trainer.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "TrainerToolsSettingsScrollViewer");

        Assert.Equal("Auto", scrollViewer.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", scrollViewer.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Null(scrollViewer.Attribute("MaxHeight"));
        Assert.Contains("TrainerToolsSettingsScrollViewer.MaxHeight = double.PositiveInfinity", trainerCode);
        Assert.Contains("TrainerToolsSettingsScrollViewer.MaxHeight = stackInstalled", trainerCode);
        Assert.Contains("Math.Min(420, height * 0.56)", trainerCode);
        Assert.Contains("VerticalContentAlignment=\"Stretch\"", trainerText);
        Assert.Contains("Style=\"{DynamicResource GscRedesignSectionCard}\"", trainerText);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", trainerText);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Disabled\"", trainerText);
    }

    [Fact]
    public void SaveCenterKeepsTableRowsVisibleWhenMetadataActionsWrap()
    {
        var repositoryRoot = FindRepositoryRoot();
        var savePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml");
        var saveCodePath = savePath + ".cs";
        var saveText = File.ReadAllText(savePath);
        var saveCode = File.ReadAllText(saveCodePath);
        var save = XDocument.Parse(saveText);

        foreach (var name in new[] { "SaveHistoryActionsScrollViewer", "SaveCandidateInspectorScrollViewer" })
        {
            var viewer = save.Descendants().Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
            Assert.Equal("Auto", viewer.Attribute("VerticalScrollBarVisibility")?.Value);
            Assert.Equal("Disabled", viewer.Attribute("HorizontalScrollBarVisibility")?.Value);
            Assert.Equal("{DynamicResource GscInspectorScrollViewer}", viewer.Attribute("Style")?.Value);
        }

        Assert.Contains("SaveHistoryActionsScrollViewer.MaxHeight = compact ? Math.Max(150, Math.Min(360, height * 0.42)) : double.PositiveInfinity;", saveCode);
        Assert.Contains("SaveCandidateInspectorScrollViewer.MaxHeight = compact ? Math.Max(150, Math.Min(360, height * 0.42)) : double.PositiveInfinity;", saveCode);
        Assert.Contains("Grid.SetRow(SaveCandidateInspectorScrollViewer, compact ? 1 : 0)", saveCode);
        Assert.Contains("SaveCandidateInspectorScrollViewer", saveText);
        Assert.DoesNotContain("SaveCandidateReasonScrollViewer", saveText);
        Assert.DoesNotContain("SaveCandidateActionsScrollViewer", saveText);
        Assert.DoesNotContain("<Border Grid.Row=\"1\" Style=\"{DynamicResource GscSurface}\"", saveText);
    }

    [Fact]
    public void SaveCenterProvidesDemoComparisonAndRetentionInspector()
    {
        var repositoryRoot = FindRepositoryRoot();
        var savePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml");
        var saveText = File.ReadAllText(savePath);
        var saveCode = File.ReadAllText(savePath + ".cs");

        Assert.Contains("<TabItem Header=\"比较与保留\">", saveText);
        Assert.Contains("x:Name=\"SaveCompareLayout\"", saveText);
        Assert.Contains("x:Name=\"SaveCompareRetentionScrollViewer\"", saveText);
        Assert.Contains("x:Name=\"SaveCompareMainScrollViewer\"", saveText);
        Assert.Contains("{Binding LastBackupDiff.Added.Count", saveText);
        Assert.Contains("{Binding LastRetentionPreview.KeepBackupIds.Count", saveText);
        Assert.Contains("Command=\"{Binding CompareBackupCommand}\"", saveText);
        Assert.Contains("Command=\"{Binding PreviewRetentionCommand}\"", saveText);
        Assert.Contains("var stackCompare = width < 980 || height < 760;", saveCode);
        Assert.Contains("SaveCompareRetentionScrollViewer.MaxHeight = stackCompare ? Math.Max(180, Math.Min(420, height * 0.42)) : double.PositiveInfinity;", saveCode);
        Assert.Contains("SaveCompareLayout.RowDefinitions[1].Height = stackCompare ? new GridLength(1, GridUnitType.Auto) : new GridLength(0);", saveCode);
        Assert.Contains("Grid.SetRow(SaveCompareRetentionScrollViewer, stackCompare ? 1 : 0);", saveCode);
        Assert.Contains("SaveCompareMainScrollViewer.MaxHeight = stackCompare ? Math.Max(220, Math.Min(420, height * 0.45)) : double.PositiveInfinity;", saveCode);
        Assert.DoesNotContain("MaxHeight=\"260\"", saveText);
    }

    [Fact]
    public void MaintenanceDeviceActionsUseSingleFiniteScrollChannel()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenanceCode = File.ReadAllText(maintenancePath + ".cs");
        var maintenance = XDocument.Parse(maintenanceText);

        // The device inspector is the single right-column scroll owner: one scroll
        // channel owns both the manual decision and the protected remote restore,
        // so the page never has two competing scroll bars beside the table.
        Assert.DoesNotContain("MaintenanceDeviceDecisionScrollViewer", maintenanceText);
        Assert.DoesNotContain("MaintenanceRemoteRestoreScrollViewer", maintenanceText);
        var viewer = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceDeviceInspectorScrollViewer");
        Assert.Equal("Auto", viewer.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", viewer.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("{DynamicResource GscInspectorScrollViewer}", viewer.Attribute("Style")?.Value);
        Assert.Equal("Stretch", viewer.Attribute("VerticalContentAlignment")?.Value);
        Assert.Null(viewer.Attribute("MaxHeight"));

        Assert.Contains("MaintenanceDeviceInspectorScrollViewer.MaxHeight = stackDevice ? Math.Max(180, Math.Min(420, height * 0.42)) : double.PositiveInfinity;", maintenanceCode);
        Assert.Contains("Command=\"{Binding SaveDeviceDecisionCommand}\"", maintenanceText);
        Assert.Contains("Command=\"{Binding StageRemoteBackupCommand}\"", maintenanceText);
        Assert.Contains("Command=\"{Binding RestoreStagedRemoteBackupCommand}\"", maintenanceText);
    }

    [Fact]
    public void MaintenanceProvidesReadOnlyRetentionPreviewTab()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);

        Assert.Contains("<TabItem Header=\"保留策略\">", maintenanceText);
        Assert.Contains("Command=\"{Binding PreviewRetentionCommand}\"", maintenanceText);
        Assert.Contains("{Binding LastRetentionPreview.KeepBackupIds.Count", maintenanceText);
        Assert.Contains("{Binding LastRetentionPreview.DeleteCandidateIds.Count", maintenanceText);
        Assert.Contains("ItemsSource=\"{Binding LastRetentionPreview.KeepBackupIds}\"", maintenanceText);
        Assert.Contains("ItemsSource=\"{Binding LastRetentionPreview.DeleteCandidateIds}\"", maintenanceText);
        Assert.Contains("Style=\"{DynamicResource GscPageScrollViewer}\"", maintenanceText);
        Assert.Contains("不会自动删除", maintenanceText);
    }

    [Fact]
    public void TrainerCenterMatchesDemoImportConfirmationTab()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trainerPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml");
        var trainerText = File.ReadAllText(trainerPath);

        Assert.Contains("<TabItem Header=\"已绑定工具\">", trainerText);
        Assert.Contains("<TabItem Header=\"导入确认\">", trainerText);
        Assert.Contains("ItemsSource=\"{Binding ImportEntryCandidates}\"", trainerText);
        Assert.Contains("Command=\"{Binding ConfirmGameToolImportCommand}\"", trainerText);
        Assert.Contains("Command=\"{Binding CancelGameToolImportCommand}\"", trainerText);
    }

    [Fact]
    public void HeaderActionsKeepAnInternalHorizontalScrollChannelAtNarrowWidths()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml");
        var dashboard = File.ReadAllText(dashboardPath);
        var document = XDocument.Parse(dashboard);
        var scroller = document.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "TopActionsScroller");

        Assert.Equal("Auto", scroller.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", scroller.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Contains("SetToolbarLabelsVisible(mode == LayoutMode.Expanded)", File.ReadAllText(dashboardPath + ".cs"));
    }

    [Fact]
    public void ExtractedWorkspaceViewsConstructInsideSta()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var pair in new (string Name, Action Factory)[]
                         {
                             ("Overview", () => _ = new OverviewView()),
                             ("Save", () => _ = new SaveCenterView()),
                             ("Trainer", () => _ = new TrainerCenterView()),
                             ("Media", () => _ = new MediaCenterView()),
                             ("Task", () => _ = new TaskCenterView()),
                             ("Maintenance", () => _ = new MaintenanceView())
                         })
                {
                    try { pair.Factory(); }
                    catch (Exception caught) { exception = new InvalidOperationException(pair.Name, caught); break; }
                }
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
    public void ExtractedWorkspacesRetainTheLessObviousOperationalEntrypoints()
    {
        var repositoryRoot = FindRepositoryRoot();
        var media = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));
        var trainer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));
        var trainerCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml.cs"));
        var mediaCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml.cs"));
        var maintenanceCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml.cs"));
        var taskCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml.cs"));
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var overview = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml"));
        var saves = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml"));
        var workspaceCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));

        Assert.Contains("MediaVideoSourceConverter", media);
        Assert.Contains("UpdateMediaMetadataCommand", media);
        Assert.Contains("ReassignMediaCommand", media);
        Assert.Contains("FavoriteSelectedMediaCommand", media);
        Assert.Contains("AddMediaSourceCommand", media);
        Assert.Contains("UpdateMediaSourceCommand", media);
        Assert.Contains("DeleteMediaSourceCommand", media);
        Assert.Contains("StageRemoteBackupCommand", maintenance);
        Assert.Contains("RestoreStagedRemoteBackupCommand", maintenance);
        Assert.Contains("HasPendingGameToolEntrySelection", trainer);
        Assert.Contains("ConfirmGameToolImportCommand", trainer);
        Assert.Contains("SelectedGameToolVersion", trainer);
        Assert.Contains("RequiresAdmin", trainer);
        Assert.Contains("TrainerReleasesLayout.RowDefinitions", trainerCode);
        Assert.Contains("x:Name=\"MediaInspectorScrollViewer\"", media);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\"", media);
        Assert.Contains("MinHeight=\"0\" ClipToBounds=\"True\"", media);
        Assert.Contains("MinHeight=\"0\" ItemsSource=\"{Binding UnassignedMedia}\"", media);
        Assert.Contains("OverridesDefaultStyle\" Value=\"True\"", maintenance);
        Assert.DoesNotContain("FindVisualChildren", maintenanceCode);
        Assert.Contains("MediaInspectorScrollViewer.MaxHeight = width >= 1080", mediaCode);
        Assert.Contains("MinHeight=\"96\" MaxHeight=\"160\"", maintenance);
        Assert.Contains("TaskSummaryPanel.Columns", taskCode);
        var taskView = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml"));
        Assert.Contains("x:Name=\"TaskDetailScrollViewer\"", taskView);
        Assert.Contains("SelectedIndex=\"0\"", taskView);
        Assert.Contains("TaskGameFilter, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, TargetNullValue=全部, FallbackValue=全部", taskView);
        Assert.Contains("TaskTypeFilter, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, TargetNullValue=全部, FallbackValue=全部", taskView);
        Assert.Contains("TaskDetailScrollViewer.MaxHeight = stack ? Math.Max(180, height * 0.42) : double.PositiveInfinity", taskCode);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\"", taskView);
        Assert.Contains("TaskWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("SaveWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("TrainerWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("MaintenanceWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("x:Key=\"GscRedesignWorkspaceTabControl\"", redesign);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", redesign);
        Assert.Contains("x:Key=\"GscRedesignWorkspaceTabItem\"", redesign);
        Assert.Contains("HorizontalContentAlignment\" Value=\"Stretch\"", redesign);
        Assert.Contains("VerticalContentAlignment\" Value=\"Stretch\"", redesign);
        Assert.Contains("CornerRadius=\"12\"", redesign);
        Assert.Contains("Stroke=\"{DynamicResource GscOnAccentTextBrush}\"", tokens);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", dashboard);
        Assert.Contains("Property=\"ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", trainer);
        Assert.Contains("VerticalScrollBarVisibility=\"Hidden\"", overview);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", saves);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", media);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", maintenance);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", trainer);
        Assert.Contains("GscCheckBox", saves);
        foreach (var view in new[] { overview, saves, trainer, media, maintenance })
        {
            Assert.DoesNotContain("Background=\"#", view);
            Assert.DoesNotContain("Foreground=\"#", view);
            Assert.Contains("DynamicResource Gsc", view);
        }
        Assert.DoesNotContain("BlurEffect", media + maintenance + trainer);
    }

    [Fact]
    public void DemoVisualVocabularyAndWorkspaceStretchContractRemainAvailable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        Assert.Contains("x:Key=\"GscReadingCardStyle\"", redesign);
        Assert.Contains("x:Key=\"GscSubCardStyle\"", redesign);
        Assert.Contains("x:Key=\"GscShellStyle\"", redesign);
        Assert.Contains("x:Key=\"GscPageTitleStyle\"", redesign);
        Assert.Contains("x:Key=\"GscSectionTitleStyle\"", redesign);
        Assert.Contains("x:Key=\"GscCaptionStyle\"", redesign);
        Assert.Contains("x:Key=\"GscBodyStyle\"", redesign);
        Assert.Contains("x:Name=\"DashboardDemoShell\" Margin=\"14\" Style=\"{StaticResource GscShellStyle}\"", dashboard);
        Assert.Contains("x:Key=\"GscButtonStyle\"", redesign);
        Assert.Contains("x:Key=\"GscPrimaryButtonStyle\"", redesign);
        Assert.Contains("x:Key=\"GscTabControlStyle\"", redesign);
        Assert.Contains("HorizontalAlignment\" Value=\"Stretch\"", redesign);
        Assert.Contains("VerticalAlignment\" Value=\"Stretch\"", redesign);

        foreach (var viewName in new[] { "SaveCenterView.xaml", "TrainerCenterView.xaml", "MediaCenterView.xaml", "MaintenanceView.xaml" })
        {
            var view = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", viewName));
            Assert.Contains("HorizontalContentAlignment\" Value=\"Stretch\"", view);
            Assert.Contains("VerticalContentAlignment\" Value=\"Stretch\"", view);
        }
    }

    [Fact]
    public void MaintenanceWorkspaceReflowsHealthCardsAndMappingEditor()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));
        var maintenanceCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml.cs"));

        Assert.Contains("x:Name=\"DiagnosticHealthPanel\"", maintenance);
        Assert.Contains("x:Name=\"ProcessMappingEditor\"", maintenance);
        Assert.Contains("MinWidth=\"220\"", maintenance);
        Assert.Contains("Command=\"{Binding SaveProcessMappingCommand}\"", maintenance);
        Assert.Contains("DiagnosticHealthPanel.Columns = width >= 1320 ? 4 : width >= 980 ? 2 : 1", maintenanceCode);
    }

    [Fact]
    public void MediaInspectorStacksBeforeItsEditingControlsAreCompressed()
    {
        var repositoryRoot = FindRepositoryRoot();
        var media = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml"));
        var mediaCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml.cs"));

        Assert.Contains("x:Name=\"MediaInspectorPanel\"", media);
        Assert.Contains("x:Name=\"MediaPreviewPanel\"", media);
        Assert.Contains("x:Name=\"MediaMetadataPanel\"", media);
        Assert.Contains("Property=\"EnableRowVirtualization\" Value=\"True\"", media);
        Assert.Contains("Property=\"EnableRowVirtualization\" Value=\"True\"", media);
        Assert.Contains("Text=\"{Binding MediaSummary.TotalSizeDisplay, Mode=OneWay}\"", media);
        Assert.DoesNotContain("MediaSummary.TotalSizeDisplay, Mode=TwoWay", media);
        Assert.Contains("var stack = width < 1080", mediaCode);
        Assert.Contains("MediaPreviewPanel.Margin = new Thickness(0, 14, 0, 14)", mediaCode);
        // Task book section 11: the current-game media inspector must keep the demo
        // details-first order (媒体详情 -> 文件名/路径 -> 预览 -> 收藏) so the preview
        // is never squeezed to the bottom of the card.
        var detailsTitle = media.IndexOf("Text=\"媒体详情\"", StringComparison.Ordinal);
        var fileNameLine = media.IndexOf("SelectedMedia.FileName, TargetNullValue=未选择媒体", StringComparison.Ordinal);
        var previewPanel = media.IndexOf("x:Name=\"MediaPreviewPanel\"", StringComparison.Ordinal);
        var favoriteToggle = media.IndexOf("Content=\"收藏\" OnContent=\"开\"", StringComparison.Ordinal);
        Assert.True(detailsTitle >= 0 && fileNameLine >= 0 && previewPanel >= 0 && favoriteToggle >= 0,
            "媒体中心 Inspector 缺少媒体详情、文件名或预览元素。");
        Assert.True(detailsTitle < fileNameLine && fileNameLine < previewPanel && previewPanel < favoriteToggle,
            "媒体中心 Inspector 顺序必须为 媒体详情 -> 文件名/路径 -> 预览 -> 收藏。");
        // Inspector width converged to the shared token (UI-152/UI-153): the
        // MediaCenter column must reference GscInspectorWidth, and the token
        // value must stay within the 350-380 DIP contract.
        Assert.Contains("Width=\"{StaticResource GscInspectorWidth}\"", media);
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));
        Assert.Contains("x:Key=\"GscInspectorWidth\">360</GridLength>", tokens);
    }

    [Fact]
    public void DemoPhaseTwoLayoutKeepsNaturalFormsAndDesktopInspectors()
    {
        var repositoryRoot = FindRepositoryRoot();
        var saves = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml"));
        var trainers = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var maintenanceCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml.cs"));

        Assert.Contains("MaxWidth=\"{StaticResource GscFormMaxWidth}\" HorizontalAlignment=\"Left\"", saves);
        // UI-170: the import-confirmation card stretches to fill the available
        // width (capped at 980) instead of shrinking to its natural width.
        Assert.Contains("MaxWidth=\"980\" HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Top\"", trainers);
        Assert.Contains("MaxWidth=\"1050\" HorizontalAlignment=\"Left\"", maintenance);
        Assert.Contains("var stackOverview = width < 1040", dashboardCode);
        Assert.Contains("var stackDiagnostics = width < 1120", maintenanceCode);
        Assert.Contains("var stackDevice = width < 1180", maintenanceCode);
        Assert.DoesNotContain("width < 1360", maintenanceCode);
    }

    [Fact]
    public void TrainerWorkspaceStacksVirtualizedPanesBeforeTheirControlsBecomeUnreadable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trainer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml.cs"));

        Assert.Contains("x:Name=\"TrainerToolsSettingsScrollViewer\"", trainer);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", trainer);
        Assert.Contains("x:Name=\"TrainerCatalogResultsPanel\"", trainer);
        Assert.Contains("x:Name=\"TrainerCatalogReleasesPanel\"", trainer);
        Assert.Contains("x:Name=\"TrainerReleasesLayout\"", trainer);
        Assert.Contains("x:Name=\"TrainerReleaseInfoPanel\"", trainer);
        Assert.Contains("var stackReleases = width < 980", codeBehind);
        Assert.Contains("Grid.SetColumnSpan(TrainerCatalogReleasesPanel, stackReleases ? 3 : 1)", codeBehind);
        Assert.Contains("Grid.SetRow(TrainerReleaseInfoPanel, stackReleases ? 1 : 0)", codeBehind);
        Assert.Contains("x:Name=\"TrainerReleaseInfoScrollViewer\"", trainer);
        Assert.Contains("TrainerReleaseInfoScrollViewer.MaxHeight = stackReleases", codeBehind);
        Assert.Contains("Math.Min(420, height * 0.42)", codeBehind);
    }

    [Fact]
    public void TrainerCatalogSelectionLoadsReleasesInTheExtractedWorkspace()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trainerPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml");
        var trainerCodePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml.cs");
        var trainer = File.ReadAllText(trainerPath);
        var trainerCode = File.ReadAllText(trainerCodePath);

        Assert.Contains("SelectedItem=\"{Binding SelectedTrainerCatalogItem}\" SelectionChanged=\"OnTrainerCatalogSelectionChanged\"", trainer);
        Assert.Contains("LoadTrainerReleasesCommand.CanExecute(null)", trainerCode);
        Assert.Contains("LoadTrainerReleasesCommand.Execute(null)", trainerCode);
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
            Assert.Equal("Auto", control.Attribute("ScrollViewer.VerticalScrollBarVisibility")?.Value);
            Assert.Equal("Disabled", control.Attribute("ScrollViewer.HorizontalScrollBarVisibility")?.Value);
        }
    }

    [Fact]
    public void AttentionActionsExposeAnAccessibleExplanationPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overview = XDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml")));
        var actions = overview.Descendants()
            .Where(element => element.Name.LocalName == "Button" && (element.Attribute("Command")?.Value.IndexOf("OpenAttentionCenterCommand", StringComparison.Ordinal) ?? -1) >= 0)
            .ToList();

        Assert.Equal(4, actions.Count);
        Assert.Contains(actions, element => element.Attribute("AutomationProperties.Name")?.Value == "查看需要关注的游戏、原因和建议处理方式");
        Assert.Contains(actions, element => element.Attribute("AutomationProperties.Name")?.Value == "打开维护中心查看完整关注详情");
        Assert.Contains(actions, element => element.Attribute("ToolTip")?.Value == "点击查看需要关注的游戏、原因和建议处理方式");
        Assert.Contains(overview.Descendants(), element => element.Name.LocalName == "ItemsControl" && (element.Attribute("ItemsSource")?.Value.IndexOf("AttentionFindings", StringComparison.Ordinal) ?? -1) >= 0);
    }

    [Fact]
    public void AttentionFindingRowsUseDemoIconTileRhythmWithAReasonButton()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overviewPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml");
        var overview = XDocument.Parse(File.ReadAllText(overviewPath));
        var itemsControl = overview.Descendants().Single(element => element.Name.LocalName == "ItemsControl" && (element.Attribute("ItemsSource")?.Value.IndexOf("AttentionFindings", StringComparison.Ordinal) ?? -1) >= 0);
        var template = itemsControl.Elements().Single(element => element.Name.LocalName == "ItemsControl.ItemTemplate").Elements().Single();

        // The demo home card renders each attention finding as a 34x34 rounded icon tile
        // with a game title, a muted reason line and a per-row "查看原因" action. The
        // production row keeps the same rhythm while staying bound to real findings.
        Assert.Equal("DataTemplate", template.Name.LocalName);
        var grid = template.Elements().Single(element => element.Name.LocalName == "Grid");
        Assert.Equal("38", grid.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions").Elements().ElementAt(0).Attribute("Width")?.Value);
        var tile = grid.Descendants().Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "AttentionFindingIcon");
        Assert.Equal("34", tile.Attribute("Width")?.Value);
        Assert.Equal("34", tile.Attribute("Height")?.Value);
        Assert.Equal("10", tile.Attribute("CornerRadius")?.Value);
        Assert.Contains(grid.Descendants(), element => element.Name.LocalName == "Button" && (element.Attribute("Content")?.Value == "查看原因") && ((element.Attribute("Command")?.Value.IndexOf("OpenAttentionFindingCommand", StringComparison.Ordinal) ?? -1) >= 0));
        Assert.Contains(template.Elements(), element => element.Name.LocalName == "DataTemplate.Triggers");
        var overviewText = File.ReadAllText(overviewPath);
        Assert.Contains("Value=\"Error\"", overviewText);
        Assert.Contains("Value=\"Critical\"", overviewText);
    }

    [Fact]
    public void OverviewRecentActivityUsesDemoIconTileRhythmWithSemanticTriggers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overviewPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml");
        var overview = XDocument.Parse(File.ReadAllText(overviewPath));
        var overviewText = File.ReadAllText(overviewPath);
        var xamlName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var list = overview.Descendants().Single(element => element.Name.LocalName == "ListBox" && element.Attribute(xamlName)?.Value == "OverviewActivityList");

        // The demo home card renders recent activity as a 38/*/Auto row with a 34x34
        // rounded icon tile, a title, a muted subtitle and the local time on the right.
        // Production keeps the same rhythm while staying bound to real tasks and to the
        // existing SelectedTask cross-page detail linkage.
        Assert.Equal("{Binding OverviewTasks}", list.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding SelectedTask}", list.Attribute("SelectedItem")?.Value);
        Assert.Equal("Stretch", list.Attribute("HorizontalContentAlignment")?.Value);
        Assert.Equal("True", list.Attribute("VirtualizingPanel.IsVirtualizing")?.Value);
        Assert.Equal("Recycling", list.Attribute("VirtualizingPanel.VirtualizationMode")?.Value);
        Assert.Equal("True", list.Attribute("ScrollViewer.CanContentScroll")?.Value);
        var template = list.Elements().Single(element => element.Name.LocalName == "ListBox.ItemTemplate").Elements().Single();
        Assert.Equal("DataTemplate", template.Name.LocalName);
        var grid = template.Elements().Single(element => element.Name.LocalName == "Grid");
        var columnWidths = grid.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(element => element.Attribute("Width")?.Value).ToArray();
        Assert.Equal(new[] { "38", "*", "Auto" }, columnWidths);

        var tile = grid.Descendants().Single(element => element.Attribute(xamlName)?.Value == "OverviewTaskStatusPill");
        Assert.Equal("34", tile.Attribute("Width")?.Value);
        Assert.Equal("34", tile.Attribute("Height")?.Value);
        Assert.Equal("10", tile.Attribute("CornerRadius")?.Value);
        Assert.Equal("{DynamicResource GscSuccessIconFillBrush}", tile.Attribute("Background")?.Value);
        Assert.Contains("Text=\"&#xE73E;\"", overviewText);

        // Long names and details must trim with a tooltip instead of pushing the row.
        var title = grid.Descendants().Single(element => element.Name.LocalName == "TextBlock" && element.Attribute("Text")?.Value == "{Binding GameName, Mode=OneWay, TargetNullValue=全局}");
        Assert.Equal("SemiBold", title.Attribute("FontWeight")?.Value);
        Assert.Equal("CharacterEllipsis", title.Attribute("TextTrimming")?.Value);
        Assert.Equal("{Binding GameName}", title.Attribute("ToolTip")?.Value);
        var subtitle = grid.Descendants().Single(element => element.Name.LocalName == "TextBlock" && element.Attribute("Margin")?.Value == "0,3,0,0");
        Assert.Equal("11", subtitle.Attribute("FontSize")?.Value);
        Assert.Equal("CharacterEllipsis", subtitle.Attribute("TextTrimming")?.Value);
        Assert.Equal("{Binding DetailMessage}", subtitle.Attribute("ToolTip")?.Value);
        Assert.Contains("TaskTypeDisplay", subtitle.ToString());
        Assert.Contains("StateDisplay", subtitle.ToString());
        Assert.Contains("StringFormat={}{0:MM-dd HH:mm}", overviewText);

        // Failed / Running / Cancelled must stay semantically distinct on the tile.
        var triggers = template.Elements().Single(element => element.Name.LocalName == "DataTemplate.Triggers")
            .Elements().Where(element => element.Name.LocalName == "DataTrigger").ToList();
        Assert.Contains(triggers, trigger => trigger.Attribute("Value")?.Value == "Failed"
            && trigger.Descendants().Any(setter => setter.Attribute("TargetName")?.Value == "OverviewTaskStatusPill" && setter.Attribute("Property")?.Value == "Background" && setter.Attribute("Value")?.Value == "{DynamicResource GscErrorTintBrush}"));
        Assert.Contains(triggers, trigger => trigger.Attribute("Value")?.Value == "Running"
            && trigger.Descendants().Any(setter => setter.Attribute("TargetName")?.Value == "OverviewTaskStatusPill" && setter.Attribute("Property")?.Value == "Background" && setter.Attribute("Value")?.Value == "{DynamicResource GscInfoIconFillBrush}"));
        Assert.Contains(triggers, trigger => trigger.Attribute("Value")?.Value == "Cancelled"
            && trigger.Descendants().Any(setter => setter.Attribute("TargetName")?.Value == "OverviewTaskStatusPill" && setter.Attribute("Property")?.Value == "Background" && setter.Attribute("Value")?.Value == "{DynamicResource GscControlFillBrush}"));

        // The shared implicit ListBox contract keeps the capped list virtualized and
        // locally scrollable instead of inheriting a square host panel.
        var production = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "WpfUiProduction.xaml"));
        Assert.Contains("<Style TargetType=\"ListBox\">", production);
        Assert.Contains("VirtualizingPanel.IsVirtualizing\" Value=\"True\"", production);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"", production);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", production);
    }

    [Fact]
    public void OverviewShowsTheSameAttentionAndRuntimeCountersReturnedByTheSnapshot()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overview = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml"));
        var dashboardService = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Worker", "Services", "DashboardService.cs"));

        // The overview must make the two states that are otherwise easy to miss visible:
        // active games and games requiring attention. Keep these bindings OneWay so a
        // read-only snapshot cannot accidentally be written back from a template.
        Assert.Contains("Text=\"{Binding Snapshot.RunningGames, Mode=OneWay}\"", overview);
        Assert.Contains("Text=\"{Binding Snapshot.WarningGames, Mode=OneWay}\"", overview);
        Assert.Contains(".Where(x=>x.Severity>=FindingSeverity.Warning)", dashboardService);
        Assert.Contains("WarningGames=findings.Where(x=>x.Severity>=FindingSeverity.Warning)", dashboardService);
    }

    [Fact]
    public void OverviewStatStripKeepsSixRealSnapshotCardsWithMotionGatedHover()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overview = XDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml")));
        var strip = overview.Descendants().Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "OverviewStatStrip");
        var cards = strip.Elements().Where(element => element.Name.LocalName == "Border").ToList();

        // The six cards are real Snapshot counters, never demo placeholders.
        Assert.Equal(6, cards.Count);
        Assert.All(cards, card => Assert.Equal("{StaticResource OverviewStatCard}", card.Attribute("Style")?.Value));
        Assert.Contains("Binding Snapshot.ManagedGames, Mode=OneWay", strip.ToString());
        Assert.Contains("Binding Snapshot.MatchedGames, Mode=OneWay", strip.ToString());
        Assert.Contains("Binding Snapshot.RunningGames, Mode=OneWay", strip.ToString());
        Assert.Contains("Binding Snapshot.WarningGames, Mode=OneWay", strip.ToString());
        Assert.Contains("Binding Snapshot.PendingCloudTasks, Mode=OneWay", strip.ToString());
        Assert.Contains("Binding Snapshot.UnassignedMediaCount, Mode=OneWay", strip.ToString());

        // Hover feedback stays render-only and is wired through EventSetters on the card style.
        var overviewText = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml"));
        Assert.Contains("<EventSetter Event=\"MouseEnter\" Handler=\"OnStatCardMouseEnter\"/>", overviewText);
        Assert.Contains("<EventSetter Event=\"MouseLeave\" Handler=\"OnStatCardMouseLeave\"/>", overviewText);
        Assert.Contains("RenderTransformOrigin", overviewText);

        // The dashboard motion gate must reach the overview cards so animations are
        // disabled when the user turns off animations, enables High Contrast, or the
        // system disables client-area animation.
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        Assert.Contains("OverviewWorkspaceView.UiAnimationsEnabled = MotionEnabled;", dashboardCode);
        var overviewCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml.cs"));
        Assert.Contains("SystemParameters.HighContrast", overviewCode);
        Assert.Contains("SystemParameters.ClientAreaAnimation", overviewCode);
        Assert.Contains("AnimateTranslate(sender as FrameworkElement, 0, -3, 160)", overviewCode);
    }

    [Fact]
    public void OverviewStatCardsShowRealRatioBarsThatCollapseOnEmptyLibrary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var overview = XDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml")));
        var strip = overview.Descendants().Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "OverviewStatStrip");
        var bars = strip.Descendants().Where(element => element.Name.LocalName == "ProgressBar").ToList();

        // Lightweight real-data charts: the coverage and attention ratio bars reuse the
        // shared ProgressBar template and bind only real Snapshot counters, OneWay.
        Assert.Equal(2, bars.Count);
        var matchedBar = Assert.Single(bars, bar => bar.Attribute("Value")?.Value == "{Binding Snapshot.MatchedGames, Mode=OneWay}");
        Assert.Equal("{Binding Snapshot.ManagedGames, Mode=OneWay}", matchedBar.Attribute("Maximum")?.Value);
        var warningBar = Assert.Single(bars, bar => bar.Attribute("Value")?.Value == "{Binding Snapshot.WarningGames, Mode=OneWay}");
        Assert.Equal("{Binding Snapshot.ManagedGames, Mode=OneWay}", warningBar.Attribute("Maximum")?.Value);
        Assert.Equal("{DynamicResource GscWarningBrush}", warningBar.Attribute("Foreground")?.Value);

        // Zero-denominator guard: both bars collapse when the library is empty so the
        // host ProgressBar never sizes PART_Indicator against Maximum == 0.
        var collapseTriggers = strip.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger"
                && element.Attribute("Binding")?.Value == "{Binding Snapshot.ManagedGames}"
                && element.Attribute("Value")?.Value == "0")
            .ToList();
        Assert.Equal(2, collapseTriggers.Count);
        Assert.All(collapseTriggers, trigger =>
            Assert.Contains("Setter Property=\"Visibility\" Value=\"Collapsed\"", trigger.ToString()));
    }

    [Fact]
    public void SharedPageScrollViewerStretchesContentAndLeavesBottomBreathingRoom()
    {
        var repositoryRoot = FindRepositoryRoot();
        var designTokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));

        Assert.Contains("x:Key=\"GscPageScrollViewer\"", designTokens);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Stretch\"/>", designTokens);
        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Top\"/>", designTokens);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"0,0,4,6\"/>", designTokens);
    }

    [Fact]
    public void SaveHistoryUsesReadableStatusLabelsAndRoundedStatusTemplates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var savePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml");
        var save = XDocument.Parse(File.ReadAllText(savePath));
        var history = save.Descendants().Single(element => element.Name.LocalName == "TabItem" && element.Attribute("Header")?.Value == "历史版本");

        Assert.Contains(history.Descendants(), element => element.Name.LocalName == "DataGridTemplateColumn" && element.Attribute("Header")?.Value == "类型");
        Assert.Contains(history.Descendants(), element => element.Name.LocalName == "DataGridTemplateColumn" && element.Attribute("Header")?.Value == "状态");
        var dashboardDtos = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Contracts", "DashboardDtos.cs"));
        Assert.Contains("BackupTypeDisplay", dashboardDtos);
        Assert.Contains("LockStateDisplay", dashboardDtos);
        Assert.Contains(history.Descendants(), element => element.Name.LocalName == "Border" && (element.Attribute("Style")?.Value.IndexOf("GscRedesignTableStatusPill", StringComparison.Ordinal) ?? -1) >= 0);
    }

    [Fact]
    public void TrainerCardsUseReadableAutoStartStatusInsteadOfRawBoolean()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trainer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));
        var contract = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Contracts", "TrainerDtos.cs"));

        Assert.Contains("Text=\"{Binding AutoStartDisplay, Mode=OneWay}\"", trainer);
        Assert.DoesNotContain("Text=\"{Binding AutoStart, Mode=OneWay}\"", trainer);
        Assert.Contains("public string AutoStartDisplay => AutoStart", contract);
    }

    [Fact]
    public void TaskAndMaintenanceTablesUseReadableSemanticStatusTemplates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var task = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml"));
        var overview = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));
        var contracts = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Contracts", "OperationDtos.cs"));

        Assert.Contains("x:Name=\"TaskStatusPill\"", task);
        Assert.Contains("x:Name=\"OverviewTaskStatusPill\"", overview);
        Assert.Contains("x:Name=\"SeverityPill\"", maintenance);
        Assert.DoesNotContain("Header=\"等级\" Binding=\"{Binding Severity}\"", maintenance);
        Assert.Contains("Text=\"{Binding SeverityDisplay, Mode=OneWay}\"", maintenance);
        Assert.Contains("public string SeverityDisplay => Severity switch", contracts);
    }

    [Fact]
    public void OptionalWpfUiProbeKeepsItsChecklistInsideAFixedGridRow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var probe = XDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "Development", "UiFrameworkProbeView.xaml")));
        var checklist = probe.Descendants().Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ProbeChecklist");

        Assert.Equal("ListBox", checklist.Name.LocalName);
        Assert.Equal("1", checklist.Attribute("Grid.Row")?.Value);
        Assert.DoesNotContain(checklist.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        Assert.Contains(checklist.Ancestors(), ancestor => ancestor.Name.LocalName == "Grid");
        Assert.Equal("132", checklist.Parent?.Elements().First(element => element.Name.LocalName == "Grid.RowDefinitions").Elements().ElementAt(1).Attribute("Height")?.Value);
    }

    [Fact]
    public void MediaSourcesKeepPathEditingAndSafetyCommandsReachableInCompactLayouts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var media = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml.cs"));

        Assert.Contains("x:Name=\"MediaSourceFields\"", media);
        Assert.Contains("Command=\"{Binding AddMediaSourceCommand}\"", media);
        Assert.Contains("Command=\"{Binding DataContext.UpdateMediaSourceCommand", media);
        Assert.Contains("Command=\"{Binding DataContext.DeleteMediaSourceCommand", media);
        Assert.Contains("Property=\"EnableRowVirtualization\" Value=\"True\"", media);
        Assert.Contains("MediaSourceFields.Columns = width >= 820 ? 2 : 1", codeBehind);
    }

    [Fact]
    public void DeviceDecisionsPreserveProtectedRecoveryAndReadableCompactFields()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenance = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml"));

        Assert.Contains("x:Name=\"MaintenanceDeviceInspectorScrollViewer\"", maintenance);
        Assert.Contains("Command=\"{Binding SaveDeviceDecisionCommand}\"", maintenance);
        Assert.Contains("Command=\"{Binding StageRemoteBackupCommand}\"", maintenance);
        Assert.Contains("Command=\"{Binding RestoreStagedRemoteBackupCommand}\"", maintenance);
        Assert.Contains("仅记录判断依据", maintenance);
    }

    [Fact]
    public void DenseGridLongTextUsesTheSharedEllipsisAndTooltipStyle()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var workspacePaths = new[] { "OverviewView.xaml", "SaveCenterView.xaml", "TrainerCenterView.xaml", "MediaCenterView.xaml", "TaskCenterView.xaml", "MaintenanceView.xaml" }
            .Select(name => Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", name))
            .ToArray();
        var workspaceText = string.Join("\n", workspacePaths.Select(File.ReadAllText));

        Assert.Contains("x:Key=\"GscLongTextCell\"", dashboard);
        Assert.Contains("BasedOn=\"{StaticResource GscLeftCellText}\"", dashboard);
        Assert.Contains("ToolTip\" Value=\"{Binding Text, RelativeSource={RelativeSource Self}}\"", dashboard);

        var documents = workspacePaths.Select(path => XDocument.Parse(File.ReadAllText(path))).ToArray();
        var columns = documents.SelectMany(document => document.Descendants().Where(element => element.Name.LocalName == "DataGridTextColumn"));
        foreach (var column in new[]
        {
            new { Header = "目标游戏", Binding = "GameName" },
            new { Header = "其他设备", Binding = "RemoteDevice" },
            new { Header = "人工决策", Binding = "DecisionDisplay" },
            new { Header = "标题", Binding = "Title" }
        })
        {
            var columnElement = columns.FirstOrDefault(element =>
                element.Name.LocalName == "DataGridTextColumn"
                && element.Attribute("Header")?.Value == column.Header
                    && (element.Attribute("Binding")?.Value.IndexOf(column.Binding, StringComparison.Ordinal) ?? -1) >= 0);
            Assert.NotNull(columnElement);
            Assert.True(
                (columnElement!.Attribute("ElementStyle")?.Value.IndexOf("LongText", StringComparison.Ordinal) ?? -1) >= 0
                || columnElement.Descendants().Any(element =>
                    element.Name.LocalName == "Style"
                    && (element.Attribute("BasedOn")?.Value.IndexOf("LongText", StringComparison.Ordinal) ?? -1) >= 0),
                $"长文本表格列未复用共享 LongTextCell：Header={column.Header}, Binding={column.Binding}");
        }

        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", workspaceText);
        Assert.All(documents.SelectMany(document => document.Descendants().Where(element => element.Name.LocalName == "DataGrid")),
            grid => Assert.DoesNotContain(grid.Descendants(), element => element.Name.LocalName == "BlurEffect"));
    }

    [Fact]
    public void FiniteWidthComboBoxesUseTheSharedLongTextTemplate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var workspacePaths = new[] { "SaveCenterView.xaml", "TrainerCenterView.xaml", "MediaCenterView.xaml", "MaintenanceView.xaml" }
            .Select(name => Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", name))
            .ToArray();
        var combined = dashboard + "\n" + string.Join("\n", workspacePaths.Select(File.ReadAllText));

        Assert.Contains("x:Key=\"GscComboBoxLongText\"", combined);
        Assert.Contains("<Setter Property=\"TextTrimming\" Value=\"CharacterEllipsis\"/>", combined);
        Assert.Contains("<Setter Property=\"ToolTip\" Value=\"{Binding Text, RelativeSource={RelativeSource Self}}\"/>", combined);

        var documents = new[] { XDocument.Parse(dashboard) }.Concat(workspacePaths.Select(path => XDocument.Parse(File.ReadAllText(path)))).ToArray();
        var xamlName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        var comboBoxes = documents.SelectMany(document => document.Descendants().Where(element => element.Name.LocalName == "ComboBox")).ToList();
        var targets = new[]
        {
            new { Description = "ImportEntryCandidates", Match = (Func<XElement, bool>)(element => element.Attribute("ItemsSource")?.Value == "{Binding ImportEntryCandidates}") },
            new { Description = "SelectedGameTool.Versions", Match = (Func<XElement, bool>)(element => element.Attribute("ItemsSource")?.Value == "{Binding SelectedGameTool.Versions}") },
            new { Description = "InboxTargetGame", Match = (Func<XElement, bool>)(element => element.Attribute("SelectedItem")?.Value == "{Binding InboxTargetGame}") },
            new { Description = "MediaTargetGame", Match = (Func<XElement, bool>)(element => element.Attribute("SelectedItem")?.Value == "{Binding MediaTargetGame}") },
            new { Description = "ProcessMappingTargetGame", Match = (Func<XElement, bool>)(element => element.Attribute("SelectedItem")?.Value == "{Binding ProcessMappingTargetGame}") }
        };

        foreach (var target in targets)
        {
            var matches = comboBoxes.Where(target.Match).ToList();
            Assert.NotEmpty(matches);
            foreach (var comboBox in matches)
            {
                Assert.True(
                    comboBox.Descendants().Any(element =>
                        element.Name.LocalName == "TextBlock"
                        && ((element.Attribute("Style")?.Value.IndexOf("GscComboBoxLongText", StringComparison.Ordinal) ?? -1) >= 0
                            || element.Attribute("TextTrimming")?.Value == "CharacterEllipsis")),
                    "受限宽度下拉选择未复用 GscComboBoxLongText：" + target.Description);
            }
        }

        Assert.DoesNotContain("DisplayMemberPath=\"Display\"", combined);
        Assert.DoesNotContain("DisplayMemberPath=\"VersionName\"", combined);
    }

    [Fact]
    public void LargeGameLibrariesUseOneVirtualizedSearchableSelectorSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var dashboardViewModel = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var xaml = XDocument.Parse(dashboard);
        var xamlName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        var contextButton = xaml.Descendants().SingleOrDefault(element =>
            element.Name.LocalName == "Button"
            && element.Attribute(xamlName)?.Value == "CompactGameSelector");
        Assert.NotNull(contextButton);
        Assert.Null(contextButton!.Attribute("ItemsSource"));
        Assert.Contains("OnToggleGameBrowserClick", contextButton.Attribute("Click")?.Value ?? string.Empty);
        Assert.Contains("SelectedGame.Name", contextButton.ToString(SaveOptions.DisableFormatting));

        var gameList = xaml.Descendants().SingleOrDefault(element =>
            element.Name.LocalName == "ListBox"
            && element.Attribute("ItemsSource")?.Value == "{Binding GamePicker.ItemsView}");
        Assert.NotNull(gameList);
        Assert.Equal(1, xaml.Descendants().Count(element =>
            element.Name.LocalName == "ListBox"
            && element.Attribute("ItemsSource")?.Value == "{Binding GamePicker.ItemsView}"));
        Assert.Equal("True", gameList!.Attribute("VirtualizingPanel.IsVirtualizing")?.Value);
        Assert.Equal("Recycling", gameList.Attribute("VirtualizingPanel.VirtualizationMode")?.Value);
        Assert.Equal("True", gameList.Attribute("ScrollViewer.CanContentScroll")?.Value);
        Assert.Equal("OnGameSelectionChanged", gameList.Attribute("SelectionChanged")?.Value);
        Assert.Equal("OnGamePickerMouseUp", gameList.Attribute("PreviewMouseLeftButtonUp")?.Value);
        Assert.Equal("OnGamePickerPreviewKeyDown", gameList.Attribute("PreviewKeyDown")?.Value);
        var gameSearch = xaml.Descendants().Single(element => element.Name.LocalName == "TextBox" && element.Attribute(xamlName)?.Value == "GameSearchTextBox");
        Assert.Equal("OnGamePickerPreviewKeyDown", gameSearch.Attribute("PreviewKeyDown")?.Value);

        Assert.Contains("GamePicker.SearchText", dashboard);
        Assert.Contains("GamePicker.StatusFilterOptions", dashboard);
        Assert.Contains("GamePicker.SortOptions", dashboard);
        Assert.Contains("GamePicker.PlatformFilterOptions", dashboard);
        Assert.Contains("SelectedIndex=\"0\" ItemsSource=\"{Binding GamePicker.PlatformFilterOptions}\"", dashboard);
        Assert.Contains("TargetNullValue=全部, FallbackValue=全部", dashboard);
        Assert.Contains("GameSwitcherHost.Visibility = gameScopedWorkspace", dashboardCode);
        Assert.Contains("ToggleGameBrowserButton.Visibility = Visibility.Collapsed", dashboardCode);
        Assert.Contains("LoadSelectionDetailsAsync", dashboardViewModel);
        Assert.Contains("CancelDetailsLoad();", dashboardViewModel);
        Assert.Contains("expectedGeneration", dashboardViewModel);
        Assert.Contains("OnGamePickerPreviewKeyDown", dashboardCode);
        Assert.DoesNotContain("x:Name=\"OverviewGameSelector\"", dashboard);
        Assert.Equal(1, xaml.Descendants().Count(element => element.Name.LocalName == "Button" && element.Attribute(xamlName)?.Value == "CompactGameSelector"));
        Assert.Contains("gameScopedWorkspace = viewModel.CurrentWorkspace != WorkspaceKind.Tasks", dashboardCode);

        foreach (var workspace in new[] { "OverviewView.xaml", "SaveCenterView.xaml", "TrainerCenterView.xaml", "MediaCenterView.xaml", "TaskCenterView.xaml", "MaintenanceView.xaml" })
        {
            var workspaceText = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", workspace));
            Assert.DoesNotContain("GamePicker.ItemsView", workspaceText);
            Assert.DoesNotContain("CompactGameSelector", workspaceText);
        }
    }

    [Fact]
    public void OverviewWorkspaceIsPhysicallyExtractedWithoutBreakingResponsiveCoordinator()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var overviewPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "OverviewView.xaml");
        var overview = XDocument.Parse(File.ReadAllText(overviewPath));

        Assert.Contains("x:Name=\"OverviewWorkspaceTab\"", dashboard);
        Assert.Contains("<views:OverviewView x:Name=\"OverviewWorkspaceView\"/>", dashboard);
        Assert.DoesNotContain("SetVisibility(OverviewTab, false);", dashboardCode);
        Assert.DoesNotContain("OverviewTab", dashboard);
        Assert.Contains("OverviewWorkspaceView.ApplyResponsiveColumns(stackOverview);", dashboardCode);
        Assert.Contains("OverviewWorkspaceView.ApplyResponsiveHeight(height, stackOverview);", dashboardCode);
        Assert.Contains("OverviewWorkspaceView.OverviewCompactSecondaryRowHeight", dashboardCode);

        var overviewGrid = overview.Descendants().Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "OverviewLayoutGrid");
        Assert.Contains(overviewGrid.Descendants(), element => element.Name.LocalName == "Grid");
        var metricPanel = overview.Descendants().Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "OverviewMetricPanel");
        Assert.Equal("WrapPanel", metricPanel.Name.LocalName);
        Assert.DoesNotContain("OverviewMetricPanel.Columns", File.ReadAllText(overviewPath + ".cs"));
        var activityList = overview.Descendants().Single(element => element.Name.LocalName == "ListBox" && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "OverviewActivityList");
        Assert.Equal("{Binding OverviewTasks}", activityList.Attribute("ItemsSource")?.Value);
        Assert.Equal("{Binding SelectedTask}", activityList.Attribute("SelectedItem")?.Value);
        Assert.DoesNotContain(activityList.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        var productionTheme = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "WpfUiProduction.xaml"));
        Assert.Contains("<Style TargetType=\"ListBox\">", productionTheme);
        Assert.Contains("VirtualizingPanel.IsVirtualizing\" Value=\"True\"", productionTheme);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"", productionTheme);
        Assert.Contains("OpenAttentionFindingCommand", File.ReadAllText(overviewPath));
        Assert.Contains("x:Name=\"OverviewRiskScrollViewer\"", File.ReadAllText(overviewPath));
        Assert.Contains("x:Name=\"OverviewSecondaryScrollViewer\"", File.ReadAllText(overviewPath));
        Assert.Contains("OverviewSecondaryScrollViewer.MaxHeight = stack", File.ReadAllText(overviewPath + ".cs"));
        Assert.DoesNotContain("OverviewSecondaryScrollViewer.MaxHeight = stack || compactHeight", File.ReadAllText(overviewPath + ".cs"));
        Assert.Contains("OverviewSecondaryScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden", File.ReadAllText(overviewPath + ".cs"));
        Assert.Contains("OverviewRiskScrollViewer.MaxHeight = Math.Max(180, Math.Min(360, height * 0.42))", File.ReadAllText(overviewPath + ".cs"));
        Assert.Contains("RowDefinition x:Name=\"OverviewSummaryRow\" Height=\"Auto\"", File.ReadAllText(overviewPath));
        Assert.Contains("GscRedesignSectionCard}\" VerticalAlignment=\"Top\">", File.ReadAllText(overviewPath));
        Assert.Contains("VerticalAlignment=\"Top\">", File.ReadAllText(overviewPath));
    }

    [Fact]
    public void DashboardDoesNotRenderASecondLegacyOverviewMetricStrip()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        // OverviewView owns the summary surface. Keeping the old six-card strip in the
        // Dashboard shell duplicates information and consumes the vertical budget needed by
        // the real activity/risk workspace at ordinary window sizes.
        Assert.DoesNotContain("x:Name=\"MetricsPanel\"", dashboard);
        Assert.DoesNotContain("MetricsPanel", dashboardCode);
        Assert.Contains("<views:OverviewView x:Name=\"OverviewWorkspaceView\"/>", dashboard);
    }

    [Fact]
    public void TaskWorkspaceIsPhysicallyExtractedAsAGlobalVirtualizedSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var taskPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml");
        var task = XDocument.Parse(File.ReadAllText(taskPath));

        Assert.Contains("x:Name=\"TaskWorkspaceTab\"", dashboard);
        Assert.Contains("<views:TaskCenterView x:Name=\"TaskWorkspaceView\"/>", dashboard);
        Assert.DoesNotContain("SetVisibility(TaskTab, false);", dashboardCode);
        Assert.DoesNotContain("TaskTab", dashboard);
        Assert.Contains("TaskWorkspaceView.ApplyResponsiveLayout(width, height)", dashboardCode);
        Assert.Contains("TaskWorkspaceView.TaskDetailCardElement", dashboardCode);
        Assert.DoesNotContain("GamePicker", File.ReadAllText(taskPath));

        var dataGrid = task.Descendants().Single(element => element.Name.LocalName == "DataGrid");
        Assert.DoesNotContain(dataGrid.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        Assert.Contains("<Setter Property=\"EnableRowVirtualization\" Value=\"True\"/>", File.ReadAllText(taskPath));
        Assert.Contains("CopyTaskErrorCommand", File.ReadAllText(taskPath));
        Assert.Contains("RetryTaskCommand", File.ReadAllText(taskPath));
        Assert.Contains("CancelTaskCommand", File.ReadAllText(taskPath));
    }

    [Fact]
    public void MediaSourceRulesKeepTheTableInAStarRowWithAFormOnlyScroller()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mediaPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MediaCenterView.xaml");
        var media = XDocument.Parse(File.ReadAllText(mediaPath));
        var tabItem = media.Descendants().Single(element => element.Name.LocalName == "TabItem" && element.Attribute("Header")?.Value == "来源规则");
        var sourceList = tabItem.Descendants().Single(element => element.Name.LocalName == "ListBox" && element.Attribute("ItemsSource")?.Value == "{Binding MediaSources}");
        Assert.DoesNotContain(sourceList.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        Assert.Contains(sourceList.Ancestors(), ancestor => ancestor.Name.LocalName == "Border");
        var sourceTableBorder = sourceList.Ancestors().First(ancestor => ancestor.Name.LocalName == "Border" && ancestor.Attribute("Grid.Row")?.Value == "1");
        Assert.DoesNotContain(sourceTableBorder.Attributes(), attribute => attribute.Name.LocalName == "MaxHeight");
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", File.ReadAllText(mediaPath));
        var formScroller = tabItem.Descendants().Single(element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain(formScroller.Attributes(), attribute => attribute.Name.LocalName == "MaxHeight");
        Assert.Equal("Auto", formScroller.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Contains(tabItem.Descendants(), element => element.Name.LocalName == "RowDefinition" && element.Attribute("Height")?.Value == "*");
        Assert.Contains("Property=\"MinHeight\" Value=\"{DynamicResource GscWorkspaceTableMinHeight}\"", File.ReadAllText(mediaPath));
    }

    [Fact]
    public void EmptyDataSurfacesExplainNextStepsWithoutBreakingLocalScrolling()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));
        Assert.Contains("x:Key=\"GscEmptyStateText\"", tokens);
        Assert.Contains("IsHitTestVisible\" Value=\"False\"", tokens);

        var views = new[]
        {
            ("TaskCenterView.xaml", "TasksView.IsEmpty"),
            ("SaveCenterView.xaml", "Backups.Count"),
            ("SaveCenterView.xaml", "SaveCandidates.Count"),
            ("MediaCenterView.xaml", "MediaView.IsEmpty"),
            ("MediaCenterView.xaml", "UnassignedMedia.Count"),
            ("MediaCenterView.xaml", "MediaSources.Count"),
            ("TrainerCenterView.xaml", "GameTools.Count"),
            ("TrainerCenterView.xaml", "TrainerCatalogResults.Count"),
            ("TrainerCenterView.xaml", "TrainerReleases.Count"),
            ("MaintenanceView.xaml", "Findings.Count"),
            ("MaintenanceView.xaml", "DeviceComparisons.Count"),
            ("MaintenanceView.xaml", "Audit.Count"),
            ("MaintenanceView.xaml", "ProcessMappings.Count")
        };

        foreach (var (file, trigger) in views)
        {
            var text = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", file));
            Assert.Contains("BasedOn=\"{StaticResource GscEmptyStateText}\"", text);
            Assert.Contains(trigger, text);
            Assert.Contains("IsHitTestVisible=\"False\"", text);
        }

        var xamlFiles = views.Select(x => x.Item1).Distinct().ToArray();
        foreach (var file in xamlFiles)
        {
            var document = XDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", file)));
            foreach (var overlay in document.Descendants().Where(element => element.Name.LocalName == "TextBlock" && element.Attribute("IsHitTestVisible")?.Value == "False"))
            {
                Assert.DoesNotContain(overlay.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
            }
        }
    }

    [Fact]
    public void EveryMaintenanceDataGridHasAnEmptyStateOverlay()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenance = XDocument.Parse(File.ReadAllText(maintenancePath));

        // Diagnostics, device, audit findings, audit log and process mappings tables all
        // own a centered, hit-test-free empty state so an empty page never shows a blank
        // DataGrid frame without explaining the next step.
        var dataGrids = maintenance.Descendants().Where(element => element.Name.LocalName == "DataGrid").ToArray();
        Assert.Equal(5, dataGrids.Length);
        foreach (var grid in dataGrids)
        {
            var overlay = grid.Parent?.Elements().FirstOrDefault(element =>
                element.Name.LocalName == "TextBlock" &&
                element.Attribute("IsHitTestVisible")?.Value == "False");
            Assert.NotNull(overlay);
            Assert.Contains("BasedOn=\"{StaticResource GscEmptyStateText}\"", overlay!.ToString());
            Assert.DoesNotContain(overlay.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        }
    }

    [Fact]
    public void EveryDashboardViewModelCommandRemainsReachableFromTheRedesignedDashboard()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var workspaceUi = string.Join("\n", new[] { "OverviewView.xaml", "SaveCenterView.xaml", "TrainerCenterView.xaml", "MediaCenterView.xaml", "TaskCenterView.xaml", "MaintenanceView.xaml" }
            .Select(name => File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", name))))
            + File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var commands = Regex.Matches(viewModel, @"public ICommand (?<name>[A-Za-z0-9_]+Command) \{ get; \}");

        Assert.NotEmpty(commands);
        foreach (Match match in commands)
        {
            var command = match.Groups["name"].Value;
            Assert.True(
                dashboard.Contains("Command=\"{Binding " + command)
                || dashboard.Contains("Command=\"{Binding DataContext." + command)
                || workspaceUi.Contains("Command=\"{Binding " + command)
                || workspaceUi.Contains("Command=\"{Binding DataContext." + command)
                || workspaceUi.Contains(command),
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
        Assert.Contains("Dispatcher.BeginInvoke(new Action(() =>", dashboardCode);
        Assert.Contains("ignored a deferred Dashboard UI callback failure", dashboardCode);
        Assert.Contains("catch (InvalidOperationException ex)", dashboardCode);
        Assert.Contains("BeginUiSafely(() => OnViewModelPropertyChanged(sender, e)", dashboardCode);
        Assert.Contains("BeginUiSafely(PlayEntranceAnimation, DispatcherPriority.Loaded)", dashboardCode);
        Assert.Contains("if (!IsLoaded) return;", dashboardCode);
        Assert.Contains("private bool BeginUiSafely(Action action, DispatcherPriority priority)", settingsCode);
        Assert.Contains("Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished", settingsCode);
        Assert.Contains("private void QueueAdaptiveThemeUpdate()", settingsCode);
        Assert.Contains("if (!IsLoaded || adaptiveThemePending) return;", settingsCode);
        Assert.Contains("adaptiveThemePending = false;", settingsCode);
        Assert.Contains("QueueAdaptiveThemeUpdate();", settingsCode);
        Assert.Contains("SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;", dashboardCode);
        Assert.Contains("SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;", dashboardCode);
        Assert.Contains("private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)", dashboardCode);
        Assert.Contains("ApplyAdaptiveTheme();", dashboardCode);
        Assert.Contains("SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;", settingsCode);
        Assert.Contains("SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;", settingsCode);
        Assert.Contains("private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)", settingsCode);
        Assert.Contains("skipped a translate animation because the visual was unavailable", dashboardCode);
        Assert.Contains("skipped a scale animation because the visual was unavailable", dashboardCode);
    }

    [Fact]
    public void AsyncUiEventBoundariesDoNotLeakFailuresIntoThePlayniteDispatcher()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

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

        // Plugin lifecycle hooks and settings persistence are also called from host/timer void
        // boundaries. Keep their work as observable Tasks so a notification failure cannot
        // escape from an async-void continuation into Playnite.
        Assert.DoesNotContain("public async void ApplySettingsAsync()", pluginCode);
        Assert.Contains("Settings changes do not change the Playnite game descriptors", pluginCode);
        Assert.Contains("await ApplySettingsCoreAsync().ConfigureAwait(false);", pluginCode);
        Assert.DoesNotContain("private async void PollTaskNotifications()", pluginCode);
        Assert.DoesNotContain("private async void FireAndForget", pluginCode);
        Assert.Contains("private async Task PollTaskNotificationsAsync()", pluginCode);
        Assert.Contains("private async Task StartWorkerAndScheduleSynchronizationAsync()", pluginCode);
        Assert.Contains("public Task SynchronizeFromDashboardAsync()", pluginCode);
        Assert.Contains("await plugin.SynchronizeFromDashboardAsync();", viewModelCode);
        Assert.Contains("synchronizationTask != null && !synchronizationTask.IsCompleted", pluginCode);
        Assert.Contains("largeLibraryStartupSyncNotBeforeUtc", pluginCode);
        Assert.Contains("await Task.Delay(quietDelay, lifetimeCancellation.Token).ConfigureAwait(false);", pluginCode);
        Assert.Contains("first-run libraries eventually synchronize", pluginCode);
        Assert.Contains("private async Task SynchronizeLoopAsync()", pluginCode);
        Assert.Contains("TimeSpan.FromMilliseconds(180)", pluginCode);
        Assert.Contains("synchronizationRequested", pluginCode);
        Assert.Contains("var initialDelay = observedCount >= LargeLibraryThreshold || observedCount == 0", pluginCode);
        Assert.Contains("TimeSpan.FromSeconds(60)", pluginCode);
        Assert.Contains("TimeSpan.FromSeconds(15)", pluginCode);
        Assert.Contains("ConfigureLargeLibraryStartupGate();", pluginCode);
        Assert.Contains("private async Task WaitForLibraryReadyAndStartWorkerAsync()", pluginCode);
        Assert.Contains("Playnite game database is not ready at application start", pluginCode);
        Assert.Contains("private void ConfigureLargeLibraryStartupGate()", pluginCode);
        Assert.Contains("TaskContinuationOptions.OnlyOnFaulted", pluginCode);
        Assert.Contains("failed to present a background operation error", pluginCode);
    }

    [Fact]
    public void LargeLibraryStartupRendersCacheWithoutKillingBusyWorker()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var launcherCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "WorkerLauncher.cs"));

        Assert.Contains("Observe(InitializeAsync())", viewModelCode);
        Assert.DoesNotContain("Run(InitializeAsync)", viewModelCode);
        Assert.Contains("WaitForHealthAsync(TimeSpan.FromSeconds(45), expectedVersion)", launcherCode);
        Assert.Contains("WaitForHealthAsync", launcherCode);
        Assert.Contains("var startupDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);", launcherCode);
        Assert.Contains("while (DateTime.UtcNow < startupDeadline)", launcherCode);
        Assert.Contains("IsHealthyAsync(TimeSpan.FromMilliseconds(650), expectedVersion)", launcherCode);
        Assert.DoesNotContain("for (var i = 0; i < 120; i++)", launcherCode);
    }

    [Fact]
    public void VeryLargeLibraryKeepsBusyWorkerInsteadOfKillingItAfterPingTimeout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));
        var launcherCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "WorkerLauncher.cs"));

        Assert.Contains("terminateUnhealthyProcess: !IsVeryLargeLibrary()", pluginCode);
        Assert.Contains("bool terminateUnhealthyProcess = true", launcherCode);
        Assert.Contains("var existingBusyProcess = false;", launcherCode);
        Assert.Contains("existingBusyProcess = !process.HasExited;", launcherCode);
        Assert.Contains("已保留现有进程，稍后可重试", launcherCode);
        Assert.Contains("if (existingBusyProcess)", launcherCode);
        Assert.Contains("if (currentCount > observedGameCount)", pluginCode);
        Assert.Contains("private void ObserveGameCount(int currentCount)", pluginCode);
        Assert.Contains("ObserveGameCount(games.Count)", pluginCode);
        Assert.DoesNotContain("observedGameCount = games.Count", pluginCode);
        Assert.DoesNotContain("observedGameCount = PlayniteApi.Database.Games.Count", pluginCode);
        Assert.Contains("return observedGameCount >= VeryLargeLibraryThreshold;", pluginCode);
    }

    [Fact]
    public void WorkerHandshakeRejectsHealthyStaleVersionBeforeLargeLibraryReuse()
    {
        var repositoryRoot = FindRepositoryRoot();
        var launcherCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "WorkerLauncher.cs"));
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));
        var dispatcherCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Worker", "Ipc", "IpcRequestDispatcher.cs"));
        var dtoPath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Contracts", "WorkerDtos.cs");

        Assert.True(File.Exists(dtoPath));
        Assert.Contains("WorkerPingDto", dispatcherCode);
        Assert.Contains("expectedVersion", launcherCode);
        Assert.Contains("ProbeHealthAsync", launcherCode);
        Assert.Contains("HealthProbe.Incompatible", launcherCode);
        Assert.Contains("expectedVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString()", pluginCode);
        Assert.Contains("if (probe != HealthProbe.Incompatible && !terminateUnhealthyProcess)", launcherCode);
        Assert.Contains("if (probe == HealthProbe.Healthy || probe == HealthProbe.Incompatible)", launcherCode);
    }

    [Fact]
    public void WorkerLaunchLogRecordsExpectedVersionForStaleInstallationDiagnostics()
    {
        var repositoryRoot = FindRepositoryRoot();
        var launcherCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "WorkerLauncher.cs"));

        Assert.Contains("expectedVersionLabel", launcherCode);
        Assert.Contains("expected GameSaveCenter Worker version", launcherCode);
        Assert.Contains("AppendLog(logPath", launcherCode);
    }

    [Fact]
    public void LargeLibraryDashboardDelaysInitialFullSynchronization()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("private volatile int observedGameCount;", pluginCode);
        Assert.Contains("public bool IsLargeLibraryForUi => observedGameCount >= 100;", pluginCode);
        Assert.Contains("var largeLibraryDelay = plugin.IsLargeLibraryForUi", viewModelCode);
        Assert.Contains("Games.Count > 0 ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(10)", viewModelCode);
        Assert.Contains("private CancellationTokenSource? initialSynchronizationCancellation;", viewModelCode);
        Assert.Contains("private long deferredUiWorkGeneration;", viewModelCode);
        Assert.Contains("Interlocked.Increment(ref deferredUiWorkGeneration);", viewModelCode);
        Assert.Contains("Interlocked.Read(ref deferredUiWorkGeneration)", viewModelCode);
        Assert.Contains("CancelInitialSynchronization();", viewModelCode);
        Assert.Contains("await Task.Delay(delay, cancellation.Token)", viewModelCode);
        Assert.Contains("catch (OperationCanceledException) when (cancellation.IsCancellationRequested)", viewModelCode);
        Assert.Contains("大型目录同步将在空闲时进行", viewModelCode);
        Assert.Contains("await RefreshCoreAsync(false, TimeSpan.FromSeconds(5));", viewModelCode);
        Assert.Contains("private async Task ListenForTaskEventsWhenReadyAsync(CancellationToken token)", viewModelCode);
        Assert.Contains("await Task.Delay(TimeSpan.FromSeconds(60), token)", viewModelCode);
    }

    [Fact]
    public void SettingsStoragePolicyFieldsUseASafeCompactSingleColumnLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("x:Name=\"StorageFormatFields\" Columns=\"2\"", settings);
        Assert.Contains("x:Name=\"StorageNumericFields\" Columns=\"3\"", settings);
        Assert.Contains("Path=\"FullBackupLimit\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"DifferentialBackupLimit\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"CompressionLevel\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("StorageFormatFields.Columns = twoColumns ? 2 : 1", settingsCode);
        Assert.Contains("StorageNumericFields.Columns = formWidth >= 720 ? 3 : formWidth >= 480 ? 2 : 1", settingsCode);
    }

    [Fact]
    public void SettingsUsesSharedResponsiveFieldGroupsWithoutShrinkingNumericInputs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("x:Name=\"AppearanceFields\" Columns=\"2\"", settings);
        Assert.Contains("x:Name=\"AutomationIntervalFields\" Columns=\"3\"", settings);
        Assert.Contains("x:Name=\"SettingsScroller\" Style=\"{DynamicResource GscPageScrollViewer}\" VerticalScrollBarVisibility=\"Hidden\" HorizontalScrollBarVisibility=\"Disabled\"", settings);
        Assert.Contains("Path=\"DefaultBackupIntervalMinutes\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"ProcessPollingSeconds\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"DashboardRefreshSeconds\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("AppearanceFields.Columns = twoColumns ? 2 : 1", settingsCode);
        Assert.Contains("var contentWidth = Math.Max(320, width - horizontalMargin * 2 - 40);", settingsCode);
        Assert.Contains("AutomationIntervalFields.Columns = expanded && formWidth >= 930 ? 3 : formWidth >= 650 ? 2 : 1", settingsCode);
        // 核心工具路径字段保持单列全宽行（路径可读性），不再参与两列网格切换。
        Assert.Contains("x:Name=\"CoreToolFields\"", settings);
        Assert.DoesNotContain("CoreToolFields.Columns", settingsCode);
    }

    [Fact]
    public void SettingsPathFieldsAreFullWidthReadableRowsPreservingEveryBinding()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));

        foreach (var binding in new[]
        {
            "WorkerExecutable", "LudusaviExecutable", "LudusaviBackupDirectory",
            "RcloneExecutable", "RcloneDestination", "MediaArchiveDirectory"
        })
        {
            Assert.Contains($"Text=\"{{Binding {binding}, UpdateSourceTrigger=PropertyChanged}}\"", settings);
        }

        Assert.Contains("x:Name=\"CoreToolFields\"", settings);
        Assert.DoesNotContain("x:Name=\"CoreToolFields\" Columns=\"2\"", settings);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", settings);
        Assert.Contains("Worker 执行任务；Playnite 负责设置入口和游戏事件。路径支持环境变量与相对路径，保存后由 Worker 校验。", settings);
        Assert.DoesNotContain("#FFFFFF", settings);
        Assert.DoesNotContain("#000000", settings);
        Assert.DoesNotContain("#1E1E1E", settings);
    }

    [Fact]
    public void SettingsSectionRhythmUsesTitlePlusCaptionRowsForToggles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));

        Assert.Contains("页面淡入、滑动、悬停与按钮按压反馈", settings);
        Assert.Contains("只用于侧栏、浮层和固定环境光，不应用到滚动内容", settings);
        Assert.Contains("管理面板未打开时也可执行已配置任务", settings);
        Assert.Contains("识别 Steam、Xbox、启动器和 MOD 管理器外部启动", settings);
        Assert.Contains("对未匹配游戏记录启动前/退出后文件差异，生成候选路径", settings);
        Assert.Contains("源文件删除不会删除归档", settings);
        Assert.Contains("管理面板关闭时也会监测", settings);
        Assert.Contains("IsChecked=\"{Binding EnableUiAnimations}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableGlassEffects}\"", settings);
        Assert.Contains("IsChecked=\"{Binding AutoStartWorker}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableProcessDetection}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableSessionSavePathDetection}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableMediaSync}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableTaskNotifications}\"", settings);
        Assert.Contains("Checked=\"OnVisualSettingChanged\" Unchecked=\"OnVisualSettingChanged\"", settings);
    }

    [Fact]
    public void CompactToolbarPreservesEveryActionThroughAnAccessibleIconOnlyMode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        foreach (var action in new[]
        {
            ("TopRefreshButton", "TopRefreshLabel", "刷新全部状态", "RefreshCommand"),
            ("TopBackupAllButton", "TopBackupAllLabel", "备份全部游戏", "BackupAllCommand"),
            ("TopMediaSyncButton", "TopMediaSyncLabel", "同步媒体", "SyncMediaCommand"),
            ("TopTrainerImportButton", "TopTrainerImportLabel", "导入修改器", "ImportTrainerCommand"),
            ("TopTrainerCatalogButton", "TopTrainerCatalogLabel", "刷新目录", "SyncTrainerCatalogCommand"),
            ("TopDiagnosticsButton", "TopDiagnosticsLabel", "刷新诊断", "RefreshDiagnosticsCommand")
        })
        {
            Assert.Contains($"x:Name=\"{action.Item1}\"", dashboard);
            Assert.Contains($"x:Name=\"{action.Item2}\"", dashboard);
            Assert.Contains($"AutomationProperties.Name=\"{action.Item3}\"", dashboard);
            Assert.Contains($"ToolTip=\"{action.Item3}\"", dashboard);
            Assert.Contains($"Command=\"{{Binding {action.Item4}}}\"", dashboard);
            Assert.Contains($"{action.Item2}.Visibility = labelVisibility;", dashboardCode);
        }

        Assert.Contains("SetToolbarLabelsVisible(mode == LayoutMode.Expanded);", dashboardCode);
        Assert.Contains("var labelVisibility = visible ? Visibility.Visible : Visibility.Collapsed;", dashboardCode);
    }

    [Fact]
    public void DashboardToastTimersAreReleasedOnUnloadAndCapacityEviction()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("private readonly Dictionary<Border, DispatcherTimer> toastTimers", dashboardCode);
        Assert.Contains("ClearToasts();", dashboardCode);
        Assert.Contains("while (ToastHost.Children.Count > 4", dashboardCode);
        Assert.Contains("RemoveToast(expired);", dashboardCode);
        Assert.Contains("foreach (var timer in toastTimers.Values) timer.Stop();", dashboardCode);
        Assert.Contains("toastTimers.Clear();", dashboardCode);
        Assert.Contains("StopToastTimer(card, timer);", dashboardCode);
    }

    [Fact]
    public void AccentBrandMarksUseTheComputedOnAccentForegroundInEveryTheme()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var palette = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "AdaptiveThemePalette.cs"));

        Assert.DoesNotContain("Fill=\"White\"", dashboard);
        Assert.DoesNotContain("Stroke=\"White\"", dashboard);
        Assert.DoesNotContain("Foreground=\"White\"", settings);
        Assert.Contains("GscOnAccentTextBrush", dashboard);
        Assert.Contains("GscOnAccentTextBrush", settings);
        Assert.Contains("resources[\"GscOnAccentTextBrush\"]", palette);
    }

    [Fact]
    public void SemanticStatusColorsAreLocalDynamicResourcesInHighContrast()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var palette = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Infrastructure", "AdaptiveThemePalette.cs"));

        Assert.DoesNotContain("{StaticResource GscInfoBrush}", dashboard + settings);
        Assert.DoesNotContain("{StaticResource GscSuccessBrush}", dashboard + settings);
        Assert.DoesNotContain("{StaticResource GscWarningBrush}", dashboard + settings);
        Assert.DoesNotContain("{StaticResource GscErrorBrush}", dashboard + settings);
        Assert.DoesNotContain("{StaticResource GscRowHoverStrongBrush}", dashboard);
        Assert.Contains("resources[\"GscInfoBrush\"]", palette);
        Assert.Contains("resources[\"GscSuccessBrush\"]", palette);
        Assert.Contains("resources[\"GscWarningBrush\"]", palette);
        Assert.Contains("resources[\"GscErrorBrush\"]", palette);
        Assert.Contains("resources[\"GscTableAlternateRowBrush\"]", palette);
        Assert.Contains("resources[\"GscRowHoverStrongBrush\"]", palette);
        Assert.Contains("resources[\"GscScrollThumbHoverBrush\"] = Brush(WithAlpha(palette.AccentHover", palette);
        Assert.DoesNotContain("Color.FromArgb(166, 124, 92, 252)", palette);
        Assert.Contains("SystemParameters.HighContrast ? (byte)0", palette);
        Assert.Contains("ApplyRuntimeThemeResources(Resources, palette", dashboardCode);
        Assert.Contains("ApplyRuntimeThemeResources(workspaceView.Resources, palette", dashboardCode);
        Assert.Contains("GetWorkspaceViews()", dashboardCode);
        foreach (var workspaceName in new[]
                 {
                     "OverviewWorkspaceView", "MediaWorkspaceView", "MaintenanceWorkspaceView",
                     "SaveWorkspaceView", "TrainerWorkspaceView", "TaskWorkspaceView"
                 })
        {
            Assert.Contains($"yield return {workspaceName};", dashboardCode);
        }
        Assert.Contains("highContrast ? primaryText", palette);
    }

    [Fact]
    public void SharedListBoxItemsStayRoundedAndKeyboardFocusable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var production = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "WpfUiProduction.xaml"));
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));
        var trainer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));

        Assert.Contains("<Style TargetType=\"ListBoxItem\">", production);
        Assert.Contains("<Style TargetType=\"ListBox\">", production);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", production);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"", production);
        Assert.Contains("ScrollViewer.PanningMode\" Value=\"VerticalOnly\"", production);
        Assert.Contains("KeyboardNavigation.TabNavigation\" Value=\"Local\"", production);
        Assert.Contains("FocusVisualStyle\" Value=\"{DynamicResource GscSharedFocusVisual}\"", production);
        Assert.Contains("CornerRadius=\"8\"", production);
        Assert.DoesNotContain("CornerRadius=\"{DynamicResource GscCornerSmall}\"", production);
        Assert.DoesNotContain("CornerRadius=\"{Binding Tag", production);
        Assert.DoesNotContain("CornerRadius=\"{Binding Tag", File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml")));
        Assert.Contains("Value=\"{DynamicResource GscRowHoverBrush}\"", production);
        Assert.DoesNotContain("FocusVisualStyle\" Value=\"{x:Null}\"", trainer);
        Assert.Contains("FocusVisualStyle\" Value=\"{DynamicResource GscSharedFocusVisual}\"", trainer);
        Assert.Contains("x:Key=\"GscRedesignGameContextButton\"", redesign);
        Assert.Contains("x:Key=\"GscRedesignSettingsTabItem\"", redesign);
        Assert.Contains("FocusVisualStyle\" Value=\"{DynamicResource GscSharedFocusVisual}\"", redesign);
    }

    [Fact]
    public void DemoCardAliasesKeepReadingSurfacesFlatAndConsistent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));

        Assert.Contains("x:Key=\"GscReadingCardStyle\"", redesign);
        Assert.Contains("CornerRadius\" Value=\"16\"", redesign);
        Assert.Contains("Padding\" Value=\"18\"", redesign);
        Assert.Contains("Effect\" Value=\"{x:Null}\"", redesign);
        Assert.Contains("x:Key=\"GscSubCardStyle\"", redesign);
        Assert.Contains("CornerRadius\" Value=\"13\"", redesign);
        Assert.Contains("x:Key=\"GscFloatingCardStyle\"", redesign);
        Assert.Contains("CornerRadius\" Value=\"18\"", redesign);
        Assert.Contains("CornerRadius=\"10\" Padding=\"{TemplateBinding Padding}\"", redesign);
    }

    [Fact]
    public void SettingsAsyncFeedbackDoesNotTargetAnUnloadedPlaynitePage()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("Unloaded += OnUnloaded;", settingsCode);
        Assert.Contains("private bool CanPresentUiFeedback => IsLoaded", settingsCode);
        Assert.Contains("if (!CanPresentUiFeedback) return;", settingsCode);
        Assert.Contains("SettingsShell.BeginAnimation(UIElement.OpacityProperty, null);", settingsCode);
        Assert.Contains("private async Task ObserveUiOperationAsync", settingsCode);
        Assert.Contains("GameSaveCenter could not present settings feedback.", settingsCode);
    }

    [Fact]
    public void ResponsiveLayoutsCoalesceResizeStormsWithoutUpdatingUnloadedViews()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        foreach (var source in new[] { dashboardCode, settingsCode })
        {
            Assert.Contains("private bool responsiveLayoutPending;", source);
            Assert.Contains("private Size pendingResponsiveSize;", source);
            Assert.Contains("private void QueueResponsiveLayout(Size size)", source);
            Assert.Contains("if (responsiveLayoutPending) return;", source);
            Assert.Contains("DispatcherPriority.Render", source);
            Assert.Contains("if (!IsLoaded) return;", source);
        }
    }

    [Fact]
    public void ResponsiveShellReclaimsCompactPaddingForWorkspaceScrollRows()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("ResponsiveShell.Margin = new Thickness(", dashboardCode);
        Assert.Contains("GameDetailCard.Padding = mode == LayoutMode.Expanded ? new Thickness(12)", dashboardCode);
        Assert.Contains("mode == LayoutMode.Compact ? new Thickness(8)", dashboardCode);
        Assert.Contains("var tableMinHeight = height < 650", dashboardCode);
        Assert.Contains("workspaceView.Resources[\"GscTableMinHeight\"] = tableMinHeight", dashboardCode);
        Assert.Contains("var workspaceTableMinHeight = height < 650", dashboardCode);
        Assert.Contains("workspaceView.Resources[\"GscWorkspaceTableMinHeight\"] = workspaceTableMinHeight", dashboardCode);
        Assert.Contains("? 112d", dashboardCode);
        Assert.Contains(": 160d", dashboardCode);
        Assert.Contains("Math.Max(520d, Math.Min(820d", dashboardCode);
        Assert.Contains("height < 700 ? 0.94 : 0.95", dashboardCode);
        Assert.Contains("mode == LayoutMode.Expanded ? 12", dashboardCode);
        Assert.Contains("viewModel.CurrentWorkspace == WorkspaceKind.Trainers", dashboardCode);
        Assert.Contains("DetailsTabControl.Margin =", dashboardCode);
    }

    [Fact]
    public void DashboardViewModelEventsFollowTheLoadedViewLifecycle()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));

        Assert.Contains("private bool viewModelSubscribed;", dashboardCode);
        Assert.Contains("SubscribeViewModel();", dashboardCode);
        Assert.Contains("UnsubscribeViewModel();", dashboardCode);
        Assert.Contains("gamePickerPersistenceCancellation = null;", viewModelCode);
        Assert.Contains("persistence.Dispose();", viewModelCode);
        Assert.Contains("private void SubscribeViewModel()", dashboardCode);
        Assert.Contains("private void UnsubscribeViewModel()", dashboardCode);
        Assert.Contains("viewModel.PropertyChanged -= OnViewModelPropertyChanged;", dashboardCode);
        Assert.Contains("viewModel.AttentionCenterRequested -= OnAttentionCenterRequested;", dashboardCode);
    }

    [Fact]
    public void ToastElevationFallsBackToAnOpaqueAccessibleSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("if (plugin.Settings.EnableGlassEffects && !SystemParameters.HighContrast)", dashboardCode);
        Assert.Contains("card.Effect = new System.Windows.Media.Effects.DropShadowEffect", dashboardCode);
        Assert.Contains("card.SetResourceReference(Border.BackgroundProperty, \"GscGlassStrongBrush\")", dashboardCode);
    }

    [Fact]
    public void BackgroundWorkerCollectionUpdatesRespectThePlayniteDispatcherLifecycle()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));

        Assert.Contains("if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;", viewModelCode);
        Assert.Contains("dispatcher.Invoke(action, DispatcherPriority.DataBind);", viewModelCode);
        Assert.Contains("catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))", viewModelCode);
        Assert.Contains("skipped a Dashboard UI collection update because the callback failed or the dispatcher is unavailable", viewModelCode);
    }

    [Fact]
    public void PluginNotificationAndConfirmationDispatchRespectPlayniteShutdown()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("private bool TryInvokeUi(Action action, string operation)", pluginCode);
        Assert.Contains("if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return false;", pluginCode);
        Assert.Contains("if (dispatcher.CheckAccess())", pluginCode);
        Assert.Contains("dispatcher.Invoke(action, DispatcherPriority.DataBind);", pluginCode);
        Assert.Contains("catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))", pluginCode);
        Assert.Contains("if (!TryInvokeUi(() => UiConfirmationRequested?.Invoke(this, args), \"confirmation request\"))", pluginCode);
        Assert.Contains("return false;", pluginCode);
        Assert.Contains("if (!TryInvokeUi(() => handler(this, args), \"notification request\")) return false;", pluginCode);
        Assert.Contains("skipped {operation} because the UI callback failed or the dispatcher is unavailable", pluginCode);
    }

    [Fact]
    public void LargeLibrarySynchronizationWaitsForAnInteractiveSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("private volatile bool interactiveSurfaceOpened;", pluginCode);
        Assert.Contains("private void RequestLibrarySynchronization(string reason)", pluginCode);
        Assert.Contains("var currentGameCount = GetPlayniteGameCount(\"library callback\");", pluginCode);
        Assert.Contains("ObserveGameCount(currentGameCount);", pluginCode);
        Assert.Contains("if (currentGameCount == 0)", pluginCode);
        Assert.Contains("if (!interactiveSurfaceOpened && IsLargeLibrary())", pluginCode);
        Assert.Contains("catalog synchronization is deferred until GameSaveCenter is opened", pluginCode);
        Assert.Contains("interactiveSurfaceOpened = true;", pluginCode);
        Assert.Contains("Opened = CreateDashboardViewSafely", pluginCode);
    }

    [Fact]
    public void LargeLibraryReadinessProbeNeverEagerlyStartsWorkerAfterASettledPartialSnapshot()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        // A 900+ Playnite library may be published in several partial callbacks. Once the
        // settled probe sees 100 or more entries, the Worker must wait for explicit user intent
        // instead of starting against a partial catalog and spawning Ludusavi lookups.
        Assert.Contains("if (gameCount >= LargeLibraryThreshold)", pluginCode);
        Assert.Contains("keeping Worker startup and catalog synchronization deferred until GameSaveCenter is opened explicitly", pluginCode);
        Assert.Contains("private const int LargeLibraryThreshold = 100", pluginCode);
    }

    [Fact]
    public void LargeLibraryDashboardStopsHiddenNotificationPollingWhenDetached()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("public void StopTaskNotificationMonitor()", pluginCode);
        Assert.Contains("taskNotificationTimer = null;", pluginCode);
        Assert.Contains("if (plugin.IsLargeLibraryForUi)", dashboardCode);
        Assert.Contains("plugin.StopTaskNotificationMonitor();", dashboardCode);
    }

    [Fact]
    public void LargeLibraryTaskNotificationsDoNotOpenAWorkerLongPollBeforeTheDashboardIsOpened()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("private bool taskNotificationMonitorDeferred;", pluginCode);
        Assert.Contains("if (taskNotificationTimer != null || taskNotificationMonitorDeferred && !interactiveSurfaceOpened)", pluginCode);
        Assert.Contains("if ((observedGameCount == 0 || observedGameCount >= LargeLibraryThreshold) && !interactiveSurfaceOpened)", pluginCode);
        Assert.Contains("Deferring task notification monitor until GameSaveCenter is opened", pluginCode);
        Assert.Contains("taskNotificationMonitorDeferred = false;", pluginCode);
        Assert.Contains("StartTaskNotificationMonitor();", pluginCode);
    }

    [Fact]
    public void LargeLibraryStartupDefersWorkerUntilExplicitUserIntent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("Deferring Worker startup for large Playnite library", pluginCode);
        Assert.Contains("if (IsLargeLibrary())", pluginCode);
        Assert.Contains("FireAndForget(EnsureWorkerAsync);", pluginCode);
        Assert.Contains("until GameSaveCenter is opened or a game starts", pluginCode);
    }

    [Fact]
    public void VeryLargeLibrariesDoNotAutomaticallyRematchOnDashboardOpen()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));
        var viewModelCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "ViewModels", "DashboardViewModel.cs"));
        var catalogCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Worker", "Services", "GameCatalogService.cs"));

        Assert.Contains("VeryLargeLibraryThreshold = 500", pluginCode);
        Assert.Contains("public bool IsVeryLargeLibraryForUi", pluginCode);
        Assert.Contains("Skipping automatic dashboard catalog synchronization for very large library", pluginCode);
        Assert.Contains("Very large Playnite library", pluginCode);
        Assert.Contains("if (plugin.IsVeryLargeLibraryForUi)", viewModelCode);
        Assert.Contains("explicit Refresh command remains available", viewModelCode);
        Assert.Contains("RefreshLargeLibraryCacheWhenWorkerReadyAsync", viewModelCode);
        Assert.Contains("var cancellation = new CancellationTokenSource();", viewModelCode);
        Assert.Contains("initialSynchronizationCancellation = cancellation;", viewModelCode);
        Assert.Contains("cancellation.IsCancellationRequested || generation != Interlocked.Read(ref deferredUiWorkGeneration)", viewModelCode);
        Assert.Contains("cancellation.Dispose();", viewModelCode);
        Assert.Contains("never turn this recovery path into a catalog synchronization", viewModelCode);
        Assert.Contains("VeryLargeLibraryBackgroundMatchBudget = 12", catalogCode);
        Assert.Contains("list.Count >= VeryLargeLibraryThreshold", catalogCode);
        Assert.Contains("if (games.Count >= LargeLibraryThreshold && !interactiveSurfaceOpened)", pluginCode);
        Assert.Contains("Playnite library is still empty", pluginCode);
    }

    [Fact]
    public void PreDashboardCatalogGuardCoversPartialLargeLibrariesAndDatabaseShutdowns()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        // A partial 100–499 game snapshot must not start a catalog request merely because a
        // library callback arrived before the final 900+ snapshot. The count read itself is
        // also guarded because Playnite can close/swap its database during profile changes.
        Assert.Contains("if (games.Count >= LargeLibraryThreshold && !interactiveSurfaceOpened)", pluginCode);
        Assert.Contains("private int GetPlayniteGameCount(string reason)", pluginCode);
        Assert.Contains("retaining observed count", pluginCode);
        Assert.Contains("catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))", pluginCode);
        Assert.Contains("ObserveGameCount(GetPlayniteGameCount(\"dashboard creation\"));", pluginCode);
        Assert.Contains("ObserveGameCount(GetPlayniteGameCount(\"settings view creation\"));", pluginCode);
    }


    [Fact]
    public void FinalRedesignKeepsNavigationAndStatusCardsInsideCompactSidebarBounds()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"SidebarWorkerCompactLabel\"", dashboard);
        Assert.Contains("x:Name=\"SidebarLudusaviCompactLabel\"", dashboard);
        Assert.Contains("x:Name=\"SidebarWorkerStatusCard\"", dashboard);
        Assert.Contains("x:Name=\"SidebarLudusaviStatusCard\"", dashboard);
        Assert.Contains("item.Width = visible ? double.NaN : 48", dashboardCode);
        Assert.Contains("item.Height = visible ? double.NaN : 48", dashboardCode);
        Assert.Contains("card.Width = expanded ? double.NaN : 48", dashboardCode);
        Assert.Contains("card.Height = expanded ? double.NaN : 50", dashboardCode);
        Assert.Contains("card.HorizontalAlignment = expanded ? HorizontalAlignment.Stretch : HorizontalAlignment.Center", dashboardCode);
        Assert.Contains("SidebarStatusPanel.HorizontalAlignment = visible ? HorizontalAlignment.Stretch : HorizontalAlignment.Center", dashboardCode);
        Assert.Contains("ContentPresenter HorizontalAlignment=\"{TemplateBinding HorizontalContentAlignment}\"", dashboard);
        Assert.Contains("x:Name=\"SidebarChrome\" Grid.Column=\"0\" Style=\"{StaticResource GscRedesignSidebarSurface}\" ClipToBounds=\"True\"", dashboard);
    }

    [Fact]
    public void FinalRedesignUsesExplicitHeaderRowsAndSharedGameContextAtEveryBreakpoint()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));
        var dashboardCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml.cs"));

        Assert.Contains("x:Name=\"HeaderCompactActionsRow\"", dashboard);
        Assert.Contains("x:Name=\"TopActionsScroller\"", dashboard);
        Assert.Contains("x:Name=\"GameSwitcherHost\"", dashboard);
        Assert.Contains("x:Name=\"HeaderGamePickerColumn\"", dashboard);
        Assert.Contains("x:Name=\"CompactGameSelector\"", dashboard);
        Assert.Contains("x:Name=\"ToggleGameBrowserButton\"", dashboard);
        Assert.Contains("width >= 1260 ? LayoutMode.Expanded", dashboardCode);
        Assert.Contains("width >= 980 ? LayoutMode.Standard", dashboardCode);
        Assert.Contains("width >= 760 ? LayoutMode.Compact", dashboardCode);
        Assert.Contains("Grid.SetRow(TopActionsScroller, 2)", dashboardCode);
        Assert.Contains("Grid.SetColumnSpan(TopActionsScroller, 3)", dashboardCode);
        Assert.Contains("var pickerOnTopBar = gameScopedWorkspace", dashboardCode);
        Assert.Contains("GameSwitcherHost.Visibility = gameScopedWorkspace", dashboardCode);
        Assert.Contains("x:Name=\"GameBrowserScrim\"", dashboard);
        Assert.Contains("Style=\"{StaticResource GscRedesignFloatingPickerCard}\"", dashboard);
        Assert.Contains("MouseLeftButtonDown=\"OnGameBrowserScrimMouseDown\"", dashboard);
        Assert.Contains("Text=\"{Binding Initials}\"", dashboard);
        Assert.Contains("Text=\"{Binding MetaDisplay}\"", dashboard);
        Assert.Contains("x:Name=\"HealthPill\"", dashboard);
        Assert.Contains("Binding=\"{Binding GamePicker.FilteredCount}\"", dashboard);
        Assert.Contains("Value=\"LudusaviUnavailable\"", dashboard);
        Assert.Contains("an in-host floating layer clipped by the Playnite page", dashboardCode);
        Assert.Contains("GameBrowserScrim.Visibility = gameBrowserVisibility", dashboardCode);
        Assert.Contains("GameBrowserPanel.Width = mode == LayoutMode.Narrow ? double.NaN : floatingPickerWidth", dashboardCode);
    }

    [Fact]
    public void SaveHistoryInspectorDoesNotShowDisabledControlsOrUnlabelledPillsWithoutASelection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var saves = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "SaveCenterView.xaml"));

        Assert.Contains("x:Name=\"SaveHistoryActionsScrollViewer\"", saves);
        Assert.Contains("SelectedBackup", saves);
        Assert.Contains("Command=\"{Binding RestoreCommand}\"", saves);
        Assert.Contains("Command=\"{Binding UndoRestoreCommand}\"", saves);
        Assert.Contains("Text=\"{Binding BackupComment", saves);
        Assert.DoesNotContain("SaveHistoryInspectorTabs", saves);
    }

    [Fact]
    public void SettingsRedesignMovesCategoriesWithoutRemovingExistingFieldsOrSaveSemantics()
    {
        var repositoryRoot = FindRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml"));
        var settingsCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Settings", "GameSaveCenterSettingsView.xaml.cs"));

        Assert.Contains("x:Name=\"SettingsSectionTabs\"", settings);
        Assert.Contains("Style=\"{StaticResource GscRedesignSettingsTabControl}\"", settings);
        Assert.Contains("x:Name=\"SettingsHeader\" Style=\"{DynamicResource GscRedesignWorkspaceHeroCard}\"", settings);
        Assert.Contains("Style=\"{DynamicResource GscRedesignHeroEyebrow}\"", settings);
        Assert.Contains("由 Playnite 的保存按钮提交", settings);
        Assert.Contains("Text=\"{Binding WorkerExecutable, UpdateSourceTrigger=PropertyChanged}\"", settings);
        Assert.Contains("SelectedValue=\"{Binding BackupFormat}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableUiAnimations}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableCloudUpload}\"", settings);
        Assert.Contains("Click=\"OnExportSettingsClick\"", settings);
        Assert.Contains("Click=\"OnImportSettingsClick\"", settings);
        Assert.Contains("SettingsSectionTabs.TabStripPlacement = compact ? Dock.Top : Dock.Left", settingsCode);
        Assert.Contains("SettingsShell.HorizontalAlignment = HorizontalAlignment.Stretch", settingsCode);
        Assert.Contains("SettingsShell.MaxWidth = 1360", settingsCode);
        Assert.Contains("tab.MinWidth = compact ? (narrow ? 132 : 158) : 218", settingsCode);
        Assert.Contains("x:Name=\"SettingsDemoShell\" Style=\"{StaticResource GscShellStyle}\"", settings);
        Assert.Contains("MaxWidth=\"1360\" HorizontalAlignment=\"Stretch\"", settings);
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));
        Assert.Contains("DockPanel.Dock=\"Left\"", redesign);
        Assert.DoesNotContain("DockPanel.Dock=\"{TemplateBinding TabStripPlacement}\"", redesign);
    }

    [Fact]
    public void ProductionXamlUsesOnlyRealDockEnumValues()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productionRoot = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite");
        var invalid = Directory.EnumerateFiles(productionRoot, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "DockPanel\\.Dock=\\\"([^\\\"]+)\\\"")
                .Cast<Match>()
                .Select(match => (Path.GetFileName(path), match.Groups[1].Value)))
            .Where(item => !item.Item2.StartsWith("{")
                && item.Item2 is not ("Top" or "Bottom" or "Left" or "Right"))
            .ToList();

        Assert.Empty(invalid);
    }

    [Fact]
    public void DashboardKeepsDemoFooterHierarchyWithoutReintroducingStatusBarLayout()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dashboard = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "DashboardView.xaml"));

        Assert.Contains("x:Name=\"DemoFooter\" Grid.Row=\"3\"", dashboard);
        Assert.Contains("x:Name=\"DemoFooterNote\"", dashboard);
        Assert.Contains("x:Name=\"DemoFooterHint\"", dashboard);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", dashboard);
        Assert.Contains("x:Name=\"StatusPill\" Visibility=\"Collapsed\"", dashboard);
    }

    [Fact]
    public void SharedSectionCardMatchesDemoReadingSurfaceGeometry()
    {
        var repositoryRoot = FindRepositoryRoot();
        var redesign = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "Redesign.xaml"));
        var sectionStart = redesign.IndexOf("x:Key=\"GscRedesignSectionCard\"", StringComparison.Ordinal);
        Assert.True(sectionStart >= 0);
        var sectionEnd = redesign.IndexOf("<Style x:Key=", sectionStart + 1, StringComparison.Ordinal);
        Assert.True(sectionEnd > sectionStart);
        var section = redesign.Substring(sectionStart, sectionEnd - sectionStart);

        Assert.Contains("CornerRadius\" Value=\"16\"", section);
        Assert.Contains("Padding\" Value=\"16\"", section);
        Assert.Contains("Effect\" Value=\"{x:Null}\"", section);
    }

    [Fact]
    public void ExtractedWorkspacesUseTheSharedDemoLayoutWithoutReplacingTheirRealContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewsRoot = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views");
        var workspaceViews = new[]
        {
            "SaveCenterView.xaml",
            "TrainerCenterView.xaml",
            "MediaCenterView.xaml",
            "TaskCenterView.xaml",
            "MaintenanceView.xaml"
        };

        foreach (var view in workspaceViews)
        {
            var xaml = File.ReadAllText(Path.Combine(viewsRoot, view));
            // All workspaces now use the demo's compact page rhythm. Game-scoped pages
            // receive their only game context from Dashboard; global pages start directly
            // with summary cards instead of spending a permanent row on a redundant hero.
            if (view != "TaskCenterView.xaml")
            {
                Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", xaml);
            }
            Assert.DoesNotContain("GscRedesignWorkspaceHeroCard", xaml);
            Assert.DoesNotContain("WorkspaceHero", xaml);
        }

        Assert.Contains("ItemsSource=\"{Binding Backups}\"", File.ReadAllText(Path.Combine(viewsRoot, "SaveCenterView.xaml")));
        Assert.Contains("ItemsSource=\"{Binding GameTools}\"", File.ReadAllText(Path.Combine(viewsRoot, "TrainerCenterView.xaml")));
        Assert.Contains("ItemsSource=\"{Binding MediaView}\"", File.ReadAllText(Path.Combine(viewsRoot, "MediaCenterView.xaml")));
        Assert.Contains("ItemsSource=\"{Binding TasksView}\"", File.ReadAllText(Path.Combine(viewsRoot, "TaskCenterView.xaml")));
        Assert.Contains("ItemsSource=\"{Binding Findings}\"", File.ReadAllText(Path.Combine(viewsRoot, "MaintenanceView.xaml")));

        var saveCenter = File.ReadAllText(Path.Combine(viewsRoot, "SaveCenterView.xaml"));
        var mediaCenter = File.ReadAllText(Path.Combine(viewsRoot, "MediaCenterView.xaml"));
        var maintenance = File.ReadAllText(Path.Combine(viewsRoot, "MaintenanceView.xaml"));

        Assert.Contains("Style=\"{DynamicResource GscRedesignSubCard}\"", saveCenter);
        Assert.Contains("Style=\"{DynamicResource GscRedesignInfoBand}\"", mediaCenter);
        Assert.Contains("Style=\"{DynamicResource GscRedesignCounterPill}\"", mediaCenter);
        Assert.Contains("SelectionMode=\"Extended\"", mediaCenter);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", mediaCenter);
        Assert.Contains("Style=\"{DynamicResource GscRedesignSubCard}\"", maintenance);
        Assert.Contains("Command=\"{Binding RestoreCommand}\"", saveCenter);
        Assert.Contains("Command=\"{Binding SaveDeviceDecisionCommand}\"", maintenance);
        Assert.Contains("Command=\"{Binding RestoreStagedRemoteBackupCommand}\"", maintenance);
    }

    [Fact]
    public void FinalRedesignResourceDictionaryParsesInsideThePluginScope()
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                var resources = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/DesignTokens.xaml""/>
        <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/WpfUiProduction.xaml""/>
        <ResourceDictionary Source=""/GameSaveCenter.Playnite;component/Themes/Redesign.xaml""/>
    </ResourceDictionary.MergedDictionaries>
</ResourceDictionary>");

                Assert.IsType<Style>(resources["GscRedesignSectionCard"]);
                Assert.IsType<Style>(resources["GscRedesignWorkspaceHeroCard"]);
                Assert.IsType<Style>(resources["GscRedesignHeroEyebrow"]);
                Assert.IsType<Style>(resources["GscRedesignHeroTitle"]);
                Assert.IsType<Style>(resources["GscRedesignInfoBand"]);
                Assert.IsType<Style>(resources["GscRedesignSubCard"]);
                Assert.IsType<Style>(resources["GscRedesignCounterPill"]);
                Assert.IsType<Style>(resources["GscRedesignSettingsTabControl"]);
                Assert.IsType<Style>(resources["GscRedesignSettingsTabItem"]);
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

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
        Assert.Contains("resources[\"GscPopupAllowsTransparency\"] = glassEnabled", paletteSource);
        Assert.Contains("resources[\"GscPopupAnimation\"] = motionEnabled ? PopupAnimation.Fade : PopupAnimation.None", paletteSource);
        Assert.Contains("if (!enabled) return null;", paletteSource);
        Assert.Contains("highContrast ? accent", paletteSource);
        Assert.DoesNotContain("{StaticResource GscAccentShadowColor}", dashboard);
        Assert.DoesNotContain("{StaticResource GscAccentShadowColor}", tokens);
        Assert.Contains("{DynamicResource GscAmbientAccentBrush}", dashboard);
        Assert.Contains("{DynamicResource GscSelectionTextBrush}", dashboard);
        Assert.Contains("{DynamicResource GscSelectionTextBrush}", tokens);
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
        Assert.Contains("x:Name=\"GameBrowserPanel\" Grid.Row=\"0\" Grid.RowSpan=\"2\" Style=\"{StaticResource GscRedesignSectionCard}\"", dashboard);
        Assert.Contains("x:Name=\"GameDetailCard\" Grid.Row=\"0\" Grid.RowSpan=\"2\" Grid.Column=\"2\" Style=\"{StaticResource GscRedesignHeroCard}\"", dashboard);
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
        Assert.Equal(typeof(Wpf.Ui.Controls.Button), buttonStyle.TargetType);
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

        Assert.Contains("<Style TargetType=\"DataGridColumnHeader\">", production);
        Assert.Contains("<Style TargetType=\"DataGridCell\">", production);
        Assert.Contains("<Style TargetType=\"DataGridRow\">", production);
        Assert.Contains("<Style TargetType=\"DataGrid\">", production);
        Assert.Contains("ScrollViewer.PanningMode\" Value=\"VerticalOnly\"", production);
        Assert.Contains("KeyboardNavigation.TabNavigation\" Value=\"Local\"", production);
        Assert.Contains("KeyboardNavigation.DirectionalNavigation\" Value=\"Contained\"", production);
        Assert.Contains("VirtualizingPanel.VirtualizationMode\" Value=\"Recycling\"", production);
        Assert.Contains("GscTableHeaderBrush", production);
        Assert.Contains("GscRowHoverBrush", production);
        Assert.Contains("GscAccentTintBrush", production);
        Assert.Contains("CornerRadius=\"10\"", production);
        Assert.Contains("Property=\"MinHeight\" Value=\"{DynamicResource GscTableRowHeight}\"", production);
        Assert.Contains("Text columns read naturally from the leading edge", production);
        Assert.Contains("<Style TargetType=\"DataGridCell\">", production);
        Assert.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Left\"/>", production);
        Assert.Contains("x:Name=\"SortGlyph\"", production);
        Assert.Contains("Property=\"SortDirection\" Value=\"Ascending\"", production);
        Assert.Contains("Property=\"SortDirection\" Value=\"Descending\"", production);
        Assert.Contains("x:Key=\"GscTableRowHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableMinHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableViewportHeight\"", designTokens);
        Assert.Contains("Property=\"Height\" Value=\"{DynamicResource GscTableViewportHeight}\"", production);
        Assert.Contains("x:Key=\"GscTableHeaderHeight\"", designTokens);
        Assert.Contains("x:Key=\"GscTableAlternateRowBrush\"", designTokens);

        foreach (var workspace in new[] { "OverviewView.xaml", "SaveCenterView.xaml", "MediaCenterView.xaml", "TaskCenterView.xaml", "MaintenanceView.xaml" })
        {
            var text = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", workspace));
            Assert.Contains("BasedOn=\"{StaticResource {x:Type DataGrid}}\"", text);
            Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility\" Value=\"Auto\"", text);
            Assert.Contains("ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", text);
            Assert.Contains("EnableRowVirtualization\" Value=\"True\"", text);
            Assert.Contains("EnableColumnVirtualization\" Value=\"True\"", text);
            Assert.Contains("Property=\"MinHeight\" Value=\"{DynamicResource GscTableMinHeight}\"", text);
            Assert.Contains("Property=\"Height\" Value=\"{DynamicResource GscTableViewportHeight}\"", text);
            Assert.Contains("Property=\"RowHeight\" Value=\"{DynamicResource GscTableRowHeight}\"", text);
            Assert.Contains("Property=\"ColumnHeaderHeight\" Value=\"{DynamicResource GscTableHeaderHeight}\"", text);
            Assert.Contains("Property=\"AlternatingRowBackground\" Value=\"{DynamicResource GscTableAlternateRowBrush}\"", text);
            Assert.DoesNotContain("BlurEffect", text);
        }
    }

    [Fact]
    public void ExtractedWorkspacesExposePageScrollAndFiniteViewportContracts()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewDirectory = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views");
        foreach (var (file, pageScrollName) in new[]
        {
            ("OverviewView.xaml", "OverviewPageScrollViewer"),
            ("SaveCenterView.xaml", "SavePageScrollViewer"),
            ("MediaCenterView.xaml", "MediaPageScrollViewer"),
            ("TaskCenterView.xaml", "TaskPageScrollViewer"),
            ("TrainerCenterView.xaml", "TrainerPageScrollViewer"),
            ("MaintenanceView.xaml", "MaintenancePageScrollViewer")
        })
        {
            var text = File.ReadAllText(Path.Combine(viewDirectory, file));
            var root = XDocument.Parse(text);
            var pageScroll = root.Descendants().Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == pageScrollName);

            Assert.Equal("Auto", pageScroll.Attribute("VerticalScrollBarVisibility")?.Value);
            Assert.Equal("Disabled", pageScroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        }

        var trainer = File.ReadAllText(Path.Combine(viewDirectory, "TrainerCenterView.xaml"));
        Assert.Contains("GscListViewportHeight", trainer);
    }

    [Fact]
    public void DiagnosticExpanderUsesSharedRoundedThemeAndKeepsLongContentScrollable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var designTokens = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Themes", "DesignTokens.xaml"));
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);
        var expander = maintenance.Descendants().Single(element => element.Name.LocalName == "Expander");

        Assert.Contains("x:Key=\"GscExpander\"", designTokens);
        Assert.Contains("TargetType=\"Expander\"", designTokens);
        Assert.Contains("CornerRadius=\"{StaticResource GscCornerControl}\"", designTokens);
        Assert.Contains("GscSharedFocusVisual", designTokens);
        Assert.Equal("GscExpander", expander.Attribute("Style")?.Value?.Replace("{DynamicResource ", string.Empty).TrimEnd('}'));

        var textBox = expander.Descendants().Single(element => element.Name.LocalName == "TextBox");
        Assert.Equal("Auto", textBox.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", textBox.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.DoesNotContain("<Expander Grid.Row=\"1\" Header=\"查看完整诊断信息\" Margin=\"0,10,0,0\">", maintenanceText);
    }

    [Fact]
    public void MaintenanceAuditKeepsTwoReadableFiniteTablesBehindAWorkspaceScrollViewer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);
        var auditScroll = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceAuditScrollViewer");

        Assert.Equal("Auto", auditScroll.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", auditScroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Contains("x:Name=\"MaintenanceAuditFindingsGrid\" Height=\"{DynamicResource GscTableViewportHeight}\"", maintenanceText);
        Assert.Contains("x:Name=\"MaintenanceAuditLogGrid\" Height=\"{DynamicResource GscTableViewportHeight}\"", maintenanceText);
        Assert.Contains("MinHeight=\"360\"", maintenanceText);
        Assert.Contains("CanContentScroll=\"False\"", maintenanceText);
    }

    [Fact]
    public void MaintenanceDeviceStateKeepsSeveralRowsReachableBehindAWorkspaceScrollViewer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenance = XDocument.Parse(maintenanceText);
        var deviceScroll = maintenance.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "MaintenanceDeviceScrollViewer");

        Assert.Equal("Auto", deviceScroll.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", deviceScroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Contains("x:Name=\"MaintenanceDeviceGrid\" Height=\"{DynamicResource GscTableViewportHeight}\"", maintenanceText);
        Assert.Contains("ItemsSource=\"{Binding DeviceComparisons}\"", maintenanceText);
    }

    [Fact]
    public void TrainerInspectorUsesAFiniteScrollChannelAtShortHeights()
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
        Assert.Equal("280", scrollViewer.Attribute("MaxHeight")?.Value);
        Assert.Contains("TrainerToolsSettingsScrollViewer.MaxHeight = Math.Max(190, Math.Min(280, height * 0.36))", trainerCode);
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

        foreach (var name in new[] { "SaveHistoryActionsScrollViewer", "SaveCandidateReasonScrollViewer", "SaveCandidateActionsScrollViewer" })
        {
            var viewer = save.Descendants().Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
            Assert.Equal("Auto", viewer.Attribute("VerticalScrollBarVisibility")?.Value);
            Assert.Equal("Disabled", viewer.Attribute("HorizontalScrollBarVisibility")?.Value);
        }

        Assert.Contains("SaveHistoryActionsScrollViewer.MaxHeight = Math.Max(130, Math.Min(220, height * (compact ? 0.24 : 0.30)))", saveCode);
        Assert.Contains("SaveCandidateReasonScrollViewer.MaxHeight = Math.Max(90, Math.Min(180, height * (compact ? 0.18 : 0.22)))", saveCode);
        Assert.Contains("SaveCandidateActionsScrollViewer.MaxHeight = Math.Max(70, Math.Min(140, height * (compact ? 0.14 : 0.18)))", saveCode);
        Assert.Contains("MaxHeight=\"220\"", saveText);
        Assert.Contains("MaxHeight=\"180\"", saveText);
        Assert.Contains("MaxHeight=\"140\"", saveText);
        Assert.DoesNotContain("<Border Grid.Row=\"1\" Style=\"{DynamicResource GscSurface}\"", saveText);
    }

    [Fact]
    public void MaintenanceDeviceActionsUseFiniteScrollChannels()
    {
        var repositoryRoot = FindRepositoryRoot();
        var maintenancePath = Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "MaintenanceView.xaml");
        var maintenanceText = File.ReadAllText(maintenancePath);
        var maintenanceCode = File.ReadAllText(maintenancePath + ".cs");
        var maintenance = XDocument.Parse(maintenanceText);

        foreach (var name in new[] { "MaintenanceDeviceDecisionScrollViewer", "MaintenanceRemoteRestoreScrollViewer" })
        {
            var viewer = maintenance.Descendants().Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
            Assert.Equal("Auto", viewer.Attribute("VerticalScrollBarVisibility")?.Value);
            Assert.Equal("Disabled", viewer.Attribute("HorizontalScrollBarVisibility")?.Value);
        }

        Assert.Contains("MaintenanceDeviceDecisionScrollViewer.MaxHeight = Math.Max(90, Math.Min(150, height * (compact ? 0.16 : 0.20)))", maintenanceCode);
        Assert.Contains("MaintenanceRemoteRestoreScrollViewer.MaxHeight = Math.Max(120, Math.Min(210, height * (compact ? 0.22 : 0.28)))", maintenanceCode);
        Assert.Contains("MaxHeight=\"150\"", maintenanceText);
        Assert.Contains("MaxHeight=\"210\"", maintenanceText);
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
        Assert.Contains("TrainerCatalogLayout.RowDefinitions", trainerCode);
        Assert.Contains("x:Name=\"MediaInspectorScrollViewer\"", media);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\" MaxHeight=\"240\"", media);
        Assert.Contains("MediaInspectorScrollViewer.MaxHeight = Math.Max(190, Math.Min(300, height * 0.42))", mediaCode);
        Assert.Contains("MinHeight=\"90\" MaxHeight=\"220\"", maintenance);
        Assert.Contains("TaskSummaryPanel.Columns", taskCode);
        var taskView = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TaskCenterView.xaml"));
        Assert.Contains("x:Name=\"TaskDetailScrollViewer\"", taskView);
        Assert.Contains("TaskDetailScrollViewer.MaxHeight = Math.Max(150, Math.Min(260, height * 0.32))", taskCode);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\" MaxHeight=\"220\"", taskView);
        Assert.Contains("TaskWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("SaveWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("TrainerWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("MaintenanceWorkspaceView.ApplyResponsiveLayout(width, height)", workspaceCode);
        Assert.Contains("x:Key=\"GscRedesignWorkspaceTabControl\"", redesign);
        Assert.Contains("HorizontalScrollBarVisibility=\"Auto\"", redesign);
        Assert.Contains("x:Key=\"GscRedesignWorkspaceTabItem\"", redesign);
        Assert.Contains("CornerRadius=\"12\"", redesign);
        Assert.Contains("Stroke=\"{DynamicResource GscOnAccentTextBrush}\"", tokens);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Auto\"", dashboard);
        Assert.Contains("Property=\"ScrollViewer.VerticalScrollBarVisibility\" Value=\"Auto\"", trainer);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", overview);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", saves);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", media);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", maintenance);
        Assert.Contains("BasedOn=\"{StaticResource GscRedesignWorkspaceTabControl}\"", trainer);
        Assert.Contains("<CheckBox Style=\"{DynamicResource GscCheckBox}\"", saves);
        foreach (var view in new[] { overview, saves, trainer, media, maintenance })
        {
            Assert.DoesNotContain("Background=\"#", view);
            Assert.DoesNotContain("Foreground=\"#", view);
            Assert.Contains("DynamicResource Gsc", view);
        }
        Assert.DoesNotContain("BlurEffect", media + maintenance + trainer);
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
        Assert.Contains("var stack = width < 1180", mediaCode);
        Assert.Contains("Grid.SetRow(MediaMetadataPanel, stack ? 1 : 0)", mediaCode);
    }

    [Fact]
    public void TrainerWorkspaceStacksVirtualizedPanesBeforeTheirControlsBecomeUnreadable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var trainer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "Views", "TrainerCenterView.xaml.cs"));

        Assert.Contains("x:Name=\"TrainerToolsSettingsScrollViewer\"", trainer);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", trainer);
        Assert.Contains("x:Name=\"TrainerCatalogLayout\"", trainer);
        Assert.Contains("x:Name=\"TrainerCatalogResultsPanel\"", trainer);
        Assert.Contains("x:Name=\"TrainerCatalogReleasesPanel\"", trainer);
        Assert.Contains("var stackCatalog = width < 980", codeBehind);
        Assert.Contains("Grid.SetRow(TrainerCatalogReleasesPanel, stackCatalog ? 1 : 0)", codeBehind);
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

        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, element => element.Attribute("AutomationProperties.Name")?.Value == "查看需要关注的游戏、原因和建议处理方式");
        Assert.Contains(actions, element => element.Attribute("AutomationProperties.Name")?.Value == "打开维护中心查看完整关注详情");
        Assert.Contains(overview.Descendants(), element => element.Name.LocalName == "ItemsControl" && (element.Attribute("ItemsSource")?.Value.IndexOf("AttentionFindings", StringComparison.Ordinal) ?? -1) >= 0);
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

        Assert.Contains("x:Name=\"MaintenanceDeviceDecisionScrollViewer\"", maintenance);
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
            new { Header = "活动", Binding = "TaskTypeDisplay" },
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
            var comboBox = comboBoxes.SingleOrDefault(target.Match);
            Assert.NotNull(comboBox);
            Assert.True(
                comboBox!.Descendants().Any(element =>
                    element.Name.LocalName == "TextBlock"
                    && ((element.Attribute("Style")?.Value.IndexOf("GscComboBoxLongText", StringComparison.Ordinal) ?? -1) >= 0
                        || element.Attribute("TextTrimming")?.Value == "CharacterEllipsis")),
                "受限宽度下拉选择未复用 GscComboBoxLongText：" + target.Description);
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
        var dataGrid = overview.Descendants().Single(element => element.Name.LocalName == "DataGrid");
        Assert.Contains("x:Key=\"OverviewDataGrid\"", File.ReadAllText(overviewPath));
        Assert.Contains("<Setter Property=\"EnableRowVirtualization\" Value=\"True\"/>", File.ReadAllText(overviewPath));
        Assert.DoesNotContain(dataGrid.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        Assert.Contains("OpenAttentionFindingCommand", File.ReadAllText(overviewPath));
        Assert.Contains("x:Name=\"OverviewRiskScrollViewer\"", File.ReadAllText(overviewPath));
        Assert.Contains("x:Name=\"OverviewSecondaryScrollViewer\"", File.ReadAllText(overviewPath));
        Assert.Contains("OverviewSecondaryScrollViewer.MaxHeight = stack", File.ReadAllText(overviewPath + ".cs"));
        Assert.Contains("OverviewSecondaryScrollViewer.VerticalScrollBarVisibility = stack", File.ReadAllText(overviewPath + ".cs"));
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
        var dataGrid = tabItem.Descendants().Single(element => element.Name.LocalName == "DataGrid");
        Assert.DoesNotContain(dataGrid.Ancestors(), ancestor => ancestor.Name.LocalName == "StackPanel");
        Assert.Contains(dataGrid.Ancestors(), ancestor => ancestor.Name.LocalName == "Border");
        Assert.Contains(tabItem.Descendants(), element => element.Name.LocalName == "ScrollViewer" && element.Attribute("MaxHeight")?.Value == "190");
        Assert.Contains(tabItem.Descendants(), element => element.Name.LocalName == "RowDefinition" && element.Attribute("Height")?.Value == "*");
        Assert.Contains("Property=\"MinHeight\" Value=\"{DynamicResource GscTableMinHeight}\"", File.ReadAllText(mediaPath));
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
            ("MaintenanceView.xaml", "DeviceComparisons.Count")
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
        Assert.Contains("private async Task SynchronizeLoopAsync()", pluginCode);
        Assert.Contains("TimeSpan.FromMilliseconds(180)", pluginCode);
        Assert.Contains("synchronizationRequested", pluginCode);
        Assert.Contains("var initialDelay = PlayniteApi.Database.Games.Count >= 100", pluginCode);
        Assert.Contains("TimeSpan.FromSeconds(60)", pluginCode);
        Assert.Contains("TimeSpan.FromSeconds(15)", pluginCode);
        Assert.Contains("ConfigureLargeLibraryStartupGate();", pluginCode);
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
        Assert.Contains("WaitForHealthAsync(TimeSpan.FromSeconds(12))", launcherCode);
        Assert.Contains("WaitForHealthAsync", launcherCode);
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
        Assert.Contains("StoragePolicyFields.Columns = twoColumns ? 2 : 1", settingsCode);
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
        Assert.Contains("x:Name=\"SettingsScroller\" VerticalScrollBarVisibility=\"Auto\" HorizontalScrollBarVisibility=\"Disabled\"", settings);
        Assert.Contains("Path=\"DefaultBackupIntervalMinutes\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"ProcessPollingSeconds\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("Path=\"DashboardRefreshSeconds\" UpdateSourceTrigger=\"LostFocus\"", settings);
        Assert.Contains("CoreToolFields.Columns = twoColumns ? 2 : 1", settingsCode);
        Assert.Contains("AppearanceFields.Columns = twoColumns ? 2 : 1", settingsCode);
        Assert.Contains("var contentWidth = Math.Max(320, width - horizontalMargin - trailingMargin);", settingsCode);
        Assert.Contains("AutomationIntervalFields.Columns = expanded && formWidth >= 930 ? 3 : formWidth >= 650 ? 2 : 1", settingsCode);
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
        Assert.Contains("CornerRadius=\"{DynamicResource GscCornerSmall}\"", production);
        Assert.Contains("Value=\"{DynamicResource GscRowHoverBrush}\"", production);
        Assert.DoesNotContain("FocusVisualStyle\" Value=\"{x:Null}\"", trainer);
        Assert.Contains("FocusVisualStyle\" Value=\"{DynamicResource GscSharedFocusVisual}\"", trainer);
        Assert.Contains("x:Key=\"GscRedesignGameContextButton\"", redesign);
        Assert.Contains("x:Key=\"GscRedesignSettingsTabItem\"", redesign);
        Assert.Contains("FocusVisualStyle\" Value=\"{DynamicResource GscSharedFocusVisual}\"", redesign);
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
        Assert.Contains("GameDetailCard.Padding = mode == LayoutMode.Expanded ? new Thickness(18)", dashboardCode);
        Assert.Contains("mode == LayoutMode.Compact ? new Thickness(12)", dashboardCode);
        Assert.Contains("var tableMinHeight = height < 650", dashboardCode);
        Assert.Contains("workspaceView.Resources[\"GscTableMinHeight\"] = tableMinHeight", dashboardCode);
        Assert.Contains("? 220d", dashboardCode);
        Assert.Contains("? 300d", dashboardCode);
        Assert.Contains(": 360d", dashboardCode);
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
        Assert.Contains("catch (InvalidOperationException ex)", viewModelCode);
        Assert.Contains("skipped a Dashboard UI collection update because the dispatcher is unavailable", viewModelCode);
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
        Assert.Contains("catch (InvalidOperationException ex) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)", pluginCode);
        Assert.Contains("catch (TaskCanceledException ex) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)", pluginCode);
        Assert.Contains("if (!TryInvokeUi(() => UiConfirmationRequested?.Invoke(this, args), \"confirmation request\"))", pluginCode);
        Assert.Contains("return false;", pluginCode);
        Assert.Contains("if (!TryInvokeUi(() => handler(this, args), \"notification request\")) return false;", pluginCode);
        Assert.Contains("skipped {operation} because the Playnite UI dispatcher is unavailable", pluginCode);
    }

    [Fact]
    public void LargeLibrarySynchronizationWaitsForAnInteractiveSurface()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("private volatile bool interactiveSurfaceOpened;", pluginCode);
        Assert.Contains("private void RequestLibrarySynchronization(string reason)", pluginCode);
        Assert.Contains("if (!interactiveSurfaceOpened && IsLargeLibrary())", pluginCode);
        Assert.Contains("catalog synchronization is deferred until GameSaveCenter is opened", pluginCode);
        Assert.Contains("interactiveSurfaceOpened = true;", pluginCode);
        Assert.Contains("Opened = CreateDashboardViewSafely", pluginCode);
    }

    [Fact]
    public void LargeLibraryTaskNotificationsDoNotOpenAWorkerLongPollBeforeTheDashboardIsOpened()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginCode = File.ReadAllText(Path.Combine(repositoryRoot, "src", "GameSaveCenter.Playnite", "GameSaveCenterPlugin.cs"));

        Assert.Contains("private bool taskNotificationMonitorDeferred;", pluginCode);
        Assert.Contains("if (taskNotificationTimer != null || taskNotificationMonitorDeferred && !interactiveSurfaceOpened)", pluginCode);
        Assert.Contains("if (observedGameCount >= 100 && !interactiveSurfaceOpened)", pluginCode);
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
        Assert.Contains("x:Name=\"CompactGameSelector\"", dashboard);
        Assert.Contains("x:Name=\"ToggleGameBrowserButton\"", dashboard);
        Assert.Contains("width >= 1260 ? LayoutMode.Expanded", dashboardCode);
        Assert.Contains("width >= 980 ? LayoutMode.Standard", dashboardCode);
        Assert.Contains("width >= 760 ? LayoutMode.Compact", dashboardCode);
        Assert.Contains("Grid.SetRow(TopActionsScroller, 2)", dashboardCode);
        Assert.Contains("Grid.SetColumnSpan(TopActionsScroller, 2)", dashboardCode);
        Assert.Contains("GameSwitcherHost.Visibility = gameScopedWorkspace", dashboardCode);
        Assert.Contains("GameBrowserPanel.MaxHeight = showCompactGameBrowser ? Math.Max(240, Math.Min(360, height * 0.42)) : 0", dashboardCode);
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
        Assert.Contains("由 Playnite 的保存按钮提交", settings);
        Assert.Contains("Text=\"{Binding WorkerExecutable, UpdateSourceTrigger=PropertyChanged}\"", settings);
        Assert.Contains("SelectedValue=\"{Binding BackupFormat}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableUiAnimations}\"", settings);
        Assert.Contains("IsChecked=\"{Binding EnableCloudUpload}\"", settings);
        Assert.Contains("Click=\"OnExportSettingsClick\"", settings);
        Assert.Contains("Click=\"OnImportSettingsClick\"", settings);
        Assert.Contains("SettingsSectionTabs.TabStripPlacement = compact ? Dock.Top : Dock.Left", settingsCode);
        Assert.Contains("tab.MinWidth = compact ? (narrow ? 132 : 158) : 218", settingsCode);
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

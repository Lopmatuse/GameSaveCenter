using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameSaveCenter.Playnite.Infrastructure;
using Playnite.SDK;
using Snackbar = Wpf.Ui.Controls.Snackbar;

namespace GameSaveCenter.Playnite.Views.Development;

public partial class UiFrameworkProbeView : UserControl
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    private readonly UiFrameworkProbeFeedback feedback;

    public UiFrameworkProbeView()
    {
        InitializeComponent();
        feedback = new UiFrameworkProbeFeedback(
            exception => Logger.Error(exception, "GameSaveCenter WPF-UI probe dialog failed."),
            ShowProbeFailure);
    }

    private void OnShowDialogClick(object sender, RoutedEventArgs e)
    {
        ShowProbeFailure("WPF-UI ContentDialogHost 只能在每个 Window 注册一次；Playnite 内嵌页面不创建该宿主。正式确认继续使用 GameSaveCenter 的插件内对话层，避免影响其他扩展。");
    }

    private async void OnShowSnackbarClick(object sender, RoutedEventArgs e)
    {
        await feedback.TryShowAsync(() =>
        {
            var snackbar = new Snackbar(SnackbarHost)
            {
                Title = "WPF-UI Snackbar",
                Content = "这是资源隔离和浮层显示验证，不是任务成功提示。",
                Timeout = TimeSpan.FromSeconds(3),
                IsCloseButtonEnabled = true
            };

            snackbar.Show();
            return Task.CompletedTask;
        });
    }

    private void ShowProbeFailure(string message)
    {
        ProbeFailureText.Text = message;
        ProbeFailurePanel.Visibility = Visibility.Visible;
    }
}

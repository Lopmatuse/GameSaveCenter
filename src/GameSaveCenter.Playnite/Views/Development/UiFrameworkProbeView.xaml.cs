using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GameSaveCenter.Playnite.Infrastructure;
using Playnite.SDK;
using Wpf.Ui.Controls;

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

    private async void OnShowDialogClick(object sender, RoutedEventArgs e)
    {
        await feedback.TryShowAsync(async () =>
        {
            var dialog = new ContentDialog(DialogHost)
            {
                Title = "WPF-UI 对话框",
                Content = "该对话框仅用于验证插件内部宿主、键盘焦点和 Esc 关闭，不会执行任何业务操作。",
                CloseButtonText = "关闭"
            };

            await dialog.ShowAsync();
        });
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

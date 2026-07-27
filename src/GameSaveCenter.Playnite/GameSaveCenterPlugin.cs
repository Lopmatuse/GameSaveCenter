using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using GameSaveCenter.Contracts;
using GameSaveCenter.Playnite.Infrastructure;
using GameSaveCenter.Playnite.Ipc;
using GameSaveCenter.Playnite.Settings;
using GameSaveCenter.Playnite.Views;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

namespace GameSaveCenter.Playnite
{
    /// <summary>Playnite UI and event bridge for GameSaveCenter.</summary>
    public sealed class GameSaveCenterPlugin : GenericPlugin
    {
        private static readonly Guid PluginId = Guid.Parse("66e9f2d7-67bb-43ef-b62a-b8e60734fcec");
        private readonly ILogger logger;
        private readonly WorkerIpcClient client;
        private readonly WorkerLauncher launcher;
        private readonly PlayniteGameAdapter adapter;
        private readonly SemaphoreSlim synchronizationGate = new SemaphoreSlim(1, 1);

        public GameSaveCenterPlugin(IPlayniteAPI api) : base(api)
        {
            logger = LogManager.GetLogger();
            client = new WorkerIpcClient();
            launcher = new WorkerLauncher(client);
            adapter = new PlayniteGameAdapter(api);
            Settings = new GameSaveCenterSettings(this);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override Guid Id => PluginId;
        public GameSaveCenterSettings Settings { get; }
        public event EventHandler? VisualSettingsChanged;

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (Settings.AutoStartWorker) FireAndForget(SynchronizeAsync);
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) => FireAndForget(SynchronizeAsync);
        public override void OnGameInstalled(OnGameInstalledEventArgs args) => FireAndForget(SynchronizeAsync);
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args) => FireAndForget(SynchronizeAsync);

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            FireAndForget(async () =>
            {
                await EnsureWorkerAsync();
                await ApplySettingsCoreAsync();
                var descriptor = adapter.Convert(args.Game);
                await RequestAsync<object>(MessageTypes.UpsertGames, new[] { descriptor });
                var action = args.SourceAction == null ? null : adapter.ConvertSourceAction(args.Game, args.SourceAction);
                await RequestAsync<object>(MessageTypes.GameSessionStarted, new GameSessionEventDto
                {
                    PlayniteId = descriptor.PlayniteId, GameName = descriptor.Name, Source = SessionSourceKind.Playnite,
                    ProcessId = args.StartedProcessId, LaunchProfile = action?.Name ?? "Playnite", ProcessName = action == null ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(action.Path),
                    StartedUtc = DateTime.UtcNow
                });
            });
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            FireAndForget(async () =>
            {
                await EnsureWorkerAsync();
                await ApplySettingsCoreAsync();
                var descriptor = adapter.Convert(args.Game);
                await RequestAsync<object>(MessageTypes.GameSessionStopped, new GameSessionEventDto
                {
                    PlayniteId = descriptor.PlayniteId, GameName = descriptor.Name, Source = SessionSourceKind.Playnite,
                    StoppedUtc = DateTime.UtcNow, ElapsedSeconds = checked((long)Math.Min(args.ElapsedSeconds, (ulong)long.MaxValue))
                });
            });
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = "GameSaveCenter",
                Type = SiderbarItemType.View,
                Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png"),
                Opened = () => new DashboardView(this)
            };
        }

        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        public override UserControl GetSettingsView(bool firstRunSettings) => new GameSaveCenterSettingsView { DataContext = Settings };

        public async Task EnsureWorkerAsync()
        {
            await launcher.EnsureStartedAsync(Environment.ExpandEnvironmentVariables(Settings.WorkerExecutable));
        }

        public void NotifyVisualSettingsChanged() => VisualSettingsChanged?.Invoke(this, EventArgs.Empty);

        public async void ApplySettingsAsync()
        {
            try { await SynchronizeAsync(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        public Task<T> RequestAsync<T>(string type, object payload, TimeSpan? timeout = null) => client.RequestAsync<T>(type, payload, timeout);

        public void ShowError(string message)
        {
            logger.Error(message);
            AddNotification("Error", message, NotificationType.Error);
        }

        public void ShowInfo(string message)
        {
            logger.Info(message);
            AddNotification("Info", message, NotificationType.Info);
        }

        public void ShowTaskNotification(TaskStatusDto task)
        {
            if (!Settings.EnableTaskNotifications || task == null) return;
            var game = string.IsNullOrWhiteSpace(task.GameName) ? "后台任务" : task.GameName;
            var text = task.State == TaskState.Failed
                ? $"{game} · {task.TaskType} 失败：{LimitNotificationText(task.DetailMessage)}"
                : task.State == TaskState.Cancelled
                    ? $"{game} · {task.TaskType} 已取消"
                    : $"{game} · {task.TaskType} 已完成";
            AddNotification("Task." + task.TaskId, text, task.State == TaskState.Failed ? NotificationType.Error : NotificationType.Info);
        }

        private void AddNotification(string category, string message, NotificationType type)
        {
            PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                PlayniteApi.Notifications.Add($"GameSaveCenter.{category}.{Guid.NewGuid():N}", message, type));
        }

        private static string LimitNotificationText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "未知错误";
            const int maximumLength = 320;
            return text.Length <= maximumLength ? text : text.Substring(0, maximumLength) + "…";
        }

        private async Task ApplySettingsCoreAsync() => await RequestAsync<object>(MessageTypes.UpdateSettings, Settings.ToWorkerSettings());

        public async Task SynchronizeAsync()
        {
            // Playnite's database is captured before asynchronous continuations leave the UI context.
            var games = PlayniteApi.Database.Games.Select(adapter.Convert).ToList();
            await synchronizationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await EnsureWorkerAsync().ConfigureAwait(false);
                await ApplySettingsCoreAsync().ConfigureAwait(false);
                await RequestAsync<object>(MessageTypes.UpsertGames, games, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
            }
            finally
            {
                synchronizationGate.Release();
            }
        }

        private async void FireAndForget(Func<Task> operation)
        {
            try { await operation(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }
    }
}

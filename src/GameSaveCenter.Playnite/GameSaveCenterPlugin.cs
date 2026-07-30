using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
        private readonly SemaphoreSlim taskNotificationPollGate = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, byte> notifiedTaskIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private Timer? taskNotificationTimer;
        private DateTime taskNotificationMonitorStartedUtc;
        private bool taskNotificationSnapshotInitialized;
        private long lastTaskNotificationSequence;
        private string lastSynchronizedLibraryFingerprint = string.Empty;
        private DateTime lastLibrarySynchronizationUtc = DateTime.MinValue;

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
        public event EventHandler<UiNotificationEventArgs>? UiNotificationRequested;
        public event EventHandler<UiConfirmationEventArgs>? UiConfirmationRequested;

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            StartTaskNotificationMonitor();
            if (Settings.AutoStartWorker) FireAndForget(SynchronizeAsync);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            taskNotificationTimer?.Dispose();
            taskNotificationTimer = null;
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

        /// <summary>Starts a best-effort task-event listener for an open dashboard.</summary>
        public Task ListenForTaskEventsAsync(Func<TaskChangeEventDto, Task> onEvent, CancellationToken token)
            => client.ListenForTaskEventsAsync(onEvent, token);

        public void ShowError(string message)
        {
            logger.Error(message);
            if (!RaiseUiNotification("操作失败", message, UiNotificationKind.Error))
                AddNotification("Error", message, NotificationType.Error);
        }

        public void ShowInfo(string message)
        {
            logger.Info(message);
            if (!RaiseUiNotification("操作完成", message, UiNotificationKind.Success))
                AddNotification("Info", message, NotificationType.Info);
        }

        public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "确认", string cancelText = "取消", bool isDangerous = false)
        {
            var args = new UiConfirmationEventArgs(title, message, confirmText, cancelText, isDangerous);
            PlayniteApi.MainView.UIDispatcher.Invoke(() => UiConfirmationRequested?.Invoke(this, args));
            if (args.Handled) return await args.Completion.Task.ConfigureAwait(false);

            var result = PlayniteApi.Dialogs.ShowMessage(message, title, System.Windows.MessageBoxButton.YesNo);
            return result == System.Windows.MessageBoxResult.Yes;
        }

        public void ShowTaskNotification(TaskStatusDto task)
        {
            if (!Settings.EnableTaskNotifications || task == null) return;
            if (task.State != TaskState.Succeeded && task.State != TaskState.Failed && task.State != TaskState.Cancelled) return;
            if (!notifiedTaskIds.TryAdd(task.TaskId, 0)) return;
            var game = string.IsNullOrWhiteSpace(task.GameName) ? "后台任务" : task.GameName;
            var text = task.State == TaskState.Failed
                ? $"{game} · {task.TaskTypeDisplay} 失败：{LimitNotificationText(task.DetailMessage)}"
                : task.State == TaskState.Cancelled
                    ? $"{game} · {task.TaskTypeDisplay} 已取消"
                    : $"{game} · {LimitNotificationText(task.DetailMessage)}";
            var kind = task.State == TaskState.Failed ? UiNotificationKind.Error
                : task.State == TaskState.Cancelled ? UiNotificationKind.Warning
                : UiNotificationKind.Success;
            if (!RaiseUiNotification(TaskNotificationTitle(task), text, kind))
                AddNotification("Task." + task.TaskId, text, task.State == TaskState.Failed ? NotificationType.Error : NotificationType.Info);
        }

        private static string TaskNotificationTitle(TaskStatusDto task)
        {
            if (task.State == TaskState.Failed) return "后台任务失败";
            if (task.State == TaskState.Cancelled) return "后台任务已取消";
            return "后台任务完成";
        }

        private bool RaiseUiNotification(string title, string message, UiNotificationKind kind)
        {
            var handler = UiNotificationRequested;
            if (handler == null) return false;
            var args = new UiNotificationEventArgs(title, LimitNotificationText(message), kind);
            PlayniteApi.MainView.UIDispatcher.Invoke(() => handler(this, args));
            return args.Handled;
        }

        private void AddNotification(string category, string message, NotificationType type)
        {
            PlayniteApi.MainView.UIDispatcher.Invoke(() =>
                PlayniteApi.Notifications.Add($"GameSaveCenter.{category}", message, type));
        }

        private void StartTaskNotificationMonitor()
        {
            taskNotificationMonitorStartedUtc = DateTime.UtcNow;
            taskNotificationTimer = new Timer(_ => PollTaskNotifications(), null, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1));
        }

        private async void PollTaskNotifications()
        {
            if (!await taskNotificationPollGate.WaitAsync(0).ConfigureAwait(false)) return;
            try
            {
                if (!taskNotificationSnapshotInitialized)
                {
                    // Seed durable history once, then switch to the Worker's signalled change feed.
                    // This does not start a disabled Worker; connection failure is handled below.
                    var tasks = await RequestAsync<TaskStatusDto[]>(MessageTypes.GetTasks, new GameQueryDto { Limit = 200 }, TimeSpan.FromSeconds(4)).ConfigureAwait(false);
                    foreach (var task in tasks)
                    {
                        var terminal = task.State == TaskState.Succeeded || task.State == TaskState.Failed || task.State == TaskState.Cancelled;
                        if (!terminal) continue;
                        if (task.CreatedUtc < taskNotificationMonitorStartedUtc.AddSeconds(-5))
                            notifiedTaskIds.TryAdd(task.TaskId, 0);
                        else if (Settings.EnableTaskNotifications) ShowTaskNotification(task);
                        else notifiedTaskIds.TryAdd(task.TaskId, 0);
                    }
                    taskNotificationSnapshotInitialized = true;
                }

                var feed = await RequestAsync<TaskChangeFeedDto>(
                    MessageTypes.WaitForTaskChanges,
                    new TaskChangeRequestDto { AfterSequence = lastTaskNotificationSequence, Limit = 200, WaitSeconds = 20 },
                    TimeSpan.FromSeconds(25)).ConfigureAwait(false);
                if (feed.ResetRequired) lastTaskNotificationSequence = 0;
                foreach (var change in feed.Changes)
                {
                    var task=change.Task;
                    var terminal=task.State==TaskState.Succeeded||task.State==TaskState.Failed||task.State==TaskState.Cancelled;
                    if (terminal)
                    {
                        if (Settings.EnableTaskNotifications) ShowTaskNotification(task);
                        else notifiedTaskIds.TryAdd(task.TaskId,0);
                    }
                    lastTaskNotificationSequence=Math.Max(lastTaskNotificationSequence,change.Sequence);
                }
                lastTaskNotificationSequence=Math.Max(lastTaskNotificationSequence,feed.LatestSequence);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Task notification poll is temporarily unavailable.");
            }
            finally
            {
                taskNotificationPollGate.Release();
            }
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
            var fingerprint = CreateLibraryFingerprint(games);
            await synchronizationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await EnsureWorkerAsync().ConfigureAwait(false);
                await ApplySettingsCoreAsync().ConfigureAwait(false);
                if (string.Equals(fingerprint, lastSynchronizedLibraryFingerprint, StringComparison.Ordinal)
                    && DateTime.UtcNow - lastLibrarySynchronizationUtc < TimeSpan.FromMinutes(5))
                {
                    return;
                }
                await RequestAsync<object>(MessageTypes.UpsertGames, games, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                lastSynchronizedLibraryFingerprint = fingerprint;
                lastLibrarySynchronizationUtc = DateTime.UtcNow;
            }
            finally
            {
                synchronizationGate.Release();
            }
        }

        private static string CreateLibraryFingerprint(IEnumerable<GameDescriptorDto> games)
        {
            var builder = new StringBuilder();
            foreach (var game in games.OrderBy(x => x.PlayniteId, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(game.PlayniteId).Append('\u001f')
                    .Append(game.Name).Append('\u001f')
                    .Append((int)game.Platform).Append('\u001f')
                    .Append(game.PlatformGameId).Append('\u001f')
                    .Append(game.InstallDirectory).Append('\u001f')
                    .Append(game.IsInstalled ? '1' : '0').Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", string.Empty);
            }
        }

        private async void FireAndForget(Func<Task> operation)
        {
            try { await operation(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }
    }
}

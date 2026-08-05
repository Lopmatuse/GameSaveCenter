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
using System.Windows.Threading;
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
        private readonly object synchronizationRequestGate = new object();
        private readonly SemaphoreSlim taskNotificationPollGate = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, byte> notifiedTaskIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private Timer? taskNotificationTimer;
        private DateTime taskNotificationMonitorStartedUtc;
        private DateTime taskNotificationRetryAfterUtc = DateTime.MinValue;
        private int taskNotificationFailureCount;
        private DateTime lastTaskNotificationFailureLogUtc = DateTime.MinValue;
        private bool taskNotificationSnapshotInitialized;
        private bool taskNotificationMonitorDeferred;
        private long lastTaskNotificationSequence;
        private string lastSynchronizedLibraryFingerprint = string.Empty;
        private DateTime lastLibrarySynchronizationUtc = DateTime.MinValue;
        private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        private DateTime largeLibraryStartupSyncNotBeforeUtc = DateTime.MinValue;
        private volatile int observedGameCount;
        private volatile bool interactiveSurfaceOpened;
        private Task? synchronizationTask;
        private bool synchronizationRequested;

        // A 500+ library must stay interactive even when the dashboard is opened for the
        // first time.  The durable SQLite snapshot is enough to render the shell; a full
        // catalog rematch is an explicit refresh operation, while game-started entries still
        // get matched through the single-game UpsertGames path.
        private const int VeryLargeLibraryThreshold = 500;

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
            // Establish the large-library quiet window before any Playnite library callback
            // can arrive.  Library providers may publish an update immediately after this
            // hook; configuring the gate here prevents that early callback from bypassing the
            // startup delay and submitting hundreds of Ludusavi requests while Playnite is
            // still importing games.
            ConfigureLargeLibraryStartupGate();
            var assembly = Assembly.GetExecutingAssembly();
            logger.Info($"GameSaveCenter {assembly.GetName().Version} loaded from {assembly.Location}; observed {observedGameCount} Playnite games.");
            StartTaskNotificationMonitor();
            if (Settings.AutoStartWorker)
            {
                // Playnite can invoke OnApplicationStarted before its library import has
                // populated Database.Games.  Treat a zero-count snapshot as "database not
                // ready", rather than starting the Worker and immediately submitting what
                // later becomes a 900+ game catalog.  The old 0.6.22 path did exactly that:
                // the first full snapshot arrived after this hook and queued one Ludusavi
                // process per title while Playnite was still starting.
                if (observedGameCount == 0)
                {
                    logger.Info("Playnite game database is not ready at application start; deferring GameSaveCenter Worker startup until the host library settles.");
                    FireAndForget(WaitForLibraryReadyAndStartWorkerAsync);
                }
                // A 900+ game Playnite profile should not start the Worker (and its process
                // detector/SQLite initialization) merely because the host loaded extensions.
                // Playnite game-start callbacks and an explicit dashboard open still start it
                // on demand, while small libraries keep the existing automatic behavior.
                else if (IsLargeLibrary())
                    logger.Info($"Deferring Worker startup for large Playnite library ({observedGameCount} games) until GameSaveCenter is opened or a game starts.");
                else
                    FireAndForget(StartWorkerAndScheduleSynchronizationAsync);
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            lifetimeCancellation.Cancel();
            taskNotificationTimer?.Dispose();
            taskNotificationTimer = null;
            taskNotificationMonitorDeferred = false;
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) => RequestLibrarySynchronization("library update");
        public override void OnGameInstalled(OnGameInstalledEventArgs args) => RequestLibrarySynchronization("game installed");
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args) => RequestLibrarySynchronization("game uninstalled");

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
                // A malformed XAML resource must not bring down Playnite's extension host. The
                // fallback keeps the sidebar usable and exposes the real exception in the
                // extension log instead of letting Playnite show its generic crash dialog.
                Opened = CreateDashboardViewSafely
            };
        }

        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        public override UserControl GetSettingsView(bool firstRunSettings) => CreateSettingsViewSafely();

        private UserControl CreateDashboardViewSafely()
        {
            // Enabling the extension should not submit a full 900+ game catalog job by itself.
            // Opening the dashboard is explicit user intent and releases this startup gate.
            ObserveGameCount(PlayniteApi.Database.Games.Count);
            interactiveSurfaceOpened = true;
            StartTaskNotificationMonitor();
            // Opening the dashboard is explicit user intent. Start the Worker in the
            // background so cache-first rendering remains non-blocking while commands and
            // the delayed catalog refresh can use a healthy pipe.
            FireAndForget(EnsureWorkerAsync);
            try
            {
                return new DashboardView(this);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "GameSaveCenter Dashboard failed to construct; showing the safe fallback view.");
                return SafeViewFactory.Create(
                    "GameSaveCenter 界面暂时无法加载",
                    "插件已阻止这次界面异常向 Playnite 冒泡。请查看 extensions.log 中的 GameSaveCenter 错误，并确认已安装最新版本。",
                    ex);
            }
        }

        private UserControl CreateSettingsViewSafely()
        {
            ObserveGameCount(PlayniteApi.Database.Games.Count);
            interactiveSurfaceOpened = true;
            StartTaskNotificationMonitor();
            try
            {
                return new GameSaveCenterSettingsView { DataContext = Settings };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "GameSaveCenter settings view failed to construct; showing the safe fallback view.");
                return SafeViewFactory.Create(
                    "GameSaveCenter 设置界面暂时无法加载",
                    "请查看 extensions.log 中的 GameSaveCenter 错误，并确认已安装最新版本。",
                    ex);
            }
        }

        public async Task EnsureWorkerAsync()
        {
            // On a 500+ game profile, a busy Worker can legitimately miss a short Ping while
            // SQLite or an explicit catalog refresh is running.  Killing that process loses
            // the in-flight durable work and creates the restart/pipe-timeout loop observed in
            // the user's 900+ game logs.  Keep the conservative stale-process recovery only
            // for smaller libraries; large libraries may retry without destructive recovery.
            await launcher.EnsureStartedAsync(
                Environment.ExpandEnvironmentVariables(Settings.WorkerExecutable),
                terminateUnhealthyProcess: !IsVeryLargeLibrary(),
                expectedVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString());
        }

        public void NotifyVisualSettingsChanged() => VisualSettingsChanged?.Invoke(this, EventArgs.Empty);

        public void ApplySettingsAsync()
        {
            // Settings changes do not change the Playnite game descriptors. Sending the
            // settings directly avoids turning every settings save into a 900-game Ludusavi
            // catalog refresh. Library callbacks and the explicit Dashboard refresh remain the
            // only paths that request catalog synchronization.
            FireAndForget(async () =>
            {
                await EnsureWorkerAsync().ConfigureAwait(false);
                await ApplySettingsCoreAsync().ConfigureAwait(false);
            });
        }

        private void RequestLibrarySynchronization(string reason)
        {
            // A library callback can arrive while Playnite still exposes an empty database.
            // Do not enqueue an automatic synchronization from that transient snapshot; the
            // provider will publish another callback after import, and the delayed startup
            // probe will re-evaluate the real game count. This avoids a race where opening the
            // dashboard a few milliseconds later accidentally promotes the queued task to an
            // interactive 900+ game full refresh.
            // Capture the current count before the readiness check.  The old 0.6.22 race
            // returned early while observedGameCount was still zero, so a 900-game profile
            // could remain classified as an empty/small library until the dashboard was
            // opened.  That allowed the first partial library callback to take the eager
            // Worker path.  Keeping the largest settled snapshot is what makes the startup
            // gate monotonic and safe across Playnite's import callbacks.
            var currentGameCount = PlayniteApi.Database.Games.Count;
            ObserveGameCount(currentGameCount);
            if (currentGameCount == 0)
            {
                logger.Debug($"Deferring automatic catalog synchronization because the Playnite library is not ready ({reason}).");
                return;
            }
            if (IsVeryLargeLibrary())
            {
                logger.Info($"Deferring automatic catalog synchronization for very large Playnite library ({observedGameCount} games); reason: {reason}. Use the dashboard refresh action to opt in.");
                return;
            }
            if (!interactiveSurfaceOpened && IsLargeLibrary())
            {
                logger.Debug($"Deferring large-library synchronization until GameSaveCenter is opened ({reason}).");
                return;
            }

            FireAndForget(SynchronizeAsync);
        }

        private bool IsLargeLibrary()
        {
            ObserveGameCount(PlayniteApi.Database.Games.Count);
            return observedGameCount >= 100;
        }

        private void ObserveGameCount(int currentCount)
        {
            // Playnite can briefly expose an empty or partial Database.Games collection while
            // a library provider is importing or shutting down. Never let that transient
            // snapshot downgrade a previously observed 500+ profile to the small-library
            // recovery path, which is allowed to terminate and restart a busy Worker.
            if (currentCount > observedGameCount)
                observedGameCount = currentCount;
        }

        private bool IsVeryLargeLibrary()
        {
            // Keep the largest settled snapshot observed so a transient Playnite database
            // count of zero (library import, shutdown, or provider refresh) cannot make a
            // 900+ game Worker look like a small-library process that is safe to terminate.
            var currentCount = PlayniteApi.Database.Games.Count;
            ObserveGameCount(currentCount);
            return observedGameCount >= VeryLargeLibraryThreshold;
        }

        /// <summary>
        /// Lets the dashboard choose a cache-first startup path without exposing the
        /// Playnite database or a full Game collection to the UI layer.
        /// </summary>
        public bool IsLargeLibraryForUi => observedGameCount >= 100;

        public bool IsVeryLargeLibraryForUi => observedGameCount >= VeryLargeLibraryThreshold;

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
            if (!TryInvokeUi(() => UiConfirmationRequested?.Invoke(this, args), "confirmation request"))
            {
                // A destructive or restore action must never proceed when its confirmation UI
                // cannot be safely displayed during Playnite shutdown.
                return false;
            }
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
            if (!TryInvokeUi(() => handler(this, args), "notification request")) return false;
            return args.Handled;
        }

        private void AddNotification(string category, string message, NotificationType type)
        {
            TryInvokeUi(() => PlayniteApi.Notifications.Add($"GameSaveCenter.{category}", message, type), "Playnite notification");
        }

        private bool TryInvokeUi(Action action, string operation)
        {
            if (action == null) return false;
            var dispatcher = PlayniteApi.MainView.UIDispatcher;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return false;
            try
            {
                if (dispatcher.CheckAccess())
                {
                    action();
                    return true;
                }

                dispatcher.Invoke(action, DispatcherPriority.DataBind);
                return true;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException) && !(ex is StackOverflowException))
            {
                // Notification/confirmation handlers belong to the visual layer. A stale
                // resource, closing window, or handler bug must never escape through
                // Playnite's shared dispatcher and become a generic extension crash.
                logger.Error(ex, $"GameSaveCenter skipped {operation} because the UI callback failed or the dispatcher is unavailable.");
                return false;
            }
        }

        private void StartTaskNotificationMonitor()
        {
            if (taskNotificationTimer != null || taskNotificationMonitorDeferred && !interactiveSurfaceOpened)
                return;

            // A large Playnite library commonly has another Ludusavi integration importing
            // hundreds of titles at the same time.  The task feed is a convenience channel,
            // not the source of truth, so do not even open a long-poll pipe until the user has
            // opened GameSaveCenter.  This avoids an otherwise idle extension competing with
            // Playnite's startup and the other Ludusavi plugin.
            if ((observedGameCount == 0 || observedGameCount >= 100) && !interactiveSurfaceOpened)
            {
                taskNotificationMonitorDeferred = true;
                logger.Info($"Deferring task notification monitor until GameSaveCenter is opened ({observedGameCount} games observed; Playnite may still be importing the library).");
                return;
            }

            taskNotificationMonitorDeferred = false;
            taskNotificationMonitorStartedUtc = DateTime.UtcNow;
            // Do not compete with Playnite's library import or the Worker's first SQLite
            // initialization.  In a large library the first sync can legitimately take a
            // while; starting a long-poll request before the Worker has finished its first
            // health check only creates pipe timeouts and extra thread-pool work.  The
            // notification feed is a convenience channel; SQLite snapshots remain the source
            // of truth, so it is safe to wait longer on a 100+ game library.
            var initialDelay = PlayniteApi.Database.Games.Count >= 100 || PlayniteApi.Database.Games.Count == 0
                ? TimeSpan.FromSeconds(60)
                : TimeSpan.FromSeconds(15);
            taskNotificationTimer = new Timer(_ => PollTaskNotifications(), null, initialDelay, TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// Stops the optional notification poll when the embedded dashboard is detached.
        /// SQLite snapshots and the dashboard's own event/poll path remain authoritative;
        /// keeping a hidden 900+ game page on a long-poll pipe only creates needless retries
        /// while Playnite is switching views or shutting down.
        /// </summary>
        public void StopTaskNotificationMonitor()
        {
            var timer = taskNotificationTimer;
            taskNotificationTimer = null;
            if (timer == null) return;
            try { timer.Dispose(); }
            catch (ObjectDisposedException) { }
        }

        private void PollTaskNotifications() => FireAndForget(PollTaskNotificationsAsync);

        private async Task PollTaskNotificationsAsync()
        {
            var gateEntered = false;
            try
            {
                // Do not reconnect to a starting/busy Worker every second. A full library
                // refresh can legitimately keep the pipe unavailable for a while; exponential
                // backoff prevents the notification timer from adding hundreds of failed pipe
                // connects to Playnite's UI log and thread pool.
                if (DateTime.UtcNow < taskNotificationRetryAfterUtc) return;
                if (!await taskNotificationPollGate.WaitAsync(0).ConfigureAwait(false)) return;
                gateEntered = true;
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
                taskNotificationFailureCount = 0;
                taskNotificationRetryAfterUtc = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                taskNotificationFailureCount = Math.Min(taskNotificationFailureCount + 1, 6);
                var delaySeconds = Math.Min(60, 5 * (1 << Math.Max(0, taskNotificationFailureCount - 1)));
                taskNotificationRetryAfterUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
                // Keep the diagnostic useful without emitting one full stack trace every few
                // seconds while the Worker is still starting or has been stopped.
                if (DateTime.UtcNow - lastTaskNotificationFailureLogUtc >= TimeSpan.FromSeconds(30))
                {
                    lastTaskNotificationFailureLogUtc = DateTime.UtcNow;
                    logger.Debug(ex, $"Task notification poll is temporarily unavailable; retrying in {delaySeconds}s.");
                }
            }
            finally
            {
                if (gateEntered) taskNotificationPollGate.Release();
            }
        }

        private static string LimitNotificationText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "未知错误";
            const int maximumLength = 320;
            return text.Length <= maximumLength ? text : text.Substring(0, maximumLength) + "…";
        }

        private async Task ApplySettingsCoreAsync() => await RequestAsync<object>(MessageTypes.UpdateSettings, Settings.ToWorkerSettings());

        public Task SynchronizeAsync()
        {
            lock (synchronizationRequestGate)
            {
                synchronizationRequested = true;
                if (synchronizationTask == null || synchronizationTask.IsCompleted)
                    synchronizationTask = SynchronizeLoopAsync();
                return synchronizationTask;
            }
        }

        /// <summary>
        /// Requests a catalog refresh from a dashboard without turning an already-running
        /// large-library startup refresh into a second back-to-back full refresh. The first
        /// dashboard instance is opened while Playnite may still be importing hundreds of
        /// games; joining the existing task keeps the UI cache-first and avoids re-queuing the
        /// same 900-game snapshot simply because the sidebar was clicked.
        /// </summary>
        public Task SynchronizeFromDashboardAsync()
        {
            interactiveSurfaceOpened = true;
            if (IsVeryLargeLibrary())
            {
                logger.Info($"Skipping automatic dashboard catalog synchronization for very large library ({observedGameCount} games); cache-first UI remains available and explicit refresh is required.");
                return Task.CompletedTask;
            }
            lock (synchronizationRequestGate)
            {
                if (synchronizationTask != null && !synchronizationTask.IsCompleted)
                    return synchronizationTask;

                if (lastLibrarySynchronizationUtc != DateTime.MinValue
                    && DateTime.UtcNow - lastLibrarySynchronizationUtc < TimeSpan.FromMinutes(5))
                    return Task.CompletedTask;
            }

            return SynchronizeAsync();
        }

        private async Task SynchronizeLoopAsync()
        {
            while (true)
            {
                lock (synchronizationRequestGate) synchronizationRequested = false;

                // Playnite can raise several library callbacks while one importer is still
                // publishing games. Coalesce that burst before taking the expensive snapshot
                // and fingerprint; one request is enough for the final library state.
                await Task.Delay(TimeSpan.FromMilliseconds(180), lifetimeCancellation.Token).ConfigureAwait(false);
                await SynchronizeOnceAsync().ConfigureAwait(false);

                lock (synchronizationRequestGate)
                {
                    if (synchronizationRequested) continue;
                    synchronizationTask = null;
                    return;
                }
            }
        }

        private async Task SynchronizeOnceAsync()
        {
            // Playnite's database is captured before asynchronous continuations leave the UI context.
            var games = PlayniteApi.Database.Games.Select(adapter.Convert).ToList();
            // The first library callback may still arrive after the extension was loaded while
            // the host is importing games.  Re-check the actual captured snapshot here: if it is
            // a very large library and the user has not opened GameSaveCenter, abort before
            // starting the Worker/IPC request.  This last gate is what protects against the
            // 0-game-at-startup race that caused 0.6.22 to launch hundreds of Ludusavi
            // processes despite the outer large-library check.
            ObserveGameCount(games.Count);
            if (games.Count >= VeryLargeLibraryThreshold && !interactiveSurfaceOpened)
            {
                logger.Info($"Skipping automatic catalog synchronization for captured very large library ({games.Count} games) before dashboard open; durable cache remains authoritative.");
                return;
            }
            var fingerprint = CreateLibraryFingerprint(games);
            // A large Playnite library should be allowed to finish its own startup before we
            // submit a full descriptor refresh.  Dashboard and library-update callbacks during
            // this grace period can still render durable cached data; the scheduled startup
            // sync will submit the snapshot once the host is idle.
            if (DateTime.UtcNow < largeLibraryStartupSyncNotBeforeUtc)
            {
                var quietDelay = largeLibraryStartupSyncNotBeforeUtc - DateTime.UtcNow;
                logger.Debug($"Deferring large-library synchronization until {largeLibraryStartupSyncNotBeforeUtc:O}; waiting {quietDelay.TotalSeconds:F1}s for Playnite to become idle ({games.Count} games captured).");
                // Do not simply return here. An empty-cache Dashboard intentionally releases
                // its first sync after 10 seconds, which can still fall inside the host's
                // 25-second startup quiet window. Waiting inside the coalesced background task
                // guarantees that first-run libraries eventually synchronize without requiring
                // another Playnite library event, while never blocking the UI thread.
                await Task.Delay(quietDelay, lifetimeCancellation.Token).ConfigureAwait(false);
            }

            // Avoid even starting Worker/IPC work for duplicate Playnite library events.  The
            // old order performed EnsureWorker and UpdateSettings before checking the fingerprint,
            // so a burst of import notifications still woke the Worker repeatedly.
            if (string.Equals(fingerprint, lastSynchronizedLibraryFingerprint, StringComparison.Ordinal)
                && DateTime.UtcNow - lastLibrarySynchronizationUtc < TimeSpan.FromMinutes(5))
            {
                return;
            }

            await synchronizationGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (string.Equals(fingerprint, lastSynchronizedLibraryFingerprint, StringComparison.Ordinal)
                    && DateTime.UtcNow - lastLibrarySynchronizationUtc < TimeSpan.FromMinutes(5))
                {
                    return;
                }
                await EnsureWorkerAsync().ConfigureAwait(false);
                await ApplySettingsCoreAsync().ConfigureAwait(false);
                await RequestAsync<object>(MessageTypes.UpsertGames, games, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                lastSynchronizedLibraryFingerprint = fingerprint;
                lastLibrarySynchronizationUtc = DateTime.UtcNow;
            }
            finally
            {
                synchronizationGate.Release();
            }
        }

        private async Task StartWorkerAndScheduleSynchronizationAsync()
        {
            try
            {
                var gameCount = PlayniteApi.Database.Games.Count;
                // OnApplicationStarted can run before library providers have finished loading.
                // Never take the zero-game snapshot as evidence of a small library. Wait for
                // the host to publish its library, then re-evaluate the large-library gates.
                if (gameCount == 0)
                {
                    logger.Info("Skipping eager Worker startup because the Playnite library is still empty; waiting for a settled library snapshot.");
                    await Task.Delay(TimeSpan.FromSeconds(25), lifetimeCancellation.Token).ConfigureAwait(false);
                    gameCount = PlayniteApi.Database.Games.Count;
                    ObserveGameCount(gameCount);
                    if (gameCount == 0)
                    {
                        logger.Info("Playnite library is still empty after the startup grace period; leaving the Worker stopped until a game or the dashboard explicitly requires it.");
                        return;
                    }
                }
                var isLargeLibrary = gameCount >= 100;
                // OnApplicationStarted normally establishes this before library callbacks can
                // run.  Keep the fallback for hosts that invoke this method directly (settings
                // repair/tests) without extending an already active quiet window.
                if (largeLibraryStartupSyncNotBeforeUtc == DateTime.MinValue)
                    ConfigureLargeLibraryStartupGate();

                // Start the Worker and apply settings promptly so process detection and task
                // handling are available, but do not submit hundreds of Ludusavi lookups while
                // Playnite is still importing a 900+ game library.
                await EnsureWorkerAsync().ConfigureAwait(false);
                await ApplySettingsCoreAsync().ConfigureAwait(false);
                if (isLargeLibrary)
                {
                    if (!interactiveSurfaceOpened)
                    {
                        logger.Info($"Detected a large Playnite library ({gameCount} games); catalog synchronization is deferred until GameSaveCenter is opened.");
                        return;
                    }

                    logger.Info($"Detected a large Playnite library ({gameCount} games); deferring initial catalog synchronization for 25 seconds.");
                    if (gameCount >= VeryLargeLibraryThreshold)
                    {
                        logger.Info($"Very large Playnite library ({gameCount} games); Worker is ready but automatic catalog synchronization stays disabled until an explicit refresh.");
                        return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(25), lifetimeCancellation.Token).ConfigureAwait(false);
                }

                await SynchronizeAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                // Playnite is shutting down; do not surface the intentional delay cancellation
                // as an extension failure.
            }
        }

        private async Task WaitForLibraryReadyAndStartWorkerAsync()
        {
            try
            {
                // This path is intentionally conservative.  It is only used when Playnite
                // reported zero games during OnApplicationStarted; by the time this delay
                // expires the library providers have normally published their final snapshot.
                await Task.Delay(TimeSpan.FromSeconds(25), lifetimeCancellation.Token).ConfigureAwait(false);
                var gameCount = PlayniteApi.Database.Games.Count;
                ObserveGameCount(gameCount);
                if (gameCount >= 100)
                {
                    logger.Info($"Playnite library settled at {gameCount} games; keeping Worker startup and catalog synchronization deferred until GameSaveCenter is opened explicitly.");
                    ConfigureLargeLibraryStartupGate();
                    return;
                }

                await StartWorkerAndScheduleSynchronizationAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
            {
                // Playnite is shutting down; the deferred startup is intentionally cancelled.
            }
        }

        private void ConfigureLargeLibraryStartupGate()
        {
            var gameCount = PlayniteApi.Database.Games.Count;
            ObserveGameCount(gameCount);
            if (gameCount == 0 || observedGameCount >= 100)
            {
                if (largeLibraryStartupSyncNotBeforeUtc == DateTime.MinValue
                    || largeLibraryStartupSyncNotBeforeUtc <= DateTime.UtcNow)
                    largeLibraryStartupSyncNotBeforeUtc = DateTime.UtcNow.AddSeconds(25);
            }
            else
            {
                largeLibraryStartupSyncNotBeforeUtc = DateTime.MinValue;
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

        private void FireAndForget(Func<Task> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            try
            {
                Observe(operation());
            }
            catch (Exception ex)
            {
                ReportBackgroundFailure(ex);
            }
        }

        private void Observe(Task operation)
        {
            _ = operation.ContinueWith(
                task => ReportBackgroundFailure(task.Exception?.GetBaseException() ?? new InvalidOperationException("未知后台任务错误。")),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private void ReportBackgroundFailure(Exception exception)
        {
            logger.Error(exception, "GameSaveCenter background operation failed.");
            try
            {
                ShowError(exception.Message);
            }
            catch (Exception reportingException)
            {
                // A broken notification surface must never turn a background operation failure
                // into an unhandled exception on Playnite's Dispatcher or a timer callback.
                logger.Error(reportingException, "GameSaveCenter failed to present a background operation error.");
            }
        }
    }
}

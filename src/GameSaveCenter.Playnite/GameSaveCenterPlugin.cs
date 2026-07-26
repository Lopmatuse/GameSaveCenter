using System;
using System.Collections.Generic;
using System.Linq;
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

        public GameSaveCenterPlugin(IPlayniteAPI api) : base(api)
        {
            logger = api.CreateLogger();
            client = new WorkerIpcClient();
            launcher = new WorkerLauncher(client);
            adapter = new PlayniteGameAdapter(api);
            Settings = new GameSaveCenterSettings(this);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override Guid Id => PluginId;
        public IPlayniteAPI PlayniteApi => PlayniteApiInternal;
        private IPlayniteAPI PlayniteApiInternal => base.PlayniteApi;
        public GameSaveCenterSettings Settings { get; }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            if (Settings.AutoStartWorker) FireAndForget(async () => { await EnsureWorkerAsync(); await ApplySettingsCoreAsync(); await ExportLibraryAsync(); });
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) => FireAndForget(ExportLibraryAsync);
        public override void OnGameInstalled(OnGameInstalledEventArgs args) => FireAndForget(ExportLibraryAsync);
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args) => FireAndForget(ExportLibraryAsync);

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            FireAndForget(async () =>
            {
                await EnsureWorkerAsync();
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
                Icon = "\ue8b7",
                Opened = () => new SidebarItemView { Type = SidebarItemViewType.UserControl, Content = new DashboardView(this) }
            };
        }

        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        public override UserControl GetSettingsView(bool firstRunSettings) => new GameSaveCenterSettingsView { DataContext = Settings };

        public async Task EnsureWorkerAsync()
        {
            await launcher.EnsureStartedAsync(Environment.ExpandEnvironmentVariables(Settings.WorkerExecutable));
        }

        public async void ApplySettingsAsync()
        {
            try { await EnsureWorkerAsync(); await ApplySettingsCoreAsync(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        public Task<T> RequestAsync<T>(string type, object payload, TimeSpan? timeout = null) => client.RequestAsync<T>(type, payload, timeout);

        public void ShowError(string message)
        {
            logger.Error(message);
            PlayniteApi.MainView.UIDispatcher.Invoke(() => PlayniteApi.Notifications.Add("GameSaveCenter.Error." + Guid.NewGuid().ToString("N"), message, NotificationType.Error));
        }

        private async Task ApplySettingsCoreAsync() => await RequestAsync<object>(MessageTypes.UpdateSettings, Settings.ToWorkerSettings());

        private async Task ExportLibraryAsync()
        {
            await EnsureWorkerAsync();
            var games = PlayniteApi.Database.Games.Select(adapter.Convert).ToList();
            await RequestAsync<object>(MessageTypes.UpsertGames, games, TimeSpan.FromMinutes(5));
        }

        private async void FireAndForget(Func<Task> operation)
        {
            try { await operation(); }
            catch (Exception ex) { ShowError(ex.Message); }
        }
    }
}

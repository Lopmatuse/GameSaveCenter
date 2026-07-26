using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GameSaveCenter.Contracts;
using Playnite.SDK;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>Apple-inspired dashboard state; all file operations remain in the Worker.</summary>
    public sealed class DashboardViewModel : ObservableObject
    {
        private readonly GameSaveCenterPlugin plugin;
        private bool isBusy;
        private string statusMessage = "准备就绪";
        private GameStatusDto selectedGame;
        private BackupVersionDto selectedBackup;
        private DashboardSnapshotDto snapshot = new DashboardSnapshotDto();

        public DashboardViewModel(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            RefreshCommand = new RelayCommand(_ => Run(RefreshAsync));
            BackupSelectedCommand = new RelayCommand(_ => Run(BackupSelectedAsync), _ => SelectedGame != null);
            BackupAllCommand = new RelayCommand(_ => Run(BackupAllAsync));
            SyncMediaCommand = new RelayCommand(_ => Run(SyncMediaAsync));
            DetectPathsCommand = new RelayCommand(_ => Run(DetectPathsAsync), _ => SelectedGame != null);
            ValidateCommand = new RelayCommand(_ => Run(ValidateAsync), _ => SelectedGame != null);
            RestoreCommand = new RelayCommand(_ => Run(RestoreAsync), _ => SelectedGame != null && SelectedBackup != null);
            UndoRestoreCommand = new RelayCommand(_ => Run(UndoRestoreAsync), _ => SelectedGame != null);
            LoadDetailsCommand = new RelayCommand(_ => Run(LoadDetailsAsync), _ => SelectedGame != null);
            Run(RefreshAsync);
        }

        public ObservableCollection<GameStatusDto> Games { get; } = new ObservableCollection<GameStatusDto>();
        public ObservableCollection<TaskStatusDto> Tasks { get; } = new ObservableCollection<TaskStatusDto>();
        public ObservableCollection<ValidationFindingDto> Findings { get; } = new ObservableCollection<ValidationFindingDto>();
        public ObservableCollection<BackupVersionDto> Backups { get; } = new ObservableCollection<BackupVersionDto>();
        public ObservableCollection<MediaItemDto> Media { get; } = new ObservableCollection<MediaItemDto>();
        public ObservableCollection<AuditLogEntryDto> Audit { get; } = new ObservableCollection<AuditLogEntryDto>();
        public ObservableCollection<SavePathCandidateDto> SaveCandidates { get; } = new ObservableCollection<SavePathCandidateDto>();

        public DashboardSnapshotDto Snapshot { get => snapshot; private set => SetValue(ref snapshot, value); }
        public bool IsBusy { get => isBusy; private set => SetValue(ref isBusy, value); }
        public string StatusMessage { get => statusMessage; private set => SetValue(ref statusMessage, value); }
        public GameStatusDto SelectedGame
        {
            get => selectedGame;
            set { SetValue(ref selectedGame, value); Run(LoadDetailsAsync); }
        }
        public BackupVersionDto SelectedBackup { get => selectedBackup; set => SetValue(ref selectedBackup, value); }

        public ICommand RefreshCommand { get; }
        public ICommand BackupSelectedCommand { get; }
        public ICommand BackupAllCommand { get; }
        public ICommand SyncMediaCommand { get; }
        public ICommand DetectPathsCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand UndoRestoreCommand { get; }
        public ICommand LoadDetailsCommand { get; }

        public async Task RefreshAsync()
        {
            StatusMessage = "正在刷新…";
            var data = await plugin.RequestAsync<DashboardSnapshotDto>(MessageTypes.GetDashboard, new { });
            ApplyOnUi(() =>
            {
                Snapshot = data;
                Replace(Games, data.Games);
                Replace(Tasks, data.RecentTasks);
                Replace(Findings, data.Findings);
                Replace(Audit, data.RecentAudit);
                StatusMessage = data.WorkerHealthy ? "Worker 正常" : "Worker 不可用";
            });
        }

        private async Task LoadDetailsAsync()
        {
            if (SelectedGame == null) return;
            var id = SelectedGame.PlayniteId;
            var backups = await plugin.RequestAsync<BackupVersionDto[]>(MessageTypes.ListBackups, new GameQueryDto { PlayniteId = id, Limit = 500 });
            var media = await plugin.RequestAsync<MediaItemDto[]>(MessageTypes.ListMedia, new GameQueryDto { PlayniteId = id, Limit = 1000 });
            ApplyOnUi(() => { Replace(Backups, backups); Replace(Media, media); SaveCandidates.Clear(); });
        }

        private async Task BackupSelectedAsync()
        {
            await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupGame, new BackupRequestDto { PlayniteIds = { SelectedGame.PlayniteId }, Force = true, Reason = "Manual" }, TimeSpan.FromMinutes(15));
            await RefreshAsync(); await LoadDetailsAsync();
        }

        private async Task BackupAllAsync()
        {
            await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupAll, new BackupRequestDto { Force = true, Reason = "ManualAll" }, TimeSpan.FromMinutes(45));
            await RefreshAsync();
        }

        private async Task SyncMediaAsync()
        {
            var ids = SelectedGame == null ? new string[0] : new[] { SelectedGame.PlayniteId };
            var request = new MediaSyncRequestDto { UploadAfterSync = plugin.Settings.EnableCloudUpload };
            foreach (var id in ids) request.PlayniteIds.Add(id);
            await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, request, TimeSpan.FromMinutes(60));
            await RefreshAsync(); if (SelectedGame != null) await LoadDetailsAsync();
        }

        private async Task DetectPathsAsync()
        {
            var candidates = await plugin.RequestAsync<SavePathCandidateDto[]>(MessageTypes.DetectSavePaths, new DetectionRequestDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(20));
            ApplyOnUi(() => Replace(SaveCandidates, candidates));
        }

        private async Task ValidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.ValidateGame, new ValidateGameRequestDto { PlayniteId = SelectedGame.PlayniteId });
            await RefreshAsync();
        }

        private async Task RestoreAsync()
        {
            var result = plugin.PlayniteApi.Dialogs.ShowMessage(
                "恢复前会先创建并锁定当前存档的 PreRestore 快照。请确认游戏、启动器和 MOD 管理器均已关闭。\n\n继续恢复选中的历史版本？",
                "GameSaveCenter 安全恢复", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            await plugin.RequestAsync<TaskStatusDto>(MessageTypes.RestoreExecute, new RestoreRequestDto
            {
                PlayniteId = SelectedGame.PlayniteId, BackupId = SelectedBackup.BackupId,
                ConfirmedCurrentSnapshot = true, ConfirmedGameClosed = true, UserComment = "Playnite restore wizard"
            }, TimeSpan.FromMinutes(30));
            await RefreshAsync(); await LoadDetailsAsync();
        }

        private async Task UndoRestoreAsync()
        {
            var result = plugin.PlayniteApi.Dialogs.ShowMessage("撤销将恢复最近的 PreRestore 快照，仍会先保存当前状态。继续？", "撤销恢复", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            await plugin.RequestAsync<TaskStatusDto>(MessageTypes.UndoRestore, new GameQueryDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(30));
            await RefreshAsync(); await LoadDetailsAsync();
        }

        private async void Run(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try { await plugin.EnsureWorkerAsync(); await action(); }
            catch (Exception ex) { StatusMessage = ex.Message; plugin.ShowError(ex.Message); }
            finally { IsBusy = false; }
        }

        private void ApplyOnUi(Action action) => plugin.PlayniteApi.MainView.UIDispatcher.Invoke(action);
        private static void Replace<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
        { target.Clear(); foreach (var item in source ?? Enumerable.Empty<T>()) target.Add(item); }
    }
}

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
        private GameStatusDto selectedGame = null!;
        private BackupVersionDto selectedBackup = null!;
        private DashboardSnapshotDto snapshot = new DashboardSnapshotDto();
        private SavePathCandidateDto selectedCandidate = null!;
        private string backupComment = string.Empty;
        private bool lockSelectedBackup;
        private string customMediaSourcePath = string.Empty;
        private string customMediaPattern = "*";
        private bool customMediaShared;
        private MediaItemDto selectedMedia = null!;
        private GameStatusDto mediaTargetGame = null!;
        private string diffSummary = string.Empty;
        private string retentionSummary = string.Empty;
        private bool suppressSelectionLoad;

        public DashboardViewModel(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            RefreshCommand = new RelayCommand(_ => Run(RefreshAsync), _ => !IsBusy);
            BackupSelectedCommand = new RelayCommand(_ => Run(BackupSelectedAsync), _ => !IsBusy && SelectedGame != null && SelectedGame.LudusaviMatched && Snapshot.LudusaviAvailable);
            BackupAllCommand = new RelayCommand(_ => Run(BackupAllAsync), _ => !IsBusy && Snapshot.LudusaviAvailable && Games.Any(x => x.LudusaviMatched));
            SyncMediaCommand = new RelayCommand(_ => Run(SyncMediaAsync), _ => !IsBusy);
            DetectPathsCommand = new RelayCommand(_ => Run(DetectPathsAsync), _ => !IsBusy && SelectedGame != null);
            ValidateCommand = new RelayCommand(_ => Run(ValidateAsync), _ => !IsBusy && SelectedGame != null && SelectedGame.LudusaviMatched);
            RestoreCommand = new RelayCommand(_ => Run(RestoreAsync), _ => !IsBusy && SelectedGame != null && SelectedBackup != null && Snapshot.LudusaviAvailable);
            UndoRestoreCommand = new RelayCommand(_ => Run(UndoRestoreAsync), _ => !IsBusy && SelectedGame != null && Backups.Any(x => x.IsPreRestore));
            LoadDetailsCommand = new RelayCommand(_ => Run(LoadDetailsAsync), _ => !IsBusy && SelectedGame != null);
            SavePolicyCommand = new RelayCommand(_ => Run(SavePolicyAsync), _ => !IsBusy && SelectedGame != null);
            UpdateBackupMetadataCommand = new RelayCommand(_ => Run(UpdateBackupMetadataAsync), _ => !IsBusy && SelectedGame != null && SelectedBackup != null);
            CompareBackupCommand = new RelayCommand(_ => Run(CompareBackupAsync), _ => !IsBusy && SelectedGame != null && SelectedBackup != null && Backups.IndexOf(SelectedBackup) >= 0 && Backups.IndexOf(SelectedBackup) + 1 < Backups.Count);
            PreviewRetentionCommand = new RelayCommand(_ => Run(PreviewRetentionAsync), _ => !IsBusy && SelectedGame != null && Backups.Count > 0);
            AddMediaSourceCommand = new RelayCommand(_ => Run(AddMediaSourceAsync), _ => !IsBusy && SelectedGame != null);
            AcceptCandidateCommand = new RelayCommand(_ => Run(AcceptCandidateAsync), _ => !IsBusy && SelectedGame != null && SelectedCandidate != null);
            ReassignMediaCommand = new RelayCommand(_ => Run(ReassignMediaAsync), _ => !IsBusy && SelectedMedia != null && MediaTargetGame != null);
            Run(RefreshAsync);
        }

        public ObservableCollection<GameStatusDto> Games { get; } = new ObservableCollection<GameStatusDto>();
        public ObservableCollection<TaskStatusDto> Tasks { get; } = new ObservableCollection<TaskStatusDto>();
        public ObservableCollection<ValidationFindingDto> Findings { get; } = new ObservableCollection<ValidationFindingDto>();
        public ObservableCollection<BackupVersionDto> Backups { get; } = new ObservableCollection<BackupVersionDto>();
        public ObservableCollection<MediaItemDto> Media { get; } = new ObservableCollection<MediaItemDto>();
        public ObservableCollection<AuditLogEntryDto> Audit { get; } = new ObservableCollection<AuditLogEntryDto>();
        public ObservableCollection<SavePathCandidateDto> SaveCandidates { get; } = new ObservableCollection<SavePathCandidateDto>();
        public ObservableCollection<MediaSourceRuleDto> MediaSources { get; } = new ObservableCollection<MediaSourceRuleDto>();

        public DashboardSnapshotDto Snapshot { get => snapshot; private set => SetValue(ref snapshot, value); }
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                SetValue(ref isBusy, value);
                RaiseCommandStates();
            }
        }
        public string StatusMessage { get => statusMessage; private set => SetValue(ref statusMessage, value); }
        public GameStatusDto SelectedGame
        {
            get => selectedGame;
            set
            {
                if (ReferenceEquals(selectedGame, value)) return;
                SetValue(ref selectedGame, value);
                RaiseCommandStates();
                if (suppressSelectionLoad) return;
                if (value != null) Run(LoadDetailsAsync);
                else ClearSelectedGameDetails();
            }
        }
        public BackupVersionDto SelectedBackup
        {
            get => selectedBackup;
            set
            {
                SetValue(ref selectedBackup, value);
                if (value != null)
                {
                    BackupComment = value.Comment;
                    LockSelectedBackup = value.IsLocked;
                }
                RaiseCommandStates();
            }
        }
        public SavePathCandidateDto SelectedCandidate
        {
            get => selectedCandidate;
            set
            {
                SetValue(ref selectedCandidate, value);
                RaiseCommandStates();
            }
        }
        public string BackupComment { get => backupComment; set => SetValue(ref backupComment,value); }
        public bool LockSelectedBackup { get => lockSelectedBackup; set => SetValue(ref lockSelectedBackup,value); }
        public string CustomMediaSourcePath { get => customMediaSourcePath; set => SetValue(ref customMediaSourcePath,value); }
        public string CustomMediaPattern { get => customMediaPattern; set => SetValue(ref customMediaPattern,value); }
        public bool CustomMediaShared { get => customMediaShared; set => SetValue(ref customMediaShared,value); }
        public MediaItemDto SelectedMedia
        {
            get => selectedMedia;
            set
            {
                SetValue(ref selectedMedia, value);
                RaiseCommandStates();
            }
        }
        public GameStatusDto MediaTargetGame
        {
            get => mediaTargetGame;
            set
            {
                SetValue(ref mediaTargetGame, value);
                RaiseCommandStates();
            }
        }
        public string DiffSummary { get => diffSummary; private set => SetValue(ref diffSummary,value); }
        public string RetentionSummary { get => retentionSummary; private set => SetValue(ref retentionSummary,value); }

        public ICommand RefreshCommand { get; }
        public ICommand BackupSelectedCommand { get; }
        public ICommand BackupAllCommand { get; }
        public ICommand SyncMediaCommand { get; }
        public ICommand DetectPathsCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand UndoRestoreCommand { get; }
        public ICommand LoadDetailsCommand { get; }
        public ICommand SavePolicyCommand { get; }
        public ICommand UpdateBackupMetadataCommand { get; }
        public ICommand CompareBackupCommand { get; }
        public ICommand PreviewRetentionCommand { get; }
        public ICommand AddMediaSourceCommand { get; }
        public ICommand AcceptCandidateCommand { get; }
        public ICommand ReassignMediaCommand { get; }

        public Task RefreshAsync() => RefreshCoreAsync(true);

        private async Task RefreshCoreAsync(bool synchronize)
        {
            StatusMessage = synchronize ? "正在同步设置、游戏库与存档状态…" : "正在刷新状态…";
            if (synchronize) await plugin.SynchronizeAsync();
            var data = await plugin.RequestAsync<DashboardSnapshotDto>(MessageTypes.GetDashboard, new { });
            ApplyOnUi(() =>
            {
                var selectedGameId = SelectedGame?.PlayniteId;
                Snapshot = data;
                suppressSelectionLoad = true;
                try
                {
                    Replace(Games, data.Games);
                    SelectedGame = Games.FirstOrDefault(x => x.PlayniteId == selectedGameId) ?? Games.FirstOrDefault();
                }
                finally { suppressSelectionLoad = false; }
                Replace(Tasks, data.RecentTasks);
                Replace(Findings, data.Findings);
                Replace(Audit, data.RecentAudit);
                StatusMessage = data.WorkerHealthy
                    ? data.LudusaviAvailable ? "Worker 与 Ludusavi 均正常" : "Worker 正常，Ludusavi 尚未配置"
                    : "Worker 不可用";
            });
            if (SelectedGame != null) await LoadDetailsAsync();
            else ClearSelectedGameDetails();
        }

        private async Task LoadDetailsAsync()
        {
            if (SelectedGame == null) return;
            var id = SelectedGame.PlayniteId;
            var backups = await plugin.RequestAsync<BackupVersionDto[]>(MessageTypes.ListBackups, new GameQueryDto { PlayniteId = id, Limit = 500 });
            var media = await plugin.RequestAsync<MediaItemDto[]>(MessageTypes.ListMedia, new GameQueryDto { PlayniteId = id, Limit = 1000 });
            var mediaSources = await plugin.RequestAsync<MediaSourceRuleDto[]>(MessageTypes.ListMediaSources, new GameQueryDto { PlayniteId = id });
            ApplyOnUi(() =>
            {
                if (SelectedGame == null || !string.Equals(SelectedGame.PlayniteId, id, StringComparison.OrdinalIgnoreCase)) return;
                Replace(Backups, backups);
                Replace(Media, media);
                Replace(MediaSources, mediaSources);
                SaveCandidates.Clear();
                SelectedBackup = Backups.FirstOrDefault();
                RaiseCommandStates();
            });
        }

        private async Task BackupSelectedAsync()
        {
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupGame, new BackupRequestDto { PlayniteIds = { SelectedGame.PlayniteId }, Force = true, Reason = "Manual" }, TimeSpan.FromMinutes(15));
            await RefreshCoreAsync(false);
            ThrowIfFailed(tasks);
        }

        private async Task BackupAllAsync()
        {
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupAll, new BackupRequestDto { Force = true, Reason = "ManualAll" }, TimeSpan.FromMinutes(45));
            await RefreshCoreAsync(false);
            ThrowIfFailed(tasks);
        }

        private async Task SyncMediaAsync()
        {
            var ids = SelectedGame == null ? new string[0] : new[] { SelectedGame.PlayniteId };
            var request = new MediaSyncRequestDto { UploadAfterSync = plugin.Settings.EnableCloudUpload };
            foreach (var id in ids) request.PlayniteIds.Add(id);
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, request, TimeSpan.FromMinutes(60));
            await RefreshCoreAsync(false);
            ThrowIfFailed(tasks);
        }

        private async Task DetectPathsAsync()
        {
            var candidates = await plugin.RequestAsync<SavePathCandidateDto[]>(MessageTypes.DetectSavePaths, new DetectionRequestDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(20));
            ApplyOnUi(() => Replace(SaveCandidates, candidates));
        }

        private async Task ValidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.ValidateGame, new ValidateGameRequestDto { PlayniteId = SelectedGame.PlayniteId });
            await RefreshCoreAsync(false);
        }

        private async Task SavePolicyAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.UpdateGamePolicy,new GamePolicyUpdateDto{PlayniteId=SelectedGame.PlayniteId,Policy=SelectedGame.Policy});
            StatusMessage="游戏策略已保存";
        }

        private async Task UpdateBackupMetadataAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.UpdateBackupMetadata,new BackupMetadataUpdateDto{PlayniteId=SelectedGame.PlayniteId,BackupId=SelectedBackup.BackupId,Comment=BackupComment,Locked=LockSelectedBackup});
            await LoadDetailsAsync();
        }

        private async Task CompareBackupAsync()
        {
            var index=Backups.IndexOf(SelectedBackup);
            if(index<0||index+1>=Backups.Count){DiffSummary="没有可比较的上一个版本。";return;}
            var diff=await plugin.RequestAsync<BackupDiffDto>(MessageTypes.CompareBackups,new BackupCompareRequestDto{PlayniteId=SelectedGame.PlayniteId,LeftBackupId=Backups[index+1].BackupId,RightBackupId=SelectedBackup.BackupId});
            DiffSummary=diff.Summary;
        }

        private async Task PreviewRetentionAsync()
        {
            var preview=await plugin.RequestAsync<RetentionPreviewDto>(MessageTypes.PreviewRetention,new GameQueryDto{PlayniteId=SelectedGame.PlayniteId});
            RetentionSummary=preview.Summary;
        }

        private async Task AddMediaSourceAsync()
        {
            if(string.IsNullOrWhiteSpace(CustomMediaSourcePath))throw new InvalidOperationException("请输入截图或录像目录。");
            await plugin.RequestAsync<MediaSourceRuleDto>(MessageTypes.AddMediaSource,new MediaSourceRuleDto{PlayniteId=SelectedGame.PlayniteId,RootPath=CustomMediaSourcePath,IncludePattern=string.IsNullOrWhiteSpace(CustomMediaPattern)?"*":CustomMediaPattern,SharedDirectory=CustomMediaShared,SourceKind=MediaSourceKind.Custom});
            StatusMessage="自定义媒体来源已添加";
            await LoadDetailsAsync();
        }

        private async Task AcceptCandidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.AcceptSavePath,new AcceptSavePathRequestDto{PlayniteId=SelectedGame.PlayniteId,Path=SelectedCandidate.Path,IncludeSubdirectories=true});
            SelectedCandidate.Status="Accepted";StatusMessage="已生成 Ludusavi 自定义规则草案";
        }

        private async Task ReassignMediaAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.ReassignMedia,new ReassignMediaRequestDto{MediaId=SelectedMedia.MediaId,TargetPlayniteId=MediaTargetGame.PlayniteId});
            StatusMessage=$"媒体已重新归类到 {MediaTargetGame.Name}";
            await LoadDetailsAsync();
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
            await RefreshCoreAsync(false);
        }

        private async Task UndoRestoreAsync()
        {
            var result = plugin.PlayniteApi.Dialogs.ShowMessage("撤销将恢复最近的 PreRestore 快照，仍会先保存当前状态。继续？", "撤销恢复", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            await plugin.RequestAsync<TaskStatusDto>(MessageTypes.UndoRestore, new GameQueryDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(30));
            await RefreshCoreAsync(false);
        }

        private async void Run(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try { await plugin.EnsureWorkerAsync(); await action(); }
            catch (Exception ex) { StatusMessage = ex.Message; plugin.ShowError(ex.Message); }
            finally { IsBusy = false; }
        }

        private static void ThrowIfFailed(System.Collections.Generic.IEnumerable<TaskStatusDto> tasks)
        {
            var failed = tasks?.FirstOrDefault(x => x.State == TaskState.Failed);
            if (failed != null) throw new InvalidOperationException(failed.DetailMessage);
        }

        private void ClearSelectedGameDetails()
        {
            ApplyOnUi(() =>
            {
                Backups.Clear();
                Media.Clear();
                MediaSources.Clear();
                SaveCandidates.Clear();
                SelectedBackup = null!;
                SelectedCandidate = null!;
                SelectedMedia = null!;
            });
        }

        private void RaiseCommandStates()
        {
            foreach (var command in new[]
            {
                RefreshCommand, BackupSelectedCommand, BackupAllCommand, SyncMediaCommand,
                DetectPathsCommand, ValidateCommand, RestoreCommand,
                UndoRestoreCommand, LoadDetailsCommand, SavePolicyCommand,
                UpdateBackupMetadataCommand, CompareBackupCommand, PreviewRetentionCommand,
                AddMediaSourceCommand, AcceptCandidateCommand, ReassignMediaCommand
            }.OfType<RelayCommand>())
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private void ApplyOnUi(Action action) => plugin.PlayniteApi.MainView.UIDispatcher.Invoke(action);
        private static void Replace<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
        { target.Clear(); foreach (var item in source ?? Enumerable.Empty<T>()) target.Add(item); }
    }
}

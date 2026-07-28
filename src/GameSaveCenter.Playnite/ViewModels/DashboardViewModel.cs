using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using Microsoft.Win32;
using GameSaveCenter.Contracts;
using Playnite.SDK;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>Apple-inspired dashboard state; all file operations remain in the Worker.</summary>
    public sealed class DashboardViewModel : ObservableObject
    {
        private readonly GameSaveCenterPlugin plugin;
        private readonly Dictionary<string, TaskState> knownTaskStates = new Dictionary<string, TaskState>(StringComparer.OrdinalIgnoreCase);
        private readonly DateTime dashboardOpenedUtc = DateTime.UtcNow;
        private bool isBusy;
        private bool isBackgroundRefreshing;
        private bool isCancellingTask;
        private bool taskSnapshotInitialized;
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
        private MediaItemDto selectedInboxMedia = null!;
        private GameStatusDto inboxTargetGame = null!;
        private TaskStatusDto selectedTask = null!;
        private WorkerSettingsSnapshotDto effectiveSettings = new WorkerSettingsSnapshotDto();
        private string diagnosticSummary = "诊断信息尚未加载。";
        private string diffSummary = string.Empty;
        private string retentionSummary = string.Empty;
        private bool suppressSelectionLoad;
        private string gameSearchText = string.Empty;
        private string gameStatusFilter = "全部";
        private string gameSortMode = "名称";
        private int filteredGameCount;
        private WorkspaceKind currentWorkspace = WorkspaceKind.Overview;
        private LayoutMode layoutMode = LayoutMode.Standard;
        private GameToolDto selectedGameTool = null!;
        private TrainerCatalogItemDto selectedTrainerCatalogItem = null!;
        private TrainerReleaseDto selectedTrainerRelease = null!;
        private string trainerSearchText = string.Empty;
        private bool showTrainerLibrary;

        public DashboardViewModel(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            GamesView = CollectionViewSource.GetDefaultView(Games);
            GamesView.Filter = FilterGame;
            ApplyGameSort();
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
            AcceptCandidateCommand = new RelayCommand(_ => Run(AcceptCandidateAsync), _ => !IsBusy && SelectedGame != null && SelectedCandidate != null && !string.Equals(SelectedCandidate.Status, "Accepted", StringComparison.OrdinalIgnoreCase));
            RejectCandidateCommand = new RelayCommand(_ => Run(RejectCandidateAsync), _ => !IsBusy && SelectedGame != null && SelectedCandidate != null && !string.Equals(SelectedCandidate.Status, "Accepted", StringComparison.OrdinalIgnoreCase));
            ReassignMediaCommand = new RelayCommand(_ => Run(ReassignMediaAsync), _ => !IsBusy && SelectedMedia != null && MediaTargetGame != null);
            AssignInboxMediaCommand = new RelayCommand(_ => Run(AssignInboxMediaAsync), _ => !IsBusy && SelectedInboxMedia != null && InboxTargetGame != null);
            IgnoreInboxMediaCommand = new RelayCommand(_ => Run(IgnoreInboxMediaAsync), _ => !IsBusy && SelectedInboxMedia != null);
            CancelTaskCommand = new RelayCommand(_ => CancelSelectedTask(), _ => SelectedTask != null && SelectedTask.CanCancel && !IsCancellingTask);
            RetryTaskCommand = new RelayCommand(_ => Run(RetrySelectedTaskAsync), _ => !IsBusy && CanRetrySelectedTask());
            CopyTaskErrorCommand = new RelayCommand(_ => RunLocal(CopySelectedTaskError), _ => SelectedTask != null && !string.IsNullOrWhiteSpace(SelectedTask.DetailMessage));
            RefreshDiagnosticsCommand = new RelayCommand(_ => Run(RefreshDiagnosticsAsync), _ => !IsBusy);
            CopyDiagnosticsCommand = new RelayCommand(_ => RunLocal(CopyDiagnostics), _ => !string.IsNullOrWhiteSpace(DiagnosticSummary));
            OpenDataDirectoryCommand = new RelayCommand(_ => RunLocal(() => OpenPath(EffectiveSettings.DataDirectory)), _ => !string.IsNullOrWhiteSpace(EffectiveSettings.DataDirectory));
            OpenBackupDirectoryCommand = new RelayCommand(_ => RunLocal(() => OpenPath(EffectiveSettings.LudusaviBackupDirectory)), _ => !string.IsNullOrWhiteSpace(EffectiveSettings.LudusaviBackupDirectory));
            OpenMediaDirectoryCommand = new RelayCommand(_ => RunLocal(() => OpenPath(EffectiveSettings.MediaArchiveDirectory)), _ => !string.IsNullOrWhiteSpace(EffectiveSettings.MediaArchiveDirectory));
            OpenWorkerLogCommand = new RelayCommand(_ => RunLocal(OpenWorkerLog));
            ImportTrainerCommand = new RelayCommand(_ => Run(() => ImportGameToolAsync(GameToolType.Trainer)), _ => !IsBusy && SelectedGame != null);
            ImportCheatTableCommand = new RelayCommand(_ => Run(() => ImportGameToolAsync(GameToolType.CheatTable)), _ => !IsBusy && SelectedGame != null);
            ImportToolFolderCommand = new RelayCommand(_ => Run(ImportGameToolFolderAsync), _ => !IsBusy && SelectedGame != null);
            SaveGameToolCommand = new RelayCommand(_ => Run(SaveSelectedGameToolAsync), _ => !IsBusy && SelectedGameTool != null);
            LaunchGameToolCommand = new RelayCommand(_ => Run(LaunchSelectedGameToolAsync), _ => !IsBusy && SelectedGameTool != null && SelectedGameTool.ActiveVersion.IsAvailable);
            OpenGameToolDirectoryCommand = new RelayCommand(_ => Run(OpenSelectedGameToolDirectoryAsync), _ => !IsBusy && SelectedGameTool != null);
            DeleteGameToolCommand = new RelayCommand(_ => Run(DeleteSelectedGameToolAsync), _ => !IsBusy && SelectedGameTool != null);
            SyncTrainerCatalogCommand = new RelayCommand(_ => Run(SyncTrainerCatalogAsync), _ => !IsBusy);
            SearchTrainerCatalogCommand = new RelayCommand(_ => Run(SearchTrainerCatalogAsync), _ => !IsBusy);
            LoadTrainerReleasesCommand = new RelayCommand(_ => Run(LoadTrainerReleasesAsync), _ => !IsBusy && SelectedTrainerCatalogItem != null);
            DownloadTrainerCommand = new RelayCommand(_ => Run(DownloadTrainerAsync), _ => !IsBusy && SelectedGame != null && SelectedTrainerRelease != null);
            Run(RefreshAsync);
        }

        public ObservableCollection<GameStatusDto> Games { get; } = new ObservableCollection<GameStatusDto>();
        public ObservableCollection<TaskStatusDto> Tasks { get; } = new ObservableCollection<TaskStatusDto>();
        public ObservableCollection<ValidationFindingDto> Findings { get; } = new ObservableCollection<ValidationFindingDto>();
        public ObservableCollection<BackupVersionDto> Backups { get; } = new ObservableCollection<BackupVersionDto>();
        public ObservableCollection<MediaItemDto> Media { get; } = new ObservableCollection<MediaItemDto>();
        public ObservableCollection<MediaItemDto> UnassignedMedia { get; } = new ObservableCollection<MediaItemDto>();
        public ObservableCollection<AuditLogEntryDto> Audit { get; } = new ObservableCollection<AuditLogEntryDto>();
        public ObservableCollection<SavePathCandidateDto> SaveCandidates { get; } = new ObservableCollection<SavePathCandidateDto>();
        public ObservableCollection<MediaSourceRuleDto> MediaSources { get; } = new ObservableCollection<MediaSourceRuleDto>();
        public ObservableCollection<GameToolDto> GameTools { get; } = new ObservableCollection<GameToolDto>();
        public ObservableCollection<TrainerCatalogItemDto> TrainerCatalogResults { get; } = new ObservableCollection<TrainerCatalogItemDto>();
        public ObservableCollection<TrainerReleaseDto> TrainerReleases { get; } = new ObservableCollection<TrainerReleaseDto>();
        public ICollectionView GamesView { get; }
        public IReadOnlyList<string> GameStatusFilterOptions { get; } = new[] { "全部", "已就绪", "未匹配", "运行中", "需关注", "有历史" };
        public IReadOnlyList<string> GameSortOptions { get; } = new[] { "名称", "运行优先", "匹配优先", "最近备份" };

        public DashboardSnapshotDto Snapshot { get => snapshot; private set => SetValue(ref snapshot, value); }
        public WorkerSettingsSnapshotDto EffectiveSettings
        {
            get => effectiveSettings;
            private set
            {
                SetValue(ref effectiveSettings, value);
                RaiseCommandStates();
            }
        }
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                SetValue(ref isBusy, value);
                RaiseCommandStates();
            }
        }
        public bool IsBackgroundRefreshing { get => isBackgroundRefreshing; private set => SetValue(ref isBackgroundRefreshing, value); }
        public bool IsCancellingTask
        {
            get => isCancellingTask;
            private set
            {
                SetValue(ref isCancellingTask, value);
                RaiseCommandStates();
            }
        }
        public string StatusMessage { get => statusMessage; private set => SetValue(ref statusMessage, value); }
        public string DiagnosticSummary { get => diagnosticSummary; private set => SetValue(ref diagnosticSummary, value); }
        public string GameSearchText
        {
            get => gameSearchText;
            set
            {
                SetValue(ref gameSearchText, value ?? string.Empty);
                RefreshGameView();
            }
        }
        public string GameStatusFilter
        {
            get => gameStatusFilter;
            set
            {
                SetValue(ref gameStatusFilter, string.IsNullOrWhiteSpace(value) ? "全部" : value);
                RefreshGameView();
            }
        }
        public string GameSortMode
        {
            get => gameSortMode;
            set
            {
                SetValue(ref gameSortMode, string.IsNullOrWhiteSpace(value) ? "名称" : value);
                ApplyGameSort();
                RefreshGameView();
            }
        }
        public int FilteredGameCount { get => filteredGameCount; private set => SetValue(ref filteredGameCount, value); }
        public WorkspaceKind CurrentWorkspace { get => currentWorkspace; set => SetValue(ref currentWorkspace, value); }
        public LayoutMode LayoutMode { get => layoutMode; set => SetValue(ref layoutMode, value); }
        public bool ShowTrainerLibrary { get => showTrainerLibrary; set => SetValue(ref showTrainerLibrary, value); }
        public string TrainerSearchText { get => trainerSearchText; set => SetValue(ref trainerSearchText, value ?? string.Empty); }
        public GameToolDto SelectedGameTool
        {
            get => selectedGameTool;
            set { SetValue(ref selectedGameTool,value); RaiseCommandStates(); }
        }
        public TrainerCatalogItemDto SelectedTrainerCatalogItem
        {
            get => selectedTrainerCatalogItem;
            set { SetValue(ref selectedTrainerCatalogItem,value); TrainerReleases.Clear(); SelectedTrainerRelease=null!; RaiseCommandStates(); }
        }
        public TrainerReleaseDto SelectedTrainerRelease
        {
            get => selectedTrainerRelease;
            set { SetValue(ref selectedTrainerRelease,value); RaiseCommandStates(); }
        }
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
        public TaskStatusDto SelectedTask
        {
            get => selectedTask;
            set
            {
                SetValue(ref selectedTask, value);
                RaiseCommandStates();
            }
        }
        public string BackupComment { get => backupComment; set => SetValue(ref backupComment, value); }
        public bool LockSelectedBackup { get => lockSelectedBackup; set => SetValue(ref lockSelectedBackup, value); }
        public string CustomMediaSourcePath { get => customMediaSourcePath; set => SetValue(ref customMediaSourcePath, value); }
        public string CustomMediaPattern { get => customMediaPattern; set => SetValue(ref customMediaPattern, value); }
        public bool CustomMediaShared { get => customMediaShared; set => SetValue(ref customMediaShared, value); }
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
        public MediaItemDto SelectedInboxMedia
        {
            get => selectedInboxMedia;
            set
            {
                SetValue(ref selectedInboxMedia, value);
                RaiseCommandStates();
            }
        }
        public GameStatusDto InboxTargetGame
        {
            get => inboxTargetGame;
            set
            {
                SetValue(ref inboxTargetGame, value);
                RaiseCommandStates();
            }
        }
        public string DiffSummary { get => diffSummary; private set => SetValue(ref diffSummary, value); }
        public string RetentionSummary { get => retentionSummary; private set => SetValue(ref retentionSummary, value); }

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
        public ICommand RejectCandidateCommand { get; }
        public ICommand ReassignMediaCommand { get; }
        public ICommand AssignInboxMediaCommand { get; }
        public ICommand IgnoreInboxMediaCommand { get; }
        public ICommand CancelTaskCommand { get; }
        public ICommand RetryTaskCommand { get; }
        public ICommand CopyTaskErrorCommand { get; }
        public ICommand RefreshDiagnosticsCommand { get; }
        public ICommand CopyDiagnosticsCommand { get; }
        public ICommand OpenDataDirectoryCommand { get; }
        public ICommand OpenBackupDirectoryCommand { get; }
        public ICommand OpenMediaDirectoryCommand { get; }
        public ICommand OpenWorkerLogCommand { get; }
        public ICommand ImportTrainerCommand { get; }
        public ICommand ImportCheatTableCommand { get; }
        public ICommand ImportToolFolderCommand { get; }
        public ICommand SaveGameToolCommand { get; }
        public ICommand LaunchGameToolCommand { get; }
        public ICommand OpenGameToolDirectoryCommand { get; }
        public ICommand DeleteGameToolCommand { get; }
        public ICommand SyncTrainerCatalogCommand { get; }
        public ICommand SearchTrainerCatalogCommand { get; }
        public ICommand LoadTrainerReleasesCommand { get; }
        public ICommand DownloadTrainerCommand { get; }

        public Task RefreshAsync() => RefreshCoreAsync(true);

        /// <summary>Lightweight polling entry used by the view timer. It remains active while a manual task is running.</summary>
        public async void RequestBackgroundRefresh()
        {
            if (!plugin.Settings.EnableDashboardAutoRefresh || IsBackgroundRefreshing) return;
            IsBackgroundRefreshing = true;
            try
            {
                await plugin.EnsureWorkerAsync();
                var refreshDetails = await RefreshDashboardAsync(false, true);
                await LoadInboxAsync();
                if (refreshDetails && !IsBusy && SelectedGame != null) await LoadDetailsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "自动刷新暂不可用：" + ex.Message;
            }
            finally
            {
                IsBackgroundRefreshing = false;
            }
        }

        private async Task RefreshCoreAsync(bool synchronize)
        {
            StatusMessage = synchronize ? "正在同步设置、游戏库与存档状态…" : "正在刷新状态…";
            await RefreshDashboardAsync(synchronize, false);
            await LoadInboxAsync();
            await LoadDiagnosticsAsync();
            if (SelectedGame != null) await LoadDetailsAsync();
            else ClearSelectedGameDetails();
        }

        private async Task<bool> RefreshDashboardAsync(bool synchronize, bool notifyTaskChanges)
        {
            if (synchronize) await plugin.SynchronizeAsync();
            var data = await plugin.RequestAsync<DashboardSnapshotDto>(MessageTypes.GetDashboard, new { });
            var notifications = new List<TaskStatusDto>();
            var selectedTaskCompleted = false;
            ApplyOnUi(() =>
            {
                var selectedGameId = SelectedGame?.PlayniteId;
                var selectedTaskId = SelectedTask?.TaskId;
                var mediaTargetId = MediaTargetGame?.PlayniteId;
                if (taskSnapshotInitialized)
                {
                    foreach (var task in data.RecentTasks)
                    {
                        var changed = !knownTaskStates.TryGetValue(task.TaskId, out var oldState) || oldState != task.State;
                        var terminal = task.State == TaskState.Succeeded || task.State == TaskState.Failed || task.State == TaskState.Cancelled;
                        if (notifyTaskChanges && !IsBusy && changed && terminal && task.CreatedUtc >= dashboardOpenedUtc.AddSeconds(-5))
                            notifications.Add(task);
                        if (changed && terminal && !string.IsNullOrWhiteSpace(selectedGameId) && string.Equals(task.GameId, selectedGameId, StringComparison.OrdinalIgnoreCase))
                            selectedTaskCompleted = true;
                    }
                }
                knownTaskStates.Clear();
                foreach (var task in data.RecentTasks) knownTaskStates[task.TaskId] = task.State;
                taskSnapshotInitialized = true;

                Snapshot = data;
                suppressSelectionLoad = true;
                try
                {
                    Replace(Games, data.Games);
                    RefreshGameView(false);
                    SelectedGame = Games.FirstOrDefault(x => x.PlayniteId == selectedGameId && GamesView.Contains(x))
                        ?? GamesView.Cast<GameStatusDto>().FirstOrDefault();
                    MediaTargetGame = Games.FirstOrDefault(x => string.Equals(x.PlayniteId, mediaTargetId, StringComparison.OrdinalIgnoreCase))
                                      ?? SelectedGame
                                      ?? Games.FirstOrDefault();
                }
                finally { suppressSelectionLoad = false; }
                Replace(Tasks, data.RecentTasks);
                SelectedTask = Tasks.FirstOrDefault(x => x.TaskId == selectedTaskId) ?? Tasks.FirstOrDefault();
                Replace(Findings, data.Findings);
                Replace(Audit, data.RecentAudit);
                StatusMessage = data.WorkerHealthy
                    ? data.LudusaviAvailable ? "Worker 与 Ludusavi 均正常" : "Worker 正常，Ludusavi 尚未配置"
                    : "Worker 不可用";
            });
            foreach (var task in notifications) plugin.ShowTaskNotification(task);
            return selectedTaskCompleted;
        }

        private async Task LoadDiagnosticsAsync()
        {
            var settings = await plugin.RequestAsync<WorkerSettingsSnapshotDto>(MessageTypes.GetSettings, new { });
            ApplyOnUi(() =>
            {
                EffectiveSettings = settings;
                DiagnosticSummary = BuildDiagnosticSummary(settings);
            });
        }

        private async Task RefreshDiagnosticsAsync()
        {
            await RefreshDashboardAsync(false, false);
            await LoadDiagnosticsAsync();
            StatusMessage = "诊断信息已更新";
        }

        private async Task LoadDetailsAsync()
        {
            if (SelectedGame == null) return;
            var id = SelectedGame.PlayniteId;
            var backups = await plugin.RequestAsync<BackupVersionDto[]>(MessageTypes.ListBackups, new GameQueryDto { PlayniteId = id, Limit = 500 });
            var media = await plugin.RequestAsync<MediaItemDto[]>(MessageTypes.ListMedia, new GameQueryDto { PlayniteId = id, Limit = 1000 });
            var mediaSources = await plugin.RequestAsync<MediaSourceRuleDto[]>(MessageTypes.ListMediaSources, new GameQueryDto { PlayniteId = id });
            var saveCandidates = await plugin.RequestAsync<SavePathCandidateDto[]>(MessageTypes.ListSaveCandidates, new GameQueryDto { PlayniteId = id });
            var gameTools = await plugin.RequestAsync<GameToolDto[]>(MessageTypes.ListGameTools, new GameQueryDto { PlayniteId = id });
            ApplyOnUi(() =>
            {
                if (SelectedGame == null || !string.Equals(SelectedGame.PlayniteId, id, StringComparison.OrdinalIgnoreCase)) return;
                Replace(Backups, backups);
                Replace(Media, media);
                Replace(MediaSources, mediaSources);
                Replace(SaveCandidates, saveCandidates);
                var selectedToolId=SelectedGameTool?.ToolId;
                Replace(GameTools,gameTools);
                SelectedGameTool=GameTools.FirstOrDefault(x=>string.Equals(x.ToolId,selectedToolId,StringComparison.OrdinalIgnoreCase))
                                 ??GameTools.FirstOrDefault();
                SelectedBackup = Backups.FirstOrDefault();
                SelectedCandidate = SaveCandidates.FirstOrDefault(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                                    ?? SaveCandidates.FirstOrDefault();
                MediaTargetGame = Games.FirstOrDefault(x => string.Equals(x.PlayniteId, MediaTargetGame?.PlayniteId, StringComparison.OrdinalIgnoreCase))
                                  ?? SelectedGame
                                  ?? Games.FirstOrDefault();
                RaiseCommandStates();
            });
        }

        private async Task LoadInboxAsync()
        {
            var selectedId = SelectedInboxMedia?.MediaId;
            var targetId = InboxTargetGame?.PlayniteId;
            var inbox = await plugin.RequestAsync<MediaItemDto[]>(MessageTypes.ListUnassignedMedia, new GameQueryDto { Limit = 500 });
            ApplyOnUi(() =>
            {
                Replace(UnassignedMedia, inbox);
                SelectedInboxMedia = UnassignedMedia.FirstOrDefault(x => string.Equals(x.MediaId, selectedId, StringComparison.OrdinalIgnoreCase))
                                     ?? UnassignedMedia.FirstOrDefault();
                InboxTargetGame = Games.FirstOrDefault(x => string.Equals(x.PlayniteId, targetId, StringComparison.OrdinalIgnoreCase))
                                  ?? SelectedGame
                                  ?? Games.FirstOrDefault();
                RaiseCommandStates();
            });
        }

        private async Task BackupSelectedAsync()
        {
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupGame, new BackupRequestDto { PlayniteIds = { SelectedGame.PlayniteId }, Force = true, Reason = "Manual" }, TimeSpan.FromMinutes(15));
            ThrowIfUnsuccessful(tasks);
            await RefreshCoreAsync(false);
            await LoadDetailsAsync();
            StatusMessage = Backups.Count > 0
                ? $"备份完成，已读取 {Backups.Count} 个历史版本"
                : "备份完成，但历史索引仍为空；请打开诊断页查看 Ludusavi 输出。";
            if (plugin.Settings.EnableTaskNotifications) plugin.ShowInfo($"{SelectedGame.Name} 的存档备份已完成");
        }

        private async Task ImportGameToolAsync(GameToolType type)
        {
            var dialog=new OpenFileDialog
            {
                Title=type==GameToolType.CheatTable?"导入 Cheat Table":"导入修改器（EXE 或 ZIP）",
                Filter=type==GameToolType.CheatTable?"Cheat Engine Table (*.ct)|*.ct|所有文件 (*.*)|*.*":"修改器 (*.exe;*.zip)|*.exe;*.zip|所有文件 (*.*)|*.*",
                Multiselect=false,CheckFileExists=true
            };
            if(dialog.ShowDialog()!=true)return;
            var imported=await plugin.RequestAsync<GameToolDto>(MessageTypes.ImportGameTool,new ImportGameToolRequestDto
            {
                PlayniteId=SelectedGame.PlayniteId,ToolType=type,SourcePath=dialog.FileName,CopyIntoLibrary=true
            },TimeSpan.FromMinutes(5));
            await LoadDetailsAsync();
            SelectedGameTool=GameTools.FirstOrDefault(x=>x.ToolId==imported.ToolId)??GameTools.FirstOrDefault();
            StatusMessage=type==GameToolType.CheatTable?"Cheat Table 已导入，自动启动保持关闭":"修改器已导入，自动启动保持关闭";
        }

        private async Task ImportGameToolFolderAsync()
        {
            var folder=plugin.PlayniteApi.Dialogs.SelectFolder();
            if(string.IsNullOrWhiteSpace(folder))return;
            var imported=await plugin.RequestAsync<GameToolDto>(MessageTypes.ImportGameTool,new ImportGameToolRequestDto
            {
                PlayniteId=SelectedGame.PlayniteId,ToolType=GameToolType.Trainer,SourcePath=folder,CopyIntoLibrary=true
            },TimeSpan.FromMinutes(5));
            await LoadDetailsAsync();SelectedGameTool=GameTools.FirstOrDefault(x=>x.ToolId==imported.ToolId)??GameTools.FirstOrDefault();
            StatusMessage="修改器目录已导入，自动启动保持关闭";
        }

        private async Task SaveSelectedGameToolAsync()
        {
            var tool=SelectedGameTool;
            await plugin.RequestAsync<object>(MessageTypes.UpdateGameTool,new UpdateGameToolRequestDto
            {
                ToolId=tool.ToolId,Enabled=tool.Enabled,AutoStart=tool.AutoStart,LaunchTiming=tool.LaunchTiming,
                LaunchDelaySeconds=Math.Max(0,Math.Min(300,tool.LaunchDelaySeconds)),CloseOnGameExit=tool.CloseOnGameExit,
                RequiresAdmin=tool.RequiresAdmin,ActiveVersionId=tool.ActiveVersionId
            });
            await LoadDetailsAsync();StatusMessage="游戏工具设置已保存";
        }

        private async Task LaunchSelectedGameToolAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.LaunchGameTool,new GameToolCommandRequestDto{ToolId=SelectedGameTool.ToolId});
            StatusMessage="已启动 "+SelectedGameTool.DisplayName;
        }

        private async Task OpenSelectedGameToolDirectoryAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.OpenGameToolDirectory,new GameToolCommandRequestDto{ToolId=SelectedGameTool.ToolId});
        }

        private async Task DeleteSelectedGameToolAsync()
        {
            var name=SelectedGameTool.DisplayName;
            await plugin.RequestAsync<object>(MessageTypes.DeleteGameTool,new GameToolCommandRequestDto{ToolId=SelectedGameTool.ToolId});
            await LoadDetailsAsync();StatusMessage="已解除绑定并保留文件："+name;
        }

        private async Task SyncTrainerCatalogAsync()
        {
            var result=await plugin.RequestAsync<TrainerCatalogSyncResultDto>(MessageTypes.SyncTrainerCatalog,new{},TimeSpan.FromMinutes(2));
            StatusMessage=result.Message;
            if(!string.IsNullOrWhiteSpace(TrainerSearchText))await SearchTrainerCatalogAsync();
        }

        private async Task SearchTrainerCatalogAsync()
        {
            var query=string.IsNullOrWhiteSpace(TrainerSearchText)?SelectedGame?.Name??string.Empty:TrainerSearchText.Trim();
            var results=await plugin.RequestAsync<TrainerCatalogItemDto[]>(MessageTypes.SearchTrainerCatalog,new TrainerCatalogQueryDto{Query=query,Limit=60},TimeSpan.FromMinutes(2));
            ApplyOnUi(()=>
            {
                Replace(TrainerCatalogResults,results);
                SelectedTrainerCatalogItem=TrainerCatalogResults.FirstOrDefault();
                StatusMessage=results.Length==0?"没有找到匹配的 FLiNG 修改器":"找到 "+results.Length+" 个 FLiNG 结果";
            });
            // A search result is only useful when its downloadable releases are immediately visible.
            // Keep the explicit button for retrying a failed release lookup, but load the first result
            // automatically and load again whenever the user selects another catalogue entry in the view.
            if (results.Length > 0) await LoadTrainerReleasesAsync();
        }

        private async Task LoadTrainerReleasesAsync()
        {
            var releases=await plugin.RequestAsync<TrainerReleaseDto[]>(MessageTypes.GetTrainerReleases,
                new TrainerCatalogQueryDto{CatalogId=SelectedTrainerCatalogItem.CatalogId},TimeSpan.FromMinutes(2));
            ApplyOnUi(()=>
            {
                Replace(TrainerReleases,releases);SelectedTrainerRelease=TrainerReleases.FirstOrDefault();
                StatusMessage=releases.Length==0?"没有可下载版本":"已加载 "+releases.Length+" 个版本";
            });
        }

        private async Task DownloadTrainerAsync()
        {
            var task=await plugin.RequestAsync<TaskStatusDto>(MessageTypes.DownloadTrainer,new DownloadTrainerRequestDto
            {PlayniteId=SelectedGame.PlayniteId,CatalogId=SelectedTrainerCatalogItem.CatalogId,ReleaseId=SelectedTrainerRelease.ReleaseId},TimeSpan.FromMinutes(10));
            ThrowIfUnsuccessful(new[]{task});await LoadDetailsAsync();ShowTrainerLibrary=false;
            StatusMessage="FLiNG 修改器已下载并绑定，自动启动保持关闭";
        }

        private async Task BackupAllAsync()
        {
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupAll, new BackupRequestDto { Force = true, Reason = "ManualAll" }, TimeSpan.FromMinutes(45));
            await RefreshCoreAsync(false);
            ThrowIfUnsuccessful(tasks);
            if (plugin.Settings.EnableTaskNotifications) plugin.ShowInfo("全部匹配游戏的备份任务已完成");
        }

        private async Task SyncMediaAsync()
        {
            var ids = SelectedGame == null ? new string[0] : new[] { SelectedGame.PlayniteId };
            var request = new MediaSyncRequestDto { UploadAfterSync = plugin.Settings.EnableCloudUpload };
            foreach (var id in ids) request.PlayniteIds.Add(id);
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, request, TimeSpan.FromMinutes(60));
            await RefreshCoreAsync(false);
            ThrowIfUnsuccessful(tasks);
            if (plugin.Settings.EnableTaskNotifications)
                plugin.ShowInfo(SelectedGame == null ? "媒体同步已完成" : $"{SelectedGame.Name} 的媒体同步已完成");
        }

        private async Task DetectPathsAsync()
        {
            var candidates = await plugin.RequestAsync<SavePathCandidateDto[]>(MessageTypes.DetectSavePaths, new DetectionRequestDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(20));
            StatusMessage = candidates.Length == 0 ? "未发现新的高可信存档路径候选" : $"发现 {candidates.Length} 个高可信存档路径候选";
            await LoadDetailsAsync();
        }

        private async Task ValidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.ValidateGame, new ValidateGameRequestDto { PlayniteId = SelectedGame.PlayniteId });
            await RefreshCoreAsync(false);
        }

        private async Task SavePolicyAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.UpdateGamePolicy, new GamePolicyUpdateDto { PlayniteId = SelectedGame.PlayniteId, Policy = SelectedGame.Policy });
            StatusMessage = "游戏策略已保存";
        }

        private async Task UpdateBackupMetadataAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.UpdateBackupMetadata, new BackupMetadataUpdateDto { PlayniteId = SelectedGame.PlayniteId, BackupId = SelectedBackup.BackupId, Comment = BackupComment, Locked = LockSelectedBackup });
            await LoadDetailsAsync();
        }

        private async Task CompareBackupAsync()
        {
            var index = Backups.IndexOf(SelectedBackup);
            if (index < 0 || index + 1 >= Backups.Count) { DiffSummary = "没有可比较的上一个版本。"; return; }
            var diff = await plugin.RequestAsync<BackupDiffDto>(MessageTypes.CompareBackups, new BackupCompareRequestDto { PlayniteId = SelectedGame.PlayniteId, LeftBackupId = Backups[index + 1].BackupId, RightBackupId = SelectedBackup.BackupId });
            DiffSummary = diff.Summary;
        }

        private async Task PreviewRetentionAsync()
        {
            var preview = await plugin.RequestAsync<RetentionPreviewDto>(MessageTypes.PreviewRetention, new GameQueryDto { PlayniteId = SelectedGame.PlayniteId });
            RetentionSummary = preview.Summary;
        }

        private async Task AddMediaSourceAsync()
        {
            if (string.IsNullOrWhiteSpace(CustomMediaSourcePath)) throw new InvalidOperationException("请输入截图或录像目录。");
            await plugin.RequestAsync<MediaSourceRuleDto>(MessageTypes.AddMediaSource, new MediaSourceRuleDto { PlayniteId = CustomMediaShared ? string.Empty : SelectedGame.PlayniteId, RootPath = CustomMediaSourcePath, IncludePattern = string.IsNullOrWhiteSpace(CustomMediaPattern) ? "*" : CustomMediaPattern, SharedDirectory = CustomMediaShared, SourceKind = MediaSourceKind.Custom });
            StatusMessage = "自定义媒体来源已添加";
            await LoadDetailsAsync();
        }

        private async Task AcceptCandidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.AcceptSavePath, new AcceptSavePathRequestDto { PlayniteId = SelectedGame.PlayniteId, Path = SelectedCandidate.Path, IncludeSubdirectories = true });
            StatusMessage = "已生成 Ludusavi 自定义规则草案";
            await LoadDetailsAsync();
        }

        private async Task RejectCandidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.RejectSavePath, new AcceptSavePathRequestDto { PlayniteId = SelectedGame.PlayniteId, Path = SelectedCandidate.Path });
            StatusMessage = "已忽略该存档路径候选";
            await LoadDetailsAsync();
        }

        private async Task ReassignMediaAsync()
        {
            await plugin.RequestAsync<MediaItemDto>(MessageTypes.ReassignMedia, new ReassignMediaRequestDto { MediaId = SelectedMedia.MediaId, TargetPlayniteId = MediaTargetGame.PlayniteId });
            StatusMessage = $"媒体已重新归类到 {MediaTargetGame.Name}";
            await LoadDetailsAsync();
            await LoadInboxAsync();
        }

        private async Task AssignInboxMediaAsync()
        {
            var media = SelectedInboxMedia ?? throw new InvalidOperationException("请先选择待归类媒体。");
            var target = InboxTargetGame ?? throw new InvalidOperationException("请选择目标游戏。");
            await plugin.RequestAsync<MediaItemDto>(MessageTypes.ReassignMedia, new ReassignMediaRequestDto { MediaId = media.MediaId, TargetPlayniteId = target.PlayniteId });
            StatusMessage = $"已将 {media.FileName} 归类到 {target.Name}";
            await RefreshDashboardAsync(false, false);
            await LoadInboxAsync();
            if (SelectedGame != null && string.Equals(SelectedGame.PlayniteId, target.PlayniteId, StringComparison.OrdinalIgnoreCase))
                await LoadDetailsAsync();
        }

        private async Task IgnoreInboxMediaAsync()
        {
            var media = SelectedInboxMedia ?? throw new InvalidOperationException("请先选择待归类媒体。");
            await plugin.RequestAsync<MediaItemDto>(MessageTypes.IgnoreMedia, new IgnoreMediaRequestDto { MediaId = media.MediaId });
            StatusMessage = $"已忽略 {media.FileName}；归档副本仍保留在媒体目录";
            await RefreshDashboardAsync(false, false);
            await LoadInboxAsync();
        }

        private async Task RestoreAsync()
        {
            var result = plugin.PlayniteApi.Dialogs.ShowMessage(
                "恢复前会先创建并锁定当前存档的 PreRestore 快照。请确认游戏、启动器和 MOD 管理器均已关闭。\n\n继续恢复选中的历史版本？",
                "GameSaveCenter 安全恢复", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            var task = await plugin.RequestAsync<TaskStatusDto>(MessageTypes.RestoreExecute, new RestoreRequestDto
            {
                PlayniteId = SelectedGame.PlayniteId,
                BackupId = SelectedBackup.BackupId,
                ConfirmedCurrentSnapshot = true,
                ConfirmedGameClosed = true,
                UserComment = "Playnite restore wizard"
            }, TimeSpan.FromMinutes(30));
            await RefreshCoreAsync(false);
            ThrowIfUnsuccessful(new[] { task });
        }

        private async Task UndoRestoreAsync()
        {
            var result = plugin.PlayniteApi.Dialogs.ShowMessage("撤销将恢复最近的 PreRestore 快照，仍会先保存当前状态。继续？", "撤销恢复", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            var task = await plugin.RequestAsync<TaskStatusDto>(MessageTypes.UndoRestore, new GameQueryDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(30));
            await RefreshCoreAsync(false);
            ThrowIfUnsuccessful(new[] { task });
        }

        private bool CanRetrySelectedTask()
        {
            if (SelectedTask == null) return false;
            if (SelectedTask.State != TaskState.Failed && SelectedTask.State != TaskState.Cancelled) return false;
            if (string.Equals(SelectedTask.TaskType, "MediaInbox", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(SelectedTask.GameId)) return false;
            return string.Equals(SelectedTask.TaskType, "Backup", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(SelectedTask.TaskType, "MediaSync", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RetrySelectedTaskAsync()
        {
            var task = SelectedTask ?? throw new InvalidOperationException("请先选择失败或已取消的任务。");
            if (string.Equals(task.TaskType, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                var result = await plugin.RequestAsync<TaskStatusDto[]>(
                    MessageTypes.BackupGame,
                    new BackupRequestDto { PlayniteIds = { task.GameId }, Force = true, Reason = "Retry" },
                    TimeSpan.FromMinutes(15));
                ThrowIfUnsuccessful(result);
            }
            else if (string.Equals(task.TaskType, "MediaSync", StringComparison.OrdinalIgnoreCase))
            {
                var request = new MediaSyncRequestDto { UploadAfterSync = plugin.Settings.EnableCloudUpload };
                request.PlayniteIds.Add(task.GameId);
                var result = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, request, TimeSpan.FromMinutes(60));
                ThrowIfUnsuccessful(result);
            }
            else if (string.Equals(task.TaskType, "MediaInbox", StringComparison.OrdinalIgnoreCase))
            {
                var result = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, new MediaSyncRequestDto
                {
                    IncludeUnassignedInbox = true,
                    SharedOnly = true,
                    UploadAfterSync = plugin.Settings.EnableCloudUpload
                }, TimeSpan.FromMinutes(60));
                ThrowIfUnsuccessful(result);
            }
            else
            {
                throw new NotSupportedException("该任务类型暂不支持安全重试。");
            }
            await RefreshCoreAsync(false);
            StatusMessage = "重试任务已完成";
        }

        private void CopySelectedTaskError()
        {
            if (SelectedTask == null) return;
            var text = $"{SelectedTask.GameName} · {SelectedTask.TaskType}\r\n{SelectedTask.DetailMessage}\r\n任务 ID：{SelectedTask.TaskId}";
            Clipboard.SetText(text);
            StatusMessage = "任务详情已复制";
        }

        private async void CancelSelectedTask()
        {
            if (SelectedTask == null || !SelectedTask.CanCancel || IsCancellingTask) return;
            var taskId = SelectedTask.TaskId;
            var result = plugin.PlayniteApi.Dialogs.ShowMessage(
                $"取消“{SelectedTask.GameName} · {SelectedTask.TaskType}”任务？\n\n取消请求会在当前文件操作的安全边界生效。",
                "取消后台任务", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            IsCancellingTask = true;
            try
            {
                await plugin.EnsureWorkerAsync();
                var response = await plugin.RequestAsync<CancelTaskResultDto>(MessageTypes.CancelTask, new CancelTaskRequestDto { TaskId = taskId });
                StatusMessage = response.Cancelled ? "已发送取消请求" : "任务已经结束或无法取消";
                await RefreshDashboardAsync(false, false);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                plugin.ShowError(ex.Message);
            }
            finally
            {
                IsCancellingTask = false;
            }
        }

        private void CopyDiagnostics()
        {
            Clipboard.SetText(DiagnosticSummary ?? string.Empty);
            StatusMessage = "诊断信息已复制到剪贴板";
            plugin.ShowInfo("GameSaveCenter 诊断信息已复制");
        }

        private string BuildDiagnosticSummary(WorkerSettingsSnapshotDto settings)
        {
            var builder = new StringBuilder();
            builder.AppendLine("GameSaveCenter 诊断摘要");
            builder.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            builder.AppendLine("插件版本：" + (typeof(DashboardViewModel).Assembly.GetName().Version?.ToString() ?? "dev"));
            builder.AppendLine("Worker：" + (Snapshot.WorkerHealthy ? "正常" : "不可用") + " / " + Snapshot.WorkerVersion);
            builder.AppendLine("Ludusavi：" + (Snapshot.LudusaviAvailable ? "可用" : "不可用") + " / " + Snapshot.LudusaviVersion);
            builder.AppendLine("Ludusavi 路径：" + EmptyAsUnset(settings.LudusaviExecutable));
            builder.AppendLine("存档目录：" + EmptyAsUnset(settings.LudusaviBackupDirectory));
            builder.AppendLine("媒体目录：" + EmptyAsUnset(settings.MediaArchiveDirectory));
            builder.AppendLine("数据目录：" + EmptyAsUnset(settings.DataDirectory));
            builder.AppendLine($"备份策略：{settings.BackupFormat} / {settings.Compression} {settings.CompressionLevel} / 完整 {settings.FullBackupLimit} / 差异 {settings.DifferentialBackupLimit}");
            builder.AppendLine("会话存档候选：" + (settings.EnableSessionSavePathDetection ? "启用" : "关闭"));
            builder.AppendLine("Rclone：" + (Snapshot.RcloneAvailable ? "可用" : "不可用") + " / 远端 " + (settings.RcloneDestinationConfigured ? "已配置" : "未配置"));
            builder.AppendLine($"游戏：管理 {Snapshot.ManagedGames} / 匹配 {Snapshot.MatchedGames} / 运行 {Snapshot.RunningGames} / 警告 {Snapshot.WarningGames}");
            builder.AppendLine();
            builder.AppendLine("最近失败任务：");
            var failed = Tasks.Where(x => x.State == TaskState.Failed).Take(10).ToList();
            if (failed.Count == 0) builder.AppendLine("- 无");
            foreach (var task in failed)
                builder.AppendLine($"- {task.CreatedLocal:yyyy-MM-dd HH:mm:ss} | {task.TaskType} | {task.GameName} | {task.DetailMessage}");
            return builder.ToString().TrimEnd();
        }

        private void OpenWorkerLog()
        {
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameSaveCenter", "Logs", "worker-launch.log");
            OpenPath(log);
        }

        private static void OpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("路径尚未配置。");
            var expanded = Environment.ExpandEnvironmentVariables(path);
            if (File.Exists(expanded))
            {
                Process.Start("explorer.exe", "/select,\"" + expanded + "\"");
                return;
            }
            if (Directory.Exists(expanded))
            {
                Process.Start("explorer.exe", "\"" + expanded + "\"");
                return;
            }
            var parent = Path.GetDirectoryName(expanded);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                Process.Start("explorer.exe", "\"" + parent + "\"");
                return;
            }
            throw new DirectoryNotFoundException(expanded);
        }

        private async void Run(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await plugin.EnsureWorkerAsync();
                await action();
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                plugin.ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RunLocal(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                plugin.ShowError(ex.Message);
            }
        }

        private static void ThrowIfUnsuccessful(IEnumerable<TaskStatusDto> tasks)
        {
            var failed = tasks?.FirstOrDefault(x => x.State == TaskState.Failed);
            if (failed != null) throw new InvalidOperationException(failed.DetailMessage);
            var cancelled = tasks?.FirstOrDefault(x => x.State == TaskState.Cancelled);
            if (cancelled != null) throw new TaskCanceledException(string.IsNullOrWhiteSpace(cancelled.Message) ? "任务已取消" : cancelled.Message);
        }

        private bool FilterGame(object item)
        {
            var game = item as GameStatusDto;
            if (game == null) return false;

            var query = (GameSearchText ?? string.Empty).Trim();
            if (query.Length > 0)
            {
                var matched = Contains(game.Name, query)
                    || Contains(game.LudusaviName, query)
                    || Contains(game.PlatformDisplay, query)
                    || Contains(game.HealthStateDisplay, query);
                if (!matched) return false;
            }

            switch (GameStatusFilter)
            {
                case "已就绪":
                    return game.LudusaviMatched && !IsAttention(game);
                case "未匹配":
                    return !game.LudusaviMatched;
                case "运行中":
                    return game.IsRunning;
                case "需关注":
                    return IsAttention(game);
                case "有历史":
                    return game.BackupVersionCount > 0;
                default:
                    return true;
            }
        }

        private void ApplyGameSort()
        {
            if (GamesView == null) return;
            GamesView.SortDescriptions.Clear();
            switch (GameSortMode)
            {
                case "运行优先":
                    GamesView.SortDescriptions.Add(new SortDescription(nameof(GameStatusDto.IsRunning), ListSortDirection.Descending));
                    break;
                case "匹配优先":
                    GamesView.SortDescriptions.Add(new SortDescription(nameof(GameStatusDto.LudusaviMatched), ListSortDirection.Descending));
                    break;
                case "最近备份":
                    GamesView.SortDescriptions.Add(new SortDescription(nameof(GameStatusDto.LastBackupUtc), ListSortDirection.Descending));
                    break;
            }
            GamesView.SortDescriptions.Add(new SortDescription(nameof(GameStatusDto.Name), ListSortDirection.Ascending));
        }

        private void RefreshGameView(bool keepSelection = true)
        {
            if (GamesView == null) return;
            GamesView.Refresh();
            FilteredGameCount = GamesView.Cast<object>().Count();
            if (!keepSelection || SelectedGame == null || GamesView.Contains(SelectedGame)) return;

            suppressSelectionLoad = true;
            try { SelectedGame = GamesView.Cast<GameStatusDto>().FirstOrDefault(); }
            finally { suppressSelectionLoad = false; }
            if (SelectedGame != null) Run(LoadDetailsAsync);
            else ClearSelectedGameDetails();
        }

        private static bool Contains(string value, string query)
            => !string.IsNullOrWhiteSpace(value) && value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private static bool IsAttention(GameStatusDto game)
            => string.Equals(game.HealthState, "Attention", StringComparison.OrdinalIgnoreCase)
               || string.Equals(game.HealthState, "Warning", StringComparison.OrdinalIgnoreCase)
               || string.Equals(game.HealthState, "LudusaviUnavailable", StringComparison.OrdinalIgnoreCase);

        private void ClearSelectedGameDetails()
        {
            ApplyOnUi(() =>
            {
                Backups.Clear();
                Media.Clear();
                MediaSources.Clear();
                SaveCandidates.Clear();
                GameTools.Clear();
                SelectedGameTool = null!;
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
                AddMediaSourceCommand, AcceptCandidateCommand, RejectCandidateCommand, ReassignMediaCommand,
                AssignInboxMediaCommand, IgnoreInboxMediaCommand,
                CancelTaskCommand, RetryTaskCommand, CopyTaskErrorCommand, RefreshDiagnosticsCommand, CopyDiagnosticsCommand,
                OpenDataDirectoryCommand, OpenBackupDirectoryCommand, OpenMediaDirectoryCommand, OpenWorkerLogCommand
                ,ImportTrainerCommand,ImportCheatTableCommand,ImportToolFolderCommand,SaveGameToolCommand,LaunchGameToolCommand,
                OpenGameToolDirectoryCommand,DeleteGameToolCommand,SyncTrainerCatalogCommand,SearchTrainerCatalogCommand,
                LoadTrainerReleasesCommand,DownloadTrainerCommand
            }.OfType<RelayCommand>())
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private void ApplyOnUi(Action action) => plugin.PlayniteApi.MainView.UIDispatcher.Invoke(action);
        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (var item in source ?? Enumerable.Empty<T>()) target.Add(item);
        }
        private static string EmptyAsUnset(string value) => string.IsNullOrWhiteSpace(value) ? "（未配置）" : value;
    }
}

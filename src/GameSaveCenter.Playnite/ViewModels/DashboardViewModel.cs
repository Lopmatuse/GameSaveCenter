using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using GameSaveCenter.Contracts;
using Playnite.SDK;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>Apple-inspired dashboard state; all file operations remain in the Worker.</summary>
    public sealed partial class DashboardViewModel : ObservableObject
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private readonly GameSaveCenterPlugin plugin;
        private readonly GamePickerViewModel gamePicker;
        private readonly SynchronizationContext? uiSynchronizationContext = SynchronizationContext.Current;
        private readonly Dictionary<string, TaskState> knownTaskStates = new Dictionary<string, TaskState>(StringComparer.OrdinalIgnoreCase);
        private readonly DateTime dashboardOpenedUtc = DateTime.UtcNow;
        private bool isBusy;
        private bool isBackgroundRefreshing;
        private bool isCancellingTask;
        private bool taskSnapshotInitialized;
        private long lastTaskEventSequence;
        private CancellationTokenSource? taskEventSubscription;
        private CancellationTokenSource? gamePickerPersistenceCancellation;
        private Task? taskEventListener;
        private DateTime lastFullDashboardRefreshUtc=DateTime.MinValue;
        private string statusMessage = "准备就绪";
        private BackupVersionDto selectedBackup = null!;
        private DashboardSnapshotDto snapshot = new DashboardSnapshotDto();
        private SavePathCandidateDto selectedCandidate = null!;
        private string backupComment = string.Empty;
        private bool lockSelectedBackup;
        private MediaItemDto selectedMedia = null!;
        private MediaStorageSummaryDto mediaSummary = new MediaStorageSummaryDto();
        private string mediaComment = string.Empty;
        private bool mediaFavorite;
        private string mediaSearchText = string.Empty;
        private string mediaFilter = "全部";
        private GameStatusDto mediaTargetGame = null!;
        private MediaItemDto selectedInboxMedia = null!;
        private GameStatusDto inboxTargetGame = null!;
        private TaskStatusDto selectedTask = null!;
        private ValidationFindingDto selectedFinding = null!;
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
        private GameToolVersionDto selectedGameToolVersion = null!;
        private TrainerCatalogItemDto selectedTrainerCatalogItem = null!;
        private TrainerReleaseDto selectedTrainerRelease = null!;
        private string trainerSearchText = string.Empty;
        private bool showTrainerLibrary;
        private string taskStatusFilter = "全部";
        private string taskGameFilter = "全部";
        private string taskTypeFilter = "全部";
        private string deviceStateMessage = "尚未刷新多设备状态。该功能只比较摘要，绝不自动恢复或覆盖存档。";
        private DeviceConflictStatusDto selectedDeviceComparison = null!;
        private string deviceDecision = "稍后处理";
        private string deviceDecisionComment = string.Empty;
        private RemoteBackupStageResultDto? stagedRemoteBackup;
        private string stagedRemoteBackupStatus = "尚未下载远端存档。下载只会写入本机隔离区，不会覆盖当前存档。";
        private string processMappingExecutable = string.Empty;
        private GameStatusDto processMappingTargetGame = null!;
        private ProcessMappingDto selectedProcessMapping = null!;
        private GameToolEntryCandidateDto selectedImportEntryCandidate = null!;
        private string pendingGameToolImportSource = string.Empty;
        private GameToolType pendingGameToolImportType = GameToolType.Trainer;
        private bool hasPendingGameToolEntrySelection;

        public DashboardViewModel(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            gamePicker = new GamePickerViewModel();
            gamePicker.ApplyPersistedState(plugin.Settings.GamePickerSearchText, plugin.Settings.GamePickerStatusFilter, plugin.Settings.GamePickerPlatformFilter, plugin.Settings.GamePickerSortMode);
            gamePicker.StateChanged += OnGamePickerStateChanged;
            gamePicker.PropertyChanged += OnGamePickerPropertyChanged;
            gameSearchText = gamePicker.SearchText;
            gameStatusFilter = gamePicker.StatusFilter;
            gameSortMode = gamePicker.SortMode;
            GamesView = CollectionViewSource.GetDefaultView(Games);
            GamesView.Filter = FilterGame;
            TasksView = CollectionViewSource.GetDefaultView(Tasks);
            TasksView.Filter = FilterTask;
            MediaView = CollectionViewSource.GetDefaultView(Media);
            MediaView.Filter = FilterMedia;
            ApplyGameSort();
            RefreshCommand = new RelayCommand(_ => Run(RefreshAsync), _ => !IsBusy);
            BackupSelectedCommand = new RelayCommand(_ => Run(BackupSelectedAsync), _ => !IsBusy && SelectedGame != null && SelectedGame.LudusaviMatched && Snapshot.LudusaviAvailable);
            BackupAllCommand = new RelayCommand(_ => Run(BackupAllAsync), _ => !IsBusy && Snapshot.LudusaviAvailable && Games.Any(x => x.LudusaviMatched));
            SyncMediaCommand = new RelayCommand(_ => Run(SyncMediaAsync), _ => !IsBusy);
            DetectPathsCommand = new RelayCommand(_ => Run(DetectPathsAsync), _ => !IsBusy && SelectedGame != null);
            ValidateCommand = new RelayCommand(_ => Run(ValidateAsync), _ => !IsBusy && SelectedGame != null && SelectedGame.LudusaviMatched);
            RestoreCommand = new RelayCommand(_ => Run(RestoreAsync), _ => !IsBusy && SelectedGame != null && SelectedBackup != null && Snapshot.LudusaviAvailable);
            UndoRestoreCommand = new RelayCommand(_ => Run(UndoRestoreAsync), _ => !IsBusy && SelectedGame != null && Backups.Any(x => x.IsPreRestore));
            LoadDetailsCommand = new RelayCommand(_ => Run(() => LoadDetailsAsync(true)), _ => !IsBusy && SelectedGame != null);
            SavePolicyCommand = new RelayCommand(_ => Run(SavePolicyAsync), _ => !IsBusy && SelectedGame != null);
            UpdateBackupMetadataCommand = new RelayCommand(_ => Run(UpdateBackupMetadataAsync), _ => !IsBusy && SelectedGame != null && SelectedBackup != null);
            CompareBackupCommand = new RelayCommand(_ => Run(CompareBackupAsync), _ => !IsBusy && SelectedGame != null && SelectedBackup != null && Backups.IndexOf(SelectedBackup) >= 0 && Backups.IndexOf(SelectedBackup) + 1 < Backups.Count);
            PreviewRetentionCommand = new RelayCommand(_ => Run(PreviewRetentionAsync), _ => !IsBusy && SelectedGame != null && Backups.Count > 0);
            AddMediaSourceCommand = new RelayCommand(_ => Run(AddMediaSourceAsync), _ => !IsBusy && SelectedGame != null);
            UpdateMediaSourceCommand = new RelayCommand(value => Run(() => UpdateMediaSourceAsync(value as MediaSourceRuleDto)), _ => !IsBusy);
            DeleteMediaSourceCommand = new RelayCommand(value => Run(() => DeleteMediaSourceAsync(value as MediaSourceRuleDto)), _ => !IsBusy);
            AcceptCandidateCommand = new RelayCommand(_ => Run(AcceptCandidateAsync), _ => !IsBusy && SelectedGame != null && SelectedCandidate != null && !string.Equals(SelectedCandidate.Status, "Accepted", StringComparison.OrdinalIgnoreCase));
            RejectCandidateCommand = new RelayCommand(_ => Run(RejectCandidateAsync), _ => !IsBusy && SelectedGame != null && SelectedCandidate != null && !string.Equals(SelectedCandidate.Status, "Accepted", StringComparison.OrdinalIgnoreCase));
            ReassignMediaCommand = new RelayCommand(_ => Run(ReassignMediaAsync), _ => !IsBusy && SelectedMedia != null && MediaTargetGame != null);
            UpdateMediaMetadataCommand = new RelayCommand(_ => Run(UpdateMediaMetadataAsync), _ => !IsBusy && SelectedMedia != null);
            FavoriteSelectedMediaCommand = new RelayCommand(value => Run(() => UpdateMediaMetadataBatchAsync(value, true, false)), _ => !IsBusy);
            UnfavoriteSelectedMediaCommand = new RelayCommand(value => Run(() => UpdateMediaMetadataBatchAsync(value, false, false)), _ => !IsBusy);
            CommentSelectedMediaCommand = new RelayCommand(value => Run(() => UpdateMediaMetadataBatchAsync(value, null, true)), _ => !IsBusy);
            OpenSelectedMediaCommand = new RelayCommand(_ => RunLocal(OpenSelectedMedia), _ => SelectedMedia != null && !string.IsNullOrWhiteSpace(SelectedMedia.ArchivePath));
            RevealSelectedMediaCommand = new RelayCommand(_ => RunLocal(() => OpenPath(SelectedMedia.ArchivePath)), _ => SelectedMedia != null && !string.IsNullOrWhiteSpace(SelectedMedia.ArchivePath));
            AssignInboxMediaCommand = new RelayCommand(_ => Run(AssignInboxMediaAsync), _ => !IsBusy && SelectedInboxMedia != null && InboxTargetGame != null);
            IgnoreInboxMediaCommand = new RelayCommand(_ => Run(IgnoreInboxMediaAsync), _ => !IsBusy && SelectedInboxMedia != null);
            CancelTaskCommand = new RelayCommand(_ => _ = CancelSelectedTaskAsync(), _ => SelectedTask != null && SelectedTask.CanCancel && !IsCancellingTask);
            RetryTaskCommand = new RelayCommand(_ => Run(RetrySelectedTaskAsync), _ => !IsBusy && CanRetrySelectedTask());
            CopyTaskErrorCommand = new RelayCommand(_ => RunLocal(CopySelectedTaskError), _ => SelectedTask != null && !string.IsNullOrWhiteSpace(SelectedTask.DetailMessage));
            OpenAttentionCenterCommand = new RelayCommand(_ => OpenAttentionCenter());
            OpenAttentionFindingCommand = new RelayCommand(value => OpenAttentionFinding(value as ValidationFindingDto));
            RefreshDiagnosticsCommand = new RelayCommand(_ => Run(RefreshDiagnosticsAsync), _ => !IsBusy);
            SyncDeviceStatesCommand = new RelayCommand(_ => Run(SyncDeviceStatesAsync), _ => !IsBusy);
            SaveDeviceDecisionCommand = new RelayCommand(_ => Run(SaveDeviceDecisionAsync), _ => !IsBusy && SelectedDeviceComparison != null);
            StageRemoteBackupCommand = new RelayCommand(_ => Run(StageRemoteBackupAsync), _ => !IsBusy && SelectedDeviceComparison != null && !string.IsNullOrWhiteSpace(SelectedDeviceComparison.RemoteBackupId));
            RestoreStagedRemoteBackupCommand = new RelayCommand(_ => Run(RestoreStagedRemoteBackupAsync), _ => !IsBusy && StagedRemoteBackup != null && StagedRemoteBackup.Verified);
            SaveProcessMappingCommand = new RelayCommand(_ => Run(SaveProcessMappingAsync), _ => !IsBusy && !string.IsNullOrWhiteSpace(ProcessMappingExecutable) && ProcessMappingTargetGame != null);
            DeleteProcessMappingCommand = new RelayCommand(_ => Run(DeleteProcessMappingAsync), _ => !IsBusy && SelectedProcessMapping != null);
            CopyDiagnosticsCommand = new RelayCommand(_ => RunLocal(CopyDiagnostics), _ => !string.IsNullOrWhiteSpace(DiagnosticSummary));
            OpenDataDirectoryCommand = new RelayCommand(_ => RunLocal(() => OpenPath(EffectiveSettings.DataDirectory)), _ => !string.IsNullOrWhiteSpace(EffectiveSettings.DataDirectory));
            OpenBackupDirectoryCommand = new RelayCommand(_ => RunLocal(() => OpenPath(EffectiveSettings.LudusaviBackupDirectory)), _ => !string.IsNullOrWhiteSpace(EffectiveSettings.LudusaviBackupDirectory));
            OpenMediaDirectoryCommand = new RelayCommand(_ => RunLocal(() => OpenPath(EffectiveSettings.MediaArchiveDirectory)), _ => !string.IsNullOrWhiteSpace(EffectiveSettings.MediaArchiveDirectory));
            OpenWorkerLogCommand = new RelayCommand(_ => RunLocal(OpenWorkerLog));
            ImportTrainerCommand = new RelayCommand(_ => Run(() => ImportGameToolAsync(GameToolType.Trainer)), _ => !IsBusy && SelectedGame != null);
            ImportCheatTableCommand = new RelayCommand(_ => Run(() => ImportGameToolAsync(GameToolType.CheatTable)), _ => !IsBusy && SelectedGame != null);
            ImportToolFolderCommand = new RelayCommand(_ => Run(ImportGameToolFolderAsync), _ => !IsBusy && SelectedGame != null);
            ConfirmGameToolImportCommand = new RelayCommand(_ => Run(ConfirmGameToolImportAsync), _ => !IsBusy && HasPendingGameToolEntrySelection && SelectedImportEntryCandidate != null);
            CancelGameToolImportCommand = new RelayCommand(_ => ClearPendingGameToolImport(), _ => HasPendingGameToolEntrySelection);
            SaveGameToolCommand = new RelayCommand(_ => Run(SaveSelectedGameToolAsync), _ => !IsBusy && SelectedGameTool != null);
            LaunchGameToolCommand = new RelayCommand(_ => Run(LaunchSelectedGameToolAsync), _ => !IsBusy && SelectedGameTool != null && SelectedGameTool.ActiveVersion.IsAvailable);
            OpenGameToolDirectoryCommand = new RelayCommand(_ => Run(OpenSelectedGameToolDirectoryAsync), _ => !IsBusy && SelectedGameTool != null);
            DeleteGameToolCommand = new RelayCommand(_ => Run(DeleteSelectedGameToolAsync), _ => !IsBusy && SelectedGameTool != null);
            SyncTrainerCatalogCommand = new RelayCommand(_ => Run(SyncTrainerCatalogAsync), _ => !IsBusy);
            SearchTrainerCatalogCommand = new RelayCommand(_ => Run(SearchTrainerCatalogAsync), _ => !IsBusy);
            LoadTrainerReleasesCommand = new RelayCommand(_ => Run(LoadTrainerReleasesAsync), _ => !IsBusy && SelectedTrainerCatalogItem != null);
            DownloadTrainerCommand = new RelayCommand(_ => Run(DownloadTrainerAsync), _ => !IsBusy && SelectedGame != null && SelectedTrainerRelease != null);
            Run(InitializeAsync);
        }

        public ObservableCollection<GameStatusDto> Games { get; } = new ObservableCollection<GameStatusDto>();
        /// <summary>Shared global picker state. The dashboard keeps the legacy bindings below for compatibility.</summary>
        public GamePickerViewModel GamePicker => gamePicker;
        public ObservableCollection<TaskStatusDto> Tasks { get; } = new ObservableCollection<TaskStatusDto>();
        public ObservableCollection<TaskStatusDto> OverviewTasks { get; } = new ObservableCollection<TaskStatusDto>();
        public ObservableCollection<string> TaskGameFilterOptions { get; } = new ObservableCollection<string> { "全部" };
        public ObservableCollection<string> TaskTypeFilterOptions { get; } = new ObservableCollection<string> { "全部" };
        public ObservableCollection<ValidationFindingDto> Findings { get; } = new ObservableCollection<ValidationFindingDto>();
        /// <summary>Small overview projection so a warning count always has a visible reason.</summary>
        public ObservableCollection<ValidationFindingDto> AttentionFindings { get; } = new ObservableCollection<ValidationFindingDto>();
        public ObservableCollection<DeviceConflictStatusDto> DeviceComparisons { get; } = new ObservableCollection<DeviceConflictStatusDto>();
        public IReadOnlyList<string> DeviceDecisionOptions { get; } = new[] { "稍后处理", "保留两者", "以本机为准", "以远端为准" };
        public ObservableCollection<ProcessMappingDto> ProcessMappings { get; } = new ObservableCollection<ProcessMappingDto>();
        public ObservableCollection<BackupVersionDto> Backups { get; } = new ObservableCollection<BackupVersionDto>();
        public ObservableCollection<MediaItemDto> Media { get; } = new ObservableCollection<MediaItemDto>();
        public ObservableCollection<MediaItemDto> UnassignedMedia { get; } = new ObservableCollection<MediaItemDto>();
        public ObservableCollection<AuditLogEntryDto> Audit { get; } = new ObservableCollection<AuditLogEntryDto>();
        public ObservableCollection<SavePathCandidateDto> SaveCandidates { get; } = new ObservableCollection<SavePathCandidateDto>();
        public ObservableCollection<MediaSourceRuleDto> MediaSources { get; } = new ObservableCollection<MediaSourceRuleDto>();
        public ObservableCollection<GameToolDto> GameTools { get; } = new ObservableCollection<GameToolDto>();
        public ObservableCollection<GameToolEntryCandidateDto> ImportEntryCandidates { get; } = new ObservableCollection<GameToolEntryCandidateDto>();
        public ObservableCollection<TrainerCatalogItemDto> TrainerCatalogResults { get; } = new ObservableCollection<TrainerCatalogItemDto>();
        public ObservableCollection<TrainerReleaseDto> TrainerReleases { get; } = new ObservableCollection<TrainerReleaseDto>();
        public ICollectionView GamesView { get; }
        public ICollectionView TasksView { get; }
        public ICollectionView MediaView { get; }
        public IReadOnlyList<string> MediaFilterOptions { get; } = new[] { "全部", "截图", "录像", "收藏" };
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
                if (!string.Equals(gamePicker.SearchText, gameSearchText, StringComparison.Ordinal)) gamePicker.SearchText = gameSearchText;
                RefreshGameView();
            }
        }
        public string GameStatusFilter
        {
            get => gameStatusFilter;
            set
            {
                SetValue(ref gameStatusFilter, string.IsNullOrWhiteSpace(value) ? "全部" : value);
                var pickerFilter = gameStatusFilter == "全部" ? "全部" : gameStatusFilter;
                if (!string.Equals(gamePicker.StatusFilter, pickerFilter, StringComparison.Ordinal)) gamePicker.StatusFilter = pickerFilter;
                RefreshGameView();
            }
        }
        public string GameSortMode
        {
            get => gameSortMode;
            set
            {
                SetValue(ref gameSortMode, string.IsNullOrWhiteSpace(value) ? "名称" : value);
                if (!string.Equals(gamePicker.SortMode, gameSortMode, StringComparison.Ordinal)) gamePicker.SortMode = gameSortMode;
                ApplyGameSort();
                RefreshGameView();
            }
        }
        public IReadOnlyList<string> TaskStatusFilterOptions { get; } = new[] { "全部", "运行中", "等待中", "失败", "已完成" };
        public string TaskStatusFilter
        {
            get => taskStatusFilter;
            set
            {
                SetValue(ref taskStatusFilter, string.IsNullOrWhiteSpace(value) ? "全部" : value);
                TasksView.Refresh();
            }
        }
        public string TaskGameFilter
        {
            get => taskGameFilter;
            set
            {
                SetValue(ref taskGameFilter, string.IsNullOrWhiteSpace(value) ? "全部" : value);
                TasksView.Refresh();
            }
        }
        public string TaskTypeFilter
        {
            get => taskTypeFilter;
            set
            {
                SetValue(ref taskTypeFilter, string.IsNullOrWhiteSpace(value) ? "全部" : value);
                TasksView.Refresh();
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
            set
            {
                SetValue(ref selectedGameTool,value);
                SelectedGameToolVersion=value==null?null!:value.ActiveVersion;
                RaiseCommandStates();
            }
        }
        public GameToolVersionDto SelectedGameToolVersion
        {
            get => selectedGameToolVersion;
            set
            {
                SetValue(ref selectedGameToolVersion,value);
                if(selectedGameTool!=null&&value!=null)selectedGameTool.ActiveVersionId=value.VersionId;
                RaiseCommandStates();
            }
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
            get => gamePicker.SelectedGame!;
            set => gamePicker.SelectGame(value);
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
        public ValidationFindingDto SelectedFinding
        {
            get => selectedFinding;
            set => SetValue(ref selectedFinding, value);
        }
        public string BackupComment { get => backupComment; set => SetValue(ref backupComment, value); }
        public bool LockSelectedBackup { get => lockSelectedBackup; set => SetValue(ref lockSelectedBackup, value); }
        public MediaItemDto SelectedMedia
        {
            get => selectedMedia;
            set
            {
                SetValue(ref selectedMedia, value);
                MediaComment=value?.Comment??string.Empty;
                MediaFavorite=value?.IsFavorite??false;
                RaiseCommandStates();
            }
        }
        public MediaStorageSummaryDto MediaSummary { get => mediaSummary; private set => SetValue(ref mediaSummary,value??new MediaStorageSummaryDto()); }
        public string MediaComment { get => mediaComment; set => SetValue(ref mediaComment,value??string.Empty); }
        public bool MediaFavorite { get => mediaFavorite; set => SetValue(ref mediaFavorite,value); }
        public string MediaSearchText
        {
            get => mediaSearchText;
            set { SetValue(ref mediaSearchText,value??string.Empty); MediaView.Refresh(); }
        }
        public string MediaFilter
        {
            get => mediaFilter;
            set { SetValue(ref mediaFilter,string.IsNullOrWhiteSpace(value)?"全部":value); MediaView.Refresh(); }
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
        public ICommand UpdateMediaSourceCommand { get; }
        public ICommand DeleteMediaSourceCommand { get; }
        public ICommand AcceptCandidateCommand { get; }
        public ICommand RejectCandidateCommand { get; }
        public ICommand ReassignMediaCommand { get; }
        public ICommand UpdateMediaMetadataCommand { get; }
        public ICommand FavoriteSelectedMediaCommand { get; }
        public ICommand UnfavoriteSelectedMediaCommand { get; }
        public ICommand CommentSelectedMediaCommand { get; }
        public ICommand OpenSelectedMediaCommand { get; }
        public ICommand RevealSelectedMediaCommand { get; }
        public ICommand AssignInboxMediaCommand { get; }
        public ICommand IgnoreInboxMediaCommand { get; }
        public ICommand CancelTaskCommand { get; }
        public ICommand RetryTaskCommand { get; }
        public ICommand CopyTaskErrorCommand { get; }
        public ICommand OpenAttentionCenterCommand { get; }
        public ICommand OpenAttentionFindingCommand { get; }
        public ICommand RefreshDiagnosticsCommand { get; }
        public ICommand SyncDeviceStatesCommand { get; }
        public ICommand SaveDeviceDecisionCommand { get; }
        public ICommand StageRemoteBackupCommand { get; }
        public ICommand RestoreStagedRemoteBackupCommand { get; }
        public ICommand SaveProcessMappingCommand { get; }
        public ICommand DeleteProcessMappingCommand { get; }
        public ICommand CopyDiagnosticsCommand { get; }
        public ICommand OpenDataDirectoryCommand { get; }
        public ICommand OpenBackupDirectoryCommand { get; }
        public ICommand OpenMediaDirectoryCommand { get; }
        public ICommand OpenWorkerLogCommand { get; }
        public string DeviceStateMessage { get => deviceStateMessage; private set => SetValue(ref deviceStateMessage,value); }
        public DeviceConflictStatusDto SelectedDeviceComparison
        {
            get=>selectedDeviceComparison;
            set
            {
                SetValue(ref selectedDeviceComparison,value);
                DeviceDecision=value?.DecisionDisplay switch
                {
                    "保留两者"=>"保留两者","记录为优先本机"=>"以本机为准","记录为优先远端"=>"以远端为准",_=>"稍后处理"
                };
                DeviceDecisionComment=value?.DecisionComment??string.Empty;
                if(StagedRemoteBackup!=null&&(value==null||
                   !string.Equals(StagedRemoteBackup.PlayniteId,value.PlayniteId,StringComparison.OrdinalIgnoreCase)||
                   !string.Equals(StagedRemoteBackup.RemoteDevice,value.RemoteDevice,StringComparison.OrdinalIgnoreCase)||
                   !string.Equals(StagedRemoteBackup.BackupId,value.RemoteBackupId,StringComparison.OrdinalIgnoreCase)))
                    StagedRemoteBackup=null;
                RaiseCommandStates();
            }
        }
        public RemoteBackupStageResultDto? StagedRemoteBackup
        {
            get=>stagedRemoteBackup;
            private set
            {
                SetValue(ref stagedRemoteBackup,value);
                StagedRemoteBackupStatus=value==null
                    ?"尚未下载远端存档。下载只会写入本机隔离区，不会覆盖当前存档。"
                    :$"已校验：{value.GameName} / {value.RemoteDevice} / {value.BackupId}；{value.ExpiresUtc.ToLocalTime():yyyy-MM-dd HH:mm} 前有效。";
                RaiseCommandStates();
            }
        }
        public string StagedRemoteBackupStatus
        {
            get=>stagedRemoteBackupStatus;
            private set=>SetValue(ref stagedRemoteBackupStatus,value);
        }
        public string DeviceDecision { get=>deviceDecision; set=>SetValue(ref deviceDecision,value??"稍后处理"); }
        public string DeviceDecisionComment { get=>deviceDecisionComment; set=>SetValue(ref deviceDecisionComment,value??string.Empty); }
        public string ProcessMappingExecutable { get => processMappingExecutable; set { SetValue(ref processMappingExecutable,value??string.Empty); RaiseCommandStates(); } }
        public GameStatusDto ProcessMappingTargetGame { get => processMappingTargetGame; set { SetValue(ref processMappingTargetGame,value); RaiseCommandStates(); } }
        public ProcessMappingDto SelectedProcessMapping { get => selectedProcessMapping; set { SetValue(ref selectedProcessMapping,value); RaiseCommandStates(); } }
        public GameToolEntryCandidateDto SelectedImportEntryCandidate
        {
            get => selectedImportEntryCandidate;
            set { SetValue(ref selectedImportEntryCandidate,value); RaiseCommandStates(); }
        }
        public bool HasPendingGameToolEntrySelection
        {
            get => hasPendingGameToolEntrySelection;
            private set { SetValue(ref hasPendingGameToolEntrySelection,value); RaiseCommandStates(); }
        }
        public ICommand ImportTrainerCommand { get; }
        public ICommand ImportCheatTableCommand { get; }
        public ICommand ImportToolFolderCommand { get; }
        public ICommand ConfirmGameToolImportCommand { get; }
        public ICommand CancelGameToolImportCommand { get; }
        public ICommand SaveGameToolCommand { get; }
        public ICommand LaunchGameToolCommand { get; }
        public ICommand OpenGameToolDirectoryCommand { get; }
        public ICommand DeleteGameToolCommand { get; }
        public ICommand SyncTrainerCatalogCommand { get; }
        public ICommand SearchTrainerCatalogCommand { get; }
        public ICommand LoadTrainerReleasesCommand { get; }
        public ICommand DownloadTrainerCommand { get; }

        public Task RefreshAsync() => RefreshCoreAsync(true);

        /// <summary>Turns the overview warning count into a route to its concrete reasons.</summary>
        private void OpenAttentionCenter()
        {
            var finding=Findings.FirstOrDefault(x=>x.Severity>=FindingSeverity.Warning);
            if(finding==null)
            {
                StatusMessage="当前没有需要处理的关注项。";
                return;
            }

            SelectedFinding=finding;
            CurrentWorkspace=WorkspaceKind.Maintenance;
            AttentionCenterRequested?.Invoke(this,EventArgs.Empty);
        }

        private void OpenAttentionFinding(ValidationFindingDto? finding)
        {
            if (finding == null) { OpenAttentionCenter(); return; }
            SelectedFinding = finding;
            CurrentWorkspace = WorkspaceKind.Maintenance;
            AttentionCenterRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? AttentionCenterRequested;

        /// <summary>
        /// Enables the optional Worker event connection while the WPF dashboard is visible.
        /// Normal task polling remains active as the durable fallback after a Worker restart,
        /// missed event, or temporarily unavailable event pipe.
        /// </summary>
        public void StartTaskEventSubscription()
        {
            if (taskEventSubscription != null) return;
            taskEventSubscription = new CancellationTokenSource();
            taskEventListener = plugin.ListenForTaskEventsAsync(ApplyTaskEventAsync, taskEventSubscription.Token);
        }

        public void StopTaskEventSubscription()
        {
            var subscription = taskEventSubscription;
            taskEventSubscription = null;
            if (subscription == null) return;
            try { subscription.Cancel(); }
            finally { subscription.Dispose(); }
            taskEventListener = null;
        }

        public void CancelDeferredUiWork()
        {
            gamePicker.CancelPendingRefresh();
            gamePickerPersistenceCancellation?.Cancel();
        }

        private async Task ApplyTaskEventAsync(TaskChangeEventDto change)
        {
            if (change == null || change.Task == null) return;
            ApplyOnUi(() =>
            {
                MergeTaskChange(Tasks, change.Task);
                Replace(OverviewTasks, Tasks.OrderByDescending(x => x.CreatedUtc).Take(8));
                knownTaskStates[change.Task.TaskId] = change.Task.State;
                taskSnapshotInitialized = true;
                if (SelectedTask == null || string.Equals(SelectedTask.TaskId, change.Task.TaskId, StringComparison.OrdinalIgnoreCase))
                    SelectedTask = Tasks.FirstOrDefault(x => string.Equals(x.TaskId, change.Task.TaskId, StringComparison.OrdinalIgnoreCase));
                RaiseCommandStates();
            });

            if (change.Task.State == TaskState.Succeeded || change.Task.State == TaskState.Failed || change.Task.State == TaskState.Cancelled)
            {
                // A terminal event can change backup/media counts and findings. Request the normal
                // cached snapshot refresh; the event itself only updates the task rows immediately.
                await RequestBackgroundRefreshAsync();
            }
        }

        private static void MergeTaskChange(ObservableCollection<TaskStatusDto> target, TaskStatusDto change)
        {
            var index = target.ToList().FindIndex(x => string.Equals(x.TaskId, change.TaskId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) target[index] = change;
            else target.Insert(0, change);
            while (target.Count > 200) target.RemoveAt(target.Count - 1);
        }

        private async Task InitializeAsync()
        {
            // Render durable SQLite state first. The library synchronization runs separately, so
            // opening the panel never waits for a large Playnite library to be rematched.
            await RefreshCoreAsync(false);
            _ = RefreshAfterSynchronizationAsync();
        }

        private async Task RefreshAfterSynchronizationAsync()
        {
            try
            {
                await plugin.SynchronizeAsync();
                await RefreshDashboardAsync(false, false);
            }
            catch (Exception ex)
            {
                ApplyOnUi(() => StatusMessage = "已显示本地缓存；后台同步暂不可用：" + ex.Message);
            }
        }

        /// <summary>Lightweight polling entry used by the view timer. It remains active while a manual task is running.</summary>
        public async Task RequestBackgroundRefreshAsync()
        {
            var started = false;
            try
            {
                ApplyOnUi(() =>
                {
                    if (!plugin.Settings.EnableDashboardAutoRefresh || IsBackgroundRefreshing) return;
                    IsBackgroundRefreshing = true;
                    started = true;
                });
                if (!started) return;
                await plugin.EnsureWorkerAsync();
                var taskChanges=await plugin.RequestAsync<TaskChangeFeedDto>(MessageTypes.GetTaskChanges,
                    new TaskChangeRequestDto{AfterSequence=lastTaskEventSequence,Limit=100});
                lastTaskEventSequence=taskChanges.LatestSequence;
                var refreshDashboard=taskChanges.ResetRequired||taskChanges.Changes.Count>0||
                    DateTime.UtcNow-lastFullDashboardRefreshUtc>=TimeSpan.FromMinutes(1);
                if(!refreshDashboard)return;
                var refreshDetails = await RefreshDashboardAsync(false, true);
                var loadInbox = false;
                var loadDetails = false;
                ApplyOnUi(() =>
                {
                    loadInbox = CurrentWorkspace == WorkspaceKind.Media;
                    loadDetails = refreshDetails && !IsBusy && SelectedGame != null;
                });
                if (loadInbox) await LoadInboxAsync();
                if (loadDetails) await LoadDetailsAsync();
            }
            catch (Exception ex)
            {
                ApplyOnUi(() => StatusMessage = "自动刷新暂不可用：" + ex.Message);
            }
            finally
            {
                if (started) ApplyOnUi(() => IsBackgroundRefreshing = false);
            }
        }

        private async Task RefreshCoreAsync(bool synchronize)
        {
            StatusMessage = synchronize ? "正在同步设置与游戏库…" : "正在读取本地状态…";
            await RefreshDashboardAsync(synchronize, false);
            if (CurrentWorkspace == WorkspaceKind.Media) await LoadInboxAsync();
            if (CurrentWorkspace == WorkspaceKind.Maintenance) await LoadDiagnosticsAsync();
            if (SelectedGame != null && IsGameScopedWorkspace(CurrentWorkspace)) await LoadDetailsAsync();
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
                    gamePicker.SetItems(Games, selectedGameId ?? plugin.Settings.GamePickerSelectedGameId);
                    RefreshGameView(false);
                    SelectedGame = gamePicker.SelectedGame
                        ?? Games.FirstOrDefault(x => x.PlayniteId == selectedGameId && GamesView.Contains(x))
                        ?? GamesView.Cast<GameStatusDto>().FirstOrDefault();
                    MediaTargetGame = Games.FirstOrDefault(x => string.Equals(x.PlayniteId, mediaTargetId, StringComparison.OrdinalIgnoreCase))
                                      ?? SelectedGame
                                      ?? Games.FirstOrDefault();
                }
                finally { suppressSelectionLoad = false; }
                Replace(Tasks, data.RecentTasks);
                Replace(OverviewTasks, data.RecentTasks.Take(8));
                RebuildTaskFilters();
                SelectedTask = Tasks.FirstOrDefault(x => x.TaskId == selectedTaskId) ?? Tasks.FirstOrDefault();
                Replace(Findings, data.Findings);
                Replace(AttentionFindings, data.Findings.Where(x => x.Severity >= FindingSeverity.Warning).Take(4));
                Replace(Audit, data.RecentAudit);
                StatusMessage = data.WorkerHealthy
                    ? data.LudusaviAvailable ? "Worker 与 Ludusavi 均正常" : "Worker 正常，Ludusavi 尚未配置"
                    : "Worker 不可用";
            });
            foreach (var task in notifications) plugin.ShowTaskNotification(task);
            lastFullDashboardRefreshUtc=DateTime.UtcNow;
            return selectedTaskCompleted;
        }

        private async Task LoadDiagnosticsAsync()
        {
            var settings = await plugin.RequestAsync<WorkerSettingsSnapshotDto>(MessageTypes.GetSettings, new { });
            var mappings = await plugin.RequestAsync<List<ProcessMappingDto>>(MessageTypes.ListProcessMappings, new { });
            ApplyOnUi(() =>
            {
                EffectiveSettings = settings;
                DiagnosticSummary = BuildDiagnosticSummary(settings);
                Replace(ProcessMappings,mappings);
                if(ProcessMappingTargetGame==null) ProcessMappingTargetGame=SelectedGame??Games.FirstOrDefault();
            });
        }

        private async Task SaveProcessMappingAsync()
        {
            var saved=await plugin.RequestAsync<ProcessMappingDto>(MessageTypes.SaveProcessMapping,new ProcessMappingDto{ExecutableName=ProcessMappingExecutable,PlayniteId=ProcessMappingTargetGame.PlayniteId});
            await LoadDiagnosticsAsync();ProcessMappingExecutable=string.Empty;StatusMessage=$"已将 {saved.ExecutableName} 绑定到 {saved.GameName}";
        }

        private async Task DeleteProcessMappingAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.DeleteProcessMapping,new ProcessMappingDto{ExecutableName=SelectedProcessMapping.ExecutableName});
            await LoadDiagnosticsAsync();StatusMessage="已删除进程映射";
        }

        private async Task RefreshDiagnosticsAsync()
        {
            await RefreshDashboardAsync(false, false);
            await LoadDiagnosticsAsync();
            StatusMessage = "诊断信息已更新";
        }

        private async Task SyncDeviceStatesAsync()
        {
            var result=await plugin.RequestAsync<DeviceStateSyncResultDto>(MessageTypes.SyncDeviceStates,new { },TimeSpan.FromMinutes(5));
            ApplyOnUi(()=>
            {
                Replace(DeviceComparisons,result.Comparisons);
                DeviceStateMessage=result.StatusMessage;
            });
            StatusMessage=result.StatusMessage;
        }

        private async Task SaveDeviceDecisionAsync()
        {
            var selected=SelectedDeviceComparison??throw new InvalidOperationException("请先选择设备比较记录。");
            var code=DeviceDecision switch{"保留两者"=>"KeepBoth","以本机为准"=>"PreferLocal","以远端为准"=>"PreferRemote",_=>"Defer"};
            var saved=await plugin.RequestAsync<DeviceConflictDecisionDto>(MessageTypes.SaveDeviceConflictDecision,new DeviceConflictDecisionDto
            {
                PlayniteId=selected.PlayniteId,RemoteDevice=selected.RemoteDevice,
                LocalBackupId=selected.LocalBackupId,RemoteBackupId=selected.RemoteBackupId,
                Decision=code,Comment=DeviceDecisionComment
            });
            selected.Decision=saved.Decision;selected.DecisionComment=saved.Comment;selected.DecidedUtc=saved.DecidedUtc;
            var index=DeviceComparisons.IndexOf(selected);
            if(index>=0)DeviceComparisons[index]=selected;
            SelectedDeviceComparison=selected;
            ConfirmSuccess("已记录人工决策；未下载、恢复、删除或覆盖任何存档");
        }

        private async Task StageRemoteBackupAsync()
        {
            var selected=SelectedDeviceComparison??throw new InvalidOperationException("请先选择包含远端备份的设备记录。");
            if(!await plugin.ConfirmAsync(
                   "下载远端备份到隔离区",
                   $"将从设备“{selected.RemoteDevice}”下载完整 Ludusavi 备份库，并在本机隔离区校验版本“{selected.RemoteBackupId}”。\n\n此步骤不会恢复或覆盖当前存档，但下载量可能较大。是否继续？",
                   "下载并校验",
                   "取消"))return;
            var staged=await plugin.RequestAsync<RemoteBackupStageResultDto>(MessageTypes.StageRemoteBackup,
                new RemoteBackupStageRequestDto
                {
                    PlayniteId=selected.PlayniteId,RemoteDevice=selected.RemoteDevice,BackupId=selected.RemoteBackupId
                },TimeSpan.FromHours(3));
            StagedRemoteBackup=staged;
            ConfirmSuccess(staged.StatusMessage);
        }

        private async Task RestoreStagedRemoteBackupAsync()
        {
            var staged=StagedRemoteBackup??throw new InvalidOperationException("请先下载并校验远端备份。");
            if(!await plugin.ConfirmAsync(
                   "从已校验的远端备份恢复",
                   $"即将恢复“{staged.GameName}”在设备“{staged.RemoteDevice}”上的版本“{staged.BackupId}”。\n\n恢复前会创建并锁定本机当前存档的 PreRestore 快照。请确认游戏、启动器和 MOD 管理器均已关闭。",
                   "创建快照并恢复",
                   "取消"))return;
            var task=await plugin.RequestAsync<TaskStatusDto>(MessageTypes.RestoreRemoteBackup,
                new RemoteRestoreRequestDto
                {
                    StagingId=staged.StagingId,ConfirmedCurrentSnapshot=true,ConfirmedGameClosed=true,
                    UserComment="Playnite remote restore wizard"
                },TimeSpan.FromMinutes(45));
            await RefreshCoreAsync(false);
            NotifyTaskResults(new[]{task});
        }

        private async Task LoadDetailsAsync(bool forceBackupHistory = false)
        {
            if (SelectedGame == null) return;
            var id = SelectedGame.PlayniteId;
            switch (CurrentWorkspace)
            {
                case WorkspaceKind.Saves:
                {
                    var backupsTask = plugin.RequestAsync<BackupVersionDto[]>(MessageTypes.ListBackups, new GameQueryDto { PlayniteId = id, Limit = 500, ForceRefresh = forceBackupHistory });
                    var candidatesTask = plugin.RequestAsync<SavePathCandidateDto[]>(MessageTypes.ListSaveCandidates, new GameQueryDto { PlayniteId = id });
                    await Task.WhenAll(backupsTask, candidatesTask);
                    ApplyOnUi(() =>
                    {
                        if (!IsSelectedGame(id)) return;
                        Replace(Backups, backupsTask.Result);
                        Replace(SaveCandidates, candidatesTask.Result);
                        SelectedBackup = Backups.FirstOrDefault();
                        SelectedCandidate = SaveCandidates.FirstOrDefault(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                                            ?? SaveCandidates.FirstOrDefault();
                        RaiseCommandStates();
                    });
                    break;
                }
                case WorkspaceKind.Media:
                {
                    var mediaTask = plugin.RequestAsync<MediaItemDto[]>(MessageTypes.ListMedia, new GameQueryDto { PlayniteId = id, Limit = 1000 });
                    var sourcesTask = plugin.RequestAsync<MediaSourceRuleDto[]>(MessageTypes.ListMediaSources, new GameQueryDto { PlayniteId = id });
                    var summaryTask = plugin.RequestAsync<MediaStorageSummaryDto>(MessageTypes.GetMediaSummary, new GameQueryDto { PlayniteId = id });
                    await Task.WhenAll(mediaTask, sourcesTask, summaryTask);
                    ApplyOnUi(() =>
                    {
                        if (!IsSelectedGame(id)) return;
                        Replace(Media, mediaTask.Result);
                        MediaView.Refresh();
                        Replace(MediaSources, sourcesTask.Result);
                        MediaSummary=summaryTask.Result;
                        SelectedMedia=Media.FirstOrDefault();
                        MediaTargetGame = Games.FirstOrDefault(x => string.Equals(x.PlayniteId, MediaTargetGame?.PlayniteId, StringComparison.OrdinalIgnoreCase))
                                          ?? SelectedGame
                                          ?? Games.FirstOrDefault();
                        RaiseCommandStates();
                    });
                    break;
                }
                case WorkspaceKind.Trainers:
                {
                    var gameTools = await plugin.RequestAsync<GameToolDto[]>(MessageTypes.ListGameTools, new GameQueryDto { PlayniteId = id });
                    ApplyOnUi(() =>
                    {
                        if (!IsSelectedGame(id)) return;
                        var selectedToolId = SelectedGameTool?.ToolId;
                        Replace(GameTools, gameTools);
                        SelectedGameTool = GameTools.FirstOrDefault(x => string.Equals(x.ToolId, selectedToolId, StringComparison.OrdinalIgnoreCase))
                                           ?? GameTools.FirstOrDefault();
                        RaiseCommandStates();
                    });
                    break;
                }
            }
        }

        public void RequestWorkspaceLoad()
        {
            if (CurrentWorkspace == WorkspaceKind.Media) Run(LoadMediaWorkspaceAsync);
            else if (CurrentWorkspace == WorkspaceKind.Maintenance) Run(LoadDiagnosticsAsync);
            else if (IsGameScopedWorkspace(CurrentWorkspace)) Run(() => LoadDetailsAsync());
        }

        private bool IsSelectedGame(string playniteId)
            => SelectedGame != null && string.Equals(SelectedGame.PlayniteId, playniteId, StringComparison.OrdinalIgnoreCase);

        private static bool IsGameScopedWorkspace(WorkspaceKind workspace)
            => workspace == WorkspaceKind.Saves || workspace == WorkspaceKind.Trainers || workspace == WorkspaceKind.Media;

        private async Task BackupSelectedAsync()
        {
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupGame, new BackupRequestDto { PlayniteIds = { SelectedGame.PlayniteId }, Force = true, Reason = "Manual" }, TimeSpan.FromMinutes(15));
            NotifyTaskResults(tasks);
            await RefreshCoreAsync(false);
            await LoadDetailsAsync();
            StatusMessage = Backups.Count > 0
                ? $"备份完成，已读取 {Backups.Count} 个历史版本"
                : "备份完成，但历史索引仍为空；请打开诊断页查看 Ludusavi 输出。";
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
            await PrepareGameToolImportAsync(dialog.FileName,type);
        }

        private async Task ImportGameToolFolderAsync()
        {
            var folder=plugin.PlayniteApi.Dialogs.SelectFolder();
            if(string.IsNullOrWhiteSpace(folder))return;
            await PrepareGameToolImportAsync(folder,GameToolType.Trainer);
        }

        private async Task PrepareGameToolImportAsync(string source,GameToolType type)
        {
            var inspection=await plugin.RequestAsync<GameToolImportInspectionDto>(MessageTypes.InspectGameToolImport,
                new InspectGameToolImportRequestDto{SourcePath=source,ToolType=type},TimeSpan.FromMinutes(2));
            if(inspection.Candidates.Count==1)
            {
                await ExecuteGameToolImportAsync(inspection.SourcePath,type,inspection.Candidates[0].RelativePath);
                return;
            }
            pendingGameToolImportSource=inspection.SourcePath;
            pendingGameToolImportType=type;
            Replace(ImportEntryCandidates,inspection.Candidates);
            SelectedImportEntryCandidate=ImportEntryCandidates.FirstOrDefault();
            HasPendingGameToolEntrySelection=true;
            StatusMessage=$"检测到 {ImportEntryCandidates.Count} 个可执行文件，请选择主程序";
        }

        private Task ConfirmGameToolImportAsync()
        {
            if(!HasPendingGameToolEntrySelection||SelectedImportEntryCandidate==null)
                throw new InvalidOperationException("请先选择修改器主程序。");
            return ExecuteGameToolImportAsync(pendingGameToolImportSource,pendingGameToolImportType,SelectedImportEntryCandidate.RelativePath);
        }

        private async Task ExecuteGameToolImportAsync(string source,GameToolType type,string entryFileName)
        {
            var imported=await plugin.RequestAsync<GameToolDto>(MessageTypes.ImportGameTool,new ImportGameToolRequestDto
            {
                PlayniteId=SelectedGame.PlayniteId,ToolType=type,SourcePath=source,EntryFileName=entryFileName,CopyIntoLibrary=true
            },TimeSpan.FromMinutes(5));
            ClearPendingGameToolImport();
            await LoadDetailsAsync();SelectedGameTool=GameTools.FirstOrDefault(x=>x.ToolId==imported.ToolId)??GameTools.FirstOrDefault();
            ConfirmSuccess(type==GameToolType.CheatTable?"Cheat Table 已导入，自动启动保持关闭":"修改器已导入，自动启动保持关闭");
        }

        private void ClearPendingGameToolImport()
        {
            pendingGameToolImportSource=string.Empty;
            ImportEntryCandidates.Clear();
            SelectedImportEntryCandidate=null!;
            HasPendingGameToolEntrySelection=false;
            StatusMessage="已取消本次导入";
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
            await LoadDetailsAsync();ConfirmSuccess("游戏工具设置已保存");
        }

        private async Task LaunchSelectedGameToolAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.LaunchGameTool,new GameToolCommandRequestDto{ToolId=SelectedGameTool.ToolId});
            ConfirmSuccess("已启动 "+SelectedGameTool.DisplayName);
        }

        private async Task OpenSelectedGameToolDirectoryAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.OpenGameToolDirectory,new GameToolCommandRequestDto{ToolId=SelectedGameTool.ToolId});
        }

        private async Task DeleteSelectedGameToolAsync()
        {
            var name = SelectedGameTool.DisplayName;
            if (!await plugin.ConfirmAsync(
                    "解除修改器绑定",
                    $"确认解除“{name}”与当前游戏的绑定？\n\n本地文件会保留，不会被删除。",
                    "解除绑定",
                    "取消")) return;
            await plugin.RequestAsync<object>(MessageTypes.DeleteGameTool,new GameToolCommandRequestDto{ToolId=SelectedGameTool.ToolId});
            await LoadDetailsAsync();ConfirmSuccess("已解除绑定并保留文件："+name);
        }

        private async Task SyncTrainerCatalogAsync()
        {
            var result=await plugin.RequestAsync<TrainerCatalogSyncResultDto>(MessageTypes.SyncTrainerCatalog,new{},TimeSpan.FromMinutes(2));
            ConfirmSuccess(result.Message);
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
            NotifyTaskResults(new[]{task});await LoadDetailsAsync();ShowTrainerLibrary=false;
            StatusMessage="FLiNG 修改器已下载并绑定，自动启动保持关闭";
        }

        private async Task BackupAllAsync()
        {
            var tasks = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.BackupAll, new BackupRequestDto { Force = true, Reason = "ManualAll" }, TimeSpan.FromMinutes(45));
            await RefreshCoreAsync(false);
            NotifyTaskResults(tasks);
        }

        private async Task DetectPathsAsync()
        {
            var candidates = await plugin.RequestAsync<SavePathCandidateDto[]>(MessageTypes.DetectSavePaths, new DetectionRequestDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(20));
            ConfirmSuccess(candidates.Length == 0 ? "未发现新的高可信存档路径候选" : $"发现 {candidates.Length} 个高可信存档路径候选");
            await LoadDetailsAsync();
        }

        private async Task ValidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.ValidateGame, new ValidateGameRequestDto { PlayniteId = SelectedGame.PlayniteId });
            await RefreshCoreAsync(false);
            ConfirmSuccess($"{SelectedGame.Name} 的存档校验已完成");
        }

        private async Task SavePolicyAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.UpdateGamePolicy, new GamePolicyUpdateDto { PlayniteId = SelectedGame.PlayniteId, Policy = SelectedGame.Policy });
            ConfirmSuccess($"已保存 {SelectedGame.Name} 的游戏策略");
        }

        private async Task UpdateBackupMetadataAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.UpdateBackupMetadata, new BackupMetadataUpdateDto { PlayniteId = SelectedGame.PlayniteId, BackupId = SelectedBackup.BackupId, Comment = BackupComment, Locked = LockSelectedBackup });
            await LoadDetailsAsync();
            ConfirmSuccess("备份备注与锁定状态已保存");
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

        private async Task AcceptCandidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.AcceptSavePath, new AcceptSavePathRequestDto { PlayniteId = SelectedGame.PlayniteId, Path = SelectedCandidate.Path, IncludeSubdirectories = true });
            ConfirmSuccess("已生成 Ludusavi 自定义规则草案");
            await LoadDetailsAsync();
        }

        private async Task RejectCandidateAsync()
        {
            await plugin.RequestAsync<object>(MessageTypes.RejectSavePath, new AcceptSavePathRequestDto { PlayniteId = SelectedGame.PlayniteId, Path = SelectedCandidate.Path });
            ConfirmSuccess("已忽略该存档路径候选");
            await LoadDetailsAsync();
        }

        private async Task RestoreAsync()
        {
            if (!await plugin.ConfirmAsync(
                    "GameSaveCenter 安全恢复",
                    "恢复前会先创建并锁定当前存档的 PreRestore 快照。请确认游戏、启动器和 MOD 管理器均已关闭。\n\n继续恢复选中的历史版本？",
                    "开始安全恢复",
                    "取消")) return;
            var task = await plugin.RequestAsync<TaskStatusDto>(MessageTypes.RestoreExecute, new RestoreRequestDto
            {
                PlayniteId = SelectedGame.PlayniteId,
                BackupId = SelectedBackup.BackupId,
                ConfirmedCurrentSnapshot = true,
                ConfirmedGameClosed = true,
                UserComment = "Playnite restore wizard"
            }, TimeSpan.FromMinutes(30));
            await RefreshCoreAsync(false);
            NotifyTaskResults(new[] { task });
        }

        private async Task UndoRestoreAsync()
        {
            if (!await plugin.ConfirmAsync(
                    "撤销恢复",
                    "撤销将恢复最近的 PreRestore 快照，并且仍会先保存当前状态。确认继续？",
                    "撤销恢复",
                    "取消")) return;
            var task = await plugin.RequestAsync<TaskStatusDto>(MessageTypes.UndoRestore, new GameQueryDto { PlayniteId = SelectedGame.PlayniteId }, TimeSpan.FromMinutes(30));
            await RefreshCoreAsync(false);
            NotifyTaskResults(new[] { task });
        }

        private bool CanRetrySelectedTask()
        {
            if (SelectedTask == null) return false;
            if (SelectedTask.State != TaskState.Failed && SelectedTask.State != TaskState.Cancelled) return false;
            if (string.Equals(SelectedTask.TaskType, "MediaInbox", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(SelectedTask.GameId)) return false;
            if (string.Equals(SelectedTask.TaskType, "CloudUpload", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(SelectedTask.ErrorCode, "RCLONE_COPY_FAILED", StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(SelectedTask.TaskType, "Backup", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(SelectedTask.TaskType, "MediaSync", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RetrySelectedTaskAsync()
        {
            var task = SelectedTask ?? throw new InvalidOperationException("请先选择失败或已取消的任务。");
            if (string.Equals(task.TaskType, "CloudUpload", StringComparison.OrdinalIgnoreCase)
                || string.Equals(task.ErrorCode, "RCLONE_COPY_FAILED", StringComparison.OrdinalIgnoreCase))
            {
                var result=await plugin.RequestAsync<TaskStatusDto>(
                    MessageTypes.RetryCloudUpload,
                    new GameQueryDto{PlayniteId=task.GameId},
                    TimeSpan.FromHours(2));
                NotifyTaskResults(new[]{result});
            }
            else if (string.Equals(task.TaskType, "Backup", StringComparison.OrdinalIgnoreCase))
            {
                var result = await plugin.RequestAsync<TaskStatusDto[]>(
                    MessageTypes.BackupGame,
                    new BackupRequestDto { PlayniteIds = { task.GameId }, Force = true, Reason = "Retry" },
                    TimeSpan.FromMinutes(15));
                NotifyTaskResults(result);
            }
            else if (string.Equals(task.TaskType, "MediaSync", StringComparison.OrdinalIgnoreCase))
            {
                var request = new MediaSyncRequestDto { UploadAfterSync = plugin.Settings.EnableCloudUpload };
                request.PlayniteIds.Add(task.GameId);
                var result = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, request, TimeSpan.FromMinutes(60));
                NotifyTaskResults(result);
            }
            else if (string.Equals(task.TaskType, "MediaInbox", StringComparison.OrdinalIgnoreCase))
            {
                var result = await plugin.RequestAsync<TaskStatusDto[]>(MessageTypes.SyncMedia, new MediaSyncRequestDto
                {
                    IncludeUnassignedInbox = true,
                    SharedOnly = true,
                    UploadAfterSync = plugin.Settings.EnableCloudUpload
                }, TimeSpan.FromMinutes(60));
                NotifyTaskResults(result);
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

        private async Task CancelSelectedTaskAsync()
        {
            try
            {
                if (SelectedTask == null || !SelectedTask.CanCancel || IsCancellingTask) return;
                var taskId = SelectedTask.TaskId;
                if (!await plugin.ConfirmAsync(
                    "取消后台任务",
                    $"取消“{SelectedTask.GameName} · {SelectedTask.TaskType}”任务？\n\n取消请求会在当前文件操作的安全边界生效。",
                    "取消任务",
                    "保留任务",
                    true)) return;
                IsCancellingTask = true;
                await plugin.EnsureWorkerAsync();
                var response = await plugin.RequestAsync<CancelTaskResultDto>(MessageTypes.CancelTask, new CancelTaskRequestDto { TaskId = taskId });
                StatusMessage = response.Cancelled ? "已发送取消请求" : "任务已经结束或无法取消";
                plugin.ShowInfo(StatusMessage);
                await RefreshDashboardAsync(false, false);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
            }
            catch (Exception ex)
            {
                ReportDashboardFailure(ex, true);
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

        private void Run(Func<Task> action)
        {
            Observe(RunAsync(action));
        }

        private async Task RunAsync(Func<Task> action)
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
            catch (NotifiedTaskException ex)
            {
                ReportDashboardFailure(ex, !plugin.Settings.EnableTaskNotifications);
            }
            catch (Exception ex)
            {
                ReportDashboardFailure(ex, true);
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
                ReportDashboardFailure(ex, true);
            }
        }

        private void ReportDashboardFailure(Exception error, bool showNotification)
        {
            StatusMessage = error.Message;
            if (!showNotification)
            {
                Logger.Error(error, "GameSaveCenter dashboard command failed.");
                return;
            }

            try
            {
                plugin.ShowError(error.Message);
            }
            catch (Exception notificationError)
            {
                // A notification failure must not turn a recoverable command failure into a
                // Playnite Dispatcher crash. Preserve the original failure in the plugin log.
                Logger.Error(error, "GameSaveCenter dashboard command failed and could not be presented.");
                Logger.Error(notificationError, "GameSaveCenter failed to present dashboard command error.");
            }
        }

        private static void Observe(Task operation)
        {
            _ = operation.ContinueWith(
                task => Logger.Error(task.Exception, "GameSaveCenter dashboard command faulted outside its error boundary."),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void NotifyTaskResults(IEnumerable<TaskStatusDto> tasks)
        {
            var completed = tasks?.ToList() ?? new List<TaskStatusDto>();
            foreach (var task in completed) plugin.ShowTaskNotification(task);
            var failed = completed.FirstOrDefault(x => x.State == TaskState.Failed);
            if (failed != null) throw new NotifiedTaskException(failed.DetailMessage);
            var cancelled = completed.FirstOrDefault(x => x.State == TaskState.Cancelled);
            if (cancelled != null) throw new NotifiedTaskException(string.IsNullOrWhiteSpace(cancelled.Message) ? "任务已取消" : cancelled.Message);
        }

        private void ConfirmSuccess(string message)
        {
            StatusMessage = message;
            plugin.ShowInfo(message);
        }

        private sealed class NotifiedTaskException : InvalidOperationException
        {
            public NotifiedTaskException(string message) : base(message) { }
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

        private bool FilterTask(object item)
        {
            var task = item as TaskStatusDto;
            if (task == null) return false;
            if (TaskGameFilter != "全部" && !string.Equals(task.GameName, TaskGameFilter, StringComparison.OrdinalIgnoreCase)) return false;
            if (TaskTypeFilter != "全部" && !string.Equals(task.TaskTypeDisplay, TaskTypeFilter, StringComparison.OrdinalIgnoreCase)) return false;
            return TaskStatusFilter switch
            {
                "运行中" => task.State == TaskState.Running,
                "等待中" => task.State == TaskState.Queued || task.State == TaskState.WaitingForUser,
                "失败" => task.State == TaskState.Failed,
                "已完成" => task.State == TaskState.Succeeded || task.State == TaskState.Cancelled,
                _ => true
            };
        }

        private void RebuildTaskFilters()
        {
            var selectedGame = TaskGameFilter;
            var selectedType = TaskTypeFilter;
            Replace(TaskGameFilterOptions, new[] { "全部" }.Concat(Tasks.Select(x => x.GameName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)));
            Replace(TaskTypeFilterOptions, new[] { "全部" }.Concat(Tasks.Select(x => x.TaskTypeDisplay).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)));

            // Replacing ObservableCollection contents makes WPF clear ComboBox.SelectedItem.
            // Force a real property notification even when the logical value remains “全部”,
            // otherwise the two dynamic filters render as empty until the user selects them.
            var nextGame = TaskGameFilterOptions.Contains(selectedGame) ? selectedGame : "全部";
            var nextType = TaskTypeFilterOptions.Contains(selectedType) ? selectedType : "全部";
            taskGameFilter = string.Empty;
            taskTypeFilter = string.Empty;
            TaskGameFilter = nextGame;
            TaskTypeFilter = nextType;
            TasksView.Refresh();
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
            if (SelectedGame != null) Run(() => LoadDetailsAsync());
            else ClearSelectedGameDetails();
        }

        private void OnGamePickerStateChanged(object? sender, EventArgs e)
        {
            // Keep the preference in the plugin settings, but debounce disk writes so fast
            // keyboard search never causes one settings write per keystroke.
            plugin.Settings.GamePickerSearchText = gamePicker.SearchText;
            plugin.Settings.GamePickerStatusFilter = gamePicker.StatusFilter;
            plugin.Settings.GamePickerPlatformFilter = gamePicker.PlatformFilter;
            plugin.Settings.GamePickerSortMode = gamePicker.SortMode;
            plugin.Settings.GamePickerSelectedGameId = gamePicker.SelectedGame?.PlayniteId ?? string.Empty;
            gamePickerPersistenceCancellation?.Cancel();
            gamePickerPersistenceCancellation?.Dispose();
            gamePickerPersistenceCancellation = new CancellationTokenSource();
            var token = gamePickerPersistenceCancellation.Token;
            _ = PersistGamePickerStateAsync(token);
        }

        private void OnGamePickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(GamePickerViewModel.SelectedItem), StringComparison.Ordinal)
                && !string.Equals(e.PropertyName, nameof(GamePickerViewModel.SelectedGame), StringComparison.Ordinal)) return;
            var selected = gamePicker.SelectedGame;
            OnPropertyChanged(nameof(SelectedGame));
            RaiseCommandStates();
            if (suppressSelectionLoad) return;
            ClearSelectedGameDetails();
            if (selected != null && IsGameScopedWorkspace(CurrentWorkspace)) Run(() => LoadDetailsAsync());
        }

        private async Task PersistGamePickerStateAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(500, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                if (uiSynchronizationContext == null)
                {
                    SaveGamePickerSettings();
                    return;
                }
                uiSynchronizationContext.Post(_ =>
                {
                    if (!token.IsCancellationRequested) SaveGamePickerSettings();
                }, null);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.Error(ex, "GameSaveCenter could not persist global game picker state."); }
        }

        private void SaveGamePickerSettings()
        {
            try { plugin.SavePluginSettings(plugin.Settings); }
            catch (Exception ex) { Logger.Error(ex, "GameSaveCenter could not save global game picker settings on the UI dispatcher."); }
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
                SelectedGameToolVersion = null!;
                SelectedBackup = null!;
                SelectedCandidate = null!;
                SelectedMedia = null!;
                MediaSummary = new MediaStorageSummaryDto();
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
                UpdateMediaMetadataCommand,OpenSelectedMediaCommand,RevealSelectedMediaCommand,
                AssignInboxMediaCommand, IgnoreInboxMediaCommand,
                CancelTaskCommand, RetryTaskCommand, CopyTaskErrorCommand, RefreshDiagnosticsCommand, SyncDeviceStatesCommand, SaveDeviceDecisionCommand,
                StageRemoteBackupCommand,RestoreStagedRemoteBackupCommand,CopyDiagnosticsCommand,
                SaveProcessMappingCommand,DeleteProcessMappingCommand,
                OpenDataDirectoryCommand, OpenBackupDirectoryCommand, OpenMediaDirectoryCommand, OpenWorkerLogCommand
                ,ImportTrainerCommand,ImportCheatTableCommand,ImportToolFolderCommand,SaveGameToolCommand,LaunchGameToolCommand,
                ConfirmGameToolImportCommand,CancelGameToolImportCommand,
                OpenGameToolDirectoryCommand,DeleteGameToolCommand,SyncTrainerCatalogCommand,SearchTrainerCatalogCommand,
                LoadTrainerReleasesCommand,DownloadTrainerCommand
            }.OfType<RelayCommand>())
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private void ApplyOnUi(Action action)
        {
            var dispatcher = plugin.PlayniteApi.MainView.UIDispatcher;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            try
            {
                // Task event listeners may be on a Worker continuation. Retain synchronous
                // ordering for collection/filter updates, but never invoke a closing Playnite UI.
                dispatcher.Invoke(action, DispatcherPriority.DataBind);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Error(ex, "GameSaveCenter skipped a Dashboard UI collection update because the dispatcher is unavailable.");
            }
        }
        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
        {
            target.Clear();
            foreach (var item in source ?? Enumerable.Empty<T>()) target.Add(item);
        }
        private static string EmptyAsUnset(string value) => string.IsNullOrWhiteSpace(value) ? "（未配置）" : value;
    }
}

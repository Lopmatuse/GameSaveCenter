namespace GameSaveCenter.Contracts
{
    /// <summary>
    /// String constants for IPC message types. Strings are used instead of serialized
    /// enums so older clients can ignore newer message kinds safely.
    /// </summary>
    public static class MessageTypes
    {
        public const string Ping = "system.ping";
        public const string GetDashboard = "dashboard.get";
        public const string UpsertGames = "games.upsert";
        public const string GameSessionStarted = "session.started";
        public const string GameSessionStopped = "session.stopped";
        public const string BackupGame = "backup.game";
        public const string BackupAll = "backup.all";
        public const string ListBackups = "backup.list";
        public const string CompareBackups = "backup.compare";
        public const string PreviewRetention = "backup.retention.preview";
        public const string UpdateBackupMetadata = "backup.metadata.update";
        public const string ValidateGame = "validation.game";
        public const string GetGamePolicy = "policy.get";
        public const string UpdateGamePolicy = "policy.update";
        public const string RestorePreview = "restore.preview";
        public const string RestoreExecute = "restore.execute";
        public const string UndoRestore = "restore.undo";
        public const string SyncMedia = "media.sync";
        public const string ListMedia = "media.list";
        public const string ListUnassignedMedia = "media.inbox.list";
        public const string ReassignMedia = "media.reassign";
        public const string IgnoreMedia = "media.inbox.ignore";
        public const string AddMediaSource = "media.source.add";
        public const string ListMediaSources = "media.source.list";
        public const string DetectSavePaths = "detection.savePaths";
        public const string ListSaveCandidates = "detection.candidates.list";
        public const string AcceptSavePath = "detection.accept";
        public const string RejectSavePath = "detection.reject";
        public const string GetTasks = "tasks.get";
        public const string GetTaskChanges = "tasks.changes";
        public const string WaitForTaskChanges = "tasks.changes.wait";
        public const string RetryCloudUpload = "cloud.upload.retry";
        public const string SyncDeviceStates = "devices.state.sync";
        public const string ListProcessMappings = "processMappings.list";
        public const string SaveProcessMapping = "processMappings.save";
        public const string DeleteProcessMapping = "processMappings.delete";
        public const string GetLogs = "logs.get";
        public const string UpdateSettings = "settings.update";
        public const string GetSettings = "settings.get";
        public const string CancelTask = "task.cancel";
        public const string ListGameTools = "tools.list";
        public const string InspectGameToolImport = "tools.import.inspect";
        public const string ImportGameTool = "tools.import";
        public const string UpdateGameTool = "tools.update";
        public const string DeleteGameTool = "tools.delete";
        public const string LaunchGameTool = "tools.launch";
        public const string OpenGameToolDirectory = "tools.directory.open";
        public const string SyncTrainerCatalog = "trainers.catalog.sync";
        public const string SearchTrainerCatalog = "trainers.catalog.search";
        public const string GetTrainerReleases = "trainers.catalog.releases";
        public const string DownloadTrainer = "trainers.download";
        public const string TaskEvent = "event.task";
        public const string NotificationEvent = "event.notification";
        public const string WorkerStateEvent = "event.workerState";
    }
}

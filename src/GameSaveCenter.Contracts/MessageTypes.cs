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
        public const string UpdateBackupMetadata = "backup.metadata.update";
        public const string ValidateGame = "validation.game";
        public const string RestorePreview = "restore.preview";
        public const string RestoreExecute = "restore.execute";
        public const string UndoRestore = "restore.undo";
        public const string SyncMedia = "media.sync";
        public const string ListMedia = "media.list";
        public const string ReassignMedia = "media.reassign";
        public const string DetectSavePaths = "detection.savePaths";
        public const string AcceptSavePath = "detection.accept";
        public const string GetTasks = "tasks.get";
        public const string GetLogs = "logs.get";
        public const string UpdateSettings = "settings.update";
        public const string GetSettings = "settings.get";
        public const string CancelTask = "task.cancel";
        public const string TaskEvent = "event.task";
        public const string NotificationEvent = "event.notification";
        public const string WorkerStateEvent = "event.workerState";
    }
}

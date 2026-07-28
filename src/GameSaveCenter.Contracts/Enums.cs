namespace GameSaveCenter.Contracts
{
    /// <summary>Supported platform/source categories.</summary>
    public enum GamePlatformKind
    {
        Unknown = 0,
        Steam = 1,
        Xbox = 2,
        Epic = 3,
        Ubisoft = 4,
        Ea = 5,
        Gog = 6,
        Other = 99
    }

    /// <summary>How a game session was discovered.</summary>
    public enum SessionSourceKind
    {
        Playnite = 0,
        ProcessDetection = 1,
        Manual = 2
    }

    /// <summary>High-level task state used by UI and persistence.</summary>
    public enum TaskState
    {
        Queued = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
        WaitingForUser = 5
    }

    /// <summary>Severity of a validation or operational finding.</summary>
    public enum FindingSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    /// <summary>Where a media file was captured.</summary>
    public enum MediaSourceKind
    {
        Unknown = 0,
        Steam = 1,
        XboxGameBar = 2,
        WindowsScreenshot = 3,
        Epic = 4,
        Ubisoft = 5,
        Ea = 6,
        Gog = 7,
        ReShade = 8,
        Nvidia = 9,
        Amd = 10,
        GameNative = 11,
        Custom = 99
    }

    /// <summary>Media classification used by the archive.</summary>
    public enum MediaKind
    {
        Screenshot = 0,
        VideoClip = 1,
        Unknown = 99
    }


    /// <summary>Storage format requested from Ludusavi for new backups.</summary>
    public enum BackupStorageFormat
    {
        Simple = 0,
        Zip = 1
    }

    /// <summary>Restore workflow state.</summary>
    public enum RestoreState
    {
        Requested = 0,
        GameClosedVerified = 1,
        PreRestoreBackupCreated = 2,
        CloudJobsPaused = 3,
        RestoreExecuted = 4,
        PostRestoreValidated = 5,
        Completed = 6,
        Failed = 7,
        RollbackAttempted = 8,
        RolledBack = 9,
        ManualInterventionRequired = 10
    }

    public enum GameToolType
    {
        Trainer = 0,
        CheatTable = 1,
        CustomExecutable = 2
    }

    public enum GameToolSourceType
    {
        Manual = 0,
        Fling = 1,
        Other = 99
    }

    public enum GameToolLaunchTiming
    {
        AfterGameStarted = 0,
        Delayed = 1
    }
}

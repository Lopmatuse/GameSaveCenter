using System;
using System.Collections.Generic;

namespace GameSaveCenter.Contracts
{
    /// <summary>Per-game backup and synchronization policy.</summary>
    public sealed class BackupPolicyDto
    {
        public bool Enabled { get; set; } = true;
        public bool BackupOnGameStop { get; set; } = true;
        public bool BackupDuringPlay { get; set; } = true;
        public int DuringPlayIntervalMinutes { get; set; } = 30;
        public bool UploadAfterBackup { get; set; }
        public bool SyncMediaDuringPlay { get; set; } = true;
        public bool SyncMediaOnGameStop { get; set; } = true;
        public bool AllowAutomaticRestore { get; set; }
        public int KeepRecentAllHours { get; set; } = 24;
        public int KeepDailyDays { get; set; } = 30;
        public int KeepWeeklyWeeks { get; set; } = 12;
        public int KeepMonthlyMonths { get; set; } = 24;
    }

    /// <summary>Request to back up one game or all games.</summary>
    public sealed class BackupRequestDto
    {
        public List<string> PlayniteIds { get; set; } = new List<string>();
        public bool Force { get; set; }
        public string Reason { get; set; } = "Manual";
        public string SessionId { get; set; } = string.Empty;
    }

    /// <summary>Request to synchronize screenshot and video sources.</summary>
    public sealed class MediaSyncRequestDto
    {
        public List<string> PlayniteIds { get; set; } = new List<string>();
        public string SessionId { get; set; } = string.Empty;
        public bool IncludeUnassignedInbox { get; set; } = true;
        public bool UploadAfterSync { get; set; }
    }

    /// <summary>Safe restore request. Automatic restore is deliberately absent.</summary>
    public sealed class RestoreRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string BackupId { get; set; } = string.Empty;
        public bool ConfirmedCurrentSnapshot { get; set; }
        public bool ConfirmedGameClosed { get; set; }
        public string UserComment { get; set; } = string.Empty;
    }

    /// <summary>Request for save path candidate analysis.</summary>
    public sealed class DetectionRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public bool IncludeXboxWgs { get; set; } = true;
        public List<string> AdditionalRoots { get; set; } = new List<string>();
    }

    /// <summary>Background task status used by progress UI and audit history.</summary>
    public sealed class TaskStatusDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public TaskState State { get; set; }
        public int ProgressPercent { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime? StartedUtc { get; set; }
        public DateTime? FinishedUtc { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>One validation result displayed to the user.</summary>
    public sealed class ValidationFindingDto
    {
        public FindingSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }
}

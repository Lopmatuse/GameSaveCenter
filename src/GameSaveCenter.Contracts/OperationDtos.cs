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
        public DateTime CreatedLocal => CreatedUtc.ToLocalTime();
        public string StateDisplay => State switch
        {
            TaskState.Queued => "等待中",
            TaskState.Running => "执行中",
            TaskState.Succeeded => "成功",
            TaskState.Failed => "失败",
            TaskState.Cancelled => "已取消",
            TaskState.WaitingForUser => "等待确认",
            _ => State.ToString()
        };
        public string DetailMessage => State == TaskState.Failed && !string.IsNullOrWhiteSpace(ErrorMessage)
            ? $"{ErrorCode}: {ErrorMessage}"
            : Message;
        public bool CanCancel => State == TaskState.Queued || State == TaskState.Running;
        public DateTime? StartedLocal => StartedUtc?.ToLocalTime();
        public DateTime? FinishedLocal => FinishedUtc?.ToLocalTime();
        public string DurationDisplay
        {
            get
            {
                var start = StartedUtc ?? CreatedUtc;
                var end = FinishedUtc ?? DateTime.UtcNow;
                var duration = end - start;
                if (duration.TotalSeconds < 1) return "< 1 秒";
                if (duration.TotalMinutes < 1) return $"{duration.TotalSeconds:0} 秒";
                if (duration.TotalHours < 1) return $"{duration.TotalMinutes:0.#} 分钟";
                return $"{duration.TotalHours:0.#} 小时";
            }
        }
    }

    /// <summary>One validation result displayed to the user.</summary>
    public sealed class ValidationFindingDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public FindingSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }


    /// <summary>Updates one game's independent automation policy.</summary>
    public sealed class GamePolicyUpdateDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public BackupPolicyDto Policy { get; set; } = new BackupPolicyDto();
    }

    /// <summary>Compares two indexed backup manifests.</summary>
    public sealed class BackupCompareRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string LeftBackupId { get; set; } = string.Empty;
        public string RightBackupId { get; set; } = string.Empty;
    }

    /// <summary>User-defined screenshot/video source for a game or shared inbox.</summary>
    public sealed class MediaSourceRuleDto
    {
        public string SourceId { get; set; } = string.Empty;
        public string PlayniteId { get; set; } = string.Empty;
        public MediaSourceKind SourceKind { get; set; } = MediaSourceKind.Custom;
        public string RootPath { get; set; } = string.Empty;
        public string IncludePattern { get; set; } = "*";
        public bool Enabled { get; set; } = true;
        public bool SharedDirectory { get; set; }
    }

    /// <summary>Updates a Ludusavi backup comment and lock state.</summary>
    public sealed class BackupMetadataUpdateDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string BackupId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public bool? Locked { get; set; }
    }

    /// <summary>Simple per-game query used by list and undo operations.</summary>
    public sealed class GameQueryDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public int Limit { get; set; } = 200;
    }

    /// <summary>Request to cancel a background task.</summary>
    public sealed class CancelTaskRequestDto
    {
        public string TaskId { get; set; } = string.Empty;
    }

    /// <summary>Result returned after requesting task cancellation.</summary>
    public sealed class CancelTaskResultDto
    {
        public bool Cancelled { get; set; }
    }

    /// <summary>Effective non-secret Worker settings used by diagnostics UI.</summary>
    public sealed class WorkerSettingsSnapshotDto
    {
        public string DataDirectory { get; set; } = string.Empty;
        public string LudusaviExecutable { get; set; } = string.Empty;
        public string LudusaviBackupDirectory { get; set; } = string.Empty;
        public string RcloneExecutable { get; set; } = string.Empty;
        public bool RcloneDestinationConfigured { get; set; }
        public string MediaArchiveDirectory { get; set; } = string.Empty;
        public int ProcessPollingSeconds { get; set; } = 5;
        public int DefaultBackupIntervalMinutes { get; set; } = 30;
        public bool EnableProcessDetection { get; set; } = true;
        public bool EnableMediaSync { get; set; } = true;
        public bool EnableCloudUpload { get; set; }
        public BackupStorageFormat BackupFormat { get; set; } = BackupStorageFormat.Zip;
        public string Compression { get; set; } = "zstd";
        public int CompressionLevel { get; set; } = 3;
        public int FullBackupLimit { get; set; } = 3;
        public int DifferentialBackupLimit { get; set; } = 5;
    }

    /// <summary>Moves an indexed media item to another game without touching the original capture.</summary>
    public sealed class ReassignMediaRequestDto
    {
        public string MediaId { get; set; } = string.Empty;
        public string TargetPlayniteId { get; set; } = string.Empty;
    }

    /// <summary>Accepts a detected save directory and creates a custom Ludusavi rule draft.</summary>
    public sealed class AcceptSavePathRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IncludeSubdirectories { get; set; } = true;
    }

    /// <summary>Requests an immediate validity check for one game's latest backup.</summary>
    public sealed class ValidateGameRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
    }

    /// <summary>Non-secret runtime settings supplied by the Playnite plugin.</summary>
    public sealed class WorkerSettingsDto
    {
        public string LudusaviExecutable { get; set; } = string.Empty;
        public string LudusaviBackupDirectory { get; set; } = string.Empty;
        public string RcloneExecutable { get; set; } = string.Empty;
        public string RcloneDestination { get; set; } = string.Empty;
        public string MediaArchiveDirectory { get; set; } = string.Empty;
        public int ProcessPollingSeconds { get; set; } = 5;
        public int DefaultBackupIntervalMinutes { get; set; } = 30;
        public bool EnableProcessDetection { get; set; } = true;
        public bool EnableMediaSync { get; set; } = true;
        public bool EnableCloudUpload { get; set; }
        public BackupStorageFormat BackupFormat { get; set; } = BackupStorageFormat.Zip;
        public string Compression { get; set; } = "zstd";
        public int CompressionLevel { get; set; } = 3;
        public int FullBackupLimit { get; set; } = 3;
        public int DifferentialBackupLimit { get; set; } = 5;
    }

}

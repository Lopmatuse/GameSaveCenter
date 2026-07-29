using System;
using System.Collections.Generic;
using System.IO;

namespace GameSaveCenter.Contracts
{
    /// <summary>Dashboard snapshot returned in a single request.</summary>
    public sealed class DashboardSnapshotDto
    {
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
        public DateTime GeneratedLocal => GeneratedUtc.ToLocalTime();
        public bool WorkerHealthy { get; set; }
        public string WorkerVersion { get; set; } = string.Empty;
        public bool LudusaviAvailable { get; set; }
        public bool RcloneAvailable { get; set; }
        public string LudusaviVersion { get; set; } = string.Empty;
        public string LudusaviExecutable { get; set; } = string.Empty;
        public string LudusaviBackupDirectory { get; set; } = string.Empty;
        public BackupStorageFormat BackupFormat { get; set; } = BackupStorageFormat.Zip;
        public int ManagedGames { get; set; }
        public int MatchedGames { get; set; }
        public int RunningGames { get; set; }
        public int WarningGames { get; set; }
        public int PendingCloudTasks { get; set; }
        public int UnassignedMediaCount { get; set; }
        public List<GameStatusDto> Games { get; set; } = new List<GameStatusDto>();
        public List<TaskStatusDto> RecentTasks { get; set; } = new List<TaskStatusDto>();
        public List<ValidationFindingDto> Findings { get; set; } = new List<ValidationFindingDto>();
        public List<AuditLogEntryDto> RecentAudit { get; set; } = new List<AuditLogEntryDto>();
    }

    /// <summary>Backup metadata presented in the timeline and restore wizard.</summary>
    public sealed class BackupVersionDto
    {
        public string BackupId { get; set; } = string.Empty;
        public string PlayniteId { get; set; } = string.Empty;
        public string LudusaviName { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public long TotalBytes { get; set; }
        public int FileCount { get; set; }
        public bool IsLocked { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string SourceDevice { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public bool IsPreRestore { get; set; }
        public DateTime CreatedLocal => CreatedUtc.ToLocalTime();
        public string SizeDisplay => FormatBytes(TotalBytes);

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024d:0.##} KiB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.##} MiB";
            return $"{bytes / 1024d / 1024d / 1024d:0.##} GiB";
        }
    }

    /// <summary>Human-readable manifest difference between two backups.</summary>
    public sealed class BackupDiffDto
    {
        public string LeftBackupId { get; set; } = string.Empty;
        public string RightBackupId { get; set; } = string.Empty;
        public List<string> Added { get; set; } = new List<string>();
        public List<string> Removed { get; set; } = new List<string>();
        public List<string> Modified { get; set; } = new List<string>();
        public int UnchangedCount { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>Retention recommendation. Deletion is never implied by this DTO.</summary>
    public sealed class RetentionPreviewDto
    {
        public List<string> KeepBackupIds { get; set; } = new List<string>();
        public List<string> DeleteCandidateIds { get; set; } = new List<string>();
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>Persisted audit message exposed to the Playnite log page.</summary>
    public sealed class AuditLogEntryDto
    {
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string DetailJson { get; set; } = "{}";
        public DateTime CreatedUtc { get; set; }
        public DateTime CreatedLocal => CreatedUtc.ToLocalTime();
    }

    /// <summary>Detected save path that still requires a user decision.</summary>
    public sealed class SavePathCandidateDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public double Score { get; set; }
        public List<string> Reasons { get; set; } = new List<string>();
        public string Status { get; set; } = "Pending";
        public string ReasonsDisplay => string.Join("；", Reasons ?? new List<string>());
        public string StatusDisplay => Status == "Accepted" ? "已接受" : Status == "Rejected" ? "已忽略" : "待确认";
    }

    /// <summary>Media item indexed by the Worker.</summary>
    public sealed class MediaItemDto
    {
        public string MediaId { get; set; } = string.Empty;
        public string PlayniteId { get; set; } = string.Empty;
        public MediaKind Kind { get; set; }
        public MediaSourceKind Source { get; set; }
        public string ArchivePath { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string CloudState { get; set; } = "Pending";
        public string ClassificationState { get; set; } = "Assigned";
        public string ClassificationReason { get; set; } = string.Empty;
        public DateTime CapturedLocal => CapturedUtc.ToLocalTime();
        public string FileName => Path.GetFileName(string.IsNullOrWhiteSpace(OriginalPath) ? ArchivePath ?? string.Empty : OriginalPath);
        public string SizeDisplay => FormatBytes(SizeBytes);
        public string KindDisplay => Kind == MediaKind.VideoClip ? "录像" : Kind == MediaKind.Screenshot ? "截图" : "未知媒体";
        public string SourceDisplay => Source switch
        {
            MediaSourceKind.Steam => "Steam",
            MediaSourceKind.XboxGameBar => "Xbox Game Bar",
            MediaSourceKind.WindowsScreenshot => "Windows 截图",
            MediaSourceKind.Epic => "Epic",
            MediaSourceKind.Ubisoft => "Ubisoft",
            MediaSourceKind.Ea => "EA",
            MediaSourceKind.Gog => "GOG",
            MediaSourceKind.ReShade => "ReShade",
            MediaSourceKind.Nvidia => "NVIDIA",
            MediaSourceKind.Amd => "AMD",
            MediaSourceKind.GameNative => "游戏内截图",
            MediaSourceKind.Custom => "自定义来源",
            _ => "其他来源"
        };
        public string CloudStateDisplay => CloudState switch
        {
            "Uploaded" => "已上传",
            "Failed" => "失败",
            "Pending" => "待上传",
            _ => string.IsNullOrWhiteSpace(CloudState) ? "未启用" : CloudState
        };
        public string ClassificationStateDisplay => string.Equals(ClassificationState, "Inbox", StringComparison.OrdinalIgnoreCase)
            ? "待归类"
            : string.Equals(ClassificationState, "Ignored", StringComparison.OrdinalIgnoreCase)
                ? "已忽略"
                : "已归类";

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024d:0.##} KiB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.##} MiB";
            return $"{bytes / 1024d / 1024d / 1024d:0.##} GiB";
        }
    }

    /// <summary>Indexed storage totals for the selected game's assigned media.</summary>
    public sealed class MediaStorageSummaryDto
    {
        public int TotalCount { get; set; }
        public int ScreenshotCount { get; set; }
        public int VideoCount { get; set; }
        public int FavoriteCount { get; set; }
        public long TotalBytes { get; set; }
        public string TotalSizeDisplay => FormatBytes(TotalBytes);

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024d:0.##} KiB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.##} MiB";
            return $"{bytes / 1024d / 1024d / 1024d:0.##} GiB";
        }
    }
}

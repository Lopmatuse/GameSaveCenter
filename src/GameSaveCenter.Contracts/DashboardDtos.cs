using System;
using System.Collections.Generic;

namespace GameSaveCenter.Contracts
{
    /// <summary>Dashboard snapshot returned in a single request.</summary>
    public sealed class DashboardSnapshotDto
    {
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
        public bool WorkerHealthy { get; set; }
        public string WorkerVersion { get; set; } = string.Empty;
        public bool LudusaviAvailable { get; set; }
        public bool RcloneAvailable { get; set; }
        public int ManagedGames { get; set; }
        public int MatchedGames { get; set; }
        public int RunningGames { get; set; }
        public int WarningGames { get; set; }
        public int PendingCloudTasks { get; set; }
        public int UnassignedMediaCount { get; set; }
        public List<GameStatusDto> Games { get; set; } = new List<GameStatusDto>();
        public List<TaskStatusDto> RecentTasks { get; set; } = new List<TaskStatusDto>();
        public List<ValidationFindingDto> Findings { get; set; } = new List<ValidationFindingDto>();
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
    }
}

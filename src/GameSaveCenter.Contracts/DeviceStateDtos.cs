using System;
using System.Collections.Generic;

namespace GameSaveCenter.Contracts
{
    /// <summary>A content-free summary of the newest local backup for one game.</summary>
    public sealed class DeviceBackupSummaryDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string BackupId { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public long TotalBytes { get; set; }
        public int FileCount { get; set; }
    }

    /// <summary>Small JSON sidecar shared between devices. It deliberately contains no save files or credentials.</summary>
    public sealed class DeviceStateSidecarDto
    {
        public int SchemaVersion { get; set; } = 1;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
        public List<DeviceBackupSummaryDto> Backups { get; set; } = new List<DeviceBackupSummaryDto>();
    }

    /// <summary>Read-only comparison result. A conflict is never resolved automatically.</summary>
    public sealed class DeviceConflictStatusDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string RemoteDevice { get; set; } = string.Empty;
        public string LocalBackupId { get; set; } = string.Empty;
        public string RemoteBackupId { get; set; } = string.Empty;
        public bool HasConflict { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string SuggestedBackupId { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public DateTime LocalCreatedUtc { get; set; }
        public DateTime RemoteCreatedUtc { get; set; }
        public string Decision { get; set; } = string.Empty;
        public string DecisionComment { get; set; } = string.Empty;
        public DateTime? DecidedUtc { get; set; }
        public string StateDisplay => HasConflict ? "需要人工决定" : "一致或仅单端存在";
        public string DecisionDisplay => Decision switch
        {
            "KeepBoth" => "保留两者",
            "PreferLocal" => "记录为优先本机",
            "PreferRemote" => "记录为优先远端",
            "Defer" => "稍后处理",
            _ => "尚未记录"
        };
        public string ReasonDisplay => Reason switch
        {
            "DifferentDevicesChangedWithinTenMinutes" => "两台设备在十分钟内产生不同备份",
            "DivergentDeviceSummaries" => "两台设备的最新备份摘要不同",
            "EquivalentSummary" => "摘要相同",
            "OnlyOneSideAvailable" => "仅一台设备存在该备份摘要",
            _ => string.IsNullOrWhiteSpace(Reason) ? "未知状态" : Reason
        };
    }

    /// <summary>Records a human decision only; it never downloads, restores, deletes or overwrites a backup.</summary>
    public sealed class DeviceConflictDecisionDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string RemoteDevice { get; set; } = string.Empty;
        public string LocalBackupId { get; set; } = string.Empty;
        public string RemoteBackupId { get; set; } = string.Empty;
        public string Decision { get; set; } = "Defer";
        public string Comment { get; set; } = string.Empty;
        public DateTime DecidedUtc { get; set; }
    }

    public sealed class DeviceStateSyncResultDto
    {
        public string LocalDevice { get; set; } = string.Empty;
        public DateTime GeneratedUtc { get; set; }
        public bool Uploaded { get; set; }
        public int RemoteSidecarsRead { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public List<DeviceConflictStatusDto> Comparisons { get; set; } = new List<DeviceConflictStatusDto>();
    }

    /// <summary>Downloads one remote device's complete Ludusavi vault into an isolated local staging directory.</summary>
    public sealed class RemoteBackupStageRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string RemoteDevice { get; set; } = string.Empty;
        public string BackupId { get; set; } = string.Empty;
    }

    /// <summary>Opaque handle for a verified, isolated remote backup vault.</summary>
    public sealed class RemoteBackupStageResultDto
    {
        public string StagingId { get; set; } = string.Empty;
        public string PlayniteId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string RemoteDevice { get; set; } = string.Empty;
        public string BackupId { get; set; } = string.Empty;
        public DateTime StagedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public bool Verified { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
    }

    /// <summary>Explicit restore request for a previously verified remote staging vault.</summary>
    public sealed class RemoteRestoreRequestDto
    {
        public string StagingId { get; set; } = string.Empty;
        public bool ConfirmedCurrentSnapshot { get; set; }
        public bool ConfirmedGameClosed { get; set; }
        public string UserComment { get; set; } = string.Empty;
    }
}

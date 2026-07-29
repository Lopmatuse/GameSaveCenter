using System;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Core.Services
{
    /// <summary>Detects likely divergent save histories between devices.</summary>
    public sealed class DeviceConflictDetector
    {
        public DeviceConflict Detect(BackupSnapshot? local, BackupSnapshot? remote)
        {
            if (local == null || remote == null)
            {
                return new DeviceConflict { HasConflict = false, Reason = "OnlyOneSideAvailable", Confidence = 1 };
            }

            if (string.Equals(local.SourceDevice, remote.SourceDevice, StringComparison.OrdinalIgnoreCase))
            {
                return new DeviceConflict { HasConflict = false, Reason = "SameDevice", Confidence = 0.9 };
            }

            var sameContent = local.TotalBytes == remote.TotalBytes && local.FileCount == remote.FileCount;
            if (sameContent)
            {
                return new DeviceConflict { HasConflict = false, Reason = "EquivalentSummary", Confidence = 0.75 };
            }

            var timeDifference = (local.CreatedUtc - remote.CreatedUtc).Duration();
            if (timeDifference <= TimeSpan.FromMinutes(10))
            {
                return new DeviceConflict
                {
                    HasConflict = true,
                    Reason = "DifferentDevicesChangedWithinTenMinutes",
                    Confidence = 0.95
                };
            }

            // A newer timestamp alone is not enough to choose a winner. We only suggest
            // the newer version with modest confidence and still require user review.
            var preferred = local.CreatedUtc > remote.CreatedUtc ? local.BackupId : remote.BackupId;
            return new DeviceConflict
            {
                HasConflict = true,
                Reason = "DivergentDeviceSummaries",
                PreferredBackupId = preferred,
                Confidence = 0.65
            };
        }
    }
}

using System;
using System.Collections.Generic;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Core.Services
{
    /// <summary>
    /// Generates explainable warnings after a backup. It never deletes a backup or
    /// blocks a restore by itself; the orchestrator decides how to react.
    /// </summary>
    public sealed class BackupValidationService
    {
        public IReadOnlyList<ValidationFinding> Validate(
            BackupSnapshot current,
            BackupSnapshot? previous,
            TimeSpan? playedDuration,
            bool sourcePathExists)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var findings = new List<ValidationFinding>();

            if (!sourcePathExists)
            {
                findings.Add(Finding(FindingSeverity.Error, "SAVE_PATH_MISSING",
                    "存档路径不存在", "已配置的存档路径在备份时不可访问。",
                    "重新扫描路径；Xbox 游戏需检查 WGS 或包目录。"));
            }

            if (current.FileCount == 0)
            {
                findings.Add(Finding(FindingSeverity.Critical, "EMPTY_BACKUP",
                    "备份没有包含任何文件", "本次任务执行完成，但文件数为 0。",
                    "不要执行恢复；先确认游戏实际存档位置。"));
            }

            foreach (var file in current.Files)
            {
                if (file.SizeBytes == 0)
                {
                    findings.Add(Finding(FindingSeverity.Warning, "ZERO_BYTE_FILE",
                        "发现零字节文件", file.RelativePath,
                        "确认该文件是否本来就应为空；必要时锁定上一个正常版本。"));
                }
            }

            if (previous != null && previous.FileCount > 0)
            {
                var fileRatio = (double)current.FileCount / previous.FileCount;
                var sizeRatio = previous.TotalBytes == 0 ? 1 : (double)current.TotalBytes / previous.TotalBytes;

                if (fileRatio < 0.5)
                {
                    findings.Add(Finding(FindingSeverity.Error, "FILE_COUNT_DROP",
                        "存档文件数量异常下降",
                        $"从 {previous.FileCount} 个下降到 {current.FileCount} 个。",
                        "检查游戏是否切换了存档槽、账号或路径。"));
                }

                if (sizeRatio < 0.35)
                {
                    findings.Add(Finding(FindingSeverity.Error, "BACKUP_SIZE_DROP",
                        "存档总体积异常下降",
                        $"当前体积仅为上个版本的 {sizeRatio:P0}。",
                        "优先保留并锁定上一个版本，确认当前存档可正常读取。"));
                }

                if (current.TotalBytes == previous.TotalBytes && current.FileCount == previous.FileCount &&
                    playedDuration.HasValue && playedDuration.Value >= TimeSpan.FromHours(1))
                {
                    findings.Add(Finding(FindingSeverity.Warning, "NO_CHANGE_AFTER_LONG_SESSION",
                        "长时间游玩后未检测到存档变化",
                        $"本次会话约 {playedDuration.Value.TotalMinutes:F0} 分钟，但备份摘要没有变化。",
                        "确认是否退出时才写盘，或当前匹配的是配置/缓存而非存档。"));
                }
            }

            if (findings.Count == 0)
            {
                findings.Add(Finding(FindingSeverity.Info, "VALIDATION_OK",
                    "备份基础校验通过", "未发现文件数量、大小或路径异常。", string.Empty));
            }

            return findings;
        }

        private static ValidationFinding Finding(FindingSeverity severity, string code, string title, string detail, string action)
        {
            return new ValidationFinding
            {
                Severity = severity,
                Code = code,
                Title = title,
                Detail = detail,
                SuggestedAction = action
            };
        }
    }
}

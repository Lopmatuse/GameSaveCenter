using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

namespace GameSaveCenter.Contracts
{
    public sealed class GameToolDto
    {
        public string ToolId { get; set; } = string.Empty;
        public string PlayniteId { get; set; } = string.Empty;
        public GameToolType ToolType { get; set; }
        public GameToolSourceType SourceType { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool AutoStart { get; set; }
        public GameToolLaunchTiming LaunchTiming { get; set; } = GameToolLaunchTiming.Delayed;
        public int LaunchDelaySeconds { get; set; } = 8;
        public bool CloseOnGameExit { get; set; }
        public bool RequiresAdmin { get; set; }
        public string ActiveVersionId { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public List<GameToolVersionDto> Versions { get; set; } = new List<GameToolVersionDto>();
        public GameToolVersionDto ActiveVersion => Versions.Find(x => x.VersionId == ActiveVersionId)
                                                       ?? (Versions.Count > 0 ? Versions[0] : new GameToolVersionDto());
        public string TypeDisplay => ToolType == GameToolType.CheatTable ? "Cheat Table"
            : ToolType == GameToolType.Trainer ? "修改器" : "自定义工具";
        public string SourceDisplay => SourceType == GameToolSourceType.Fling ? "FLiNG"
            : SourceType == GameToolSourceType.Manual ? "手动导入" : "其他来源";
        public string FileStateDisplay => ActiveVersion.IsAvailable ? "已就绪" : "文件缺失";
    }

    public sealed class GameToolVersionDto
    {
        public string VersionId { get; set; } = string.Empty;
        public string ToolId { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string EntryPath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string FileSha256 { get; set; } = string.Empty;
        public DateTime? DownloadUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool IsAvailable { get; set; }
        public string FileName => Path.GetFileName(EntryPath ?? string.Empty);
    }

    public sealed class ImportGameToolRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public GameToolType ToolType { get; set; } = GameToolType.Trainer;
        public string SourcePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string EntryFileName { get; set; } = string.Empty;
        public bool CopyIntoLibrary { get; set; } = true;
    }

    public sealed class InspectGameToolImportRequestDto
    {
        public string SourcePath { get; set; } = string.Empty;
        public GameToolType ToolType { get; set; } = GameToolType.Trainer;
    }

    public sealed class GameToolEntryCandidateDto
    {
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string FileName => Path.GetFileName(RelativePath ?? string.Empty);
        public string SizeDisplay => SizeBytes < 1024 * 1024
            ? $"{SizeBytes / 1024d:0.#} KiB"
            : $"{SizeBytes / 1024d / 1024d:0.#} MiB";
        public string Display => $"{RelativePath} · {SizeDisplay}";
    }

    public sealed class GameToolImportInspectionDto
    {
        public string SourcePath { get; set; } = string.Empty;
        public GameToolType ToolType { get; set; }
        public List<GameToolEntryCandidateDto> Candidates { get; set; } = new List<GameToolEntryCandidateDto>();
        public bool RequiresSelection => Candidates.Count > 1;
    }

    public sealed class UpdateGameToolRequestDto
    {
        public string ToolId { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public bool AutoStart { get; set; }
        public GameToolLaunchTiming LaunchTiming { get; set; } = GameToolLaunchTiming.Delayed;
        public int LaunchDelaySeconds { get; set; } = 8;
        public bool CloseOnGameExit { get; set; }
        public bool RequiresAdmin { get; set; }
        public string ActiveVersionId { get; set; } = string.Empty;
    }

    public sealed class GameToolCommandRequestDto
    {
        public string ToolId { get; set; } = string.Empty;
    }

    public sealed class TrainerCatalogItemDto
    {
        public string CatalogId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string NormalizedTitle { get; set; } = string.Empty;
        public string PageUrl { get; set; } = string.Empty;
        public string GameVersion { get; set; } = string.Empty;
        public int OptionCount { get; set; }
        public DateTime? LastUpdatedUtc { get; set; }
        public DateTime LastSyncedUtc { get; set; }
    }

    public sealed class TrainerReleaseDto
    {
        public string ReleaseId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime? PublishedUtc { get; set; }
        public string SizeDisplay => SizeBytes <= 0 ? "未知大小"
            : SizeBytes < 1024 * 1024 ? $"{SizeBytes / 1024d:0.#} KiB"
            : $"{SizeBytes / 1024d / 1024d:0.#} MiB";
        public string VersionDisplay
        {
            get
            {
                var version = Regex.Match(DisplayName ?? string.Empty, @"(?i)\bv\d+(?:\.\d+)+(?:-\d+)?\b");
                return version.Success ? version.Value : "可下载版本";
            }
        }
        public string OptionCountDisplay
        {
            get
            {
                var options = Regex.Match(DisplayName ?? string.Empty, @"(?i)(?:plus|trainer)[\s._-]*(\d+)");
                return options.Success ? $"+{options.Groups[1].Value} 项" : string.Empty;
            }
        }
        public string PublishedDisplay => PublishedUtc.HasValue ? PublishedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd") : "日期未知";
    }

    public sealed class TrainerCatalogQueryDto
    {
        public string Query { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public int Limit { get; set; } = 50;
    }

    public sealed class DownloadTrainerRequestDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string CatalogId { get; set; } = string.Empty;
        public string ReleaseId { get; set; } = string.Empty;
    }

    public sealed class TrainerCatalogSyncResultDto
    {
        public int ItemCount { get; set; }
        public DateTime SyncedUtc { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

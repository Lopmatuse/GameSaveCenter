using System;
using System.Collections.Generic;

namespace GameSaveCenter.Contracts
{
    /// <summary>
    /// Playnite-neutral game descriptor sent to the Worker. It contains only the
    /// fields required for matching, process detection and display.
    /// </summary>
    public sealed class GameDescriptorDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public GamePlatformKind Platform { get; set; }
        public string PlatformGameId { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public List<GameActionDto> Actions { get; set; } = new List<GameActionDto>();
        public List<string> KnownProcessNames { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>Serializable launch action used to learn original and MOD launch paths.</summary>
    public sealed class GameActionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public bool IsPlayAction { get; set; }
        public bool IsModLoader { get; set; }
    }

    /// <summary>Event indicating that a game session was started or discovered.</summary>
    public sealed class GameSessionEventDto
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public string PlayniteId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public SessionSourceKind Source { get; set; }
        public int? ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string LaunchProfile { get; set; } = string.Empty;
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? StoppedUtc { get; set; }
        public long ElapsedSeconds { get; set; }
    }

    /// <summary>Summarized game state displayed by the dashboard.</summary>
    public sealed class GameStatusDto
    {
        public string PlayniteId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public GamePlatformKind Platform { get; set; }
        public bool IsRunning { get; set; }
        public bool LudusaviMatched { get; set; }
        public string LudusaviName { get; set; } = string.Empty;
        public DateTime? LastBackupUtc { get; set; }
        public int BackupVersionCount { get; set; }
        public DateTime? LastMediaSyncUtc { get; set; }
        public int MediaCount { get; set; }
        public string CloudState { get; set; } = "Disabled";
        public string HealthState { get; set; } = "Unknown";
        public BackupPolicyDto Policy { get; set; } = new BackupPolicyDto();
    }
}

using System;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>
    /// Lightweight, UI-only projection used by the global game picker. It deliberately
    /// does not hold a Playnite Game object or trigger any Worker request.
    /// </summary>
    public sealed class GamePickerItem
    {
        public GamePickerItem(GameStatusDto game)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
        }

        public GameStatusDto Game { get; }
        public string PlayniteId => Game.PlayniteId;
        public string Name => Game.Name ?? string.Empty;
        public string PlatformDisplay => Game.PlatformDisplay;
        public bool IsInstalled => Game.IsInstalled;
        public bool IsRunning => Game.IsRunning;
        public bool IsMatched => Game.LudusaviMatched;
        public bool HasBackups => Game.BackupVersionCount > 0;
        public bool NeedsAttention => IsAttention(Game);
        public int BackupVersionCount => Game.BackupVersionCount;
        public int MediaCount => Game.MediaCount;
        public DateTime? LastBackupUtc => Game.LastBackupUtc;
        public DateTime? LastPlayedUtc => Game.LastPlayedUtc;
        /// <summary>Recent-play-first with a useful backup timestamp fallback for older caches.</summary>
        public DateTime? RecentActivityUtc => Game.LastPlayedUtc ?? Game.LastBackupUtc;
        public string HealthStateDisplay => Game.HealthStateDisplay;
        public string CloudStateDisplay => Game.CloudStateDisplay;
        public string SearchText => string.Join(" ", Name, Game.LudusaviName, PlatformDisplay, HealthStateDisplay, CloudStateDisplay);

        public override string ToString() => Name;

        public static bool IsAttention(GameStatusDto game)
            => string.Equals(game.HealthState, "Attention", StringComparison.OrdinalIgnoreCase)
               || string.Equals(game.HealthState, "Warning", StringComparison.OrdinalIgnoreCase)
               || string.Equals(game.HealthState, "LudusaviUnavailable", StringComparison.OrdinalIgnoreCase);
    }
}

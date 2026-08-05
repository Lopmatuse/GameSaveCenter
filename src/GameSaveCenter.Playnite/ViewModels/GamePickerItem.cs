using System;
using System.Globalization;
using System.Linq;
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
        public string InstallStateDisplay => Game.InstallStateDisplay;
        public string MatchStateDisplay => Game.MatchStateDisplay;
        public string HealthState => Game.HealthState ?? string.Empty;
        public string Initials => CreateInitials(Name);
        public string MetaDisplay => JoinNonEmpty(" · ", PlatformDisplay, InstallStateDisplay, MatchStateDisplay);
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

        private static string CreateInitials(string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0) return "?";

            var parts = value.Split(new[] { ' ', '-', '_', '.', ':', '/', '\\', '·' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (FirstTextElement(parts[0]) + FirstTextElement(parts[1])).ToUpperInvariant();

            var enumerator = StringInfo.GetTextElementEnumerator(value);
            var result = string.Empty;
            while (enumerator.MoveNext() && result.Length < 2)
                result += enumerator.GetTextElement();
            return result.ToUpperInvariant();
        }

        private static string FirstTextElement(string value)
        {
            var enumerator = StringInfo.GetTextElementEnumerator(value ?? string.Empty);
            return enumerator.MoveNext() ? enumerator.GetTextElement() ?? string.Empty : string.Empty;
        }

        private static string JoinNonEmpty(string separator, params string[] values)
            => values == null
                ? string.Empty
                : string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));

        public static bool IsAttention(GameStatusDto game)
            => string.Equals(game.HealthState, "Attention", StringComparison.OrdinalIgnoreCase)
               || string.Equals(game.HealthState, "Warning", StringComparison.OrdinalIgnoreCase)
               || string.Equals(game.HealthState, "LudusaviUnavailable", StringComparison.OrdinalIgnoreCase);
    }
}

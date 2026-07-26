using System;
using System.Collections.Generic;
using System.Linq;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Core.Services
{
    /// <summary>Matches Playnite games to Ludusavi manifest names.</summary>
    public sealed class GameMatcher
    {
        /// <summary>
        /// Returns the best candidate and a confidence from 0 to 1. Platform IDs are
        /// matched first; normalized title similarity is used only as a fallback.
        /// </summary>
        public GameMatchResult Match(GameProfile game, IEnumerable<LudusaviGameIdentity> candidates)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var list = candidates.ToList();
            if (!string.IsNullOrWhiteSpace(game.PlatformGameId))
            {
                var idMatch = list.FirstOrDefault(x => x.PlatformIds.Any(id =>
                    string.Equals(id, game.PlatformGameId, StringComparison.OrdinalIgnoreCase)));
                if (idMatch != null)
                {
                    return new GameMatchResult(idMatch.Name, 1.0, "PlatformId");
                }
            }

            var normalizedGame = NameNormalizer.Normalize(game.Name);
            var exact = list.FirstOrDefault(x => NameNormalizer.Normalize(x.Name) == normalizedGame);
            if (exact != null)
            {
                return new GameMatchResult(exact.Name, 0.95, "NormalizedExactTitle");
            }

            var best = list
                .Select(x => new { Candidate = x, Score = Similarity(normalizedGame, NameNormalizer.Normalize(x.Name)) })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            if (best == null || best.Score < 0.72)
            {
                return GameMatchResult.None;
            }

            return new GameMatchResult(best.Candidate.Name, best.Score, "TitleSimilarity");
        }

        private static double Similarity(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return 0;
            if (left == right) return 1;

            var distance = LevenshteinDistance(left, right);
            return 1.0 - (double)distance / Math.Max(left.Length, right.Length);
        }

        private static int LevenshteinDistance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (var j = 0; j <= right.Length; j++) previous[j] = j;

            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= right.Length; j++)
                {
                    var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[right.Length];
        }
    }

    /// <summary>Ludusavi identity available for matching.</summary>
    public sealed class LudusaviGameIdentity
    {
        public string Name { get; set; } = string.Empty;
        public List<string> PlatformIds { get; set; } = new List<string>();
    }

    /// <summary>Immutable match outcome.</summary>
    public sealed class GameMatchResult
    {
        public static readonly GameMatchResult None = new GameMatchResult(string.Empty, 0, "None");

        public GameMatchResult(string ludusaviName, double confidence, string method)
        {
            LudusaviName = ludusaviName;
            Confidence = confidence;
            Method = method;
        }

        public string LudusaviName { get; }
        public double Confidence { get; }
        public string Method { get; }
        public bool Matched => Confidence > 0;
    }
}

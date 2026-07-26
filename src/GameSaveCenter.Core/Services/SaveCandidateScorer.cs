using System;
using System.Collections.Generic;
using System.IO;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Core.Services
{
    /// <summary>Scores changed directories as possible save locations.</summary>
    public sealed class SaveCandidateScorer
    {
        private static readonly HashSet<string> SaveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".sav", ".save", ".dat", ".bin", ".profile", ".slot", ".json", ".xml"
        };

        private static readonly HashSet<string> CacheExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".log", ".tmp", ".cache", ".dmp", ".shader", ".bak.tmp"
        };

        /// <summary>
        /// Creates an explainable score. The output is a candidate only; it must be
        /// confirmed by the user before becoming an active Ludusavi custom rule.
        /// </summary>
        public SavePathCandidate Score(
            string directory,
            IEnumerable<string> changedFiles,
            bool changedNearSessionEnd,
            bool repeatedAcrossSessions,
            bool locatedInXboxWgs)
        {
            var candidate = new SavePathCandidate
            {
                Path = directory ?? string.Empty,
                ChangedNearSessionEnd = changedNearSessionEnd,
                RepeatedAcrossSessions = repeatedAcrossSessions,
                LocatedInXboxWgs = locatedInXboxWgs
            };

            foreach (var file in changedFiles ?? Array.Empty<string>())
            {
                candidate.ChangedFileCount++;
                var extension = Path.GetExtension(file);
                if (SaveExtensions.Contains(extension)) candidate.SaveLikeExtensionCount++;
                if (CacheExtensions.Contains(extension)) candidate.CacheLikeExtensionCount++;
            }

            var score = 0.0;
            if (candidate.ChangedFileCount > 0)
            {
                score += Math.Min(0.2, candidate.ChangedFileCount * 0.02);
                candidate.Reasons.Add($"游戏会话期间有 {candidate.ChangedFileCount} 个文件发生变化");
            }

            if (candidate.SaveLikeExtensionCount > 0)
            {
                score += Math.Min(0.35, candidate.SaveLikeExtensionCount * 0.08);
                candidate.Reasons.Add($"包含 {candidate.SaveLikeExtensionCount} 个常见存档扩展名文件");
            }

            if (changedNearSessionEnd)
            {
                score += 0.2;
                candidate.Reasons.Add("文件在游戏退出附近发生变化");
            }

            if (repeatedAcrossSessions)
            {
                score += 0.25;
                candidate.Reasons.Add("连续多次游戏会话均出现相同变化模式");
            }

            if (locatedInXboxWgs)
            {
                score += 0.1;
                candidate.Reasons.Add("目录位于 Xbox WGS 存档结构中");
            }

            if (candidate.CacheLikeExtensionCount > candidate.SaveLikeExtensionCount)
            {
                score -= 0.25;
                candidate.Reasons.Add("缓存/日志类文件多于存档类文件，已降低可信度");
            }

            if (LooksLikeCacheDirectory(directory))
            {
                score -= 0.3;
                candidate.Reasons.Add("路径名称疑似缓存、日志、着色器或崩溃目录");
            }

            candidate.Score = Math.Max(0, Math.Min(1, score));
            return candidate;
        }

        private static bool LooksLikeCacheDirectory(string path)
        {
            var value = (path ?? string.Empty).ToLowerInvariant();
            return value.Contains("cache") || value.Contains("shader") || value.Contains("logs") ||
                   value.Contains("crash") || value.Contains("temp") || value.Contains("screenshots");
        }
    }
}

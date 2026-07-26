using System;
using System.Collections.Generic;
using System.Linq;
using GameSaveCenter.Core.Models;

namespace GameSaveCenter.Core.Services
{
    /// <summary>Compares two file manifests using path, size, write time and hash.</summary>
    public sealed class FileManifestDiffService
    {
        public FileManifestDiff Compare(IEnumerable<FileManifestEntry> before, IEnumerable<FileManifestEntry> after)
        {
            var oldMap = (before ?? Enumerable.Empty<FileManifestEntry>())
                .ToDictionary(x => NormalizePath(x.RelativePath), StringComparer.OrdinalIgnoreCase);
            var newMap = (after ?? Enumerable.Empty<FileManifestEntry>())
                .ToDictionary(x => NormalizePath(x.RelativePath), StringComparer.OrdinalIgnoreCase);
            var result = new FileManifestDiff();

            foreach (var item in newMap)
            {
                if (!oldMap.TryGetValue(item.Key, out var oldEntry))
                {
                    result.Added.Add(item.Value);
                    continue;
                }

                if (IsEquivalent(oldEntry, item.Value)) result.Unchanged.Add(item.Value);
                else result.Modified.Add(item.Value);
            }

            foreach (var item in oldMap)
            {
                if (!newMap.ContainsKey(item.Key)) result.Removed.Add(item.Value);
            }

            return result;
        }

        private static bool IsEquivalent(FileManifestEntry left, FileManifestEntry right)
        {
            if (left.SizeBytes != right.SizeBytes) return false;
            if (!string.IsNullOrWhiteSpace(left.Sha256) && !string.IsNullOrWhiteSpace(right.Sha256))
            {
                return string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase);
            }

            return left.LastWriteUtc == right.LastWriteUtc;
        }

        private static string NormalizePath(string path) => (path ?? string.Empty).Replace('/', '\').TrimStart('\');
    }
}

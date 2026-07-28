namespace GameSaveCenter.Core.Services
{
    /// <summary>Pure path validation shared by archive import code and unit tests.</summary>
    public static class ArchivePathGuard
    {
        public static string ResolveEntryPath(string destinationDirectory, string entryName)
        {
            if (string.IsNullOrWhiteSpace(destinationDirectory)) throw new ArgumentException("Destination is required.", nameof(destinationDirectory));
            var root = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(Path.Combine(root, entryName ?? string.Empty));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive entry escapes the destination directory.");
            return target;
        }
    }
}

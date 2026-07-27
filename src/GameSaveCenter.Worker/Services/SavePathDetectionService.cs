using System.Collections.Concurrent;
using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Finds save-path candidates through bounded recent-change scans and optional game-session
/// before/after snapshots. Candidates are never activated until the user confirms them.
/// </summary>
public sealed class SavePathDetectionService
{
    private static readonly HashSet<string> SaveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sav", ".save", ".dat", ".bin", ".slot", ".profile", ".json", ".xml",
        ".ini", ".cfg", ".db", ".sqlite", ".vdf"
    };

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".log", ".tmp", ".cache", ".dmp", ".shader", ".etl", ".evtx"
    };

    private static readonly string[] IgnoredDirectoryNames =
    {
        "Cache", "Caches", "Temp", "Logs", "Crash", "Crashes", "ShaderCache", "GPUCache",
        "Code Cache", "node_modules", ".git", "Screenshots", "Video", "Videos"
    };

    private const int MaximumSnapshotFiles = 12000;
    private const int MaximumDirectories = 4500;
    private readonly WorkerOptions _options;
    private readonly GameCatalogService _catalog;
    private readonly SqliteStateStore _store;
    private readonly ILogger<SavePathDetectionService> _logger;
    private readonly SaveCandidateScorer _scorer = new();
    private readonly ConcurrentDictionary<string, Task> _captureTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public SavePathDetectionService(
        WorkerOptions options,
        GameCatalogService catalog,
        SqliteStateStore store,
        ILogger<SavePathDetectionService> logger)
    {
        _options = options;
        _catalog = catalog;
        _store = store;
        _logger = logger;
    }

    public async Task<List<SavePathCandidateDto>> DetectAsync(DetectionRequestDto request, CancellationToken token)
    {
        var game = await _catalog.GetGameAsync(request.PlayniteId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Game not found.");
        var output = new List<SavePathCandidateDto>();
        foreach (var root in CandidateRoots(game, request))
        {
            token.ThrowIfCancellationRequested();
            if (!Directory.Exists(root.Path)) continue;
            foreach (var directory in EnumerateBoundedDirectories(root.Path, root.Depth))
            {
                var files = ReadDirectoryFiles(directory, 500)
                    .Where(x => x.Exists && x.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-14))
                    .ToList();
                if (files.Count == 0) continue;
                var changedFiles = files.Where(x => x.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-8)).Select(x => x.FullName).ToList();
                if (changedFiles.Count == 0) continue;
                var candidate = _scorer.Score(
                    directory,
                    changedFiles,
                    files.Any(x => x.LastWriteTimeUtc > DateTime.UtcNow.AddMinutes(-15)),
                    false,
                    IsXboxWgs(directory));
                if (candidate.Score < 0.35) continue;
                var dto = new SavePathCandidateDto
                {
                    PlayniteId = game.PlayniteId,
                    Path = directory,
                    Score = candidate.Score,
                    Reasons = candidate.Reasons
                };
                output.Add(dto);
                await _store.AddSaveCandidateAsync(game.PlayniteId, directory, candidate.Score, JsonSerializer.Serialize(candidate.Reasons), token).ConfigureAwait(false);
            }
        }
        return output.OrderByDescending(x => x.Score).Take(50).ToList();
    }

    /// <summary>Starts a non-blocking snapshot for an unmatched game session.</summary>
    public void BeginSessionCapture(GameSessionEventDto session)
    {
        if (!_options.EnableSessionSavePathDetection || string.IsNullOrWhiteSpace(session.SessionId)) return;
        var task = CaptureSessionStartAsync(session, CancellationToken.None);
        _captureTasks[session.SessionId] = task;
        _ = task.ContinueWith(
            completed =>
            {
                _captureTasks.TryRemove(session.SessionId, out _);
                if (completed.Exception != null)
                    _logger.LogWarning(completed.Exception, "Session save-path snapshot failed for {Game}", session.GameName);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async Task<int> AnalyzeSessionStopAsync(GameSessionEventDto session, CancellationToken token)
    {
        if (!_options.EnableSessionSavePathDetection || string.IsNullOrWhiteSpace(session.SessionId)) return 0;
        if (_captureTasks.TryGetValue(session.SessionId, out var capture))
        {
            try { await capture.WaitAsync(TimeSpan.FromMinutes(2), token).ConfigureAwait(false); }
            catch (TimeoutException) { _logger.LogWarning("Session snapshot capture timed out for {Game}", session.GameName); }
        }

        var snapshotPath = SnapshotPath(session.SessionId);
        if (!File.Exists(snapshotPath)) return 0;
        SessionSnapshot? before;
        try
        {
            before = JsonSerializer.Deserialize<SessionSnapshot>(await File.ReadAllTextAsync(snapshotPath, token).ConfigureAwait(false), _json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read session snapshot {Snapshot}", snapshotPath);
            return 0;
        }
        if (before == null) return 0;

        var game = await _catalog.GetGameAsync(session.PlayniteId, token).ConfigureAwait(false);
        if (game == null) return 0;
        var after = await CaptureAsync(game, session.SessionId, token).ConfigureAwait(false);
        var changed = after.Files.Values
            .Where(file => !before.Files.TryGetValue(file.Path, out var old)
                           || old.Length != file.Length
                           || old.LastWriteUtcTicks != file.LastWriteUtcTicks)
            .Where(file => file.LastWriteUtcTicks >= before.CapturedUtc.AddMinutes(-2).Ticks)
            .ToList();

        var existing = await _store.GetSaveCandidatesAsync(game.PlayniteId, token).ConfigureAwait(false);
        var created = 0;
        foreach (var group in changed.GroupBy(x => Path.GetDirectoryName(x.Path) ?? string.Empty).Where(x => x.Key.Length > 0))
        {
            token.ThrowIfCancellationRequested();
            var paths = group.Select(x => x.Path).Take(200).ToList();
            var repeated = existing.Any(x => string.Equals(NormalizePath(x.Path), NormalizePath(group.Key), StringComparison.OrdinalIgnoreCase));
            var nearEnd = group.Any(x => new DateTime(x.LastWriteUtcTicks, DateTimeKind.Utc) >= (session.StoppedUtc ?? DateTime.UtcNow).AddMinutes(-10));
            var candidate = _scorer.Score(group.Key, paths, nearEnd, repeated, IsXboxWgs(group.Key));
            if (candidate.Score < 0.35) continue;
            var newCount = group.Count(x => !before.Files.ContainsKey(x.Path));
            var modifiedCount = group.Count() - newCount;
            candidate.Reasons.Insert(0, $"会话前后快照发现新增 {newCount}、修改 {modifiedCount} 个文件");
            await _store.AddSaveCandidateAsync(game.PlayniteId, group.Key, candidate.Score, JsonSerializer.Serialize(candidate.Reasons), token).ConfigureAwait(false);
            created++;
        }

        TryDelete(snapshotPath);
        await _store.AppendAuditAsync(
            "Detection",
            created > 0 ? $"游戏退出后发现 {created} 个存档路径候选" : "游戏退出后未发现高可信存档路径候选",
            JsonSerializer.Serialize(new { session.SessionId, session.PlayniteId, changedFiles = changed.Count, candidates = created }),
            token).ConfigureAwait(false);
        return created;
    }

    public async Task<object> AcceptAsync(AcceptSavePathRequestDto request, CancellationToken token)
    {
        var game = await _catalog.GetGameAsync(request.PlayniteId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Game not found.");
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.Path));
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException(fullPath);
        var drafts = Path.Combine(_options.DataDirectory, "CustomRuleDrafts");
        Directory.CreateDirectory(drafts);
        var safeName = string.Concat(game.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var draftPath = Path.Combine(drafts, $"{safeName}-{game.PlayniteId}.json");
        var draft = new
        {
            game = game.Name,
            playniteId = game.PlayniteId,
            path = fullPath,
            includeSubdirectories = request.IncludeSubdirectories,
            note = "Review and import into Ludusavi custom games. GameSaveCenter does not silently alter Ludusavi configuration."
        };
        await File.WriteAllTextAsync(draftPath, JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }), token).ConfigureAwait(false);
        await _store.SetSaveCandidateStatusAsync(game.PlayniteId, fullPath, "Accepted", token).ConfigureAwait(false);
        await _store.AppendAuditAsync("Detection", "Accepted save path", JsonSerializer.Serialize(new { game.PlayniteId, fullPath, draftPath }), token).ConfigureAwait(false);
        return new { accepted = true, draftPath };
    }

    public async Task<object> RejectAsync(AcceptSavePathRequestDto request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.PlayniteId)) throw new ArgumentException("PlayniteId is required.");
        if (string.IsNullOrWhiteSpace(request.Path)) throw new ArgumentException("Candidate path is required.");
        var normalized = Path.GetFullPath(Environment.ExpandEnvironmentVariables(request.Path));
        await _store.SetSaveCandidateStatusAsync(request.PlayniteId, normalized, "Rejected", token).ConfigureAwait(false);
        await _store.AppendAuditAsync(
            "Detection",
            "Rejected save path candidate",
            JsonSerializer.Serialize(new { request.PlayniteId, path = normalized }),
            token).ConfigureAwait(false);
        return new { rejected = true };
    }

    public Task CleanupExpiredSnapshotsAsync(CancellationToken token)
        => Task.Run(() =>
        {
            if (!Directory.Exists(_options.DetectionSnapshotDirectory)) return;
            foreach (var file in Directory.EnumerateFiles(_options.DetectionSnapshotDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-2)) File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not remove expired detection snapshot {Snapshot}", file);
                }
            }
        }, token);

    private async Task CaptureSessionStartAsync(GameSessionEventDto session, CancellationToken token)
    {
        var matches = await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
        if (matches.TryGetValue(session.PlayniteId, out var match) && !string.IsNullOrWhiteSpace(match.Name)) return;
        var game = await _catalog.GetGameAsync(session.PlayniteId, token).ConfigureAwait(false);
        if (game == null) return;
        var snapshot = await CaptureAsync(game, session.SessionId, token).ConfigureAwait(false);
        Directory.CreateDirectory(_options.DetectionSnapshotDirectory);
        var path = SnapshotPath(session.SessionId);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot, _json), token).ConfigureAwait(false);
        File.Move(temporary, path, true);
        await _store.AppendAuditAsync(
            "Detection",
            "已记录未匹配游戏的会话前文件快照",
            JsonSerializer.Serialize(new { session.SessionId, session.PlayniteId, files = snapshot.Files.Count }),
            token).ConfigureAwait(false);
    }

    private Task<SessionSnapshot> CaptureAsync(GameDescriptorDto game, string sessionId, CancellationToken token)
        => Task.Run(() =>
        {
            var snapshot = new SessionSnapshot
            {
                SessionId = sessionId,
                PlayniteId = game.PlayniteId,
                CapturedUtc = DateTime.UtcNow
            };
            foreach (var root in CandidateRoots(game, new DetectionRequestDto { PlayniteId = game.PlayniteId, IncludeXboxWgs = true }))
            {
                if (!Directory.Exists(root.Path)) continue;
                foreach (var directory in EnumerateBoundedDirectories(root.Path, root.Depth))
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var file in ReadDirectoryFiles(directory, 300))
                    {
                        if (snapshot.Files.Count >= MaximumSnapshotFiles) return snapshot;
                        if (!IsPotentialSaveFile(file, root.IsInstallRoot, IsXboxWgs(directory))) continue;
                        snapshot.Files[file.FullName] = new SnapshotFile
                        {
                            Path = file.FullName,
                            Length = file.Length,
                            LastWriteUtcTicks = file.LastWriteTimeUtc.Ticks
                        };
                    }
                }
            }
            return snapshot;
        }, token);

    private IEnumerable<RootSpec> CandidateRoots(GameDescriptorDto game, DetectionRequestDto request)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return new RootSpec(Path.Combine(profile, "Saved Games"), 4, false);
        yield return new RootSpec(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 3, false);
        yield return new RootSpec(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 3, false);
        yield return new RootSpec(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 3, false);
        if (!string.IsNullOrWhiteSpace(game.InstallDirectory)) yield return new RootSpec(game.InstallDirectory, 4, true);
        if (request.IncludeXboxWgs && game.Platform == GamePlatformKind.Xbox)
            yield return new RootSpec(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"), 6, false);
        foreach (var root in request.AdditionalRoots)
            yield return new RootSpec(Environment.ExpandEnvironmentVariables(root), 4, false);
    }

    private static IEnumerable<string> EnumerateBoundedDirectories(string root, int depth)
    {
        var queue = new Queue<(string Path, int Depth)>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue((Path.GetFullPath(root), 0));
        var visited = 0;
        while (queue.Count > 0 && visited < MaximumDirectories)
        {
            var item = queue.Dequeue();
            if (!visitedPaths.Add(item.Path)) continue;
            visited++;
            yield return item.Path;
            if (item.Depth >= depth) continue;
            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(item.Path).Take(300).ToList(); }
            catch { continue; }
            foreach (var child in children)
            {
                if (IgnoredDirectoryNames.Any(x => string.Equals(Path.GetFileName(child), x, StringComparison.OrdinalIgnoreCase))) continue;
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                }
                catch { continue; }
                queue.Enqueue((child, item.Depth + 1));
            }
        }
    }

    private static IEnumerable<FileInfo> ReadDirectoryFiles(string directory, int limit)
    {
        try { return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).Take(limit).Select(x => new FileInfo(x)).Where(x => x.Exists).ToList(); }
        catch { return Array.Empty<FileInfo>(); }
    }

    private static bool IsPotentialSaveFile(FileInfo file, bool installRoot, bool xboxWgs)
    {
        if (file.Length < 0 || file.Length > 512L * 1024 * 1024) return false;
        var extension = file.Extension;
        if (IgnoredExtensions.Contains(extension)) return false;
        if (xboxWgs || SaveExtensions.Contains(extension)) return true;
        if (installRoot && file.Length <= 64L * 1024 * 1024) return true;
        return string.IsNullOrWhiteSpace(extension) && file.Length <= 64L * 1024 * 1024;
    }

    private string SnapshotPath(string sessionId) => Path.Combine(_options.DetectionSnapshotDirectory, sessionId + ".json");
    private static bool IsXboxWgs(string path) => path.Contains("SystemAppData", StringComparison.OrdinalIgnoreCase) && path.Contains("wgs", StringComparison.OrdinalIgnoreCase);
    private static string NormalizePath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private static void TryDelete(string path) { try { File.Delete(path); } catch { } }

    private sealed record RootSpec(string Path, int Depth, bool IsInstallRoot);

    private sealed class SessionSnapshot
    {
        public string SessionId { get; set; } = string.Empty;
        public string PlayniteId { get; set; } = string.Empty;
        public DateTime CapturedUtc { get; set; }
        public Dictionary<string, SnapshotFile> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SnapshotFile
    {
        public string Path { get; set; } = string.Empty;
        public long Length { get; set; }
        public long LastWriteUtcTicks { get; set; }
    }
}

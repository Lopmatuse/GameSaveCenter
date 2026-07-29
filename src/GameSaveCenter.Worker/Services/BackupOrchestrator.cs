using System.Text.Json;
using GameSaveCenter.Contracts;
using GameSaveCenter.Core.Services;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;

namespace GameSaveCenter.Worker.Services;

/// <summary>Coordinates safe Ludusavi backups, validation, history indexing and optional upload.</summary>
public sealed class BackupOrchestrator
{
    private readonly GameCatalogService _catalog;
    private readonly SqliteStateStore _store;
    private readonly LudusaviClient _ludusavi;
    private readonly RcloneClient _rclone;
    private readonly CloudTransferCoordinator _cloudTransfers;
    private readonly TaskCoordinator _tasks;
    private readonly WorkerOptions _options;
    private readonly BackupValidationService _validator = new();

    public BackupOrchestrator(
        GameCatalogService catalog,
        SqliteStateStore store,
        LudusaviClient ludusavi,
        RcloneClient rclone,
        CloudTransferCoordinator cloudTransfers,
        TaskCoordinator tasks,
        WorkerOptions options)
    {
        _catalog = catalog;
        _store = store;
        _ludusavi = ludusavi;
        _rclone = rclone;
        _cloudTransfers = cloudTransfers;
        _tasks = tasks;
        _options = options;
    }

    public async Task<List<TaskStatusDto>> BackupAsync(BackupRequestDto request, CancellationToken token)
    {
        var games = await _catalog.GetGamesAsync(token).ConfigureAwait(false);
        var matches = await _catalog.GetMatchesAsync(token).ConfigureAwait(false);
        if (request.PlayniteIds.Count > 0)
        {
            games = games
                .Where(x => request.PlayniteIds.Contains(x.PlayniteId, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        var results = new List<TaskStatusDto>();
        var requestLabel = GetRequestLabel(request.Reason);
        foreach (var game in games)
        {
            if (!_ludusavi.IsAvailable)
            {
                results.Add(await _tasks.RunAsync(
                    "Backup",
                    game.PlayniteId,
                    game.Name,
                    (_, _) => Task.FromException(new WorkerOperationException(
                        "LUDUSAVI_NOT_CONFIGURED",
                        "Ludusavi 尚未配置或可执行文件不存在。",
                        _options.LudusaviExecutable)),
                    token).ConfigureAwait(false));
                continue;
            }

            if (!matches.TryGetValue(game.PlayniteId, out var match) || string.IsNullOrWhiteSpace(match.Name))
            {
                if (request.PlayniteIds.Count > 0)
                {
                    results.Add(await _tasks.RunAsync(
                        "Backup",
                        game.PlayniteId,
                        game.Name,
                        (_, _) => Task.FromException(new WorkerOperationException(
                            "LUDUSAVI_GAME_UNMATCHED",
                            "该游戏尚未匹配到 Ludusavi 存档规则。",
                            game.Name)),
                        token).ConfigureAwait(false));
                }
                continue;
            }

            results.Add(await _tasks.RunAsync(
                "Backup",
                game.PlayniteId,
                game.Name,
                async (progress, ct) =>
                {
                    await progress.ReportAsync(10, $"{requestLabel}：正在扫描存档").ConfigureAwait(false);
                    var operation = await _ludusavi
                        .BackupAsync(new[] { match.Name }, request.Force, false, ct)
                        .ConfigureAwait(false);

                    if (!operation.Success)
                    {
                        throw new WorkerOperationException(
                            operation.ErrorCode,
                            operation.ErrorMessage,
                            BuildDiagnostic(operation));
                    }

                    if (!operation.Json.HasValue)
                    {
                        throw new WorkerOperationException(
                            "LUDUSAVI_EMPTY_RESULT",
                            "Ludusavi 没有返回可解析的备份结果。",
                            operation.RawOutput);
                    }

                    if (LudusaviResultParser.SomeGamesFailed(operation.Json.Value))
                    {
                        throw new WorkerOperationException(
                            "LUDUSAVI_GAME_FAILED",
                            "Ludusavi 报告该游戏备份失败。",
                            operation.RawOutput);
                    }

                    var change = LudusaviResultParser.ParseGameChange(operation.Json.Value, match.Name);
                    await _store.AppendAuditAsync(
                        "Backup",
                        $"Ludusavi 备份结果：{game.Name} / {change}",
                        JsonSerializer.Serialize(new
                        {
                            game.PlayniteId,
                            ludusaviName = match.Name,
                            change,
                            operation.ExitCode,
                            operation.WarningText,
                            output = operation.RawOutput
                        }),
                        ct).ConfigureAwait(false);

                    await progress.ReportAsync(55, $"{requestLabel}：正在校验备份摘要").ConfigureAwait(false);
                    var now = DateTime.UtcNow;
                    var snapshot = LudusaviResultParser.ParseOperationSnapshot(
                        operation.Json.Value,
                        match.Name,
                        $"pending-{now:yyyyMMddHHmmss}",
                        now);

                    var previous = (await _store.GetBackupVersionsAsync(game.PlayniteId, ct).ConfigureAwait(false))
                        .FirstOrDefault();
                    var previousDomain = previous == null
                        ? null
                        : new GameSaveCenter.Core.Models.BackupSnapshot
                        {
                            BackupId = previous.BackupId,
                            CreatedUtc = previous.CreatedUtc,
                            FileCount = previous.FileCount,
                            TotalBytes = previous.TotalBytes
                        };

                    foreach (var finding in _validator.Validate(snapshot, previousDomain, null, true))
                    {
                        await _store.AddFindingAsync(
                            game.PlayniteId,
                            new ValidationFindingDto
                            {
                                Severity = finding.Severity,
                                Code = finding.Code,
                                Title = finding.Title,
                                Detail = finding.Detail,
                                SuggestedAction = finding.SuggestedAction
                            },
                            ct).ConfigureAwait(false);
                    }

                    await progress.ReportAsync(70, $"{requestLabel}：正在索引历史版本").ConfigureAwait(false);
                    await RefreshBackupHistoryAsync(game.PlayniteId, match.Name, ct).ConfigureAwait(false);

                    var indexed = (await _store.GetBackupVersionsAsync(game.PlayniteId, ct).ConfigureAwait(false))
                        .OrderByDescending(x => x.CreatedUtc)
                        .FirstOrDefault();
                    if (indexed == null)
                    {
                        throw new WorkerOperationException(
                            "BACKUP_HISTORY_EMPTY",
                            "本地备份已执行，但没有从 Ludusavi 读取到历史版本。",
                            operation.RawOutput);
                    }

                    indexed.TotalBytes = snapshot.TotalBytes;
                    indexed.FileCount = snapshot.FileCount;
                    await _store.AddBackupVersionAsync(indexed, JsonSerializer.Serialize(snapshot.Files), ct)
                        .ConfigureAwait(false);

                    var policy = await _store.GetPolicyAsync(game.PlayniteId, ct).ConfigureAwait(false);
                    if (_options.EnableCloudUpload && policy.UploadAfterBackup && _rclone.IsConfigured)
                    {
                        await progress.ReportAsync(82, $"{requestLabel}：正在复制到云端").ConfigureAwait(false);
                        var cloud = await _cloudTransfers.RunUploadAsync("backup", transferToken => _rclone
                            .CopyAsync(_options.LudusaviBackupDirectory, Path.Combine(Environment.MachineName, "Saves"), transferToken), ct)
                            .ConfigureAwait(false);
                        if (!cloud.Success)
                        {
                            throw new WorkerOperationException(
                                "RCLONE_COPY_FAILED",
                                "本地备份成功，但云端复制失败。",
                                cloud.StandardError);
                        }
                    }

                    var completion = change switch
                    {
                        "New" => "已创建首个备份版本",
                        "Different" => _options.BackupFormat == BackupStorageFormat.Zip
                            ? "已创建新的历史版本"
                            : "已更新 Simple 当前副本",
                        "Same" => "存档无变化，历史未新增",
                        _ => "备份完成"
                    };
                    await progress.ReportAsync(100, $"{requestLabel}：{completion}").ConfigureAwait(false);
                },
                token).ConfigureAwait(false));
        }

        if (request.PlayniteIds.Count > 0 && results.Count == 0)
        {
            throw new WorkerOperationException(
                "BACKUP_GAME_NOT_FOUND",
                "没有找到需要备份的游戏。",
                string.Join(",", request.PlayniteIds));
        }

        return results;
    }

    public async Task RefreshBackupHistoryAsync(string playniteId, string ludusaviName, CancellationToken token)
    {
        var listed = await _ludusavi.ListBackupsAsync(new[] { ludusaviName }, token).ConfigureAwait(false);
        if (!listed.Success)
        {
            throw new WorkerOperationException(
                listed.ErrorCode,
                "读取 Ludusavi 历史版本失败：" + listed.ErrorMessage,
                BuildDiagnostic(listed));
        }
        if (!listed.Json.HasValue)
        {
            throw new WorkerOperationException(
                "LUDUSAVI_BACKUPS_EMPTY_RESULT",
                "Ludusavi 没有返回历史版本 JSON。",
                listed.RawOutput);
        }

        var versions = LudusaviResultParser
            .ParseBackupList(listed.Json.Value, playniteId, ludusaviName)
            .ToList();
        var reportedCount = LudusaviResultParser.GetReportedBackupCount(listed.Json.Value, ludusaviName);
        if (reportedCount.GetValueOrDefault() > 0 && versions.Count == 0)
        {
            throw new WorkerOperationException(
                "LUDUSAVI_BACKUP_LIST_PARSE_FAILED",
                "Ludusavi 报告存在历史版本，但 GameSaveCenter 未能解析版本列表。",
                listed.RawOutput);
        }

        await _store.AppendAuditAsync(
            "BackupHistory",
            $"已同步 {ludusaviName} 的 {versions.Count} 个历史版本",
            listed.RawOutput,
            token).ConfigureAwait(false);

        await _store.RemoveMissingBackupVersionsAsync(
            playniteId,
            versions.Select(x => x.BackupId).ToArray(),
            token).ConfigureAwait(false);
        foreach (var version in versions)
        {
            await _store.AddBackupVersionAsync(version, "{}", token).ConfigureAwait(false);
        }
    }

    private static string BuildDiagnostic(LudusaviCommandResult result)
    {
        return JsonSerializer.Serialize(new
        {
            result.ExitCode,
            result.WarningText,
            result.RawOutput
        });
    }

    private static string GetRequestLabel(string? reason) => reason switch
    {
        "DuringPlay" => "游玩中定时备份",
        "GameStopped" => "退出后自动备份",
        "ManualAll" => "全部手动备份",
        "Manual" => "手动备份",
        _ => "备份"
    };
}

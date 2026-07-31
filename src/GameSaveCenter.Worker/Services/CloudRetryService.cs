using GameSaveCenter.Contracts;
using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Resumes failed backup uploads after the Worker restarts without ever touching local saves.</summary>
public sealed class CloudRetryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private readonly WorkerOptions _options;
    private readonly SqliteStateStore _store;
    private readonly BackupOrchestrator _backups;
    private readonly RcloneClient _rclone;
    private readonly ILogger<CloudRetryService> _logger;

    public CloudRetryService(WorkerOptions options, SqliteStateStore store, BackupOrchestrator backups, RcloneClient rclone, ILogger<CloudRetryService> logger)
    { _options = options; _store = store; _backups = backups; _rclone = rclone; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.EnableCloudUpload && _rclone.IsConfigured && Directory.Exists(_options.LudusaviBackupDirectory))
                {
                    var due = await _store.GetDueCloudRetriesAsync(DateTime.UtcNow, 10, stoppingToken).ConfigureAwait(false);
                    foreach (var entry in due)
                    {
                        try
                        {
                            var task = await _backups.RetryCloudUploadAsync(entry.PlayniteId, stoppingToken).ConfigureAwait(false);
                            if (task.State == TaskState.Succeeded)
                                _logger.LogInformation("Cloud retry completed for {GameId}", entry.PlayniteId);
                            else if (string.Equals(task.ErrorCode, "CLOUD_GAME_NOT_FOUND", StringComparison.Ordinal))
                            {
                                await _store.RemoveCloudRetryAsync(entry.PlayniteId, stoppingToken).ConfigureAwait(false);
                                await _store.AppendAuditAsync("CloudRetry", "已移除不存在游戏的云端重试任务", entry.PlayniteId, stoppingToken).ConfigureAwait(false);
                            }
                            else if (!string.Equals(task.ErrorCode, "RCLONE_COPY_FAILED", StringComparison.Ordinal))
                            {
                                await _store.DeferCloudRetryAsync(entry.PlayniteId, DateTime.UtcNow.AddMinutes(5),
                                    task.ErrorMessage ?? "云端上传暂不可用", stoppingToken).ConfigureAwait(false);
                            }
                        }
                        catch (WorkerOperationException ex) when (string.Equals(ex.Code, "CLOUD_GAME_NOT_FOUND", StringComparison.Ordinal))
                        {
                            await _store.RemoveCloudRetryAsync(entry.PlayniteId, stoppingToken).ConfigureAwait(false);
                            await _store.AppendAuditAsync("CloudRetry", "已移除不存在游戏的云端重试任务", entry.PlayniteId, stoppingToken).ConfigureAwait(false);
                        }
                        catch (WorkerOperationException ex)
                        {
                            await _store.DeferCloudRetryAsync(entry.PlayniteId, DateTime.UtcNow.AddMinutes(5),
                                ex.Message, stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cloud retry sweep failed; queued uploads will be retried on the next sweep");
            }

            try { await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Initializes durable storage before regular background operations begin.</summary>
public sealed class WorkerInitializationService : IHostedService
{
    private readonly SqliteStateStore _store;
    private readonly SavePathDetectionService _detection;
    private readonly ILogger<WorkerInitializationService> _logger;

    public WorkerInitializationService(SqliteStateStore store, SavePathDetectionService detection, ILogger<WorkerInitializationService> logger)
    { _store=store; _detection=detection; _logger=logger; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _store.MarkInterruptedTasksAsync(cancellationToken).ConfigureAwait(false);
        await _detection.CleanupExpiredSnapshotsAsync(cancellationToken).ConfigureAwait(false);
        var version = typeof(WorkerInitializationService).Assembly.GetName().Version?.ToString() ?? "unknown";
        _logger.LogInformation("GameSaveCenter Worker {Version} storage initialized and stale tasks reconciled", version);
    }

    public Task StopAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
}

using GameSaveCenter.Worker.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>Initializes durable storage before regular background operations begin.</summary>
public sealed class WorkerInitializationService : IHostedService
{
    private readonly SqliteStateStore _store;
    private readonly ILogger<WorkerInitializationService> _logger;

    public WorkerInitializationService(SqliteStateStore store, ILogger<WorkerInitializationService> logger)
    { _store=store; _logger=logger; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _store.MarkInterruptedTasksAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("GameSaveCenter Worker storage initialized and stale tasks reconciled");
    }

    public Task StopAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
}

using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Services;

/// <summary>
/// Serializes rclone writes and lets a restore reserve the same gate. Rclone copies shared
/// backup roots, so a global gate is safer than a per-game lock: another game's backup could
/// otherwise upload files for the game currently being restored.
/// </summary>
public sealed class CloudTransferCoordinator
{
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly ILogger<CloudTransferCoordinator> logger;

    public CloudTransferCoordinator(ILogger<CloudTransferCoordinator> logger)=>this.logger=logger;

    public async Task<T> RunUploadAsync<T>(string operation,Func<CancellationToken,Task<T>> action,CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            logger.LogDebug("Cloud transfer gate acquired for {Operation}",operation);
            return await action(token).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    /// <summary>Waits for active uploads and blocks new uploads until the returned lease is disposed.</summary>
    public async Task<IDisposable> PauseForRestoreAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        logger.LogInformation("Cloud transfer gate reserved for restore");
        return new Lease(gate,logger);
    }

    private sealed class Lease : IDisposable
    {
        private readonly SemaphoreSlim gate;
        private readonly ILogger logger;
        private int disposed;

        public Lease(SemaphoreSlim gate,ILogger logger){this.gate=gate;this.logger=logger;}

        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;
            gate.Release();
            logger.LogInformation("Cloud transfer gate released after restore");
        }
    }
}

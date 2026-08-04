using GameSaveCenter.Worker.Configuration;
using GameSaveCenter.Worker.Infrastructure;
using GameSaveCenter.Worker.Ipc;
using GameSaveCenter.Worker.Persistence;
using GameSaveCenter.Worker.Services;
using GameSaveCenter.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker;

/// <summary>Worker entry point. All long-running file and external-process work stays outside Playnite.</summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        // The Playnite extension can receive overlapping startup callbacks while the host is
        // importing a large library.  A second Worker sharing the same named pipe is not a
        // harmless duplicate: clients can land on different SQLite/process-detection state
        // and the original instance may be killed by a later health probe.  Keep one current-
        // user Worker per protocol pipe and let the existing instance serve all clients.
        using var singleInstance = new Mutex(true, "Local\\" + ProtocolConstants.PipeName, out var createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("GameSaveCenter Worker is already running; exiting duplicate instance.");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });

        var options = WorkerOptions.Load(builder.Configuration);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<SqliteStateStore>();
        builder.Services.AddSingleton<ExternalProcessRunner>();
        builder.Services.AddSingleton<LudusaviClient>();
        builder.Services.AddSingleton<RcloneClient>();
        builder.Services.AddSingleton<CloudTransferCoordinator>();
        builder.Services.AddSingleton<DeviceStateService>();
        builder.Services.AddSingleton<RemoteBackupStagingService>();
        builder.Services.AddSingleton<GameCatalogService>();
        builder.Services.AddSingleton<TaskEventBroadcaster>();
        builder.Services.AddSingleton<TaskCoordinator>();
        builder.Services.AddSingleton<BackupOrchestrator>();
        builder.Services.AddSingleton<RestoreOrchestrator>();
        builder.Services.AddSingleton<MediaSyncService>();
        builder.Services.AddSingleton<SavePathDetectionService>();
        builder.Services.AddSingleton<DashboardService>();
        builder.Services.AddSingleton<ITrainerCatalogSource,FlingTrainerCatalogSource>();
        builder.Services.AddSingleton<GameToolService>();
        builder.Services.AddSingleton<IpcRequestDispatcher>();
        builder.Services.AddHostedService<WorkerInitializationService>();
        builder.Services.AddHostedService<CloudRetryService>();
        builder.Services.AddHostedService<NamedPipeServerService>();
        builder.Services.AddHostedService<TaskEventPipeServerService>();
        builder.Services.AddSingleton<GameSessionCoordinator>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<GameSessionCoordinator>());
        builder.Services.AddHostedService<ExternalGameProcessDetector>();

        try
        {
            await builder.Build().RunAsync().ConfigureAwait(false);
        }
        finally
        {
            try { singleInstance.ReleaseMutex(); } catch (ApplicationException) { }
        }
    }
}

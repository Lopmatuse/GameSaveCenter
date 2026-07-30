using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using GameSaveCenter.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Ipc;

/// <summary>
/// Streams best-effort task events to any currently open Playnite dashboard.
/// It intentionally uses a separate, current-user-only pipe so a long-lived event
/// reader can never delay normal request/response IPC.
/// </summary>
public sealed class TaskEventPipeServerService : BackgroundService
{
    private readonly TaskEventBroadcaster broadcaster;
    private readonly ILogger<TaskEventPipeServerService> logger;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public TaskEventPipeServerService(TaskEventBroadcaster broadcaster, ILogger<TaskEventPipeServerService> logger)
    {
        this.broadcaster = broadcaster;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipe = new NamedPipeServerStream(
                    ProtocolConstants.EventPipeName,
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                    64 * 1024,
                    64 * 1024);
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                _ = StreamEventsAsync(pipe, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Task event pipe accept failed");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task StreamEventsAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        await using (pipe)
        using (var subscription = broadcaster.Subscribe())
        await using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 64 * 1024, true) { AutoFlush = true })
        {
            try
            {
                await foreach (var change in subscription.Reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    var envelope = new IpcEnvelope
                    {
                        Type = MessageTypes.TaskEvent,
                        PayloadJson = JsonSerializer.Serialize(change, json)
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(envelope, json)).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
                // The dashboard can close while a progress update is being written.
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Task event client disconnected unexpectedly");
            }
        }
    }
}

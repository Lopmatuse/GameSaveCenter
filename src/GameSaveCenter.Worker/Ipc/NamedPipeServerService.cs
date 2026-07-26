using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using GameSaveCenter.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Ipc;

/// <summary>Current-user-only newline-delimited JSON named-pipe server.</summary>
public sealed class NamedPipeServerService : BackgroundService
{
    private readonly IpcRequestDispatcher _dispatcher;
    private readonly ILogger<NamedPipeServerService> _logger;
    private readonly JsonSerializerOptions _json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=true};

    public NamedPipeServerService(IpcRequestDispatcher dispatcher,ILogger<NamedPipeServerService> logger)
    { _dispatcher=dispatcher;_logger=logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipe=new NamedPipeServerStream(ProtocolConstants.PipeName,PipeDirection.InOut,NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,64*1024,64*1024);
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                _=HandleClientAsync(pipe,stoppingToken);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){_logger.LogError(ex,"Named pipe accept failed");await Task.Delay(1000,stoppingToken).ConfigureAwait(false);}
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe,CancellationToken token)
    {
        await using (pipe)
        {
            using var reader=new StreamReader(pipe,new UTF8Encoding(false),false,64*1024,true);
            await using var writer=new StreamWriter(pipe,new UTF8Encoding(false),64*1024,true){AutoFlush=true};
            try
            {
                while(pipe.IsConnected&&!token.IsCancellationRequested)
                {
                    var line=await reader.ReadLineAsync(token).ConfigureAwait(false);if(line==null)break;
                    if(Encoding.UTF8.GetByteCount(line)>ProtocolConstants.MaximumMessageBytes)
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcEnvelope{IsResponse=true,Success=false,ErrorCode="MESSAGE_TOO_LARGE",ErrorMessage="IPC message exceeded the configured limit."},_json)).ConfigureAwait(false);continue;
                    }
                    IpcEnvelope? request;
                    try{request=JsonSerializer.Deserialize<IpcEnvelope>(line,_json);}catch(JsonException ex)
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new IpcEnvelope{IsResponse=true,Success=false,ErrorCode="INVALID_JSON",ErrorMessage=ex.Message},_json)).ConfigureAwait(false);continue;
                    }
                    if(request==null)continue;
                    var response=await _dispatcher.DispatchAsync(request,token).ConfigureAwait(false);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response,_json)).ConfigureAwait(false);
                }
            }
            catch(IOException){/* Client closed while a response was in flight. */}
            catch(OperationCanceledException) when(token.IsCancellationRequested){ }
            catch(Exception ex){_logger.LogWarning(ex,"Named pipe client failed");}
        }
    }
}

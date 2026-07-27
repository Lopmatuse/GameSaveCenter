using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameSaveCenter.Contracts;
using Newtonsoft.Json;

namespace GameSaveCenter.Playnite.Ipc
{
    /// <summary>Short-lived request/response client for the local Worker named pipe.</summary>
    public sealed class WorkerIpcClient
    {
        private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Include
        };

        public async Task<TResponse> RequestAsync<TResponse>(string type, object payload, TimeSpan? timeout = null)
        {
            var request = new IpcEnvelope
            {
                Type = type,
                PayloadJson = JsonConvert.SerializeObject(payload, jsonSettings)
            };
            var timeoutValue = timeout ?? ProtocolConstants.DefaultRequestTimeout;
            using (var cancellation = new CancellationTokenSource(timeoutValue))
            using (var pipe = new NamedPipeClientStream(".", ProtocolConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await ConnectAsync(pipe, (int)timeoutValue.TotalMilliseconds).ConfigureAwait(false);
                using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 64 * 1024, true))
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 64 * 1024, true) { AutoFlush = true })
                {
                    var line = JsonConvert.SerializeObject(request, jsonSettings);
                    if (Encoding.UTF8.GetByteCount(line) > ProtocolConstants.MaximumMessageBytes)
                        throw new InvalidOperationException("IPC request is too large.");
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                    var responseLine = await ReadLineWithCancellationAsync(reader, cancellation.Token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(responseLine)) throw new IOException("Worker closed the pipe without a response.");
                    var response = JsonConvert.DeserializeObject<IpcEnvelope>(responseLine, jsonSettings);
                    if (response == null) throw new IOException("Worker returned an invalid response.");
                    if (!response.Success) throw new WorkerRequestException(response.ErrorCode, response.ErrorMessage);
                    var payloadResult = JsonConvert.DeserializeObject<TResponse>(response.PayloadJson, jsonSettings);
                    if (payloadResult is null) throw new IOException("Worker returned an empty or incompatible payload.");
                    return payloadResult;
                }
            }
        }

        private static Task ConnectAsync(NamedPipeClientStream pipe, int timeoutMilliseconds)
        {
            // ConnectAsync overloads differ across .NET Framework versions. Running the
            // bounded synchronous call on the pool keeps Playnite's UI responsive.
            return Task.Run(() => pipe.Connect(timeoutMilliseconds));
        }

        private static async Task<string?> ReadLineWithCancellationAsync(StreamReader reader, CancellationToken token)
        {
            var read = reader.ReadLineAsync();
            var cancellation = Task.Delay(Timeout.Infinite, token);
            var completed = await Task.WhenAny(read, cancellation).ConfigureAwait(false);
            if (completed != read) throw new TimeoutException("Worker response timed out.");
            return await read.ConfigureAwait(false);
        }
    }

    /// <summary>Typed Worker error surfaced to the UI.</summary>
    public sealed class WorkerRequestException : Exception
    {
        public WorkerRequestException(string code, string message) : base(message) { Code = code ?? string.Empty; }
        public string Code { get; private set; }
    }
}

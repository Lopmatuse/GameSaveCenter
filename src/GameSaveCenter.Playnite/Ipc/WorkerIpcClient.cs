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
                    var response = JsonConvert.DeserializeObject<IpcEnvelope>(responseLine!, jsonSettings);
                    if (response == null) throw new IOException("Worker returned an invalid response.");
                    if (!response.Success) throw new WorkerRequestException(response.ErrorCode, response.ErrorMessage);
                    var payloadResult = JsonConvert.DeserializeObject<TResponse>(response.PayloadJson, jsonSettings);
                    if (payloadResult is null) throw new IOException("Worker returned an empty or incompatible payload.");
                    return payloadResult;
                }
            }
        }

        /// <summary>
        /// Reads best-effort task events from the Worker while a dashboard is open.
        /// This never replaces normal request/response calls: an unavailable event pipe
        /// simply reconnects in the background and the caller can still use SQLite-backed
        /// snapshots and the bounded change feed.
        /// </summary>
        public async Task ListenForTaskEventsAsync(Func<TaskChangeEventDto, Task> onEvent, CancellationToken token)
        {
            if (onEvent == null) throw new ArgumentNullException(nameof(onEvent));
            var retryDelay = TimeSpan.FromMilliseconds(300);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(".", ProtocolConstants.EventPipeName, PipeDirection.In, PipeOptions.Asynchronous))
                    {
                        await ConnectAsync(pipe, 3000).ConfigureAwait(false);
                        using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 64 * 1024, true))
                        {
                            retryDelay = TimeSpan.FromMilliseconds(300);
                            while (pipe.IsConnected && !token.IsCancellationRequested)
                            {
                                var line = await ReadLineWithCancellationAsync(reader, token).ConfigureAwait(false);
                                if (string.IsNullOrWhiteSpace(line)) break;
                                if (Encoding.UTF8.GetByteCount(line) > ProtocolConstants.MaximumMessageBytes) continue;
                                var envelope = JsonConvert.DeserializeObject<IpcEnvelope>(line!, jsonSettings);
                                if (envelope == null || !envelope.Success || !string.Equals(envelope.Type, MessageTypes.TaskEvent, StringComparison.Ordinal)) continue;
                                var change = JsonConvert.DeserializeObject<TaskChangeEventDto>(envelope.PayloadJson, jsonSettings);
                                if (change != null && change.Task != null) await onEvent(change).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException)
                {
                    // The Worker may be starting, restarting, or the dashboard may be closing.
                }
                catch (TimeoutException)
                {
                    // The event endpoint is optional and reconnects without surfacing UI errors.
                }
                catch (Exception)
                {
                    // Keep the event channel isolated from normal plugin operations. The durable
                    // request path will continue to provide task state and diagnostics.
                }

                try { await Task.Delay(retryDelay, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, 5000));
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

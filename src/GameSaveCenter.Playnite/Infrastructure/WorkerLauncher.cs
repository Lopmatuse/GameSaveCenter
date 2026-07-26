using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameSaveCenter.Contracts;
using GameSaveCenter.Playnite.Ipc;

namespace GameSaveCenter.Playnite.Infrastructure
{
    /// <summary>Starts the packaged Worker once and waits until its pipe responds.</summary>
    public sealed class WorkerLauncher
    {
        private readonly WorkerIpcClient client;
        public WorkerLauncher(WorkerIpcClient client) { this.client = client; }

        public async Task EnsureStartedAsync(string executable)
        {
            if (await IsHealthyAsync().ConfigureAwait(false)) return;
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                throw new FileNotFoundException("GameSaveCenter Worker was not found.", executable);
            var processName = Path.GetFileNameWithoutExtension(executable);
            if (!Process.GetProcessesByName(processName).Any())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(250).ConfigureAwait(false);
                if (await IsHealthyAsync().ConfigureAwait(false)) return;
            }
            throw new TimeoutException("Worker started but did not become ready.");
        }

        public async Task<bool> IsHealthyAsync()
        {
            try { await client.RequestAsync<object>(MessageTypes.Ping, new { }, TimeSpan.FromSeconds(2)).ConfigureAwait(false); return true; }
            catch { return false; }
        }
    }
}

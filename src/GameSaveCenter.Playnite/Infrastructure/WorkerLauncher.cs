using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameSaveCenter.Contracts;
using GameSaveCenter.Playnite.Ipc;

namespace GameSaveCenter.Playnite.Infrastructure
{
    /// <summary>
    /// Starts the packaged Worker, recovers stale instances and waits long enough
    /// for first-run SQLite/Defender initialization. Worker output is persisted for diagnostics.
    /// </summary>
    public sealed class WorkerLauncher
    {
        private static readonly object logGate = new object();
        private readonly WorkerIpcClient client;
        public WorkerLauncher(WorkerIpcClient client) { this.client = client; }

        public async Task EnsureStartedAsync(string executable)
        {
            if (await IsHealthyAsync().ConfigureAwait(false)) return;
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                throw new FileNotFoundException("未找到 GameSaveCenter Worker。", executable);

            var fullExecutable = Path.GetFullPath(executable);
            var processName = Path.GetFileNameWithoutExtension(fullExecutable);
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var runningPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(runningPath) &&
                        string.Equals(Path.GetFullPath(runningPath), fullExecutable, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch
                {
                    // A process owned by another security context is left untouched.
                }
                finally { process.Dispose(); }
            }

            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameSaveCenter", "Logs", "worker-launch.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            var worker = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fullExecutable,
                    WorkingDirectory = Path.GetDirectoryName(fullExecutable),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            worker.OutputDataReceived += (_, args) => AppendLog(logPath, args.Data);
            worker.ErrorDataReceived += (_, args) => AppendLog(logPath, args.Data);
            if (!worker.Start()) throw new InvalidOperationException("Worker 进程启动失败。");
            worker.BeginOutputReadLine();
            worker.BeginErrorReadLine();

            for (var i = 0; i < 120; i++)
            {
                await Task.Delay(250).ConfigureAwait(false);
                if (worker.HasExited)
                    throw new InvalidOperationException($"Worker 启动后立即退出，退出码 {worker.ExitCode}。日志：{logPath}");
                if (await IsHealthyAsync().ConfigureAwait(false)) return;
            }
            throw new TimeoutException($"Worker 已启动，但 30 秒内未就绪。请查看日志：{logPath}");
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                await client.RequestAsync<object>(MessageTypes.Ping, new { }, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                return true;
            }
            catch { return false; }
        }

        private static void AppendLog(string path, string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            lock (logGate)
            {
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
    }
}

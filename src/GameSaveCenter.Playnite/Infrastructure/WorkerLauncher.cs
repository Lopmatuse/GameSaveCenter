using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameSaveCenter.Contracts;
using GameSaveCenter.Playnite.Ipc;
using GameSaveCenter.Playnite.Settings;

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
        private readonly SemaphoreSlim startupGate = new SemaphoreSlim(1, 1);
        private Process? runningWorker;
        public WorkerLauncher(WorkerIpcClient client) { this.client = client; }

        public async Task EnsureStartedAsync(string executable)
        {
            if (await IsHealthyAsync().ConfigureAwait(false)) return;
            await startupGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (await IsHealthyAsync().ConfigureAwait(false)) return;
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                    throw new FileNotFoundException("未找到 GameSaveCenter Worker。", executable);
                if (!GameSaveCenterSettings.IsWorkerExecutable(executable))
                    throw new InvalidOperationException(
                        $"Worker 路径配置错误：{executable}。必须选择 GameSaveCenter.Worker.exe；Ludusavi 请填写在单独的 Ludusavi 路径中。");

                var fullExecutable = Path.GetFullPath(executable);
                var processName = Path.GetFileNameWithoutExtension(fullExecutable);
                // A Worker can be temporarily unable to answer a two-second Ping while it is
                // opening SQLite or yielding a large background Ludusavi batch.  Do not kill a
                // live instance merely because that first probe timed out: doing so loses the
                // durable request queue and creates the restart/pipe-timeout loop seen in large
                // Playnite libraries. Give the existing process a bounded grace period first.
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        var runningPath = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(runningPath) &&
                            string.Equals(Path.GetFullPath(runningPath), fullExecutable, StringComparison.OrdinalIgnoreCase))
                        {
                            // Large SQLite stores and a first-run process scan can legitimately
                            // take longer than the old 12-second grace period.  Killing that
                            // instance created the exact restart/pipe-timeout loop reported by
                            // 900+ game libraries.  Give a live, same-path Worker enough time
                            // to finish initialization before treating it as wedged.
                            var healthy = await WaitForHealthAsync(TimeSpan.FromSeconds(45)).ConfigureAwait(false);
                            if (healthy) return;

                            // Only replace a process that is still alive after the grace period.
                            // This is intentionally the last resort for a genuinely wedged or
                            // stale process, not the normal recovery path for a busy Worker.
                            if (!process.HasExited)
                            {
                                process.Kill();
                                process.WaitForExit(5000);
                            }
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
                AppendLog(logPath, $"Starting Worker: {fullExecutable}");

                runningWorker?.Dispose();
                var worker = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fullExecutable,
                        WorkingDirectory = Path.GetDirectoryName(fullExecutable),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                worker.OutputDataReceived += (_, args) => AppendLog(logPath, args.Data);
                worker.ErrorDataReceived += (_, args) => AppendLog(logPath, args.Data);
                worker.Exited += (_, __) =>
                {
                    try { AppendLog(logPath, $"Worker exited with code {worker.ExitCode}."); }
                    catch { AppendLog(logPath, "Worker exited before its exit code could be read."); }
                };
                if (!worker.Start()) throw new InvalidOperationException("Worker 进程启动失败。");
                runningWorker = worker;
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
            finally
            {
                startupGate.Release();
            }
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

        private async Task<bool> WaitForHealthAsync(TimeSpan gracePeriod)
        {
            var deadline = DateTime.UtcNow + gracePeriod;
            do
            {
                if (await IsHealthyAsync().ConfigureAwait(false)) return true;
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(500, remaining.TotalMilliseconds))).ConfigureAwait(false);
            }
            while (DateTime.UtcNow < deadline);

            return false;
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

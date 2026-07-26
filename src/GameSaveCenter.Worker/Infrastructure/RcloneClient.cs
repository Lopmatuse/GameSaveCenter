using GameSaveCenter.Worker.Configuration;

namespace GameSaveCenter.Worker.Infrastructure;

/// <summary>Safe, one-way Rclone adapter. It never calls sync, move, delete or purge.</summary>
public sealed class RcloneClient
{
    private readonly WorkerOptions _options;
    private readonly ExternalProcessRunner _runner;

    public RcloneClient(WorkerOptions options, ExternalProcessRunner runner)
    {
        _options = options;
        _runner = runner;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.RcloneExecutable) && File.Exists(_options.RcloneExecutable);
    public bool IsConfigured => IsAvailable && !string.IsNullOrWhiteSpace(_options.RcloneDestination);

    public Task<ProcessResult> CopyAsync(string localDirectory, string remoteSubPath, CancellationToken token)
    {
        if (!IsConfigured) return Task.FromResult(ProcessResult.Failed(-1, string.Empty, "Rclone is not configured."));
        var destination = CombineRemote(_options.RcloneDestination, remoteSubPath);
        return _runner.RunAsync(_options.RcloneExecutable,
            new[] { "copy", localDirectory, destination, "--checksum", "--check-first", "--create-empty-src-dirs", "--stats-one-line" },
            null, TimeSpan.FromHours(2), token);
    }

    public Task<ProcessResult> CheckAsync(string localDirectory, string remoteSubPath, CancellationToken token)
    {
        if (!IsConfigured) return Task.FromResult(ProcessResult.Failed(-1, string.Empty, "Rclone is not configured."));
        var destination = CombineRemote(_options.RcloneDestination, remoteSubPath);
        return _runner.RunAsync(_options.RcloneExecutable,
            new[] { "check", localDirectory, destination, "--one-way", "--size-only" },
            null, TimeSpan.FromHours(1), token);
    }

    public async Task<string> GetVersionAsync(CancellationToken token)
    {
        if (!IsAvailable) return string.Empty;
        var result = await _runner.RunAsync(_options.RcloneExecutable, new[] { "version" }, null, TimeSpan.FromSeconds(15), token).ConfigureAwait(false);
        return result.Success ? result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty : string.Empty;
    }

    private static string CombineRemote(string root, string child)
    {
        var separator = root.EndsWith(":", StringComparison.Ordinal) || root.EndsWith("/", StringComparison.Ordinal) ? string.Empty : "/";
        return root + separator + child.Replace('\\', '/').TrimStart('/');
    }
}

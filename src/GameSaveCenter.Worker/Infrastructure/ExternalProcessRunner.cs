using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Infrastructure;

/// <summary>Runs trusted local executables without invoking a shell.</summary>
public sealed class ExternalProcessRunner
{
    private readonly ILogger<ExternalProcessRunner> _logger;

    public ExternalProcessRunner(ILogger<ExternalProcessRunner> logger) => _logger = logger;

    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            return ProcessResult.Failed(-1, string.Empty, $"Executable not found: {executable}");
        }

        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput != null,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        _logger.LogInformation("Starting {Executable} {Arguments}", Path.GetFileName(executable), string.Join(" ", start.ArgumentList));
        process.Start();

        if (standardInput != null)
        {
            await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Cancelling external process {Executable}", Path.GetFileName(executable));
            TryKill(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return ProcessResult.Failed(-2, await stdoutTask.ConfigureAwait(false), "Process timed out.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited between checks. The original timeout result is retained.
        }
    }
}

/// <summary>External process execution result.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
    public static ProcessResult Failed(int exitCode, string stdout, string stderr) => new(exitCode, stdout, stderr);
}

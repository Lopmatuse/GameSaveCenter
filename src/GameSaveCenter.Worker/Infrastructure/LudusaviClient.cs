using System.Text.Json;
using GameSaveCenter.Worker.Configuration;
using Microsoft.Extensions.Logging;

namespace GameSaveCenter.Worker.Infrastructure;

/// <summary>Thin adapter over Ludusavi's stable command-line and JSON API output.</summary>
public sealed class LudusaviClient
{
    private readonly WorkerOptions _options;
    private readonly ExternalProcessRunner _runner;
    private readonly ILogger<LudusaviClient> _logger;

    public LudusaviClient(WorkerOptions options, ExternalProcessRunner runner, ILogger<LudusaviClient> logger)
    {
        _options = options;
        _runner = runner;
        _logger = logger;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_options.LudusaviExecutable) && File.Exists(_options.LudusaviExecutable);

    public Task<LudusaviCommandResult> BackupAsync(IEnumerable<string> games, bool force, bool preview, CancellationToken token)
    {
        var args = new List<string>
        {
            "backup", "--api", "--path", _options.LudusaviBackupDirectory, "--no-cloud-sync",
            "--format", _options.BackupFormat == GameSaveCenter.Contracts.BackupStorageFormat.Zip ? "zip" : "simple",
            "--full-limit", _options.FullBackupLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--differential-limit", _options.DifferentialBackupLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (_options.BackupFormat == GameSaveCenter.Contracts.BackupStorageFormat.Zip)
        {
            args.Add("--compression");
            args.Add(_options.Compression);
            if (string.Equals(_options.Compression, "zstd", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("--compression-level");
                args.Add(_options.CompressionLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        if (force) args.Add("--force");
        if (preview) args.Add("--preview");
        args.AddRange(games.Where(x => !string.IsNullOrWhiteSpace(x)));
        return ExecuteJsonAsync(args, token);
    }

    public Task<LudusaviCommandResult> ListBackupsAsync(IEnumerable<string> games, CancellationToken token)
    {
        var args = new List<string> { "backups", "--api", "--path", _options.LudusaviBackupDirectory };
        args.AddRange(games.Where(x => !string.IsNullOrWhiteSpace(x)));
        return ExecuteJsonAsync(args, token);
    }

    public Task<LudusaviCommandResult> RestoreAsync(string game, string backupId, bool preview, CancellationToken token)
    {
        var args = new List<string>
        {
            "restore", "--api", "--path", _options.LudusaviBackupDirectory,
            "--no-cloud-sync", "--backup", backupId
        };
        if (preview) args.Add("--preview");
        else args.Add("--force");
        args.Add(game);
        return ExecuteJsonAsync(args, token);
    }

    public Task<LudusaviCommandResult> FindAsync(string name, string platformId, bool isSteam, bool isGog, CancellationToken token)
    {
        var args = new List<string> { "find", "--api", "--multiple", "--backup" };
        if (isSteam && !string.IsNullOrWhiteSpace(platformId)) { args.Add("--steam-id"); args.Add(platformId); }
        else if (isGog && !string.IsNullOrWhiteSpace(platformId)) { args.Add("--gog-id"); args.Add(platformId); }
        else { args.Add("--normalized"); args.Add(name); }
        return ExecuteJsonAsync(args, token);
    }


    public async Task<LudusaviCommandResult> EditBackupAsync(string game, string backupId, string? comment, bool? locked, CancellationToken token)
    {
        if (!IsAvailable) return LudusaviCommandResult.Failure("LUDUSAVI_NOT_CONFIGURED", "Ludusavi executable is unavailable.");
        var input = JsonSerializer.Serialize(new
        {
            config = new { backupPath = _options.LudusaviBackupDirectory },
            requests = new[]
            {
                new { editBackup = new { game, backup = backupId, comment, locked } }
            }
        });
        var result = await _runner.RunAsync(_options.LudusaviExecutable, new[] { "api" }, input, TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
        if (!result.Success) return LudusaviCommandResult.Failure("LUDUSAVI_EDIT_EXIT", result.StandardError, result.ExitCode, result.StandardOutput);
        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            return LudusaviCommandResult.SuccessResult(document.RootElement.Clone(), result.StandardError, result.ExitCode, result.StandardOutput);
        }
        catch (JsonException ex)
        {
            return LudusaviCommandResult.Failure("LUDUSAVI_EDIT_INVALID_JSON", ex.Message, result.ExitCode, result.StandardOutput);
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken token)
    {
        if (!IsAvailable) return string.Empty;
        var result = await _runner.RunAsync(_options.LudusaviExecutable, new[] { "--version" }, null, TimeSpan.FromSeconds(15), token).ConfigureAwait(false);
        return result.Success ? result.StandardOutput.Trim() : string.Empty;
    }

    private async Task<LudusaviCommandResult> ExecuteJsonAsync(IReadOnlyCollection<string> arguments, CancellationToken token)
    {
        if (!IsAvailable) return LudusaviCommandResult.Failure("LUDUSAVI_NOT_CONFIGURED", "Ludusavi executable is unavailable.");
        Directory.CreateDirectory(_options.LudusaviBackupDirectory);
        var result = await _runner.RunAsync(_options.LudusaviExecutable, arguments, null, TimeSpan.FromMinutes(15), token).ConfigureAwait(false);
        if (!result.Success)
        {
            return LudusaviCommandResult.Failure("LUDUSAVI_EXIT", string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError, result.ExitCode);
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return LudusaviCommandResult.Failure("LUDUSAVI_EMPTY_OUTPUT", "Ludusavi returned no JSON output.", result.ExitCode);
        }

        try
        {
            using var document = JsonDocument.Parse(result.StandardOutput);
            return LudusaviCommandResult.SuccessResult(document.RootElement.Clone(), result.StandardError, result.ExitCode, result.StandardOutput);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not parse Ludusavi JSON output");
            return LudusaviCommandResult.Failure("LUDUSAVI_INVALID_JSON", ex.Message, result.ExitCode, result.StandardOutput);
        }
    }
}

/// <summary>Parsed Ludusavi invocation, preserving raw output for diagnostics.</summary>
public sealed class LudusaviCommandResult
{
    public bool Success { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public JsonElement? Json { get; init; }
    public string WarningText { get; init; } = string.Empty;
    public string RawOutput { get; init; } = string.Empty;

    public static LudusaviCommandResult SuccessResult(JsonElement json, string warnings, int exitCode, string raw = "") => new()
    { Success = true, Json = json, WarningText = warnings, ExitCode = exitCode, RawOutput = raw };

    public static LudusaviCommandResult Failure(string code, string message, int exitCode = -1, string raw = "") => new()
    { Success = false, ErrorCode = code, ErrorMessage = message, ExitCode = exitCode, RawOutput = raw };
}

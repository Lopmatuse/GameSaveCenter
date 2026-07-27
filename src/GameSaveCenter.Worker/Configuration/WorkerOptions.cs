using System.Text.Json;
using GameSaveCenter.Contracts;
using Microsoft.Extensions.Configuration;

namespace GameSaveCenter.Worker.Configuration;

/// <summary>
/// Validated Worker settings. Runtime values supplied by the Playnite plugin are
/// persisted locally so a Worker restart never silently loses the Ludusavi path.
/// Secrets remain in Rclone's own configuration.
/// </summary>
public sealed class WorkerOptions
{
    private readonly object _persistenceGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameSaveCenter");
    public string LudusaviExecutable { get; set; } = string.Empty;
    public string LudusaviBackupDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GameSaveCenter", "Saves");
    public string RcloneExecutable { get; set; } = string.Empty;
    public string RcloneDestination { get; set; } = string.Empty;
    public string MediaArchiveDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "GameSaveCenter");
    public int ProcessPollingSeconds { get; set; } = 5;
    public int DefaultBackupIntervalMinutes { get; set; } = 30;
    public bool EnableProcessDetection { get; set; } = true;
    public bool EnableMediaSync { get; set; } = true;
    public bool EnableCloudUpload { get; set; }
    public BackupStorageFormat BackupFormat { get; set; } = BackupStorageFormat.Zip;
    public string Compression { get; set; } = "zstd";
    public int CompressionLevel { get; set; } = 3;
    public int FullBackupLimit { get; set; } = 3;
    public int DifferentialBackupLimit { get; set; } = 5;

    public string DatabasePath => Path.Combine(DataDirectory, "gamesavecenter.db");
    public string LogDirectory => Path.Combine(DataDirectory, "Logs");
    public string DetectionSnapshotDirectory => Path.Combine(DataDirectory, "DetectionSnapshots");
    public string RuntimeSettingsPath => Path.Combine(DataDirectory, "worker-settings.json");

    public static WorkerOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection("GameSaveCenter").Get<WorkerOptions>() ?? new WorkerOptions();
        options.Normalize();
        options.LoadPersistedSettings();
        return options;
    }

    public void Apply(WorkerSettingsDto settings, bool persist = false)
    {
        LudusaviExecutable = Expand(settings.LudusaviExecutable);
        LudusaviBackupDirectory = Expand(settings.LudusaviBackupDirectory);
        RcloneExecutable = Expand(settings.RcloneExecutable);
        RcloneDestination = settings.RcloneDestination ?? string.Empty;
        MediaArchiveDirectory = Expand(settings.MediaArchiveDirectory);
        ProcessPollingSeconds = Math.Clamp(settings.ProcessPollingSeconds, 2, 60);
        DefaultBackupIntervalMinutes = Math.Clamp(settings.DefaultBackupIntervalMinutes, 5, 1440);
        EnableProcessDetection = settings.EnableProcessDetection;
        EnableMediaSync = settings.EnableMediaSync;
        EnableCloudUpload = settings.EnableCloudUpload;
        BackupFormat = settings.BackupFormat;
        Compression = NormalizeCompression(settings.Compression);
        CompressionLevel = Math.Clamp(settings.CompressionLevel, -7, 22);
        FullBackupLimit = Math.Clamp(settings.FullBackupLimit, 1, 255);
        DifferentialBackupLimit = Math.Clamp(settings.DifferentialBackupLimit, 0, 255);
        Normalize();
        if (persist) Persist();
    }

    public WorkerSettingsDto ToDto() => new()
    {
        LudusaviExecutable = LudusaviExecutable,
        LudusaviBackupDirectory = LudusaviBackupDirectory,
        RcloneExecutable = RcloneExecutable,
        RcloneDestination = RcloneDestination,
        MediaArchiveDirectory = MediaArchiveDirectory,
        ProcessPollingSeconds = ProcessPollingSeconds,
        DefaultBackupIntervalMinutes = DefaultBackupIntervalMinutes,
        EnableProcessDetection = EnableProcessDetection,
        EnableMediaSync = EnableMediaSync,
        EnableCloudUpload = EnableCloudUpload,
        BackupFormat = BackupFormat,
        Compression = Compression,
        CompressionLevel = CompressionLevel,
        FullBackupLimit = FullBackupLimit,
        DifferentialBackupLimit = DifferentialBackupLimit
    };

    private void Normalize()
    {
        DataDirectory = Expand(DataDirectory);
        LudusaviExecutable = Expand(LudusaviExecutable);
        LudusaviBackupDirectory = Expand(LudusaviBackupDirectory);
        RcloneExecutable = Expand(RcloneExecutable);
        RcloneDestination = Environment.ExpandEnvironmentVariables(RcloneDestination ?? string.Empty);
        MediaArchiveDirectory = Expand(MediaArchiveDirectory);
        ProcessPollingSeconds = Math.Clamp(ProcessPollingSeconds, 2, 60);
        DefaultBackupIntervalMinutes = Math.Clamp(DefaultBackupIntervalMinutes, 5, 1440);
        Compression = NormalizeCompression(Compression);
        CompressionLevel = Math.Clamp(CompressionLevel, -7, 22);
        FullBackupLimit = Math.Clamp(FullBackupLimit, 1, 255);
        DifferentialBackupLimit = Math.Clamp(DifferentialBackupLimit, 0, 255);
        Directory.CreateDirectory(DataDirectory);
        if (!string.IsNullOrWhiteSpace(LudusaviBackupDirectory)) Directory.CreateDirectory(LudusaviBackupDirectory);
        if (!string.IsNullOrWhiteSpace(MediaArchiveDirectory)) Directory.CreateDirectory(MediaArchiveDirectory);
    }

    private void LoadPersistedSettings()
    {
        try
        {
            if (!File.Exists(RuntimeSettingsPath)) return;
            var persisted = JsonSerializer.Deserialize<WorkerSettingsDto>(File.ReadAllText(RuntimeSettingsPath), JsonOptions);
            if (persisted != null) Apply(persisted, false);
        }
        catch
        {
            // Invalid runtime settings must not prevent the Worker from starting.
            // The next settings.update call will replace the damaged file atomically.
        }
    }

    private void Persist()
    {
        lock (_persistenceGate)
        {
            Directory.CreateDirectory(DataDirectory);
            var temporary = RuntimeSettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(ToDto(), JsonOptions));
            File.Move(temporary, RuntimeSettingsPath, true);
        }
    }

    private static string NormalizeCompression(string? value)
    {
        var normalized = (value ?? "zstd").Trim().ToLowerInvariant();
        return normalized is "none" or "deflate" or "bzip2" or "zstd" ? normalized : "zstd";
    }

    private static string Expand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
    }
}

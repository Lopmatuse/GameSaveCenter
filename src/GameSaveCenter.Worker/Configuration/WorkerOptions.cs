using Microsoft.Extensions.Configuration;
using GameSaveCenter.Contracts;

namespace GameSaveCenter.Worker.Configuration;

/// <summary>Validated Worker settings. Secrets remain in Rclone's own configuration.</summary>
public sealed class WorkerOptions
{
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

    public string DatabasePath => Path.Combine(DataDirectory, "gamesavecenter.db");
    public string LogDirectory => Path.Combine(DataDirectory, "Logs");
    public string DetectionSnapshotDirectory => Path.Combine(DataDirectory, "DetectionSnapshots");

    public static WorkerOptions Load(IConfiguration configuration)
    {
        var options = configuration.GetSection("GameSaveCenter").Get<WorkerOptions>() ?? new WorkerOptions();
        options.DataDirectory = Expand(options.DataDirectory);
        options.LudusaviExecutable = Expand(options.LudusaviExecutable);
        options.LudusaviBackupDirectory = Expand(options.LudusaviBackupDirectory);
        options.RcloneExecutable = Expand(options.RcloneExecutable);
        options.RcloneDestination = Environment.ExpandEnvironmentVariables(options.RcloneDestination ?? string.Empty);
        options.MediaArchiveDirectory = Expand(options.MediaArchiveDirectory);
        options.ProcessPollingSeconds = Math.Clamp(options.ProcessPollingSeconds, 2, 60);
        options.DefaultBackupIntervalMinutes = Math.Clamp(options.DefaultBackupIntervalMinutes, 5, 1440);
        return options;
    }



    public void Apply(WorkerSettingsDto settings)
    {
        LudusaviExecutable=Expand(settings.LudusaviExecutable);
        LudusaviBackupDirectory=Expand(settings.LudusaviBackupDirectory);
        RcloneExecutable=Expand(settings.RcloneExecutable);
        RcloneDestination=settings.RcloneDestination??string.Empty;
        MediaArchiveDirectory=Expand(settings.MediaArchiveDirectory);
        ProcessPollingSeconds=Math.Clamp(settings.ProcessPollingSeconds,2,60);
        DefaultBackupIntervalMinutes=Math.Clamp(settings.DefaultBackupIntervalMinutes,5,1440);
        EnableProcessDetection=settings.EnableProcessDetection;
        EnableMediaSync=settings.EnableMediaSync;
        EnableCloudUpload=settings.EnableCloudUpload;
        Directory.CreateDirectory(LudusaviBackupDirectory);
        Directory.CreateDirectory(MediaArchiveDirectory);
    }

    private static string Expand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
    }
}

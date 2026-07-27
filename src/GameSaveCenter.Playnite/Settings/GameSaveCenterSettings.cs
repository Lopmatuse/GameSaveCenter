using System;
using System.Collections.Generic;
using System.IO;
using GameSaveCenter.Contracts;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace GameSaveCenter.Playnite.Settings
{
    /// <summary>Serializable non-secret plugin settings.</summary>
    public sealed class GameSaveCenterSettings : ObservableObject, ISettings
    {
        private readonly GameSaveCenterPlugin? plugin;
        private GameSaveCenterSettings? editingClone;

        public GameSaveCenterSettings() { }

        public GameSaveCenterSettings(GameSaveCenterPlugin plugin)
        {
            this.plugin = plugin;
            var saved = plugin.LoadPluginSettings<GameSaveCenterSettings>();
            if (saved != null) CopyFrom(saved);
            var pluginInstallPath = Path.GetDirectoryName(typeof(GameSaveCenterPlugin).Assembly.Location) ?? plugin.GetPluginUserDataPath();
            EnsureDefaults(pluginInstallPath);
        }

        public string WorkerExecutable { get; set; } = string.Empty;
        public string LudusaviExecutable { get; set; } = string.Empty;
        public string LudusaviBackupDirectory { get; set; } = string.Empty;
        public string RcloneExecutable { get; set; } = string.Empty;
        public string RcloneDestination { get; set; } = string.Empty;
        public string MediaArchiveDirectory { get; set; } = string.Empty;
        public bool AutoStartWorker { get; set; } = true;
        public bool EnableProcessDetection { get; set; } = true;
        public bool EnableMediaSync { get; set; } = true;
        public bool EnableCloudUpload { get; set; }
        public int ProcessPollingSeconds { get; set; } = 5;
        public int DefaultBackupIntervalMinutes { get; set; } = 30;
        public BackupStorageFormat BackupFormat { get; set; } = BackupStorageFormat.Zip;
        public string Compression { get; set; } = "zstd";
        public int CompressionLevel { get; set; } = 3;
        public int FullBackupLimit { get; set; } = 3;
        public int DifferentialBackupLimit { get; set; } = 5;

        public void BeginEdit() => editingClone = Clone();

        public void CancelEdit()
        {
            if (editingClone != null) CopyFrom(editingClone);
        }

        public void EndEdit()
        {
            if (plugin == null) return;
            plugin.SavePluginSettings(this);
            plugin.ApplySettingsAsync();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(WorkerExecutable) || !File.Exists(Environment.ExpandEnvironmentVariables(WorkerExecutable)))
                errors.Add("未找到 GameSaveCenter Worker。请先运行打包脚本，或选择正确的 Worker 可执行文件。");
            if (!string.IsNullOrWhiteSpace(LudusaviExecutable) && !File.Exists(Environment.ExpandEnvironmentVariables(LudusaviExecutable)))
                errors.Add("Ludusavi 路径不存在。");
            if (!string.IsNullOrWhiteSpace(RcloneExecutable) && !File.Exists(Environment.ExpandEnvironmentVariables(RcloneExecutable)))
                errors.Add("Rclone 路径不存在。");
            if (DefaultBackupIntervalMinutes < 5 || DefaultBackupIntervalMinutes > 1440)
                errors.Add("定时备份间隔必须为 5–1440 分钟。");
            if (ProcessPollingSeconds < 2 || ProcessPollingSeconds > 60)
                errors.Add("进程检测间隔必须为 2–60 秒。");
            if (FullBackupLimit < 1 || FullBackupLimit > 255)
                errors.Add("完整备份保留数量必须为 1–255。");
            if (DifferentialBackupLimit < 0 || DifferentialBackupLimit > 255)
                errors.Add("差异备份保留数量必须为 0–255。");
            if (CompressionLevel < -7 || CompressionLevel > 22)
                errors.Add("压缩等级必须为 -7–22；zstd 建议使用 3。");
            return errors.Count == 0;
        }

        public WorkerSettingsDto ToWorkerSettings() => new WorkerSettingsDto
        {
            LudusaviExecutable = Expand(LudusaviExecutable),
            LudusaviBackupDirectory = Expand(LudusaviBackupDirectory),
            RcloneExecutable = Expand(RcloneExecutable),
            RcloneDestination = RcloneDestination ?? string.Empty,
            MediaArchiveDirectory = Expand(MediaArchiveDirectory),
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

        private void EnsureDefaults(string pluginInstallPath)
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(WorkerExecutable))
                WorkerExecutable = Path.Combine(pluginInstallPath, "Worker", "GameSaveCenter.Worker.exe");
            if (string.IsNullOrWhiteSpace(LudusaviBackupDirectory))
                LudusaviBackupDirectory = Path.Combine(documents, "GameSaveCenter", "Saves");
            if (string.IsNullOrWhiteSpace(MediaArchiveDirectory))
                MediaArchiveDirectory = Path.Combine(pictures, "GameSaveCenter");
        }

        private GameSaveCenterSettings Clone() => JsonConvert.DeserializeObject<GameSaveCenterSettings>(JsonConvert.SerializeObject(this)) ?? new GameSaveCenterSettings();

        private void CopyFrom(GameSaveCenterSettings other)
        {
            WorkerExecutable = other.WorkerExecutable;
            LudusaviExecutable = other.LudusaviExecutable;
            LudusaviBackupDirectory = other.LudusaviBackupDirectory;
            RcloneExecutable = other.RcloneExecutable;
            RcloneDestination = other.RcloneDestination;
            MediaArchiveDirectory = other.MediaArchiveDirectory;
            AutoStartWorker = other.AutoStartWorker;
            EnableProcessDetection = other.EnableProcessDetection;
            EnableMediaSync = other.EnableMediaSync;
            EnableCloudUpload = other.EnableCloudUpload;
            ProcessPollingSeconds = other.ProcessPollingSeconds;
            DefaultBackupIntervalMinutes = other.DefaultBackupIntervalMinutes;
            BackupFormat = other.BackupFormat;
            Compression = other.Compression;
            CompressionLevel = other.CompressionLevel;
            FullBackupLimit = other.FullBackupLimit;
            DifferentialBackupLimit = other.DifferentialBackupLimit;
        }

        private static string Expand(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : Environment.ExpandEnvironmentVariables(value);
    }
}

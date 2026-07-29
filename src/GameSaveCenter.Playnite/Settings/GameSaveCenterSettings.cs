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
            if (EnsureDefaults(pluginInstallPath))
                plugin.SavePluginSettings(this);
        }

        public string WorkerExecutable { get; set; } = string.Empty;
        public string LudusaviExecutable { get; set; } = string.Empty;
        public string LudusaviBackupDirectory { get; set; } = string.Empty;
        public string RcloneExecutable { get; set; } = string.Empty;
        public string RcloneDestination { get; set; } = string.Empty;
        public string MediaArchiveDirectory { get; set; } = string.Empty;
        public bool AutoStartWorker { get; set; } = true;
        public bool EnableProcessDetection { get; set; } = true;
        public bool EnableSessionSavePathDetection { get; set; } = true;
        public bool EnableMediaSync { get; set; } = true;
        public bool EnableCloudUpload { get; set; }
        public bool EnableDashboardAutoRefresh { get; set; } = true;
        public bool EnableTaskNotifications { get; set; } = true;
        public GameSaveCenterThemeMode ThemeMode { get; set; } = GameSaveCenterThemeMode.FollowPlaynite;
        public bool EnableUiAnimations { get; set; } = true;
        public bool EnableGlassEffects { get; set; } = true;
        public int GlassEffectStrength { get; set; } = 78;
        public int DashboardRefreshSeconds { get; set; } = 10;
        public int ProcessPollingSeconds { get; set; } = 5;
        public int DefaultBackupIntervalMinutes { get; set; } = 30;
        public BackupStorageFormat BackupFormat { get; set; } = BackupStorageFormat.Zip;
        public string Compression { get; set; } = "zstd";
        public int CompressionLevel { get; set; } = 3;
        public int FullBackupLimit { get; set; } = 3;
        public int DifferentialBackupLimit { get; set; } = 5;

        public string ExportPortableJson()
        {
            var package = new PortableSettingsPackage
            {
                SchemaVersion = 1,
                ExportedUtc = DateTime.UtcNow,
                Settings = Clone()
            };
            return JsonConvert.SerializeObject(package, Formatting.Indented);
        }

        public SettingsImportReport ImportPortableJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("设置文件为空。");
            if (json.Length > 1024 * 1024) throw new InvalidDataException("设置文件超过 1 MiB 安全上限。");
            var package = JsonConvert.DeserializeObject<PortableSettingsPackage>(json)
                          ?? throw new InvalidDataException("设置文件格式无效。");
            if (package.SchemaVersion != 1) throw new InvalidDataException($"不支持的设置架构版本：{package.SchemaVersion}。");
            var imported = package.Settings ?? throw new InvalidDataException("设置文件不包含 settings 节点。");
            var valueErrors = ValidateValueRanges(imported);
            if (valueErrors.Count > 0) throw new InvalidDataException("设置值无效：" + string.Join("；", valueErrors));

            CopyFrom(imported);
            var report = new SettingsImportReport { SchemaVersion = package.SchemaVersion, ExportedUtc = package.ExportedUtc };
            AddMissingFile(report, "Worker", WorkerExecutable);
            AddMissingFile(report, "Ludusavi", LudusaviExecutable);
            AddMissingFile(report, "Rclone", RcloneExecutable);
            AddMissingDirectory(report, "存档目录", LudusaviBackupDirectory);
            AddMissingDirectory(report, "媒体目录", MediaArchiveDirectory);
            return report;
        }

        public void BeginEdit() => editingClone = Clone();

        public void CancelEdit()
        {
            if (editingClone != null) CopyFrom(editingClone);
        }

        public void EndEdit()
        {
            if (plugin == null) return;
            plugin.SavePluginSettings(this);
            plugin.NotifyVisualSettingsChanged();
            plugin.ApplySettingsAsync();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(WorkerExecutable) || !File.Exists(Environment.ExpandEnvironmentVariables(WorkerExecutable)))
                errors.Add("未找到 GameSaveCenter Worker。请先运行打包脚本，或选择正确的 Worker 可执行文件。");
            else if (!IsWorkerExecutable(WorkerExecutable))
                errors.Add("Worker 路径必须指向 GameSaveCenter.Worker.exe，不能选择 Ludusavi 或其他程序。");
            if (!string.IsNullOrWhiteSpace(LudusaviExecutable) && !File.Exists(Environment.ExpandEnvironmentVariables(LudusaviExecutable)))
                errors.Add("Ludusavi 路径不存在。");
            if (!string.IsNullOrWhiteSpace(RcloneExecutable) && !File.Exists(Environment.ExpandEnvironmentVariables(RcloneExecutable)))
                errors.Add("Rclone 路径不存在。");
            if (DefaultBackupIntervalMinutes < 1 || DefaultBackupIntervalMinutes > 1440)
                errors.Add("定时备份间隔必须为 1–1440 分钟。");
            if (ProcessPollingSeconds < 2 || ProcessPollingSeconds > 60)
                errors.Add("进程检测间隔必须为 2–60 秒。");
            if (DashboardRefreshSeconds < 5 || DashboardRefreshSeconds > 300)
                errors.Add("管理面板自动刷新间隔必须为 5–300 秒。");
            if (GlassEffectStrength < 20 || GlassEffectStrength > 100)
                errors.Add("毛玻璃强度必须为 20–100。");
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
            EnableSessionSavePathDetection = EnableSessionSavePathDetection,
            EnableMediaSync = EnableMediaSync,
            EnableCloudUpload = EnableCloudUpload,
            BackupFormat = BackupFormat,
            Compression = Compression,
            CompressionLevel = CompressionLevel,
            FullBackupLimit = FullBackupLimit,
            DifferentialBackupLimit = DifferentialBackupLimit
        };

        private bool EnsureDefaults(string pluginInstallPath)
        {
            var changed = false;
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var packagedWorker = Path.Combine(pluginInstallPath, "Worker", "GameSaveCenter.Worker.exe");
            if (!string.IsNullOrWhiteSpace(WorkerExecutable) &&
                !IsWorkerExecutable(WorkerExecutable) &&
                string.IsNullOrWhiteSpace(LudusaviExecutable) &&
                IsLudusaviExecutable(WorkerExecutable) &&
                File.Exists(Expand(WorkerExecutable)))
            {
                // Repair the 0.4.2 settings mix-up without losing the user's valid Ludusavi path.
                LudusaviExecutable = WorkerExecutable;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(WorkerExecutable) || !IsWorkerExecutable(WorkerExecutable))
            {
                WorkerExecutable = packagedWorker;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(LudusaviBackupDirectory))
            {
                LudusaviBackupDirectory = Path.Combine(documents, "GameSaveCenter", "Saves");
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(MediaArchiveDirectory))
            {
                MediaArchiveDirectory = Path.Combine(pictures, "GameSaveCenter");
                changed = true;
            }
            return changed;
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
            EnableSessionSavePathDetection = other.EnableSessionSavePathDetection;
            EnableMediaSync = other.EnableMediaSync;
            EnableCloudUpload = other.EnableCloudUpload;
            EnableDashboardAutoRefresh = other.EnableDashboardAutoRefresh;
            EnableTaskNotifications = other.EnableTaskNotifications;
            ThemeMode = other.ThemeMode;
            EnableUiAnimations = other.EnableUiAnimations;
            EnableGlassEffects = other.EnableGlassEffects;
            GlassEffectStrength = other.GlassEffectStrength <= 0 ? 78 : other.GlassEffectStrength;
            DashboardRefreshSeconds = other.DashboardRefreshSeconds;
            ProcessPollingSeconds = other.ProcessPollingSeconds;
            DefaultBackupIntervalMinutes = other.DefaultBackupIntervalMinutes;
            BackupFormat = other.BackupFormat;
            Compression = other.Compression;
            CompressionLevel = other.CompressionLevel;
            FullBackupLimit = other.FullBackupLimit;
            DifferentialBackupLimit = other.DifferentialBackupLimit;
        }

        private static List<string> ValidateValueRanges(GameSaveCenterSettings value)
        {
            var errors = new List<string>();
            if (value.DefaultBackupIntervalMinutes < 1 || value.DefaultBackupIntervalMinutes > 1440) errors.Add("备份间隔超出 1–1440");
            if (value.ProcessPollingSeconds < 2 || value.ProcessPollingSeconds > 60) errors.Add("进程检测间隔超出 2–60");
            if (value.DashboardRefreshSeconds < 5 || value.DashboardRefreshSeconds > 300) errors.Add("面板刷新间隔超出 5–300");
            if (value.GlassEffectStrength < 20 || value.GlassEffectStrength > 100) errors.Add("毛玻璃强度超出 20–100");
            if (value.FullBackupLimit < 1 || value.FullBackupLimit > 255) errors.Add("完整版本数超出 1–255");
            if (value.DifferentialBackupLimit < 0 || value.DifferentialBackupLimit > 255) errors.Add("差异版本数超出 0–255");
            if (value.CompressionLevel < -7 || value.CompressionLevel > 22) errors.Add("压缩等级超出 -7–22");
            if (!Enum.IsDefined(typeof(GameSaveCenterThemeMode), value.ThemeMode)) errors.Add("未知主题模式");
            if (!Enum.IsDefined(typeof(BackupStorageFormat), value.BackupFormat)) errors.Add("未知备份格式");
            return errors;
        }

        private static void AddMissingFile(SettingsImportReport report, string label, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(Expand(path))) report.MissingPaths.Add($"{label}：{path}");
        }

        private static void AddMissingDirectory(SettingsImportReport report, string label, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(Expand(path))) report.MissingPaths.Add($"{label}：{path}");
        }

        private static string Expand(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : Environment.ExpandEnvironmentVariables(value);

        internal static bool IsWorkerExecutable(string value)
            => string.Equals(Path.GetFileName(Expand(value)), "GameSaveCenter.Worker.exe", StringComparison.OrdinalIgnoreCase);

        private static bool IsLudusaviExecutable(string value)
            => string.Equals(Path.GetFileName(Expand(value)), "ludusavi.exe", StringComparison.OrdinalIgnoreCase);

        private sealed class PortableSettingsPackage
        {
            public int SchemaVersion { get; set; }
            public DateTime ExportedUtc { get; set; }
            public GameSaveCenterSettings? Settings { get; set; }
        }
    }

    public sealed class SettingsImportReport
    {
        public int SchemaVersion { get; set; }
        public DateTime ExportedUtc { get; set; }
        public List<string> MissingPaths { get; } = new List<string>();
        public string Summary => MissingPaths.Count == 0
            ? "设置已载入，未发现缺失路径。点击 Playnite 的保存按钮后生效。"
            : $"设置已载入，但有 {MissingPaths.Count} 个路径需要重新选择：\n" + string.Join("\n", MissingPaths);
    }
}

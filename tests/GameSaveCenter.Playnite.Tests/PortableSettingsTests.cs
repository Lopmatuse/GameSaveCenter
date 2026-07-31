using System;
using System.IO;
using GameSaveCenter.Contracts;
using GameSaveCenter.Playnite.Settings;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameSaveCenter.Playnite.Tests
{
    public sealed class PortableSettingsTests
    {
        [Fact]
        public void ExportImport_RoundTripsNonSecretSettings()
        {
            var source = CreateSettings();
            var json = source.ExportPortableJson();
            var package = JObject.Parse(json);

            Assert.Equal(1, package["SchemaVersion"]!.Value<int>());
            Assert.NotNull(package["ExportedUtc"]);
            Assert.Null(package.SelectToken("Settings.RclonePassword"));

            var imported = new GameSaveCenterSettings();
            var report = imported.ImportPortableJson(json);

            Assert.Equal(1, report.SchemaVersion);
            Assert.Equal(source.WorkerExecutable, imported.WorkerExecutable);
            Assert.Equal(source.RcloneDestination, imported.RcloneDestination);
            Assert.Equal(source.ThemeMode, imported.ThemeMode);
            Assert.Equal(source.BackupFormat, imported.BackupFormat);
            Assert.Equal(source.CompressionLevel, imported.CompressionLevel);
            Assert.Equal(source.DifferentialBackupLimit, imported.DifferentialBackupLimit);
            Assert.Equal(source.EnableXboxGameBarMedia, imported.EnableXboxGameBarMedia);
            Assert.Equal(source.EnableCustomMedia, imported.EnableCustomMedia);
        }

        [Fact]
        public void Import_LegacyPackageUsesDefaultsForNewFields()
        {
            const string json = @"{
  ""SchemaVersion"": 1,
  ""ExportedUtc"": ""2026-07-01T00:00:00Z"",
  ""Settings"": {
    ""WorkerExecutable"": ""C:\\Portable\\GameSaveCenter.Worker.exe"",
    ""LudusaviBackupDirectory"": ""C:\\Portable\\Saves"",
    ""MediaArchiveDirectory"": ""C:\\Portable\\Media""
  }
}";

            var imported = new GameSaveCenterSettings();
            imported.ImportPortableJson(json);

            Assert.True(imported.EnableUiAnimations);
            Assert.True(imported.EnableGlassEffects);
            Assert.Equal(78, imported.GlassEffectStrength);
            Assert.Equal(10, imported.DashboardRefreshSeconds);
            Assert.Equal(BackupStorageFormat.Zip, imported.BackupFormat);
            Assert.Equal("zstd", imported.Compression);
            Assert.True(imported.EnableSteamMedia);
            Assert.True(imported.EnableXboxGameBarMedia);
            Assert.True(imported.EnableWindowsScreenshotMedia);
            Assert.True(imported.EnablePlatformAdjacentMedia);
            Assert.True(imported.EnableCustomMedia);
        }

        [Fact]
        public void Import_InvalidValuesDoesNotMutateCurrentSettings()
        {
            var settings = CreateSettings();
            var originalWorker = settings.WorkerExecutable;
            var originalInterval = settings.DefaultBackupIntervalMinutes;
            var package = JObject.Parse(settings.ExportPortableJson());
            package.SelectToken("Settings.DefaultBackupIntervalMinutes")!.Replace(0);

            Assert.Throws<InvalidDataException>(() => settings.ImportPortableJson(package.ToString()));
            Assert.Equal(originalWorker, settings.WorkerExecutable);
            Assert.Equal(originalInterval, settings.DefaultBackupIntervalMinutes);
        }

        [Fact]
        public void Import_RejectsUnknownSchemaEnumAndOversizedInput()
        {
            var settings = CreateSettings();
            var package = JObject.Parse(settings.ExportPortableJson());
            package["SchemaVersion"] = 2;
            Assert.Throws<InvalidDataException>(() => settings.ImportPortableJson(package.ToString()));

            package = JObject.Parse(settings.ExportPortableJson());
            package.SelectToken("Settings.ThemeMode")!.Replace(999);
            Assert.Throws<InvalidDataException>(() => settings.ImportPortableJson(package.ToString()));

            Assert.Throws<InvalidDataException>(() => settings.ImportPortableJson(new string('x', 1024 * 1024 + 1)));
        }

        [Fact]
        public void Import_ReportsMissingProgramsAndDirectoriesWithoutCreatingThem()
        {
            var root = Path.Combine(Path.GetTempPath(), "GameSaveCenter.Settings.Tests", Guid.NewGuid().ToString("N"));
            var settings = CreateSettings();
            settings.WorkerExecutable = Path.Combine(root, "GameSaveCenter.Worker.exe");
            settings.LudusaviExecutable = Path.Combine(root, "ludusavi.exe");
            settings.RcloneExecutable = Path.Combine(root, "rclone.exe");
            settings.LudusaviBackupDirectory = Path.Combine(root, "Saves");
            settings.MediaArchiveDirectory = Path.Combine(root, "Media");

            var imported = new GameSaveCenterSettings();
            var report = imported.ImportPortableJson(settings.ExportPortableJson());

            Assert.Equal(5, report.MissingPaths.Count);
            Assert.False(Directory.Exists(root));
        }

        private static GameSaveCenterSettings CreateSettings() => new GameSaveCenterSettings
        {
            WorkerExecutable = @"C:\Tools\GameSaveCenter.Worker.exe",
            LudusaviExecutable = @"C:\Tools\ludusavi.exe",
            LudusaviBackupDirectory = @"D:\Backups\Saves",
            RcloneExecutable = @"C:\Tools\rclone.exe",
            RcloneDestination = "encrypted-remote:GameSaveCenter",
            MediaArchiveDirectory = @"D:\Backups\Media",
            AutoStartWorker = false,
            EnableProcessDetection = true,
            EnableSessionSavePathDetection = false,
            EnableMediaSync = true,
            EnableSteamMedia = false,
            EnableXboxGameBarMedia = true,
            EnableWindowsScreenshotMedia = false,
            EnablePlatformAdjacentMedia = true,
            EnableCustomMedia = false,
            EnableCloudUpload = true,
            EnableDashboardAutoRefresh = false,
            EnableTaskNotifications = true,
            ThemeMode = GameSaveCenterThemeMode.Dark,
            EnableUiAnimations = false,
            EnableGlassEffects = false,
            GlassEffectStrength = 64,
            DashboardRefreshSeconds = 30,
            ProcessPollingSeconds = 9,
            DefaultBackupIntervalMinutes = 45,
            BackupFormat = BackupStorageFormat.Zip,
            Compression = "zstd",
            CompressionLevel = 8,
            FullBackupLimit = 7,
            DifferentialBackupLimit = 11
        };
    }
}

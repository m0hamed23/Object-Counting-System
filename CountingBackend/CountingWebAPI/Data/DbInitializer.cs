using CountingWebAPI.Models.Database;
using CountingWebAPI.Helpers;
using CountingWebAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CountingWebAPI.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IDatabaseHelper dbHelper, IServiceProvider serviceProvider, ILogger logger)
        {
            var appSettingsOptions = serviceProvider.GetRequiredService<IOptions<AppSettings>>();
            var appSettings = appSettingsOptions.Value;

            logger.LogInformation("Ensuring SQLite database and tables exist...");
            await CreateTablesIfNotExistsAsync(dbHelper, logger);

            await SeedUsersAsync(dbHelper, logger, appSettings);
            await SeedOrUpdateSettingsAsync(dbHelper, logger, appSettings);

            logger.LogInformation("Database initialization with SQLite complete.");
        }

        private static async Task CreateTablesIfNotExistsAsync(IDatabaseHelper dbHelper, ILogger logger)
        {
            await dbHelper.ExecuteNonQueryAsync("PRAGMA foreign_keys = ON;");
            await dbHelper.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL);");
            await dbHelper.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS Settings (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL, Value TEXT, Description TEXT, IsVisible INTEGER NOT NULL DEFAULT 1, SortOrder INTEGER NOT NULL DEFAULT 0);");
            await dbHelper.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS Logs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Timestamp TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')), Event TEXT NOT NULL, ImageName TEXT, CameraIndex INTEGER);");
            await dbHelper.ExecuteNonQueryAsync("CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON Logs (Timestamp DESC);");
            await dbHelper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS Cameras (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE, RtspUrl TEXT NOT NULL, 
                    IsEnabled INTEGER NOT NULL DEFAULT 1, LastUpdated TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now'))
                );");
            await dbHelper.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS CameraRois (CameraIndex INTEGER PRIMARY KEY, RoiData TEXT NOT NULL, LastUpdated TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')));");
            await dbHelper.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS Zones (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE);");
            await dbHelper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS ZoneCameras (
                    ZoneId INTEGER NOT NULL, CameraId INTEGER NOT NULL, 
                    PRIMARY KEY (ZoneId, CameraId), 
                    FOREIGN KEY (ZoneId) REFERENCES Zones(Id) ON DELETE CASCADE, 
                    FOREIGN KEY (CameraId) REFERENCES Cameras(Id) ON DELETE CASCADE
                );");
            await dbHelper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS Actions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    Name TEXT NOT NULL UNIQUE, 
                    IpAddress TEXT NOT NULL, 
                    Port INTEGER NOT NULL, 
                    IntervalMilliseconds INTEGER NOT NULL,
                    Protocol TEXT NOT NULL,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    LastUpdated TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now'))
                );");
            await dbHelper.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS Locations (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE);");
            await dbHelper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS LocationZones (
                    LocationId INTEGER NOT NULL, ZoneId INTEGER NOT NULL, 
                    PRIMARY KEY (LocationId, ZoneId), 
                    FOREIGN KEY (LocationId) REFERENCES Locations(Id) ON DELETE CASCADE, 
                    FOREIGN KEY (ZoneId) REFERENCES Zones(Id) ON DELETE CASCADE
                );");
        }

        private static async Task SeedUsersAsync(IDatabaseHelper dbHelper, ILogger logger, AppSettings appSettings)
        {
            if (await dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users WHERE Username = @Username", dbHelper.CreateParameter("@Username", appSettings.DefaultUser.Username)) == 0)
            {
                logger.LogInformation("Default user not found. Seeding...");
                await dbHelper.ExecuteNonQueryAsync(
                    "INSERT INTO Users (Username, PasswordHash) VALUES (@Username, @PasswordHash)",
                    dbHelper.CreateParameter("@Username", appSettings.DefaultUser.Username),
                    dbHelper.CreateParameter("@PasswordHash", PasswordHelper.HashPassword(appSettings.DefaultUser.Password))
                );
            }
        }

        private static async Task SeedOrUpdateSettingsAsync(IDatabaseHelper dbHelper, ILogger logger, AppSettings appSettings)
        {
            var defaultSettingsList = GetDefaultSettingsList(appSettings);
            var existingSettings = await dbHelper.QueryAsync("SELECT Name, DisplayName, Description, IsVisible FROM Settings",
                r => new { Name = r.GetStringSafe("Name"), DisplayName = r.GetStringSafe("DisplayName"), Description = r.GetNullableString("Description"), IsVisible = r.GetBooleanSafe("IsVisible") });
            var existingSettingNames = new HashSet<string>(existingSettings.Select(s => s.Name));

            foreach (var setting in defaultSettingsList)
            {
                if (!existingSettingNames.Contains(setting.Name))
                {
                    logger.LogDebug($"Seeding new setting: {setting.Name}");
                    await dbHelper.ExecuteNonQueryAsync(
                        "INSERT INTO Settings (Name, DisplayName, Value, Description, IsVisible, SortOrder) VALUES (@Name, @DisplayName, @Value, @Description, @IsVisible, @SortOrder)",
                        dbHelper.CreateParameter("@Name", setting.Name), dbHelper.CreateParameter("@DisplayName", setting.DisplayName),
                        dbHelper.CreateParameter("@Value", (object?)setting.Value ?? DBNull.Value),
                        dbHelper.CreateParameter("@Description", (object?)setting.Description ?? DBNull.Value),
                        dbHelper.CreateParameter("@IsVisible", setting.IsVisible),
                        dbHelper.CreateParameter("@SortOrder", setting.SortOrder));
                }
            }
        }

        private static List<Setting> GetDefaultSettingsList(AppSettings appSettings)
        {
            return new List<Setting> {
                // AI Model Configuration (10-99)
                new Setting { Name = "model_type", DisplayName = "AI Model Type", Value = "RF-DETR", Description = "Select the detection model architecture to use (Options: YOLO, RF-DETR).", IsVisible = true, SortOrder = 10 },
                new Setting { Name = "confidence_threshold", DisplayName = "Detection Confidence", Value = "0.3", Description = "Minimum detection confidence to consider an object (e.g., 0.3).", IsVisible = true, SortOrder = 20 },
                new Setting { Name = "nms_threshold", DisplayName = "NMS Threshold", Value = "0.45", Description = "Non-Maximum Suppression threshold for filtering overlapping boxes (e.g., 0.45).", IsVisible = true, SortOrder = 30 },
                new Setting { Name = "target_classes", DisplayName = "Target Classes", Value = "car", Description = "Choose the object classes for the AI to detect. Move items from the 'Available' list to the 'Selected' list.", IsVisible = true, SortOrder = 40 },

                // Processing Strategy (100-199)
                new Setting { Name = "idle_scan_mode_enabled", DisplayName = "Enable Idle Scan Mode", Value = "true", Description = "Controls the processing strategy. Enabled ('true'): Uses motion detection to save resources by switching to an idle scan after a period of inactivity. Disabled ('false'): Forces the system into a permanent 'Active Mode', continuously processing frames. This offers constant monitoring but uses more CPU.", IsVisible = true, SortOrder = 100 },
                new Setting { Name = "active_mode_process_nth_frame", DisplayName = "Active Mode: Process Nth Frame", Value = "10", Description = "When motion is detected, run AI on every Nth frame (1=all frames).", IsVisible = true, SortOrder = 110 },
                new Setting { Name = "active_state_timeout_seconds", DisplayName = "Active State Timeout (s)", Value = "15.0", Description = "Time without motion before switching to idle scan mode.", IsVisible = true, SortOrder = 120 },
                new Setting { Name = "idle_scan_mode_interval_seconds", DisplayName = "Idle Scan Mode: Interval (s)", Value = "10.0", Description = "When in idle scan mode, run AI check this many seconds apart.", IsVisible = true, SortOrder = 130 },

                // Motion Detection (200-299)
                new Setting { Name = "motion_detection_threshold", DisplayName = "Motion Area Threshold", Value = "0.005", Description = "Percentage of changed pixels to trigger active mode (e.g., 0.01 for 1%).", IsVisible = true, SortOrder = 200 },
                new Setting { Name = "motion_pixel_difference_threshold", DisplayName = "Motion Pixel Sensitivity", Value = "10", Description = "The sensitivity for detecting a change in a single pixel (1-255). A lower value is more sensitive to subtle changes.", IsVisible = true, SortOrder = 210 },
                
                // System & File Paths (300-399)
                new Setting { Name = "model_path", DisplayName = "YOLO Model Path", Value = appSettings.DefaultYoloModelPath, Description = "Path to the ONNX YOLO model file.", IsVisible = true, SortOrder = 300 },
                new Setting { Name = "rfdetr_model_path", DisplayName = "RF-DETR Model Path", Value = appSettings.DefaultRfDetrModelPath, Description = "Path to the ONNX RF-DETR model file (e.g., rf-detr-medium.onnx).", IsVisible = true, SortOrder = 310 },
                new Setting { Name = "ffmpeg_path", DisplayName = "FFmpeg Bin Path", Value = appSettings.FfmpegPath, Description = "Path to FFmpeg binaries for RTSP processing.", IsVisible = true, SortOrder = 320 }
            };
        }
    }
}
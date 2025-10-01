using CountingWebAPI.Models.Database;
using CountingWebAPI.Helpers;
using CountingWebAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CountingWebAPI.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IDatabaseHelper dbHelper, IServiceProvider serviceProvider, ILogger logger)
        {
            var appSettingsOptions = serviceProvider.GetRequiredService<IOptions<AppSettings>>();
            var appSettings = appSettingsOptions.Value;
            var dbSettings = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Database").Get<DatabaseSettings>()
                ?? throw new InvalidOperationException("Database settings are not configured.");
            var schema = dbSettings.Schema;

            if (string.IsNullOrEmpty(schema))
            {
                logger.LogError("Database schema name is not configured. Aborting initialization.");
                return;
            }

            logger.LogInformation("Ensuring PostgreSQL database schema and tables exist...");
            await CreateSchemaAndTablesIfNotExistsAsync(dbHelper, logger, schema);

            await SeedUsersAsync(dbHelper, logger, schema, appSettings);
            await SeedOrUpdateSettingsAsync(dbHelper, logger, schema, appSettings);
            
            // The Seeding logic is now disabled by default.
            // logger.LogWarning("Default entity seeding is disabled. Manage entities via the UI.");
            // To re-enable seeding for a fresh database, uncomment the line below.
            // await SeedDefaultEntitiesAsync(dbHelper, logger, schema);
            
            logger.LogInformation("Database initialization with PostgreSQL complete.");
        }

        private static async Task CreateSchemaAndTablesIfNotExistsAsync(IDatabaseHelper dbHelper, ILogger logger, string schema)
        {
            await dbHelper.ExecuteNonQueryAsync($"CREATE SCHEMA IF NOT EXISTS {schema};");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.Users (Id SERIAL PRIMARY KEY, Username TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL);");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.Settings (Id SERIAL PRIMARY KEY, Name TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL, Value TEXT, Description TEXT, IsVisible BOOLEAN NOT NULL DEFAULT TRUE);");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.Logs (Id SERIAL PRIMARY KEY, Timestamp TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'), Event TEXT NOT NULL, ImageName TEXT, CameraIndex INTEGER);");
            await dbHelper.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON {schema}.Logs (Timestamp DESC);");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.Controllers (Id SERIAL PRIMARY KEY, IpAddress TEXT NOT NULL UNIQUE, Port1 INTEGER, Port2 INTEGER, Port3 INTEGER, Port4 INTEGER, Port5 INTEGER, Port6 INTEGER, Port7 INTEGER, Port8 INTEGER, OutputStartAddress INTEGER, InputStartAddress INTEGER, NumOutputs INTEGER, NumInputs INTEGER);");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.Barriers (Id SERIAL PRIMARY KEY, Name TEXT NOT NULL UNIQUE, ControllerId INTEGER NOT NULL, OpenRelay INTEGER NOT NULL, CloseRelay INTEGER NOT NULL, FOREIGN KEY (ControllerId) REFERENCES {schema}.Controllers(Id) ON DELETE RESTRICT);");
            await dbHelper.ExecuteNonQueryAsync($@"
                CREATE TABLE IF NOT EXISTS {schema}.Cameras (
                    Id SERIAL PRIMARY KEY, Name TEXT NOT NULL UNIQUE, RtspUrl TEXT, DeviceIndex INTEGER, 
                    IsEnabled BOOLEAN NOT NULL DEFAULT TRUE, LastUpdated TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
                    CONSTRAINT chk_camera_source CHECK (RtspUrl IS NOT NULL OR DeviceIndex IS NOT NULL)
                );");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.CameraRois (CameraIndex INTEGER PRIMARY KEY, RoiData TEXT NOT NULL, LastUpdated TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'));");
            await dbHelper.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {schema}.Zones (Id SERIAL PRIMARY KEY, Name TEXT NOT NULL UNIQUE);");
            await dbHelper.ExecuteNonQueryAsync($@"
                CREATE TABLE IF NOT EXISTS {schema}.ZoneCameras (ZoneId INTEGER NOT NULL, CameraId INTEGER NOT NULL, PRIMARY KEY (ZoneId, CameraId), 
                FOREIGN KEY (ZoneId) REFERENCES {schema}.Zones(Id) ON DELETE CASCADE, FOREIGN KEY (CameraId) REFERENCES {schema}.Cameras(Id) ON DELETE CASCADE);");
            await dbHelper.ExecuteNonQueryAsync($@"
                CREATE TABLE IF NOT EXISTS {schema}.ZoneBarriers (ZoneId INTEGER NOT NULL, BarrierId INTEGER NOT NULL, PRIMARY KEY (ZoneId, BarrierId), 
                FOREIGN KEY (ZoneId) REFERENCES {schema}.Zones(Id) ON DELETE CASCADE, FOREIGN KEY (BarrierId) REFERENCES {schema}.Barriers(Id) ON DELETE CASCADE);");
        }

        private static async Task SeedUsersAsync(IDatabaseHelper dbHelper, ILogger logger, string schema, AppSettings appSettings)
        {
            if (await dbHelper.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {schema}.Users WHERE Username = @Username", new NpgsqlParameter("@Username", appSettings.DefaultUser.Username)) == 0)
            {
                logger.LogInformation("Default user not found. Seeding...");
                await dbHelper.ExecuteNonQueryAsync(
                    $"INSERT INTO {schema}.Users (Username, PasswordHash) VALUES (@Username, @PasswordHash)",
                    new NpgsqlParameter("@Username", appSettings.DefaultUser.Username),
                    new NpgsqlParameter("@PasswordHash", PasswordHelper.HashPassword(appSettings.DefaultUser.Password))
                );
            }
        }
        
        private static async Task SeedDefaultEntitiesAsync(IDatabaseHelper dbHelper, ILogger logger, string schema)
        {
            const string controllerIp = "192.168.1.120";
            var controllerId = await dbHelper.ExecuteScalarAsync<int?>($"SELECT Id FROM {schema}.Controllers WHERE IpAddress = @Ip", new NpgsqlParameter("@Ip", controllerIp));
            if (!controllerId.HasValue)
            {
                logger.LogInformation("Seeding default controller '{Ip}'.", controllerIp);
                controllerId = await dbHelper.ExecuteScalarAsync<int>(
                    $"INSERT INTO {schema}.Controllers (IpAddress, OutputStartAddress, InputStartAddress, NumOutputs, NumInputs) VALUES (@Ip, 8256, 0, 16, 16) RETURNING Id;",
                     new NpgsqlParameter("@Ip", controllerIp));
            }

            const string barrierName = "Main Gate Barrier";
            var barrierId = await dbHelper.ExecuteScalarAsync<int?>($"SELECT Id FROM {schema}.Barriers WHERE Name = @Name", new NpgsqlParameter("@Name", barrierName));
            if (!barrierId.HasValue)
            {
                logger.LogInformation("Seeding default barrier '{Name}'.", barrierName);
                barrierId = await dbHelper.ExecuteScalarAsync<int>(
                    $"INSERT INTO {schema}.Barriers (Name, ControllerId, OpenRelay, CloseRelay) VALUES (@Name, @ControllerId, 1, 2) RETURNING Id;",
                    new NpgsqlParameter("@Name", barrierName), new NpgsqlParameter("@ControllerId", controllerId.Value));
            }
            
            const string cameraName = "Local Webcam 1";
            var cameraId = await dbHelper.ExecuteScalarAsync<int?>($"SELECT Id FROM {schema}.Cameras WHERE Name = @Name", new NpgsqlParameter("@Name", cameraName));
            if (!cameraId.HasValue)
            {
                logger.LogInformation("Seeding default camera '{Name}'.", cameraName);
                cameraId = await dbHelper.ExecuteScalarAsync<int>(
                    $"INSERT INTO {schema}.Cameras (Name, DeviceIndex, IsEnabled) VALUES (@Name, 0, TRUE) RETURNING Id;",
                     new NpgsqlParameter("@Name", cameraName));
            }

            const string zoneName = "Main Entrance Zone";
            var zoneId = await dbHelper.ExecuteScalarAsync<int?>($"SELECT Id FROM {schema}.Zones WHERE Name = @Name", new NpgsqlParameter("@Name", zoneName));
            if (!zoneId.HasValue)
            {
                logger.LogInformation("Seeding default zone '{Name}'.", zoneName);
                zoneId = await dbHelper.ExecuteScalarAsync<int>(
                    $"INSERT INTO {schema}.Zones (Name) VALUES (@Name) RETURNING Id;",
                     new NpgsqlParameter("@Name", zoneName));
            }
            
            if (await dbHelper.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {schema}.ZoneCameras WHERE ZoneId = @ZoneId AND CameraId = @CameraId", new NpgsqlParameter("ZoneId", zoneId.Value), new NpgsqlParameter("CameraId", cameraId.Value)) == 0)
            {
                logger.LogInformation("Seeding default Zone-Camera association (ZoneID: {ZoneId}, CameraID: {CameraId}).", zoneId.Value, cameraId.Value);
                await dbHelper.ExecuteNonQueryAsync($"INSERT INTO {schema}.ZoneCameras (ZoneId, CameraId) VALUES (@ZoneId, @CameraId);", new NpgsqlParameter("ZoneId", zoneId.Value), new NpgsqlParameter("CameraId", cameraId.Value));
            }
            
            if (await dbHelper.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {schema}.ZoneBarriers WHERE ZoneId = @ZoneId AND BarrierId = @BarrierId", new NpgsqlParameter("ZoneId", zoneId.Value), new NpgsqlParameter("BarrierId", barrierId.Value)) == 0)
            {
                 logger.LogInformation("Seeding default Zone-Barrier association (ZoneID: {ZoneId}, BarrierID: {BarrierId}).", zoneId.Value, barrierId.Value);
                await dbHelper.ExecuteNonQueryAsync($"INSERT INTO {schema}.ZoneBarriers (ZoneId, BarrierId) VALUES (@ZoneId, @BarrierId);", new NpgsqlParameter("ZoneId", zoneId.Value), new NpgsqlParameter("BarrierId", barrierId.Value));
            }
        }

        private static async Task SeedOrUpdateSettingsAsync(IDatabaseHelper dbHelper, ILogger logger, string schema, AppSettings appSettings)
        {
            var defaultSettingsList = GetDefaultSettingsList(appSettings);
            var existingSettings = await dbHelper.QueryAsync($"SELECT Name, DisplayName, Description, IsVisible FROM {schema}.Settings",
                r => new { Name = r.GetStringSafe("Name"), DisplayName = r.GetStringSafe("DisplayName"), Description = r.GetNullableString("Description"), IsVisible = r.GetBooleanSafe("IsVisible") });
            var existingSettingNames = new HashSet<string>(existingSettings.Select(s => s.Name));

            foreach (var setting in defaultSettingsList)
            {
                if (!existingSettingNames.Contains(setting.Name))
                {
                    logger.LogDebug($"Seeding new setting: {setting.Name}");
                    await dbHelper.ExecuteNonQueryAsync(
                        $"INSERT INTO {schema}.Settings (Name, DisplayName, Value, Description, IsVisible) VALUES (@Name, @DisplayName, @Value, @Description, @IsVisible)",
                        new NpgsqlParameter("@Name", setting.Name), new NpgsqlParameter("@DisplayName", setting.DisplayName),
                        new NpgsqlParameter("@Value", (object?)setting.Value ?? DBNull.Value),
                        new NpgsqlParameter("@Description", (object?)setting.Description ?? DBNull.Value),
                        new NpgsqlParameter("@IsVisible", setting.IsVisible));
                }
            }
        }

        private static List<Setting> GetDefaultSettingsList(AppSettings appSettings)
        {
            // MODIFIED: Removed the obsolete rtsp_url settings
            return new List<Setting> {
                new Setting { Name = "movement_threshold", DisplayName = "Movement Threshold", Value = appSettings.DetectionSettings.MovementThreshold.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), Description = "Minimum pixel change distance.", IsVisible = true },
                new Setting { Name = "stationary_time", DisplayName = "Stationary Time (s)", Value = appSettings.DetectionSettings.StationaryTime.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), Description = "Time to consider stationary.", IsVisible = true },
                new Setting { Name = "jam_threshold", DisplayName = "Jam Threshold", Value = appSettings.DetectionSettings.JamThreshold.ToString(), Description = "Stationary objects in a ZONE for jam.", IsVisible = true },
                new Setting { Name = "clear_threshold", DisplayName = "Clear Threshold", Value = appSettings.DetectionSettings.ClearThreshold.ToString(), Description = "Moving objects in a ZONE to clear jam.", IsVisible = true },
                new Setting { Name = "jam_duration", DisplayName = "Jam Duration (s)", Value = appSettings.DetectionSettings.JamDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), Description = "Jam condition persistence.", IsVisible = true },
                new Setting { Name = "clear_duration", DisplayName = "Clear Duration (s)", Value = appSettings.DetectionSettings.ClearDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), Description = "Clear condition persistence.", IsVisible = true },
                new Setting { Name = "confidence_threshold", DisplayName = "Detection Confidence", Value = appSettings.DetectionSettings.ConfidenceThreshold.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), Description = "Min detection confidence.", IsVisible = true },
                new Setting { Name = "model_path", DisplayName = "Detection Model Path", Value = appSettings.DefaultYoloModelPath, Description = "Path to YOLO model.", IsVisible = false },
                new Setting { Name = "operation_mode", DisplayName = "Operation Mode", Value = appSettings.OperationMode, Description = "Manual/Automatic.", IsVisible = true },
                new Setting { Name = "modbus_impulse_duration", DisplayName = "Modbus Impulse (s)", Value = appSettings.ModbusImpulseDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), Description = "Modbus impulse duration.", IsVisible = true },
                new Setting { Name = "process_frame_interval", DisplayName = "Process Nth Frame", Value = appSettings.ProcessFrameInterval.ToString(), Description = "Process every Nth frame (1=all).", IsVisible = true },
                new Setting { Name = "log_image_path", DisplayName = "Log Image Path", Value = appSettings.ImageLogPath, Description = "Directory for log images.", IsVisible = false },
                new Setting { Name = "ffmpeg_path", DisplayName = "FFmpeg Bin Path", Value = appSettings.FfmpegPath, Description = "Path to FFmpeg binaries.", IsVisible = false }
            };
        }
    }
}
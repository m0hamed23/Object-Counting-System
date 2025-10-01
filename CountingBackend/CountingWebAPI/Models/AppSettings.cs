using System.Collections.Generic;

namespace CountingWebAPI.Models
{
    public class AppSettings
    {
        public DefaultUserSettings DefaultUser { get; set; } = new();
        public string ImageLogPath { get; set; } = string.Empty;
        public string FfmpegPath { get; set; } = string.Empty;
        public string DefaultYoloModelPath { get; set; } = string.Empty;
        public string DefaultRfDetrModelPath { get; set; } = string.Empty;
        public string OperationMode { get; set; } = "Manual";

        // --- REMOVED ---
        // public int ProcessFrameInterval { get; set; } = 1;
        // public List<CameraConfig> CameraSettings { get; set; } = new();
        // public DetectionConfig DetectionSettings { get; set; } = new();
    }

    public class DefaultUserSettings
    {
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
    }

    // --- REMOVED ---
    // The CameraConfig and DetectionConfig classes are now obsolete as this
    // configuration is managed in the database.
    //
    // public class CameraConfig { ... }
    // public class DetectionConfig { ... }

    public class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
    }
}
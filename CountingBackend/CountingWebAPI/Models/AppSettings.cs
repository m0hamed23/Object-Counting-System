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


    }

    public class DefaultUserSettings
    {
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin";
    }

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
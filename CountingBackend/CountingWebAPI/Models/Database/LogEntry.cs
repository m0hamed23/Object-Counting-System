using System;

namespace CountingWebAPI.Models.Database
{
    public class LogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Event { get; set; } = string.Empty;
        public string? ImageName { get; set; }
        public int? CameraIndex { get; set; }
    }
}
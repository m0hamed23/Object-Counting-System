using System;

namespace CountingWebAPI.Models.Database
{
    public class Camera
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
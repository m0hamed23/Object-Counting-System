
using System;

namespace CountingWebAPI.Models.Database
{
    public class Action
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public int IntervalMilliseconds { get; set; }
        public string Protocol { get; set; } = string.Empty; // "TCP" or "UDP"
        public bool IsEnabled { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
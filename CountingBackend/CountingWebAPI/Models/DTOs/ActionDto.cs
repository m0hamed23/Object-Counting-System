
using System.ComponentModel.DataAnnotations;

namespace CountingWebAPI.Models.DTOs
{
    public class ActionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public int IntervalMilliseconds { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class ActionCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)\.?\b){4}$", ErrorMessage = "Invalid IP Address")]
        public string IpAddress { get; set; } = string.Empty;

        [Required]
        [Range(1, 65535)]
        public int Port { get; set; }

        [Required]
        [Range(100, int.MaxValue, ErrorMessage = "Interval must be at least 100ms")]
        public int IntervalMilliseconds { get; set; }

        [Required]
        public string Protocol { get; set; } = "TCP";

        [Required]
        public bool IsEnabled { get; set; } = true;
    }
}
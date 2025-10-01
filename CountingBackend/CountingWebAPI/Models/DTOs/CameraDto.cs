using System;
using System.ComponentModel.DataAnnotations;

namespace CountingWebAPI.Models.DTOs
{
    public class CameraDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class CameraCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string RtspUrl { get; set; } = string.Empty;

        [Required]
        public bool IsEnabled { get; set; } = true;
    }
}
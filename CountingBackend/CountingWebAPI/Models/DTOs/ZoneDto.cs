using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CountingWebAPI.Models.DTOs
{
    public class ZoneDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<CameraDto> Cameras { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int TotalTrackedCount { get; set; }
    }

    public class ZoneCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;
    }
}
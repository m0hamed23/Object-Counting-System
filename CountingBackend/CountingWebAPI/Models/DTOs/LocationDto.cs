using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CountingWebAPI.Models.DTOs
{
    public class LocationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ZoneDto> Zones { get; set; } = new();
    }

    public class LocationCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;
    }
}
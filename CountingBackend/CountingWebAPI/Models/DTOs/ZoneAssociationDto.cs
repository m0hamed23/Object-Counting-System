using System.ComponentModel.DataAnnotations;

namespace CountingWebAPI.Models.DTOs
{
    public class ZoneAssociationDto
    {
        [Required]
        public int Id { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace CountingWebAPI.Models.DTOs
{
    public class UserUpdateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 3)] // Adjusted minimum length
        public string? Password { get; set; } 
    }
}

//
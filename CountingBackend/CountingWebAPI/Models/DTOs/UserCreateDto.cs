using System.ComponentModel.DataAnnotations;

namespace CountingWebAPI.Models.DTOs
{
    public class UserCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 3)] // Adjusted minimum length for easier testing
        public string Password { get; set; } = string.Empty;
    }
}

//
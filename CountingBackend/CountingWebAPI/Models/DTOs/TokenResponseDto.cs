namespace CountingWebAPI.Models.DTOs
{
    public class TokenResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
    }
}

//
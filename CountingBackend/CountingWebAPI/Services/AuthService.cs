using CountingWebAPI.Data;
using CountingWebAPI.Helpers;
using CountingWebAPI.Models;
using CountingWebAPI.Models.Database;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class AuthService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IDatabaseHelper dbHelper, IOptions<JwtSettings> jwtSettings, ILogger<AuthService> logger)
        {
            _dbHelper = dbHelper;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        public async Task<TokenResponseDto?> LoginAsync(LoginRequestDto loginRequest)
        {
            string sql = "SELECT Id, Username, PasswordHash FROM Users WHERE Username = @Username";
            var user = await _dbHelper.QuerySingleOrDefaultAsync(sql,
                reader => new User {
                    Id = reader.GetInt32Safe("Id"),
                    Username = reader.GetStringSafe("Username"),
                    PasswordHash = reader.GetStringSafe("PasswordHash")
                },
                _dbHelper.CreateParameter("@Username", loginRequest.Username)
            );

            if (user == null || !PasswordHelper.VerifyPassword(loginRequest.Password, user.PasswordHash))
            {
                _logger.LogWarning($"Login failed for user: {loginRequest.Username}. Invalid credentials.");
                return null;
            }

            var token = JwtHelper.GenerateJwtToken(user, _jwtSettings);
            _logger.LogInformation($"Login successful for user: {loginRequest.Username}. Token generated.");

            return new TokenResponseDto {
                Message = "Login successful", Token = token,
                User = new UserDto { Id = user.Id, Username = user.Username }
            };
        }
    }
}
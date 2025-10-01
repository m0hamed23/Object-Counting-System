using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Models.DTOs;
using CountingWebAPI.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;
        public AuthController(AuthService authService, ILogger<AuthController> logger) { _authService = authService; _logger = logger; }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            _logger.LogInformation($"Login attempt for: '{loginRequest.Username}'");
            var tokenResponse = await _authService.LoginAsync(loginRequest);
            if (tokenResponse == null) { _logger.LogWarning($"Login failed for '{loginRequest.Username}'."); return Unauthorized(new { message = "Invalid username or password" }); }
            return Ok(tokenResponse); 
        }
    }
}
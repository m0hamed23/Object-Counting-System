using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Services;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CountingWebAPI.Data;
using System.Security.Claims;
using System;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SettingsController : ControllerBase
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger<SettingsController> _logger;
        private readonly IDatabaseHelper _dbHelper;

        public SettingsController(SettingsService settingsService, ILogger<SettingsController> logger, IDatabaseHelper dbHelper) 
        { 
            _settingsService = settingsService; 
            _logger = logger;
            _dbHelper = dbHelper;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private async Task LogActionAsync(string eventText)
        {
            var currentUserId = GetCurrentUserId();
            string logEvent = $"{eventText} Action by User:{currentUserId}";
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", logEvent)
            );
        }

        [HttpGet] public async Task<ActionResult<IEnumerable<SettingDto>>> GetSettings() => Ok(await _settingsService.GetVisibleSettingsAsync());
        
        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] List<SettingDto> settingsToUpdate) {
            if (settingsToUpdate == null || !settingsToUpdate.Any()) return BadRequest(new { message = "No settings provided." });
            _logger.LogInformation($"Updating {settingsToUpdate.Count} settings.");
            
            if (await _settingsService.UpdateSettingsAsync(settingsToUpdate))
            {
                await LogActionAsync("Application settings updated.");
                return Ok(new { message = "Settings updated successfully" });
            }

            return StatusCode(500, new { message = "Failed to update settings." });
        }
    }
}
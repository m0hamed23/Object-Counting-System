using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Services;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace CountingWebAPI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class SettingsController : BaseApiController
    {
        private readonly SettingsService _settingsService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(SettingsService settingsService, ILogger<SettingsController> logger) 
        { 
            _settingsService = settingsService; 
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SettingDto>>> GetSettings()
        {
            return Ok(await _settingsService.GetVisibleSettingsAsync());
        }
        
        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] List<SettingDto> settingsToUpdate)
        {
            if (settingsToUpdate == null || !settingsToUpdate.Any())
            {
                return BadRequest(new { message = "No settings provided." });
            }

            _logger.LogInformation($"User {GetCurrentUserId()} is updating {settingsToUpdate.Count} settings.");
            
            var success = await _settingsService.UpdateSettingsAsync(settingsToUpdate, GetCurrentUserId());

            if (success)
            {
                return Ok(new { message = "Settings updated successfully" });
            }

            return StatusCode(500, new { message = "Failed to update settings." });
        }
    }
}
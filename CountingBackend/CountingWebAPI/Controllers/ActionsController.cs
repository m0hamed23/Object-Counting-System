using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Data;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using CountingWebAPI.Services;
using System.Security.Claims;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ActionsController : ControllerBase
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<ActionsController> _logger;
        private readonly ActionExecutionService _actionExecutionService;
        private readonly VideoProcessingManager _videoManager;

        public ActionsController(IDatabaseHelper dbHelper, ILogger<ActionsController> logger, ActionExecutionService actionExecutionService, VideoProcessingManager videoManager)
        {
            _dbHelper = dbHelper;
            _logger = logger;
            _actionExecutionService = actionExecutionService;
            _videoManager = videoManager;
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ActionDto>>> GetActions()
        {
            string sql = "SELECT Id, Name, IpAddress, Port, IntervalMilliseconds, Protocol, IsEnabled FROM Actions ORDER BY Name";
            var actions = await _dbHelper.QueryAsync(sql, r => new ActionDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name"),
                IpAddress = r.GetStringSafe("IpAddress"),
                Port = r.GetInt32Safe("Port"),
                IntervalMilliseconds = r.GetInt32Safe("IntervalMilliseconds"),
                Protocol = r.GetStringSafe("Protocol"),
                IsEnabled = r.GetBooleanSafe("IsEnabled")
            });
            return Ok(actions);
        }

        [HttpPost]
        public async Task<ActionResult<ActionDto>> AddAction([FromBody] ActionCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Actions WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0) return Conflict(new { message = $"Action with name '{dto.Name}' already exists." });

            string insertSql = @"
                INSERT INTO Actions (Name, IpAddress, Port, IntervalMilliseconds, Protocol, IsEnabled, LastUpdated) 
                VALUES (@Name, @IpAddress, @Port, @IntervalMilliseconds, @Protocol, @IsEnabled, datetime('now'))";

            var newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(insertSql,
                _dbHelper.CreateParameter("@Name", dto.Name),
                _dbHelper.CreateParameter("@IpAddress", dto.IpAddress),
                _dbHelper.CreateParameter("@Port", dto.Port),
                _dbHelper.CreateParameter("@IntervalMilliseconds", dto.IntervalMilliseconds),
                _dbHelper.CreateParameter("@Protocol", dto.Protocol),
                _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled)
            );

            await _actionExecutionService.ReloadConfigurationAsync();
            await LogActionAsync($"Action '{dto.Name}' created.");

            var newDto = new ActionDto { Id = (int)newId, Name = dto.Name, IpAddress = dto.IpAddress, Port = dto.Port, IntervalMilliseconds = dto.IntervalMilliseconds, Protocol = dto.Protocol, IsEnabled = dto.IsEnabled };
            return CreatedAtAction(nameof(GetActions), new { id = newId }, newDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAction(int id, [FromBody] ActionCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Actions WHERE Name = @Name AND Id != @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0) return Conflict(new { message = $"Action with name '{dto.Name}' already exists." });

            string updateSql = @"
                UPDATE Actions SET 
                Name = @Name, IpAddress = @IpAddress, Port = @Port, IntervalMilliseconds = @IntervalMilliseconds, 
                Protocol = @Protocol, IsEnabled = @IsEnabled, LastUpdated = datetime('now')
                WHERE Id = @Id";
            
            int rowsAffected = await _dbHelper.ExecuteNonQueryAsync(updateSql,
                _dbHelper.CreateParameter("@Name", dto.Name),
                _dbHelper.CreateParameter("@IpAddress", dto.IpAddress),
                _dbHelper.CreateParameter("@Port", dto.Port),
                _dbHelper.CreateParameter("@IntervalMilliseconds", dto.IntervalMilliseconds),
                _dbHelper.CreateParameter("@Protocol", dto.Protocol),
                _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled),
                _dbHelper.CreateParameter("@Id", id)
            );

            if (rowsAffected == 0) return NotFound(new { message = "Action not found." });

            await _actionExecutionService.ReloadConfigurationAsync();
            await LogActionAsync($"Action '{dto.Name}' (ID:{id}) updated.");
            
            return Ok(new { message = "Action updated successfully."});
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAction(int id)
        {
            var actionName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Actions WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (actionName == null) return NotFound(new { message = "Action not found." });

            int rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Actions WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows == 0) return NotFound(new { message = "Action not found." });
            
            await _actionExecutionService.ReloadConfigurationAsync();
            await LogActionAsync($"Action '{actionName}' (ID:{id}) deleted.");

            return Ok(new { message = "Action deleted successfully." });
        }
    }
}
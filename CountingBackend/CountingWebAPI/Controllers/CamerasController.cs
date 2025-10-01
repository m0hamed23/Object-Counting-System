
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Data;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Security.Claims;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/cameras")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CamerasController : ControllerBase
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<CamerasController> _logger;

        public CamerasController(IDatabaseHelper dbHelper, ILogger<CamerasController> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
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
        public async Task<ActionResult<IEnumerable<CameraDto>>> GetCameras()
        {
            string sql = "SELECT Id, Name, RtspUrl, IsEnabled, LastUpdated FROM Cameras ORDER BY Name";
            var cameras = await _dbHelper.QueryAsync(sql, r => new CameraDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name"),
                RtspUrl = r.GetStringSafe("RtspUrl"),
                IsEnabled = r.GetBooleanSafe("IsEnabled"),
                LastUpdated = r.GetDateTimeSafe("LastUpdated")
            });
            return Ok(cameras);
        }

        [HttpPost]
        public async Task<ActionResult<CameraDto>> AddCamera([FromBody] CameraCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Cameras WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0) return Conflict(new { message = $"Camera with name '{dto.Name}' already exists." });

            var trimmedRtspUrl = dto.RtspUrl.Trim();

            string insertSql = @"
                INSERT INTO Cameras (Name, RtspUrl, IsEnabled, LastUpdated) 
                VALUES (@Name, @RtspUrl, @IsEnabled, datetime('now'))";

            long newId;
            try
            {
                newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(insertSql,
                    _dbHelper.CreateParameter("@Name", dto.Name),
                    _dbHelper.CreateParameter("@RtspUrl", trimmedRtspUrl),
                    _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled)
                );
                await LogActionAsync($"Camera '{dto.Name}' created.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding camera.");
                return StatusCode(500, "An internal error occurred while adding the camera.");
            }

            var newDto = new CameraDto { Id = (int)newId, Name = dto.Name, RtspUrl = trimmedRtspUrl, IsEnabled = dto.IsEnabled, LastUpdated = DateTime.UtcNow };
            return CreatedAtAction(nameof(GetCameras), new { id = newId }, newDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCamera(int id, [FromBody] CameraCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Cameras WHERE Name = @Name AND Id != @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0) return Conflict(new { message = $"Camera with name '{dto.Name}' already exists." });

            var trimmedRtspUrl = dto.RtspUrl.Trim();

            string updateSql = @"
                UPDATE Cameras SET 
                Name = @Name, RtspUrl = @RtspUrl, IsEnabled = @IsEnabled, LastUpdated = datetime('now')
                WHERE Id = @Id";
            
            int rowsAffected = await _dbHelper.ExecuteNonQueryAsync(updateSql,
                _dbHelper.CreateParameter("@Name", dto.Name),
                _dbHelper.CreateParameter("@RtspUrl", trimmedRtspUrl),
                _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled),
                _dbHelper.CreateParameter("@Id", id)
            );

            if (rowsAffected == 0) return NotFound(new { message = "Camera not found." });

            await LogActionAsync($"Camera '{dto.Name}' (ID:{id}) updated.");
            _logger.LogInformation("Camera {CameraId} updated. Note: App may require restart to apply changes.", id);
            return Ok(new { message = "Camera updated successfully. Restart the application to apply changes to video processing."});
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCamera(int id)
        {
            var cameraName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (cameraName == null) return NotFound(new { message = "Camera not found." });

            int rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows == 0) return NotFound(new { message = "Camera not found." });

            await LogActionAsync($"Camera '{cameraName}' (ID:{id}) deleted.");
            _logger.LogInformation("Camera {CameraId} deleted. Note: App may require restart to apply changes.", id);
            return Ok(new { message = "Camera deleted successfully. Restart the application to apply changes to video processing." });
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Data;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/zones")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ZonesController : ControllerBase
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<ZonesController> _logger;

        public ZonesController(IDatabaseHelper dbHelper, ILogger<ZonesController> logger)
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
        public async Task<ActionResult<IEnumerable<ZoneDto>>> GetZones()
        {
            var zones = await _dbHelper.QueryAsync("SELECT Id, Name FROM Zones", r => new ZoneDto { Id = r.GetInt32Safe("Id"), Name = r.GetStringSafe("Name") });

            foreach (var zone in zones)
            {
                zone.Cameras = await GetCamerasForZoneAsync(zone.Id);
            }
            return Ok(zones);
        }

        [HttpPost]
        public async Task<ActionResult<ZoneDto>> CreateZone([FromBody] ZoneCreateDto dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Zone name is required." });

            var existing = await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Zones WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0) return Conflict(new { message = $"Zone with name '{dto.Name}' already exists." });
            
            string sql = "INSERT INTO Zones (Name) VALUES (@Name)";
            var newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(sql, _dbHelper.CreateParameter("@Name", dto.Name));

            await LogActionAsync($"Zone '{dto.Name}' created.");

            var newZone = new ZoneDto { Id = (int)newId, Name = dto.Name };
            return CreatedAtAction(nameof(GetZones), new { id = newId }, newZone);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateZone(int id, [FromBody] ZoneCreateDto dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Zone name is required." });
            
            var existing = await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Zones WHERE Name = @Name AND Id != @Id", _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0) return Conflict(new { message = $"Zone with name '{dto.Name}' already exists." });

            string sql = "UPDATE Zones SET Name = @Name WHERE Id = @Id";
            var rows = await _dbHelper.ExecuteNonQueryAsync(sql, _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));

            if (rows > 0)
            {
                await LogActionAsync($"Zone '{dto.Name}' (ID:{id}) updated.");
                return NoContent();
            }

            return NotFound(new { message = "Zone not found." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteZone(int id)
        {
            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (zoneName == null) return NotFound(new { message = "Zone not found." });

            var rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            
            if (rows > 0)
            {
                await LogActionAsync($"Zone '{zoneName}' (ID:{id}) deleted.");
                return Ok(new { message = "Zone deleted successfully."});
            }

            return NotFound(new { message = "Zone not found." });
        }

        [HttpPost("{id:int}/cameras")]
        public async Task<IActionResult> AddCameraToZone(int id, [FromBody] ZoneAssociationDto dto)
        {
            string sql = "INSERT OR IGNORE INTO ZoneCameras (ZoneId, CameraId) VALUES (@ZoneId, @CameraId)";
            await _dbHelper.ExecuteNonQueryAsync(sql, _dbHelper.CreateParameter("@ZoneId", id), _dbHelper.CreateParameter("@CameraId", dto.Id));

            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            var cameraName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", dto.Id));
            await LogActionAsync($"Camera '{cameraName}' associated with Zone '{zoneName}'.");

            return Ok(new { message = "Camera associated with zone." });
        }

        [HttpDelete("{id:int}/cameras/{cameraId:int}")]
        public async Task<IActionResult> RemoveCameraFromZone(int id, int cameraId)
        {
            string sql = "DELETE FROM ZoneCameras WHERE ZoneId = @ZoneId AND CameraId = @CameraId";
            await _dbHelper.ExecuteNonQueryAsync(sql, _dbHelper.CreateParameter("@ZoneId", id), _dbHelper.CreateParameter("@CameraId", cameraId));

            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            var cameraName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", cameraId));
            await LogActionAsync($"Camera '{cameraName}' disassociated from Zone '{zoneName}'.");

            return Ok(new { message = "Camera disassociated from zone." });
        }

        private async Task<List<CameraDto>> GetCamerasForZoneAsync(int zoneId)
        {
            string sql = @"SELECT c.Id, c.Name, c.RtspUrl, c.IsEnabled, c.LastUpdated 
                            FROM Cameras c
                            JOIN ZoneCameras zc ON c.Id = zc.CameraId
                            WHERE zc.ZoneId = @ZoneId";
            return await _dbHelper.QueryAsync(sql, r => new CameraDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name"),
                RtspUrl = r.GetStringSafe("RtspUrl"),
                IsEnabled = r.GetBooleanSafe("IsEnabled"),
                LastUpdated = r.GetDateTimeSafe("LastUpdated")
            }, _dbHelper.CreateParameter("@ZoneId", zoneId));
        }
    }
}
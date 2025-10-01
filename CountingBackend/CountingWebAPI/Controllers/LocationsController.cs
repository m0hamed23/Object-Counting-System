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
    [Route("api/locations")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class LocationsController : ControllerBase
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<LocationsController> _logger;

        public LocationsController(IDatabaseHelper dbHelper, ILogger<LocationsController> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private async Task LogActionAsync(string eventText)
        {
            var currentUserId = GetCurrentUserId();
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", $"{eventText} Action by User:{currentUserId}")
            );
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LocationDto>>> GetLocations()
        {
            var locations = await _dbHelper.QueryAsync("SELECT Id, Name FROM Locations ORDER BY Name", r => new LocationDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name")
            });

            foreach (var location in locations)
            {
                location.Zones = await GetZonesForLocationAsync(location.Id);
            }
            return Ok(locations);
        }

        [HttpPost]
        public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] LocationCreateDto dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Location name is required." });

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Locations WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0)
                return Conflict(new { message = $"Location with name '{dto.Name}' already exists." });

            var newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(
                "INSERT INTO Locations (Name) VALUES (@Name)", _dbHelper.CreateParameter("@Name", dto.Name));

            await LogActionAsync($"Location '{dto.Name}' created.");

            var newLocation = new LocationDto { Id = (int)newId, Name = dto.Name };
            return CreatedAtAction(nameof(GetLocations), new { id = newId }, newLocation);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationCreateDto dto)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Location name is required." });

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Locations WHERE Name = @Name AND Id != @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0)
                return Conflict(new { message = $"Location with name '{dto.Name}' already exists." });

            var rows = await _dbHelper.ExecuteNonQueryAsync(
                "UPDATE Locations SET Name = @Name WHERE Id = @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));

            if (rows > 0)
            {
                await LogActionAsync($"Location '{dto.Name}' (ID:{id}) updated.");
                return NoContent();
            }
            return NotFound(new { message = "Location not found." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var locationName = await _dbHelper.ExecuteScalarAsync<string>(
                "SELECT Name FROM Locations WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (locationName == null)
                return NotFound(new { message = "Location not found." });

            var rows = await _dbHelper.ExecuteNonQueryAsync(
                "DELETE FROM Locations WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));

            if (rows > 0)
            {
                await LogActionAsync($"Location '{locationName}' (ID:{id}) deleted.");
                return Ok(new { message = "Location deleted successfully." });
            }
            return NotFound(new { message = "Location not found." });
        }

        [HttpPost("{id:int}/zones")]
        public async Task<IActionResult> AddZoneToLocation(int id, [FromBody] ZoneAssociationDto dto)
        {
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT OR IGNORE INTO LocationZones (LocationId, ZoneId) VALUES (@LocationId, @ZoneId)",
                _dbHelper.CreateParameter("@LocationId", id), _dbHelper.CreateParameter("@ZoneId", dto.Id));

            await LogAssociationChange(id, dto.Id, "associated with");
            return Ok(new { message = "Zone associated with location." });
        }

        [HttpDelete("{id:int}/zones/{zoneId:int}")]
        public async Task<IActionResult> RemoveZoneFromLocation(int id, int zoneId)
        {
            await _dbHelper.ExecuteNonQueryAsync(
                "DELETE FROM LocationZones WHERE LocationId = @LocationId AND ZoneId = @ZoneId",
                _dbHelper.CreateParameter("@LocationId", id), _dbHelper.CreateParameter("@ZoneId", zoneId));

            await LogAssociationChange(id, zoneId, "disassociated from");
            return Ok(new { message = "Zone disassociated from location." });
        }
        
        private async Task LogAssociationChange(int locationId, int zoneId, string action)
        {
            var locationName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Locations WHERE Id = @Id", _dbHelper.CreateParameter("@Id", locationId));
            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", zoneId));
            if(locationName != null && zoneName != null)
            {
                await LogActionAsync($"Zone '{zoneName}' {action} Location '{locationName}'.");
            }
        }

        private async Task<List<ZoneDto>> GetZonesForLocationAsync(int locationId)
        {
            string sql = @"SELECT z.Id, z.Name 
                            FROM Zones z
                            JOIN LocationZones lz ON z.Id = lz.ZoneId
                            WHERE lz.LocationId = @LocationId";
            return await _dbHelper.QueryAsync(sql, r => new ZoneDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name")
            }, _dbHelper.CreateParameter("@LocationId", locationId));
        }
    }
}
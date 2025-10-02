using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Collections.Generic;
using System.Threading.Tasks;
using CountingWebAPI.Services;
using CountingWebAPI.Models;

namespace CountingWebAPI.Controllers
{
    [Route("api/zones")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ZonesController : BaseApiController
    {
        private readonly ZoneService _zoneService;

        public ZonesController(ZoneService zoneService)
        {
            _zoneService = zoneService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ZoneDto>>> GetZones()
        {
            var zones = await _zoneService.GetZonesAsync();
            return Ok(zones);
        }

        [HttpPost]
        public async Task<ActionResult<ZoneDto>> CreateZone([FromBody] ZoneCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Zone name is required." });

            var result = await _zoneService.CreateZoneAsync(dto, GetCurrentUserId());
            
            return result.Status switch
            {
                ServiceResultStatus.Created => CreatedAtAction(nameof(GetZones), new { id = result.Value!.Id }, result.Value),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                ServiceResultStatus.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateZone(int id, [FromBody] ZoneCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Zone name is required." });
            
            var result = await _zoneService.UpdateZoneAsync(id, dto, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Success => NoContent(),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                ServiceResultStatus.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteZone(int id)
        {
            var result = await _zoneService.DeleteZoneAsync(id, GetCurrentUserId());
            
            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { message = "Zone deleted successfully." }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpPost("{id:int}/cameras")]
        public async Task<IActionResult> AddCameraToZone(int id, [FromBody] ZoneAssociationDto dto)
        {
            await _zoneService.AddCameraToZoneAsync(id, dto.Id, GetCurrentUserId());
            return Ok(new { message = "Camera associated with zone." });
        }

        [HttpDelete("{id:int}/cameras/{cameraId:int}")]
        public async Task<IActionResult> RemoveCameraFromZone(int id, int cameraId)
        {
            await _zoneService.RemoveCameraFromZoneAsync(id, cameraId, GetCurrentUserId());
            return Ok(new { message = "Camera disassociated from zone." });
        }
    }
}
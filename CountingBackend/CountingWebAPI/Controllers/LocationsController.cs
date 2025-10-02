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
    [Route("api/locations")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class LocationsController : BaseApiController
    {
        private readonly LocationService _locationService;

        public LocationsController(LocationService locationService)
        {
            _locationService = locationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LocationDto>>> GetLocations()
        {
            var locations = await _locationService.GetLocationsAsync();
            return Ok(locations);
        }

        [HttpPost]
        public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] LocationCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Location name is required." });

            var result = await _locationService.CreateLocationAsync(dto, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Created => CreatedAtAction(nameof(GetLocations), new { id = result.Value!.Id }, result.Value),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                ServiceResultStatus.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(new { message = "Location name is required." });

            var result = await _locationService.UpdateLocationAsync(id, dto, GetCurrentUserId());
            
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
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var result = await _locationService.DeleteLocationAsync(id, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { message = "Location deleted successfully." }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpPost("{id:int}/zones")]
        public async Task<IActionResult> AddZoneToLocation(int id, [FromBody] ZoneAssociationDto dto)
        {
            await _locationService.AddZoneToLocationAsync(id, dto.Id, GetCurrentUserId());
            return Ok(new { message = "Zone associated with location." });
        }

        [HttpDelete("{id:int}/zones/{zoneId:int}")]
        public async Task<IActionResult> RemoveZoneFromLocation(int id, int zoneId)
        {
            await _locationService.RemoveZoneFromLocationAsync(id, zoneId, GetCurrentUserId());
            return Ok(new { message = "Zone disassociated from location." });
        }
    }
}
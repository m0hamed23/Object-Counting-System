using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using CountingWebAPI.Services;
using CountingWebAPI.Models;

namespace CountingWebAPI.Controllers
{
    [Route("api/cameras")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CamerasController : BaseApiController
    {
        private readonly CameraService _cameraService;
        private readonly ILogger<CamerasController> _logger;

        public CamerasController(CameraService cameraService, ILogger<CamerasController> logger)
        {
            _cameraService = cameraService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CameraDto>>> GetCameras()
        {
            var cameras = await _cameraService.GetCamerasAsync();
            return Ok(cameras);
        }

        [HttpPost]
        public async Task<ActionResult<CameraDto>> AddCamera([FromBody] CameraCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var result = await _cameraService.AddCameraAsync(dto, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Created => CreatedAtAction(nameof(GetCameras), new { id = result.Value!.Id }, result.Value),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                ServiceResultStatus.Error => StatusCode(500, new { message = result.ErrorMessage }),
                _ => StatusCode(500, new { message = "An unexpected error occurred while adding the camera." })
            };
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCamera(int id, [FromBody] CameraCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var result = await _cameraService.UpdateCameraAsync(id, dto, GetCurrentUserId());
            
            _logger.LogInformation("Camera {CameraId} updated. Note: App may require restart to apply changes.", id);

            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { message = "Camera updated successfully. Restart the application to apply changes to video processing." }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                _ => StatusCode(500, new { message = "An unexpected error occurred while updating the camera." })
            };
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCamera(int id)
        {
            var result = await _cameraService.DeleteCameraAsync(id, GetCurrentUserId());

            if (result.Status == ServiceResultStatus.Success)
            {
                _logger.LogInformation("Camera {CameraId} deleted. Note: App may require restart to apply changes.", id);
            }

            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { message = "Camera deleted successfully. Restart the application to apply changes to video processing." }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(500, new { message = "An unexpected error occurred while deleting the camera." })
            };
        }
    }
}
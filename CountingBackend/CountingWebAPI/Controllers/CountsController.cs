using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Services;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CountsController : ControllerBase
    {
        private readonly VideoProcessingManager _videoManager;

        public CountsController(VideoProcessingManager videoManager)
        {
            _videoManager = videoManager;
        }

        [HttpGet("locations")]
        public async Task<ActionResult<IEnumerable<ObjectCountDto>>> GetLocationCounts()
        {
            await _videoManager.InitializationTask;
            var locationCounts = await _videoManager.GetAllLocationCountsAsync();
            var result = locationCounts.Select(l => new ObjectCountDto
            {
                Id = l.LocationId,
                Name = l.LocationName,
                Count = l.TotalCount
            });
            return Ok(result);
        }

        [HttpGet("zones")]
        public async Task<ActionResult<IEnumerable<ObjectCountDto>>> GetZoneCounts()
        {
            await _videoManager.InitializationTask;
            var zoneStatuses = await _videoManager.GetAllZoneStatusesAsync();

            var result = zoneStatuses.Select(z => new ObjectCountDto
            {
                Id = z.Id,
                Name = z.Name,
                Count = z.TotalTrackedCount
            });

            return Ok(result);
        }
        
        [HttpGet("cameras")]
        public async Task<ActionResult<IEnumerable<CameraCountDto>>> GetCameraCounts()
        {
            await _videoManager.InitializationTask;
            var processors = _videoManager.GetActiveProcessors();
            
            var result = processors.Select(p => new CameraCountDto
            {
                CameraId = p.Id,
                CameraName = p.Name,
                TotalTrackedCount = p.TotalTrackedCount,
                Status = p.GetCurrentStatus()
            });

            return Ok(result);
        }
    }
}
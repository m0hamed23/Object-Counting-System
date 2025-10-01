
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CountingWebAPI.Services;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DetectionController : ControllerBase
    {
        public DetectionController() { }

        [HttpGet("classes")]
        public IActionResult GetAvailableClasses()
        {
            // Return the public static list of class names from the YoloDetector service
            return Ok(YoloDetector.CocoClassNames);
        }
    }
}
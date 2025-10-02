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
    [ApiController]
    [Route("api")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class LogsController : ControllerBase
    {
        private readonly LogService _logService;
        private readonly ILogger<LogsController> _logger;

        public LogsController(LogService logService, ILogger<LogsController> logger)
        {
            _logService = logService;
            _logger = logger;
        }

        [HttpGet("logs")]
        public async Task<ActionResult<IEnumerable<LogEntryDto>>> GetLogs([FromQuery] string? from_date, [FromQuery] string? to_date, [FromQuery] string? event_text)
        {
            var logs = await _logService.GetLogsAsync(from_date, to_date, event_text);
            return Ok(logs);
        }

        [HttpGet("images/{imageName}")]
        public IActionResult GetLogImage(string imageName)
        {
            var result = _logService.GetLogImage(imageName);

            if (!result.IsSuccess)
            {
                return result.Status switch
                {
                    ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                    ServiceResultStatus.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                    _ => StatusCode(500, new { message = result.ErrorMessage })
                };
            }

            // The 'Value' property of a successful ServiceResult will not be null here.
            // Corrected line: removed the extra ".Value"
            return File(result.Value!.Bytes, result.Value!.ContentType);
        }
    }
}
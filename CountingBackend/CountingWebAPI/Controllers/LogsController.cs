
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Data;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using CountingWebAPI.Models;
using System.Globalization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Data.Common;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class LogsController : ControllerBase
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<LogsController> _logger;
        private readonly string _imageLogPath;

        public LogsController(IDatabaseHelper dbHelper, ILogger<LogsController> logger, IOptions<AppSettings> appOpt)
        {
            _dbHelper = dbHelper;
            _logger = logger;
            _imageLogPath = appOpt.Value.ImageLogPath;
            if (string.IsNullOrEmpty(_imageLogPath)) _imageLogPath = Path.Combine(AppContext.BaseDirectory, "ImageLogs_Fallback");
            if (!Directory.Exists(_imageLogPath)) try { Directory.CreateDirectory(_imageLogPath); _logger.LogInformation($"Created image log dir: {_imageLogPath}"); } catch (Exception ex) { _logger.LogError(ex, $"Fail create image log dir: {_imageLogPath}"); }
        }

        [HttpGet("logs")]
        public async Task<ActionResult<IEnumerable<LogEntryDto>>> GetLogs([FromQuery] string? from_date, [FromQuery] string? to_date, [FromQuery] string? event_text)
        {
            var sqlBuilder = new StringBuilder(@"
                SELECT l.Id, l.Timestamp, l.Event, l.ImageName, l.CameraIndex, u.Username 
                FROM Logs l
                LEFT JOIN Users u ON l.Event LIKE '%Action by User:%' AND u.Id = CAST(SUBSTR(l.Event, INSTR(l.Event, 'User:') + 5) AS INTEGER)
                WHERE 1=1");
            
            var parameters = new List<DbParameter>();
            if (DateTime.TryParse(from_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fromDate)) { sqlBuilder.Append(" AND l.Timestamp >= @FromDate"); parameters.Add(_dbHelper.CreateParameter("@FromDate", fromDate.ToString("yyyy-MM-dd HH:mm:ss"))); }
            if (DateTime.TryParse(to_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var toDate)) { sqlBuilder.Append(" AND l.Timestamp <= @ToDate"); parameters.Add(_dbHelper.CreateParameter("@ToDate", toDate.ToString("yyyy-MM-dd HH:mm:ss"))); }
            if (!string.IsNullOrEmpty(event_text)) { sqlBuilder.Append(" AND l.Event LIKE @EventText"); parameters.Add(_dbHelper.CreateParameter("@EventText", $"%{event_text}%")); }
            
            sqlBuilder.Append(" ORDER BY l.Timestamp DESC LIMIT 1000");

            var logs = await _dbHelper.QueryAsync(sqlBuilder.ToString(), r => 
            {
                var eventTextVal = r.GetStringSafe("Event");
                var username = r.GetNullableString("Username");
                
                if (!string.IsNullOrEmpty(username) && eventTextVal.Contains("Action by User:"))
                {
                    eventTextVal = Regex.Replace(eventTextVal, @"Action by User:\d+", $"Action by User '{username}'");
                }

                return new LogEntryDto { 
                    Id = r.GetInt32Safe("Id"), 
                    Timestamp = r.GetDateTimeSafe("Timestamp").ToString("o"), 
                    Event = eventTextVal, 
                    ImageName = r.GetNullableString("ImageName"), 
                    CameraIndex = r.GetNullableInt32("CameraIndex") 
                };
            }, parameters.ToArray());

            _logger.LogInformation($"Fetched {logs.Count} logs. Filters:from={from_date},to={to_date},event={event_text}."); return Ok(logs);
        }

        [HttpGet("images/{imageName}")]
        public IActionResult GetLogImage(string imageName)
        {
            if (string.IsNullOrEmpty(imageName) || imageName.Contains("..") || imageName.Contains("/") || imageName.Contains("\\")) { _logger.LogWarning($"Invalid image name: {imageName}"); return BadRequest(new { message = "Invalid image name." }); }
            var imgPath = Path.Combine(_imageLogPath, imageName);
            if (!System.IO.File.Exists(imgPath)) { _logger.LogWarning($"Image not found: {imgPath}"); return NotFound(new { message = "Image not found." }); }
            try { var bytes = System.IO.File.ReadAllBytes(imgPath); var contentType = imageName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg"; return File(bytes, contentType); }
            catch (Exception ex) { _logger.LogError(ex, $"Error serving image: {imgPath}"); return StatusCode(500, new { message = $"Error serving image: {ex.Message}" }); }
        }
    }
}
using CountingWebAPI.Data;
using CountingWebAPI.Models;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class LogService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<LogService> _logger;
        private readonly string _imageLogPath;

        public LogService(IDatabaseHelper dbHelper, ILogger<LogService> logger, IOptions<AppSettings> appOpt)
        {
            _dbHelper = dbHelper;
            _logger = logger;
            _imageLogPath = appOpt.Value.ImageLogPath;
            if (string.IsNullOrEmpty(_imageLogPath)) _imageLogPath = Path.Combine(AppContext.BaseDirectory, "ImageLogs_Fallback");
            if (!Directory.Exists(_imageLogPath))
            {
                try
                {
                    Directory.CreateDirectory(_imageLogPath);
                    _logger.LogInformation($"Created image log directory: {_imageLogPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to create image log directory: {_imageLogPath}");
                }
            }
        }

        public async Task<IEnumerable<LogEntryDto>> GetLogsAsync(string? from_date, string? to_date, string? event_text)
        {
            var sqlBuilder = new StringBuilder(@"
                SELECT l.Id, l.Timestamp, l.Event, l.ImageName, l.CameraIndex, u.Username 
                FROM Logs l
                LEFT JOIN Users u ON l.Event LIKE '%Action by User:%' AND u.Id = CAST(SUBSTR(l.Event, INSTR(l.Event, 'User:') + 5) AS INTEGER)
                WHERE 1=1");

            var parameters = new List<DbParameter>();

            // --- FIX IS HERE: Removed DateTimeStyles.AssumeUniversal ---
            if (DateTime.TryParse(from_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var fromDate))
            {
                sqlBuilder.Append(" AND l.Timestamp >= @FromDate");
                parameters.Add(_dbHelper.CreateParameter("@FromDate", fromDate.ToString("yyyy-MM-dd HH:mm:ss")));
            }
            // --- FIX IS HERE: Removed DateTimeStyles.AssumeUniversal ---
            if (DateTime.TryParse(to_date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var toDate))
            {
                sqlBuilder.Append(" AND l.Timestamp <= @ToDate");
                parameters.Add(_dbHelper.CreateParameter("@ToDate", toDate.ToString("yyyy-MM-dd HH:mm:ss")));
            }

            if (!string.IsNullOrEmpty(event_text))
            {
                sqlBuilder.Append(" AND l.Event LIKE @EventText");
                parameters.Add(_dbHelper.CreateParameter("@EventText", $"%{event_text}%"));
            }

            sqlBuilder.Append(" ORDER BY l.Timestamp DESC LIMIT 1000");

            var logs = await _dbHelper.QueryAsync(sqlBuilder.ToString(), r =>
            {
                var eventTextVal = r.GetStringSafe("Event");
                var username = r.GetNullableString("Username");

                if (!string.IsNullOrEmpty(username) && eventTextVal.Contains("Action by User:"))
                {
                    eventTextVal = Regex.Replace(eventTextVal, @"Action by User:\d+", $"Action by User '{username}'");
                }

                return new LogEntryDto
                {
                    Id = r.GetInt32Safe("Id"),
                    Timestamp = r.GetDateTimeSafe("Timestamp").ToString("o"),
                    Event = eventTextVal,
                    ImageName = r.GetNullableString("ImageName"),
                    CameraIndex = r.GetNullableInt32("CameraIndex")
                };
            }, parameters.ToArray());

            _logger.LogInformation($"Fetched {logs.Count()} logs. Filters:from={from_date},to={to_date},event={event_text}.");
            return logs;
        }

        public ServiceResult<(byte[] Bytes, string ContentType)> GetLogImage(string imageName)
        {
            if (string.IsNullOrEmpty(imageName) || imageName.Contains("..") || imageName.Contains('/') || imageName.Contains('\\'))
            {
                _logger.LogWarning($"Invalid image name request: {imageName}");
                return ServiceResult<(byte[] Bytes, string ContentType)>.Fail(ServiceResultStatus.BadRequest, "Invalid image name.");
            }

            var imgPath = Path.Combine(_imageLogPath, imageName);
            if (!System.IO.File.Exists(imgPath))
            {
                _logger.LogWarning($"Image not found: {imgPath}");
                return ServiceResult<(byte[] Bytes, string ContentType)>.Fail(ServiceResultStatus.NotFound, "Image not found.");
            }

            try
            {
                var bytes = System.IO.File.ReadAllBytes(imgPath);
                var contentType = imageName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                return ServiceResult<(byte[] Bytes, string ContentType)>.Success((bytes, contentType));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error serving image: {imgPath}");
                return ServiceResult<(byte[] Bytes, string ContentType)>.Fail(ServiceResultStatus.Error, $"An internal error occurred while serving the image: {ex.Message}");
            }
        }
    }
}
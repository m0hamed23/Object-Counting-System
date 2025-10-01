using CountingWebAPI.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class RoiService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<RoiService> _logger;

        public RoiService(IDatabaseHelper dbHelper, ILogger<RoiService> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
        }

        public async Task<List<List<double>>?> GetRoiAsync(int cameraId)
        {
            string sql = "SELECT RoiData FROM CameraRois WHERE CameraIndex = @CameraId";
            var roiJson = await _dbHelper.ExecuteScalarAsync<string>(sql, _dbHelper.CreateParameter("@CameraId", cameraId));

            if (string.IsNullOrEmpty(roiJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<List<List<double>>>(roiJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize ROI JSON for camera {CameraId}", cameraId);
                return null;
            }
        }

        public async Task SaveRoiAsync(int cameraId, List<List<double>> roi)
        {
            try
            {
                var roiJson = JsonSerializer.Serialize(roi);
                string sql = @"
                    INSERT INTO CameraRois (CameraIndex, RoiData, LastUpdated)
                    VALUES (@CameraId, @RoiData, datetime('now'))
                    ON CONFLICT (CameraIndex) DO UPDATE 
                    SET RoiData = excluded.RoiData,
                        LastUpdated = datetime('now');";

                await _dbHelper.ExecuteNonQueryAsync(sql,
                    _dbHelper.CreateParameter("@CameraId", cameraId),
                    _dbHelper.CreateParameter("@RoiData", roiJson));

                _logger.LogInformation("Successfully saved ROI for camera {CameraId}", cameraId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save ROI for camera {CameraId}", cameraId);
            }
        }
    }
}
using CountingWebAPI.Data;
using CountingWebAPI.Models;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class CameraService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<CameraService> _logger;

        public CameraService(IDatabaseHelper dbHelper, ILogger<CameraService> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
        }

        private async Task LogActionAsync(string eventText, string userId)
        {
            string logEvent = $"{eventText} Action by User:{userId}";
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", logEvent)
            );
        }

        public async Task<IEnumerable<CameraDto>> GetCamerasAsync()
        {
            string sql = "SELECT Id, Name, RtspUrl, IsEnabled, LastUpdated FROM Cameras ORDER BY Name";
            return await _dbHelper.QueryAsync(sql, r => new CameraDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name"),
                RtspUrl = r.GetStringSafe("RtspUrl"),
                IsEnabled = r.GetBooleanSafe("IsEnabled"),
                LastUpdated = r.GetDateTimeSafe("LastUpdated")
            });
        }

        public async Task<ServiceResult<CameraDto>> AddCameraAsync(CameraCreateDto dto, string userId)
        {
            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Cameras WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0)
            {
                return ServiceResult<CameraDto>.Fail(ServiceResultStatus.Conflict, $"Camera with name '{dto.Name}' already exists.");
            }

            var trimmedRtspUrl = dto.RtspUrl.Trim();
            string insertSql = @"
                INSERT INTO Cameras (Name, RtspUrl, IsEnabled, LastUpdated) 
                VALUES (@Name, @RtspUrl, @IsEnabled, datetime('now'))";

            long newId;
            try
            {
                newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(insertSql,
                    _dbHelper.CreateParameter("@Name", dto.Name),
                    _dbHelper.CreateParameter("@RtspUrl", trimmedRtspUrl),
                    _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled)
                );
                await LogActionAsync($"Camera '{dto.Name}' created.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding camera.");
                return ServiceResult<CameraDto>.Fail(ServiceResultStatus.Error, "An internal error occurred while adding the camera.");
            }

            var newDto = new CameraDto { Id = (int)newId, Name = dto.Name, RtspUrl = trimmedRtspUrl, IsEnabled = dto.IsEnabled, LastUpdated = DateTime.UtcNow };
            return ServiceResult<CameraDto>.Success(newDto, ServiceResultStatus.Created);
        }

        public async Task<ServiceResult> UpdateCameraAsync(int id, CameraCreateDto dto, string userId)
        {
            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Cameras WHERE Name = @Name AND Id != @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.Conflict, $"Camera with name '{dto.Name}' already exists.");
            }

            var trimmedRtspUrl = dto.RtspUrl.Trim();
            string updateSql = @"
                UPDATE Cameras SET 
                Name = @Name, RtspUrl = @RtspUrl, IsEnabled = @IsEnabled, LastUpdated = datetime('now')
                WHERE Id = @Id";

            int rowsAffected = await _dbHelper.ExecuteNonQueryAsync(updateSql,
                _dbHelper.CreateParameter("@Name", dto.Name),
                _dbHelper.CreateParameter("@RtspUrl", trimmedRtspUrl),
                _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled),
                _dbHelper.CreateParameter("@Id", id)
            );

            if (rowsAffected == 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Camera not found.");
            }

            await LogActionAsync($"Camera '{dto.Name}' (ID:{id}) updated.", userId);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteCameraAsync(int id, string userId)
        {
            var cameraName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (cameraName == null)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Camera not found.");
            }

            int rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows == 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Camera not found.");
            }

            await LogActionAsync($"Camera '{cameraName}' (ID:{id}) deleted.", userId);
            return ServiceResult.Success();
        }
    }
}
using CountingWebAPI.Data;
using CountingWebAPI.Models;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class ZoneService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<ZoneService> _logger;

        public ZoneService(IDatabaseHelper dbHelper, ILogger<ZoneService> logger)
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

        public async Task<IEnumerable<ZoneDto>> GetZonesAsync()
        {
            var zones = await _dbHelper.QueryAsync("SELECT Id, Name FROM Zones", r => new ZoneDto { Id = r.GetInt32Safe("Id"), Name = r.GetStringSafe("Name") });
            foreach (var zone in zones)
            {
                zone.Cameras = await GetCamerasForZoneAsync(zone.Id);
            }
            return zones;
        }

        public async Task<ServiceResult<ZoneDto>> CreateZoneAsync(ZoneCreateDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return ServiceResult<ZoneDto>.Fail(ServiceResultStatus.BadRequest, "Zone name is required.");
            }

            var existing = await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Zones WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0)
            {
                return ServiceResult<ZoneDto>.Fail(ServiceResultStatus.Conflict, $"Zone with name '{dto.Name}' already exists.");
            }

            string sql = "INSERT INTO Zones (Name) VALUES (@Name)";
            var newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(sql, _dbHelper.CreateParameter("@Name", dto.Name));

            await LogActionAsync($"Zone '{dto.Name}' created.", userId);

            var newZone = new ZoneDto { Id = (int)newId, Name = dto.Name };
            return ServiceResult<ZoneDto>.Success(newZone, ServiceResultStatus.Created);
        }

        public async Task<ServiceResult> UpdateZoneAsync(int id, ZoneCreateDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return ServiceResult.Fail(ServiceResultStatus.BadRequest, "Zone name is required.");
            }

            var existing = await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Zones WHERE Name = @Name AND Id != @Id", _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.Conflict, $"Zone with name '{dto.Name}' already exists.");
            }

            string sql = "UPDATE Zones SET Name = @Name WHERE Id = @Id";
            var rows = await _dbHelper.ExecuteNonQueryAsync(sql, _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));

            if (rows > 0)
            {
                await LogActionAsync($"Zone '{dto.Name}' (ID:{id}) updated.", userId);
                return ServiceResult.Success();
            }

            return ServiceResult.Fail(ServiceResultStatus.NotFound, "Zone not found.");
        }

        public async Task<ServiceResult> DeleteZoneAsync(int id, string userId)
        {
            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (zoneName == null)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Zone not found.");
            }

            var rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows > 0)
            {
                await LogActionAsync($"Zone '{zoneName}' (ID:{id}) deleted.", userId);
                return ServiceResult.Success();
            }
            return ServiceResult.Fail(ServiceResultStatus.NotFound, "Zone not found.");
        }

        public async Task AddCameraToZoneAsync(int zoneId, int cameraId, string userId)
        {
            string sql = "INSERT OR IGNORE INTO ZoneCameras (ZoneId, CameraId) VALUES (@ZoneId, @CameraId)";
            await _dbHelper.ExecuteNonQueryAsync(sql, _dbHelper.CreateParameter("@ZoneId", zoneId), _dbHelper.CreateParameter("@CameraId", cameraId));
            
            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", zoneId));
            var cameraName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", cameraId));
            await LogActionAsync($"Camera '{cameraName}' associated with Zone '{zoneName}'.", userId);
        }

        public async Task RemoveCameraFromZoneAsync(int zoneId, int cameraId, string userId)
        {
            string sql = "DELETE FROM ZoneCameras WHERE ZoneId = @ZoneId AND CameraId = @CameraId";
            await _dbHelper.ExecuteNonQueryAsync(sql, _dbHelper.CreateParameter("@ZoneId", zoneId), _dbHelper.CreateParameter("@CameraId", cameraId));

            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", zoneId));
            var cameraName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Cameras WHERE Id = @Id", _dbHelper.CreateParameter("@Id", cameraId));
            await LogActionAsync($"Camera '{cameraName}' disassociated from Zone '{zoneName}'.", userId);
        }
        
        private async Task<List<CameraDto>> GetCamerasForZoneAsync(int zoneId)
        {
            string sql = @"SELECT c.Id, c.Name, c.RtspUrl, c.IsEnabled, c.LastUpdated 
                            FROM Cameras c
                            JOIN ZoneCameras zc ON c.Id = zc.CameraId
                            WHERE zc.ZoneId = @ZoneId";
            return await _dbHelper.QueryAsync(sql, r => new CameraDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name"),
                RtspUrl = r.GetStringSafe("RtspUrl"),
                IsEnabled = r.GetBooleanSafe("IsEnabled"),
                LastUpdated = r.GetDateTimeSafe("LastUpdated")
            }, _dbHelper.CreateParameter("@ZoneId", zoneId));
        }
    }
}
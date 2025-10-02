using CountingWebAPI.Data;
using CountingWebAPI.Models;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class LocationService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<LocationService> _logger;

        public LocationService(IDatabaseHelper dbHelper, ILogger<LocationService> logger)
        {
            _dbHelper = dbHelper;
            _logger = logger;
        }

        private async Task LogActionAsync(string eventText, string userId)
        {
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", $"{eventText} Action by User:{userId}")
            );
        }

        public async Task<List<LocationDto>> GetLocationsAsync()
        {
            var locations = await _dbHelper.QueryAsync("SELECT Id, Name FROM Locations ORDER BY Name", r => new LocationDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name")
            });

            foreach (var location in locations)
            {
                location.Zones = await GetZonesForLocationAsync(location.Id);
            }
            return locations;
        }

        public async Task<ServiceResult<LocationDto>> CreateLocationAsync(LocationCreateDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return ServiceResult<LocationDto>.Fail(ServiceResultStatus.BadRequest, "Location name is required.");
            }

            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Locations WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0)
            {
                return ServiceResult<LocationDto>.Fail(ServiceResultStatus.Conflict, $"Location with name '{dto.Name}' already exists.");
            }

            var newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(
                "INSERT INTO Locations (Name) VALUES (@Name)", _dbHelper.CreateParameter("@Name", dto.Name));

            await LogActionAsync($"Location '{dto.Name}' created.", userId);

            var newLocation = new LocationDto { Id = (int)newId, Name = dto.Name };
            return ServiceResult<LocationDto>.Success(newLocation, ServiceResultStatus.Created);
        }

        public async Task<ServiceResult> UpdateLocationAsync(int id, LocationCreateDto dto, string userId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return ServiceResult.Fail(ServiceResultStatus.BadRequest, "Location name is required.");
            }
            
            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Locations WHERE Name = @Name AND Id != @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.Conflict, $"Location with name '{dto.Name}' already exists.");
            }

            var rows = await _dbHelper.ExecuteNonQueryAsync(
                "UPDATE Locations SET Name = @Name WHERE Id = @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));

            if (rows > 0)
            {
                await LogActionAsync($"Location '{dto.Name}' (ID:{id}) updated.", userId);
                return ServiceResult.Success();
            }
            return ServiceResult.Fail(ServiceResultStatus.NotFound, "Location not found.");
        }

        public async Task<ServiceResult> DeleteLocationAsync(int id, string userId)
        {
            var locationName = await _dbHelper.ExecuteScalarAsync<string>(
                "SELECT Name FROM Locations WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (locationName == null)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Location not found.");
            }

            var rows = await _dbHelper.ExecuteNonQueryAsync(
                "DELETE FROM Locations WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));

            if (rows > 0)
            {
                await LogActionAsync($"Location '{locationName}' (ID:{id}) deleted.", userId);
                return ServiceResult.Success();
            }
            return ServiceResult.Fail(ServiceResultStatus.NotFound, "Location not found.");
        }

        public async Task AddZoneToLocationAsync(int locationId, int zoneId, string userId)
        {
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT OR IGNORE INTO LocationZones (LocationId, ZoneId) VALUES (@LocationId, @ZoneId)",
                _dbHelper.CreateParameter("@LocationId", locationId), _dbHelper.CreateParameter("@ZoneId", zoneId));

            await LogAssociationChange(locationId, zoneId, "associated with", userId);
        }

        public async Task RemoveZoneFromLocationAsync(int locationId, int zoneId, string userId)
        {
            await _dbHelper.ExecuteNonQueryAsync(
                "DELETE FROM LocationZones WHERE LocationId = @LocationId AND ZoneId = @ZoneId",
                _dbHelper.CreateParameter("@LocationId", locationId), _dbHelper.CreateParameter("@ZoneId", zoneId));

            await LogAssociationChange(locationId, zoneId, "disassociated from", userId);
        }

        private async Task LogAssociationChange(int locationId, int zoneId, string action, string userId)
        {
            var locationName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Locations WHERE Id = @Id", _dbHelper.CreateParameter("@Id", locationId));
            var zoneName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Zones WHERE Id = @Id", _dbHelper.CreateParameter("@Id", zoneId));
            if (locationName != null && zoneName != null)
            {
                await LogActionAsync($"Zone '{zoneName}' {action} Location '{locationName}'.", userId);
            }
        }

        private async Task<List<ZoneDto>> GetZonesForLocationAsync(int locationId)
        {
            string sql = @"SELECT z.Id, z.Name 
                            FROM Zones z
                            JOIN LocationZones lz ON z.Id = lz.ZoneId
                            WHERE lz.LocationId = @LocationId";
            return await _dbHelper.QueryAsync(sql, r => new ZoneDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name")
            }, _dbHelper.CreateParameter("@LocationId", locationId));
        }
    }
}
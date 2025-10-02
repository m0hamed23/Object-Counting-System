using CountingWebAPI.Data;
using CountingWebAPI.Models;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class ActionService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<ActionService> _logger;
        private readonly ActionExecutionService _actionExecutionService;

        public ActionService(IDatabaseHelper dbHelper, ILogger<ActionService> logger, ActionExecutionService actionExecutionService)
        {
            _dbHelper = dbHelper;
            _logger = logger;
            _actionExecutionService = actionExecutionService;
        }

        private async Task LogActionAsync(string eventText, string userId)
        {
            string logEvent = $"{eventText} Action by User:{userId}";
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", logEvent)
            );
        }

        public async Task<IEnumerable<ActionDto>> GetActionsAsync()
        {
            string sql = "SELECT Id, Name, IpAddress, Port, IntervalMilliseconds, Protocol, IsEnabled FROM Actions ORDER BY Name";
            return await _dbHelper.QueryAsync(sql, r => new ActionDto
            {
                Id = r.GetInt32Safe("Id"),
                Name = r.GetStringSafe("Name"),
                IpAddress = r.GetStringSafe("IpAddress"),
                Port = r.GetInt32Safe("Port"),
                IntervalMilliseconds = r.GetInt32Safe("IntervalMilliseconds"),
                Protocol = r.GetStringSafe("Protocol"),
                IsEnabled = r.GetBooleanSafe("IsEnabled")
            });
        }

        public async Task<ServiceResult<ActionDto>> AddActionAsync(ActionCreateDto dto, string userId)
        {
            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Actions WHERE Name = @Name", _dbHelper.CreateParameter("@Name", dto.Name));
            if (existing > 0)
            {
                return ServiceResult<ActionDto>.Fail(ServiceResultStatus.Conflict, $"Action with name '{dto.Name}' already exists.");
            }

            string insertSql = @"
                INSERT INTO Actions (Name, IpAddress, Port, IntervalMilliseconds, Protocol, IsEnabled, LastUpdated) 
                VALUES (@Name, @IpAddress, @Port, @IntervalMilliseconds, @Protocol, @IsEnabled, datetime('now'))";

            var newId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(insertSql,
                _dbHelper.CreateParameter("@Name", dto.Name),
                _dbHelper.CreateParameter("@IpAddress", dto.IpAddress),
                _dbHelper.CreateParameter("@Port", dto.Port),
                _dbHelper.CreateParameter("@IntervalMilliseconds", dto.IntervalMilliseconds),
                _dbHelper.CreateParameter("@Protocol", dto.Protocol),
                _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled)
            );

            await _actionExecutionService.ReloadConfigurationAsync();
            await LogActionAsync($"Action '{dto.Name}' created.", userId);

            var newDto = new ActionDto { Id = (int)newId, Name = dto.Name, IpAddress = dto.IpAddress, Port = dto.Port, IntervalMilliseconds = dto.IntervalMilliseconds, Protocol = dto.Protocol, IsEnabled = dto.IsEnabled };
            return ServiceResult<ActionDto>.Success(newDto, ServiceResultStatus.Created);
        }

        public async Task<ServiceResult> UpdateActionAsync(int id, ActionCreateDto dto, string userId)
        {
            var existing = await _dbHelper.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM Actions WHERE Name = @Name AND Id != @Id",
                _dbHelper.CreateParameter("@Name", dto.Name), _dbHelper.CreateParameter("@Id", id));
            if (existing > 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.Conflict, $"Action with name '{dto.Name}' already exists.");
            }

            string updateSql = @"
                UPDATE Actions SET 
                Name = @Name, IpAddress = @IpAddress, Port = @Port, IntervalMilliseconds = @IntervalMilliseconds, 
                Protocol = @Protocol, IsEnabled = @IsEnabled, LastUpdated = datetime('now')
                WHERE Id = @Id";

            int rowsAffected = await _dbHelper.ExecuteNonQueryAsync(updateSql,
                _dbHelper.CreateParameter("@Name", dto.Name),
                _dbHelper.CreateParameter("@IpAddress", dto.IpAddress),
                _dbHelper.CreateParameter("@Port", dto.Port),
                _dbHelper.CreateParameter("@IntervalMilliseconds", dto.IntervalMilliseconds),
                _dbHelper.CreateParameter("@Protocol", dto.Protocol),
                _dbHelper.CreateParameter("@IsEnabled", dto.IsEnabled),
                _dbHelper.CreateParameter("@Id", id)
            );

            if (rowsAffected == 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Action not found.");
            }

            await _actionExecutionService.ReloadConfigurationAsync();
            await LogActionAsync($"Action '{dto.Name}' (ID:{id}) updated.", userId);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteActionAsync(int id, string userId)
        {
            var actionName = await _dbHelper.ExecuteScalarAsync<string>("SELECT Name FROM Actions WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (actionName == null)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Action not found.");
            }

            int rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Actions WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows == 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "Action not found.");
            }

            await _actionExecutionService.ReloadConfigurationAsync();
            await LogActionAsync($"Action '{actionName}' (ID:{id}) deleted.", userId);

            return ServiceResult.Success();
        }
    }
}
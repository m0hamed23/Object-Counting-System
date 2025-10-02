using CountingWebAPI.Data;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class SettingsService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SettingsService> _logger;
        private const string SettingsCacheKey = "GlobalSettings_v2_Sqlite";

        public event Func<Task>? OnSettingsChangedAsync;

        public SettingsService(IDatabaseHelper dbHelper, IMemoryCache cache, ILogger<SettingsService> logger)
        {
            _dbHelper = dbHelper;
            _cache = cache;
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

        public async Task<List<SettingDto>> GetVisibleSettingsAsync()
        {
            string sql = "SELECT Name, DisplayName, Value, Description, IsVisible FROM Settings WHERE IsVisible = 1 ORDER BY SortOrder, DisplayName";
            return await _dbHelper.QueryAsync(sql, reader => new SettingDto
            {
                Name = reader.GetStringSafe("Name"),
                DisplayName = reader.GetStringSafe("DisplayName"),
                Value = reader.GetNullableString("Value"),
                Description = reader.GetNullableString("Description"),
                IsVisible = reader.GetBooleanSafe("IsVisible")
            });
        }

        public async Task<bool> UpdateSettingsAsync(List<SettingDto> settingsToUpdate, string userId)
        {
            _cache.Remove(SettingsCacheKey);

            await using var connection = _dbHelper.GetConnection();
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var settingUpdate in settingsToUpdate)
                {
                    if (settingUpdate.Name == null) continue;

                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE Settings SET Value = @Value WHERE Name = @Name";
                    command.Parameters.Add(_dbHelper.CreateParameter("@Value", (object?)settingUpdate.Value ?? DBNull.Value));
                    command.Parameters.Add(_dbHelper.CreateParameter("@Name", settingUpdate.Name));

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    if (rowsAffected == 0)
                    {
                        _logger.LogWarning($"Setting '{settingUpdate.Name}' not found or value unchanged during batch update.");
                    }
                }
                await transaction.CommitAsync();
                _logger.LogInformation("Settings batch update processed successfully within a transaction.");

                // Log the action after a successful transaction
                await LogActionAsync("Application settings updated.", userId);

                await PublishSettingsChangedEvent();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update settings batch. Transaction rolled back.");
                return false;
            }
        }

        public async Task<Dictionary<string, string?>> GetAllSettingsAsDictionaryAsync(bool forceReload = false)
        {
            if (!forceReload && _cache.TryGetValue(SettingsCacheKey, out Dictionary<string, string?>? cachedSettings))
            {
                if (cachedSettings != null)
                {
                    return cachedSettings;
                }
            }
            _logger.LogInformation("Loading settings from database into cache (SQLite).");
            string sql = "SELECT Name, Value FROM Settings";
            var settingsList = await _dbHelper.QueryAsync(sql, reader =>
                new { Name = reader.GetStringSafe("Name"), Value = reader.GetNullableString("Value") });

            var settingsDict = settingsList.ToDictionary(s => s.Name, s => s.Value);

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(10)).SetAbsoluteExpiration(TimeSpan.FromHours(1));
            _cache.Set(SettingsCacheKey, settingsDict, cacheEntryOptions);
            return settingsDict;
        }

        public async Task<T?> GetSettingValueAsync<T>(string name, T? defaultValue = default)
        {
            var settingsDict = await GetAllSettingsAsDictionaryAsync();
            if (settingsDict.TryGetValue(name, out var stringValue))
            {
                if (stringValue == null) return defaultValue;
                try
                {
                    if (typeof(T) == typeof(bool)) return (T)(object)bool.Parse(stringValue);
                    if (typeof(T) == typeof(int)) return (T)(object)int.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (typeof(T) == typeof(double)) return (T)(object)double.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
                    if (typeof(T) == typeof(float)) return (T)(object)float.Parse(stringValue, System.Globalization.CultureInfo.InvariantCulture);
                    return (T?)Convert.ChangeType(stringValue, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception ex) { _logger.LogError(ex, $"Error converting setting '{name}' value '{stringValue}' to {typeof(T).Name}."); return defaultValue; }
            }
            return defaultValue;
        }

        public async Task<bool> UpdateSingleSettingAsync(string name, string value, string userId)
        {
            _cache.Remove(SettingsCacheKey);
            string sql = "UPDATE Settings SET Value = @Value WHERE Name = @Name";
            int rowsAffected = await _dbHelper.ExecuteNonQueryAsync(sql,
                _dbHelper.CreateParameter("@Value", value), _dbHelper.CreateParameter("@Name", name));
            if (rowsAffected > 0)
            {
                _logger.LogInformation($"Setting '{name}' updated to '{value}' in DB.");
                await LogActionAsync($"Setting '{name}' updated.", userId);
                await PublishSettingsChangedEvent();
                return true;
            }
            _logger.LogWarning($"Setting '{name}' not found or value unchanged for single update.");
            return false;
        }

        private async Task PublishSettingsChangedEvent()
        {
            if (OnSettingsChangedAsync != null)
            {
                _logger.LogInformation("Publishing OnSettingsChangedAsync event to subscribers.");
                try
                {
                    await OnSettingsChangedAsync.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in an OnSettingsChangedAsync event subscriber.");
                }
            }
        }
    }
}
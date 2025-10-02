using CountingWebAPI.Data;
using CountingWebAPI.Helpers;
using CountingWebAPI.Models;
using CountingWebAPI.Models.DTOs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CountingWebAPI.Services
{
    public class UserService
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<UserService> _logger;

        public UserService(IDatabaseHelper dbHelper, ILogger<UserService> logger)
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

        public async Task<IEnumerable<UserDto>> GetUsersAsync()
        {
            string sql = "SELECT Id, Username FROM Users";
            return await _dbHelper.QueryAsync(sql, reader => new UserDto
            {
                Id = reader.GetInt32Safe("Id"),
                Username = reader.GetStringSafe("Username")
            });
        }

        public async Task<ServiceResult<UserDto>> CreateUserAsync(UserCreateDto dto, string currentUserId)
        {
            var existingCount = await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users WHERE Username = @Username", _dbHelper.CreateParameter("@Username", dto.Username));
            if (existingCount > 0)
            {
                return ServiceResult<UserDto>.Fail(ServiceResultStatus.Conflict, "Username already exists");
            }

            long newUserId;
            try
            {
                string sql = "INSERT INTO Users (Username, PasswordHash) VALUES (@Username, @PasswordHash)";
                newUserId = await _dbHelper.ExecuteInsertAndGetLastIdAsync(sql,
                    _dbHelper.CreateParameter("@Username", dto.Username),
                    _dbHelper.CreateParameter("@PasswordHash", PasswordHelper.HashPassword(dto.Password))
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user.");
                return ServiceResult<UserDto>.Fail(ServiceResultStatus.Error, "Failed to create user.");
            }

            if (newUserId <= 0)
            {
                return ServiceResult<UserDto>.Fail(ServiceResultStatus.Error, "Failed to retrieve new user ID.");
            }

            await LogActionAsync($"User '{dto.Username}' created.", currentUserId);
            _logger.LogInformation($"User '{dto.Username}' (ID:{newUserId}) created by User ID {currentUserId}.");

            var newUserDto = new UserDto { Id = (int)newUserId, Username = dto.Username };
            return ServiceResult<UserDto>.Success(newUserDto, ServiceResultStatus.Created);
        }

        public async Task<ServiceResult> UpdateUserAsync(int id, UserUpdateDto dto, string currentUserId)
        {
            var existingUsername = await _dbHelper.ExecuteScalarAsync<string>("SELECT Username FROM Users WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (existingUsername == null)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "User not found");
            }

            if (existingUsername != dto.Username && (await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users WHERE Username = @Username AND Id != @Id", _dbHelper.CreateParameter("@Username", dto.Username), _dbHelper.CreateParameter("@Id", id))) > 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.Conflict, "Username already exists");
            }

            var parameters = new List<System.Data.Common.DbParameter> { _dbHelper.CreateParameter("@Username", dto.Username), _dbHelper.CreateParameter("@Id", id) };
            string sql = "UPDATE Users SET Username = @Username";
            if (!string.IsNullOrEmpty(dto.Password))
            {
                sql += ", PasswordHash = @PasswordHash";
                parameters.Add(_dbHelper.CreateParameter("@PasswordHash", PasswordHelper.HashPassword(dto.Password)));
            }
            sql += " WHERE Id = @Id";
            int rows = await _dbHelper.ExecuteNonQueryAsync(sql, parameters.ToArray());
            if (rows == 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "User not found or no update needed.");
            }

            string passwordChangeLog = string.IsNullOrEmpty(dto.Password) ? "" : " Password changed.";
            await LogActionAsync($"User '{dto.Username}' (ID:{id}) updated.{passwordChangeLog}", currentUserId);
            _logger.LogInformation($"User ID {id} updated by User ID {currentUserId}.");

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteUserAsync(int id, string currentUserId)
        {
            var username = await _dbHelper.ExecuteScalarAsync<string>("SELECT Username FROM Users WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (username == null)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "User not found");
            }
            if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) && (await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users")) == 1)
            {
                _logger.LogWarning($"Blocked delete of last admin (ID:{id}).");
                return ServiceResult.Fail(ServiceResultStatus.BadRequest, "Cannot delete last admin.");
            }

            int rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Users WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows == 0)
            {
                return ServiceResult.Fail(ServiceResultStatus.NotFound, "User not found.");
            }

            await LogActionAsync($"User '{username}' (ID:{id}) deleted.", currentUserId);
            _logger.LogInformation($"User ID {id} ('{username}') deleted by User ID {currentUserId}.");
            return ServiceResult.Success();
        }
    }
}
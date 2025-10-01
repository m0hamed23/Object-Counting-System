using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Data;
using CountingWebAPI.Models.DTOs;
using CountingWebAPI.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Security.Claims;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UsersController : ControllerBase
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IDatabaseHelper dbHelper, ILogger<UsersController> logger) 
        { 
            _dbHelper = dbHelper;
            _logger = logger;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet] public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers() {
            string sql = "SELECT Id, Username FROM Users";
            var users = await _dbHelper.QueryAsync(sql, reader => new UserDto {
                Id = reader.GetInt32Safe("Id"), Username = reader.GetStringSafe("Username")
            });
            return Ok(users);
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto) {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var existingCount = await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users WHERE Username = @Username", _dbHelper.CreateParameter("@Username", dto.Username));
            if (existingCount > 0) return Conflict(new { message = "Username already exists" });

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
                return StatusCode(500, new { message = "Failed to create user." });
            }

            if (newUserId <= 0) return StatusCode(500, new {message = "Failed to retrieve new user ID."});

            var currentUserId = GetCurrentUserId();
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", $"User '{dto.Username}' created. Action by User:{currentUserId}")
            );

            _logger.LogInformation($"User '{dto.Username}' (ID:{newUserId}) created by User ID {currentUserId}.");
            return CreatedAtAction(nameof(GetUsers), new { id = newUserId }, new { id = newUserId, username = dto.Username, message = "User created successfully" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto dto) {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var existingUsername = await _dbHelper.ExecuteScalarAsync<string>("SELECT Username FROM Users WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (existingUsername == null) return NotFound(new { message = "User not found" });
            if (existingUsername != dto.Username && (await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users WHERE Username = @Username AND Id != @Id", _dbHelper.CreateParameter("@Username", dto.Username), _dbHelper.CreateParameter("@Id", id))) > 0)
                return Conflict(new { message = "Username already exists" });
            
            var parameters = new List<System.Data.Common.DbParameter> { _dbHelper.CreateParameter("@Username", dto.Username), _dbHelper.CreateParameter("@Id", id) };
            string sql = "UPDATE Users SET Username = @Username";
            if (!string.IsNullOrEmpty(dto.Password)) {
                sql += ", PasswordHash = @PasswordHash";
                parameters.Add(_dbHelper.CreateParameter("@PasswordHash", PasswordHelper.HashPassword(dto.Password)));
            }
            sql += " WHERE Id = @Id";
            int rows = await _dbHelper.ExecuteNonQueryAsync(sql, parameters.ToArray());
            if (rows == 0) return NotFound(new { message = "User not found or no update needed." });

            var currentUserId = GetCurrentUserId();
            string passwordChangeLog = string.IsNullOrEmpty(dto.Password) ? "" : " Password changed.";
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", $"User '{dto.Username}' (ID:{id}) updated.{passwordChangeLog} Action by User:{currentUserId}")
            );

            _logger.LogInformation($"User ID {id} updated by User ID {currentUserId}.");
            return Ok(new { id = id, username = dto.Username, message = "User updated successfully" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id) {
            var username = await _dbHelper.ExecuteScalarAsync<string>("SELECT Username FROM Users WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (username == null) return NotFound(new { message = "User not found" });
            if (username.Equals("admin", StringComparison.OrdinalIgnoreCase) && (await _dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users")) == 1) {
                _logger.LogWarning($"Blocked delete of last admin (ID:{id})."); return BadRequest(new { message = "Cannot delete last admin." });
            }
            int rows = await _dbHelper.ExecuteNonQueryAsync("DELETE FROM Users WHERE Id = @Id", _dbHelper.CreateParameter("@Id", id));
            if (rows == 0) return NotFound(new { message = "User not found." });
            
            var currentUserId = GetCurrentUserId();
            await _dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO Logs (Event) VALUES (@Event)",
                _dbHelper.CreateParameter("@Event", $"User '{username}' (ID:{id}) deleted. Action by User:{currentUserId}")
            );

            _logger.LogInformation($"User ID {id} ('{username}') deleted by User ID {currentUserId}.");
            return Ok(new { message = "User deleted successfully" });
        }
    }
}
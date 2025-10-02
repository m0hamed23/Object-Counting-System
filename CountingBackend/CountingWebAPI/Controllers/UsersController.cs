using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using CountingWebAPI.Services;
using CountingWebAPI.Models;

namespace CountingWebAPI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UsersController : BaseApiController
    {
        private readonly UserService _userService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(UserService userService, ILogger<UsersController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
        {
            var users = await _userService.GetUsersAsync();
            return Ok(users);
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _userService.CreateUserAsync(dto, GetCurrentUserId());
            
            return result.Status switch
            {
                ServiceResultStatus.Created => CreatedAtAction(nameof(GetUsers), new { id = result.Value!.Id }, new { id = result.Value.Id, username = result.Value.Username, message = "User created successfully" }),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                ServiceResultStatus.Error => StatusCode(500, new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var result = await _userService.UpdateUserAsync(id, dto, GetCurrentUserId());
            
            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { id, username = dto.Username, message = "User updated successfully" }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var result = await _userService.DeleteUserAsync(id, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { message = "User deleted successfully" }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceResultStatus.BadRequest => BadRequest(new { message = result.ErrorMessage }),
                _ => StatusCode(500, "An unexpected error occurred.")
            };
        }
    }
}
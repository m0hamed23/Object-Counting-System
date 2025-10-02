using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CountingWebAPI.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Collections.Generic;
using System.Threading.Tasks;
using CountingWebAPI.Services;
using CountingWebAPI.Models;

namespace CountingWebAPI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ActionsController : BaseApiController
    {
        private readonly ActionService _actionService;

        public ActionsController(ActionService actionService)
        {
            _actionService = actionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ActionDto>>> GetActions()
        {
            var actions = await _actionService.GetActionsAsync();
            return Ok(actions);
        }

        [HttpPost]
        public async Task<ActionResult<ActionDto>> AddAction([FromBody] ActionCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _actionService.AddActionAsync(dto, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Created => CreatedAtAction(nameof(GetActions), new { id = result.Value!.Id }, result.Value),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                _ => StatusCode(500, new { message = "An unexpected error occurred while creating the action." })
            };
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAction(int id, [FromBody] ActionCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _actionService.UpdateActionAsync(id, dto, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Success => NoContent(),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                ServiceResultStatus.Conflict => Conflict(new { message = result.ErrorMessage }),
                _ => StatusCode(500, new { message = "An unexpected error occurred while updating the action." })
            };
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAction(int id)
        {
            var result = await _actionService.DeleteActionAsync(id, GetCurrentUserId());

            return result.Status switch
            {
                ServiceResultStatus.Success => Ok(new { message = "Action deleted successfully." }),
                ServiceResultStatus.NotFound => NotFound(new { message = result.ErrorMessage }),
                _ => StatusCode(500, new { message = "An unexpected error occurred while deleting the action." })
            };
        }
    }
}
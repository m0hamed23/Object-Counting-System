using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CountingWebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Gets the string representation of the current user's ID from the JWT claims.
        /// Defaults to "0" if not found.
        /// </summary>
        protected string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0";
    }
}
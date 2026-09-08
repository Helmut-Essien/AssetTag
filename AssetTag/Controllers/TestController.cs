using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Constants;

namespace AssetTag.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        /// <summary>Anonymous health/connectivity probe used by the mobile app.</summary>
        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok(new
            {
                status = "ok",
                timestamp = DateTime.UtcNow,
                message = "API is reachable"
            });
        }

        [Authorize]
        [HttpGet("protected")]
        public IActionResult Protected()
        {
            return Ok("This is a protected endpoint. You are authorized");
        }

        [Authorize(Roles = RoleNames.Admin)]
        [HttpGet("admin")]
        public IActionResult Admin()
        {
            return Ok("This is an admin endpoint. You are authorized as an Admin");
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DiagnosticsController : ControllerBase
{
    [HttpGet("server-time")]
    public IActionResult GetServerTime()
    {
        return Ok(new { serverUtc = DateTime.UtcNow });
    }
}

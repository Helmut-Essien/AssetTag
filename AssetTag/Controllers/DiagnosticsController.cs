using Microsoft.AspNetCore.Mvc;

namespace AssetTag.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DiagnosticsController : ControllerBase
{
    [HttpGet("server-time")]
    public IActionResult GetServerTime()
    {
        // Return server UTC time as ISO format in JSON
        return Ok(new { serverUtc = DateTime.UtcNow });
    }
}

using Microsoft.AspNetCore.Mvc;
using System;

namespace OcelotGateway.Controllers
{
    [ApiController]
    public class HealthController : ControllerBase
    {
        [HttpGet("/health")]
        public IActionResult Get()
        {
            return Ok(new { status = "healthy", time = DateTime.UtcNow });
        }
    }
}

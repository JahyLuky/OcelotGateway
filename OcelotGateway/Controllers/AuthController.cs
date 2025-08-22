using log4net;
using Microsoft.AspNetCore.Mvc;
using OcelotGateway.Services;

namespace OcelotGateway.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly ILog _logger;

        public AuthController(IJwtService jwtService)
        {
            _jwtService = jwtService;
            _logger = LogManager.GetLogger(typeof(AuthController));
        }

        [HttpPost("token")]
        public IActionResult GetToken([FromQuery] string clientId, [FromQuery] string clientSecret)
        {
            _logger.Info($"Token request received for client ID: {clientId}");

            try
            {
                var token = _jwtService.GenerateToken(clientId, clientSecret);
                _logger.Info($"Token generated successfully for client ID: {clientId}");
                return Ok(new { accessToken = token });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.Warn($"Unauthorized token request for client ID: {clientId}");
                return Unauthorized();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error generating token: {ex.Message}");
                return StatusCode(500, "An error occurred while generating the token");
            }
        }

        [HttpGet("verify")]
        public IActionResult VerifyToken()
        {
            return Ok(new
            {
                message = "Token is valid",
                timestamp = DateTime.UtcNow
            });
        }
    }

    public class ClientLogin
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
}

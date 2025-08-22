using log4net;
using System.Security.Claims;

namespace OcelotGateway.Middleware
{
    public class AuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILog _logger;

        public AuthorizationMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
            _logger = LogManager.GetLogger(typeof(AuthorizationMiddleware));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var user = context.User;

            if (user.Identity.IsAuthenticated)
            {
                var role = user.FindFirst(ClaimTypes.Role)?.Value;
                var clientId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                _logger.Info($"User is authenticated. Role: '{role}', ClientId: '{clientId}'");

                var requestPath = context.Request.Path.Value;
                _logger.Info($"Request path: '{requestPath}'");

                if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(clientId))
                {
                    _logger.Warn("Role or ClientId is missing in the token.");
                    context.Response.StatusCode = 403; // Forbidden
                    await context.Response.WriteAsync("Forbidden: Role or ClientId is missing.");
                    return;
                }

                var clients = _configuration.GetSection("JwtSettings:Clients").Get<Client[]>();
                var client = clients.FirstOrDefault(c => c.ClientId == clientId && c.Role == role);

                if (client != null)
                {
                    _logger.Info($"Client found for role '{role}' and clientId '{clientId}': ClientId '{client.ClientId}'");

                    if (client.Role == "admin")
                    {
                        _logger.Info("User is an admin, access granted.");
                        await _next(context);
                        return;
                    }

                    if (client.Allowed != null && client.Allowed.Any())
                    {
                        _logger.Info($"Allowed paths for role '{role}': {string.Join(", ", client.Allowed)}");

                        if (client.Allowed.Any(route => requestPath.StartsWith(route.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.Info($"Access granted to '{requestPath}' for role '{role}'.");
                            await _next(context);
                            return;
                        }
                        else
                        {
                            _logger.Warn($"Access denied to '{requestPath}' for role '{role}'. The path is not in the allowed list.");
                        }
                    }
                    else
                    {
                        _logger.Warn($"No allowed paths configured for role '{role}'.");
                    }
                }
                else
                {
                    _logger.Warn($"No client configuration found for role '{role}'.");
                }

                context.Response.StatusCode = 403; // Forbidden
                await context.Response.WriteAsync("Forbidden");
                return;
            }

            _logger.Info("User is not authenticated.");
            await _next(context);
        }
    }

    public class Client
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? Role { get; set; }
        public string[]? Allowed { get; set; }
    }
}

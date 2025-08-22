using log4net;
using Microsoft.IdentityModel.Tokens;
using OcelotGateway.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OcelotGateway.Services
{
    public interface IJwtService
    {
        string GenerateToken(string clientId, string clientSecret);
        bool ValidateToken(string token, string clientId);
    }

    public class JwtService : IJwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILog _logger;

        public JwtService(JwtSettings jwtSettings)
        {
            _jwtSettings = jwtSettings;
            _logger = LogManager.GetLogger(typeof(JwtService));
        }

        public string GenerateToken(string clientId, string clientSecret)
        {
            //_logger.Info($"Attempting to generate token for client ID: {clientId}");

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                _logger.Warn("Empty client ID or secret provided");
                throw new UnauthorizedAccessException("Client ID and secret are required");
            }

            // Find the client in the configured clients
            var client = _jwtSettings.Clients.FirstOrDefault(c =>
                c.ClientId.Equals(clientId, StringComparison.OrdinalIgnoreCase) &&
                c.ClientSecret.Equals(clientSecret, StringComparison.OrdinalIgnoreCase));

            if (client == null)
            {
                _logger.Warn($"Invalid client credentials for client ID: {clientId}");
                throw new UnauthorizedAccessException("Invalid client credentials");
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, clientId),
                new Claim(ClaimTypes.Role, client.Role),
                new Claim(ClaimTypes.NameIdentifier, clientId),
                new Claim(JwtRegisteredClaimNames.Nbf, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                new Claim(JwtRegisteredClaimNames.Exp, DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            if (client.Allowed != null && client.Allowed.Any())
            {
                claims.Add(new Claim("Allowed", string.Join(",", client.Allowed)));
            }

            if (client.Role == "admin")
            {
                claims.Add(new Claim(ClaimTypes.Role, "user"));
            }

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                signingCredentials: credentials
            );

            //_logger.Info($"Successfully generated token for client ID: {clientId} with role: {client.Role}");
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public bool ValidateToken(string token, string clientId)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(clientId))
            {
                _logger.Warn("Token validation failed: Empty token or client ID");
                return false;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.NameIdentifier
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var tokenClientId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;

                if (tokenClientId != clientId)
                {
                    _logger.Warn($"Token client ID mismatch. Token: {tokenClientId}, Expected: {clientId}");
                    return false;
                }

                //_logger.Info($"Token validation succeeded for client ID: {clientId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Token validation error: {ex.Message}");
                return false;
            }
        }
    }
}
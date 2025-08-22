using System.Collections.Generic;

namespace OcelotGateway.Models
{
    public class JwtSettings
    {
        public string Key { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string Subject { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public List<ClientConfig> Clients { get; set; }
    }

    public class ClientConfig
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Role { get; set; }
    }
} 
namespace OcelotGateway.Services
{
    public class GatewaySecretDelegatingHandler : DelegatingHandler
    {
        private readonly IConfiguration _configuration;

        public GatewaySecretDelegatingHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var gatewaySecret = _configuration["GatewaySecret"];
            request.Headers.Add("X-Gateway-Secret", gatewaySecret);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}

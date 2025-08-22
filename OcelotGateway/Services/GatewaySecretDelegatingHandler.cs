using log4net;

namespace OcelotGateway.Services
{
    public class GatewaySecretDelegatingHandler : DelegatingHandler
    {
        private readonly IConfiguration _configuration;
        private readonly ILog _logger = LogManager.GetLogger(typeof(GatewaySecretDelegatingHandler));

        public GatewaySecretDelegatingHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var gatewaySecret = _configuration["GatewaySecret"];
            request.Headers.Add("X-Gateway-Secret", gatewaySecret);
            // Log outgoing request
            _logger.Info($"Forwarding request: {request.Method} {request.RequestUri}");
            foreach (var header in request.Headers)
            {
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    || header.Key.StartsWith("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                _logger.Info($"Request Header: {header.Key} = {string.Join(",", header.Value)}");
            }
            if (request.Content != null)
            {
                var requestBody = await request.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(requestBody))
                {
                    _logger.Info($"Request Body: {requestBody.Substring(0, Math.Min(1000, requestBody.Length))}");
                }
            }
            var response = await base.SendAsync(request, cancellationToken);
            // Log response
            _logger.Info($"Response Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            foreach (var header in response.Headers)
            {
                _logger.Info($"Response Header: {header.Key} = {string.Join(",", header.Value)}");
            }
            if (response.Content != null)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(responseBody))
                {
                    _logger.Info($"Response Body: {responseBody.Substring(0, Math.Min(1000, responseBody.Length))}");
                }
            }
            return response;
        }
    }
}

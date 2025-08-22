using log4net;

namespace OcelotGateway.LoadBalancers
{
    public class HealthChecker
    {
        private readonly ILog _logger;
        private readonly HttpClient _httpClient;

        public HealthChecker()
        {
            _logger = LogManager.GetLogger(typeof(HealthChecker));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public async Task<bool> IsHealthyAsync(string host, int port)
        {
            try
            {
                var services = new[]
                {
                    "health",
                    "system/health",
                    "sirael/health",
                    "database/health"
                };

                foreach (var service in services)
                {
                    // Check health endpoint for each service
                    _logger.Debug($"Checking health for {service} at {host}:{port}");

                    // Construct the health endpoint URL
                    var endpoint = $"http://{host}:{port}/{service}";

                    try
                    {
                        _logger.Debug($"Checking health endpoint: {endpoint}");
                        var response = await _httpClient.GetAsync(endpoint);

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.Debug($"Health check successful for {host}:{port} via {endpoint}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Health check failed for {endpoint}: {ex.Message}");
                        return false;
                    }
                }

                // If no health endpoints work, try a simple connection test
                _logger.Debug($"No health endpoints responded, trying basic connectivity test for {host}:{port}");
                return await IsPortOpenAsync(host, port);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Health check failed for {host}:{port}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> IsPortOpenAsync(string host, int port)
        {
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                await tcpClient.ConnectAsync(host, port);
                return tcpClient.Connected;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
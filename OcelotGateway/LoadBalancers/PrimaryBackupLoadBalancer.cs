using log4net;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.Values;

namespace OcelotGateway.LoadBalancers
{
    public class PrimaryBackupLoadBalancer : ILoadBalancer, IDisposable
    {
        private readonly List<ServiceHostAndPort> _services;
        private readonly ILog _logger;
        private readonly HealthChecker _healthChecker;

        public PrimaryBackupLoadBalancer(List<ServiceHostAndPort> services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = LogManager.GetLogger(typeof(PrimaryBackupLoadBalancer));
            _healthChecker = new HealthChecker();

            if (_services.Count < 2)
            {
                _logger.Warn($"PrimaryBackupLoadBalancer initialized with {_services.Count} services. Expected at least 2 (primary + backup).");
            }

            _logger.Info($"PrimaryBackupLoadBalancer initialized with {_services.Count} services");
            for (int i = 0; i < _services.Count; i++)
            {
                var role = i == 0 ? "Primary" : "Backup";
                _logger.Info($"{role}: {_services[i].DownstreamHost}:{_services[i].DownstreamPort}");
            }
        }

        public string Type => "PrimaryBackup";

        public async Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
        {
            return await Lease(httpContext);
        }

        public async Task<Response<ServiceHostAndPort>> Lease(HttpContext httpContext)
        {
            if (_services == null || _services.Count == 0)
            {
                _logger.Error("No services available for load balancing");
                return new ErrorResponse<ServiceHostAndPort>(
                    new UnableToFindLoadBalancerError("No services available"));
            }

            var primary = _services[0];
            var primaryHealthy = await _healthChecker.IsHealthyAsync(primary.DownstreamHost, primary.DownstreamPort);

            if (primaryHealthy)
            {
                _logger.Debug($"Routing to primary: {primary.DownstreamHost}:{primary.DownstreamPort}");
                return new OkResponse<ServiceHostAndPort>(primary);
            }

            if (_services.Count > 1)
            {
                var backup = _services[1];
                _logger.Warn($"Primary unhealthy, routing to backup: {backup.DownstreamHost}:{backup.DownstreamPort}");
                return new OkResponse<ServiceHostAndPort>(backup);
            }

            _logger.Error("Primary is unhealthy and no backup available, returning primary anyway");
            return new OkResponse<ServiceHostAndPort>(primary);
        }

        public void Release(ServiceHostAndPort hostAndPort)
        {
            _logger.Debug($"Released: {hostAndPort.DownstreamHost}:{hostAndPort.DownstreamPort}");
        }

        public void Dispose()
        {
            _healthChecker?.Dispose();
        }
    }
}
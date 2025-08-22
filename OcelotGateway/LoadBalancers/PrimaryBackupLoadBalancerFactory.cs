using Ocelot.Configuration;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery;
using Ocelot.Values;
using log4net;

namespace OcelotGateway.LoadBalancers
{
    public class PrimaryBackupLoadBalancerFactory : ILoadBalancerFactory
    {
        private readonly ILog _logger;

        public PrimaryBackupLoadBalancerFactory()
        {
            _logger = LogManager.GetLogger(typeof(PrimaryBackupLoadBalancerFactory));
        }

        public Response<ILoadBalancer> Get(DownstreamRoute route, ServiceProviderConfiguration serviceProviderConfig)
        {
            try
            {
                // Convert DownstreamAddresses to ServiceHostAndPort list
                var serviceList = route.DownstreamAddresses.Select(h => 
                    new ServiceHostAndPort(h.Host, h.Port)).ToList();
                
                _logger.Info($"Creating PrimaryBackupLoadBalancer with {serviceList.Count} services");

                var loadBalancer = new PrimaryBackupLoadBalancer(serviceList);
                return new OkResponse<ILoadBalancer>(loadBalancer);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error creating PrimaryBackupLoadBalancer: {ex.Message}", ex);
                return new ErrorResponse<ILoadBalancer>(
                    new UnableToFindLoadBalancerError($"Failed to create load balancer: {ex.Message}"));
            }
        }
    }
}
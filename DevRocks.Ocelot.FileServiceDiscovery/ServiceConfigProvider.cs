using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ocelot.ServiceDiscovery;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace DevRocks.Ocelot.FileServiceDiscovery
{
    public class ServiceConfigProvider : IServiceDiscoveryProvider
    {
        private readonly List<Service> _services;

        public ServiceConfigProvider(Service service)
        {
            _services = new List<Service> { service };
        }

        public static readonly ServiceDiscoveryFinderDelegate GetFactory = (provider, config, route) =>
        {
            var options = provider.GetService<IOptions<ServicesConfig>>().Value;
            if (!options.Services.TryGetValue(route.ServiceName, out var serviceConfig))
            {
                throw new ArgumentException($"Invalid service name {route.ServiceName}");
            }

            var service = new Service(
                route.ServiceName,
                new ServiceHostAndPort(serviceConfig.Host ?? options.DefaultHost, serviceConfig.Port),
                route.ServiceName,
                null,
                Enumerable.Empty<string>());
            
            var consulServiceDiscoveryProvider = new ServiceConfigProvider(service);

            return consulServiceDiscoveryProvider;
        };

        public Task<List<Service>> Get()
        {
            return Task.FromResult(_services);
        }
    }
}

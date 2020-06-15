using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;

namespace DevRocks.Ocelot.FileServiceDiscovery
{
    public static class OcelotBuilderExtensions
    {
        public static void AddServiceConfigProvider(this IOcelotBuilder builder, IConfiguration configuration)
        {
            builder.Services.Configure<ServicesConfig>(options => configuration.GetSection(nameof(ServicesConfig)).Bind(options));
            builder.Services.AddSingleton(ServiceConfigProvider.GetFactory);
        }
    }
}

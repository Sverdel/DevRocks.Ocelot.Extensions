using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PolicyServer.Local;
using PolicyServer.Runtime.Client;

namespace DevRocks.Ocelot.PolicyServer
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddOcelotAuthorization(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<PolicyOptions>(opts => configuration.GetSection(nameof(PolicyOptions)).Bind(opts));
            services.Configure<Policy>(configuration);
            services.AddTransient<IPolicyServerRuntimeClient, PolicyServerRuntimeClient>();
            services.AddScoped(provider => provider.GetRequiredService<IOptionsSnapshot<PolicyOptions>>().Value.ToPolicy());

            new PolicyServerBuilder(services).AddAuthorizationPermissionPolicies();
            return services;
        }
    }
}
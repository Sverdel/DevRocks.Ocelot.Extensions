using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using DevRocks.Ocelot.Grpc.Swagger.Config;
using DevRocks.Ocelot.Grpc.Swagger.Middleware;
using DevRocks.Ocelot.Grpc.Swagger.Model;
using DevRocks.Ocelot.Swagger;

namespace DevRocks.Ocelot.Grpc.Swagger
{
    public static class SwaggerExtensions
    {
        [Obsolete]
        public static IServiceCollection AddOcelotSwagger(this IServiceCollection services,
            string appName,
            string authorityUrl,
            IConfiguration configuration,
            IDictionary<Type, Func<OpenApiSchema>> typeMap,
            Func<IServiceProvider, Dictionary<string, string>> serviceUrlFactory,
            Action<SwaggerConfig> configAction = null)
        {
            return services
                .AddOpenApiSchemaTypeMap(typeMap)
                .AddGrpcOcelotSwagger(configuration, appName, authorityUrl, serviceUrlFactory, configAction);
        }
        
        public static IServiceCollection AddGrpcOcelotSwagger(this IServiceCollection services,
            IConfiguration configuration,
            string appName = null,
            string authorityUrl = null,
            Func<IServiceProvider, Dictionary<string, string>> serviceUrlFactory = null,
            Action<SwaggerConfig> configAction = null)
        {
            services.AddOcelotSwagger(appName, authorityUrl, configuration, serviceUrlFactory);
            services.Configure<SwaggerConfig>(opts => configAction?.Invoke(opts));
            return services;
        }

        public static IServiceCollection AddOpenApiSchemaTypeMap(this IServiceCollection services, IDictionary<Type, Func<OpenApiSchema>> typeMap)
        {
            return services.AddSingleton<Func<IDictionary<Type, Func<OpenApiSchema>>>>(() => typeMap ?? new Dictionary<Type, Func<OpenApiSchema>>());
        }

        public static void UseOcelotSwagger(this IApplicationBuilder app)
        {
            var config = app.ApplicationServices.GetService<IOptions<SwaggerConfig>>().Value;

            app.UseOcelotSwagger(opts =>
            {
                config.SwaggerEndPoints.ForEach(i => opts.SwaggerEndpoint(i.Url, i.Name));
            });

            var dict = config
                .SwaggerEndPoints
                .ToDictionary(
                    x => x.Url.ToUpper(),
                    y => new SwaggerBuilder(app.ApplicationServices.GetService<IOptions<SwaggerGeneratorOptions>>(),
                        new GrpcServiceDescriptor(y.Assembly),
                        app.ApplicationServices.GetService<Func<IDictionary<Type, Func<OpenApiSchema>>>>()));

            app.UseMiddleware<SwaggerMiddleware>(dict);
        }
    }
}

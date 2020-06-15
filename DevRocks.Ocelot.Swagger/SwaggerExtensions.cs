using System;
using System.Collections.Generic;
using System.Linq;
using DevRocks.Ocelot.Swagger.Middleware;
using DevRocks.Ocelot.Swagger.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

// ReSharper disable UnusedMember.Global

namespace DevRocks.Ocelot.Swagger
{
    public static class SwaggerExtensions
    {
        private const string _swaggerApplicationVersion = "v1";

        public static void AddOcelotSwagger(this IServiceCollection services,
            string appName,
            string authorityUrl,
            IConfiguration configuration,
            Func<IServiceProvider, Dictionary<string, string>> serviceUrlFactory)
        {
            services.Configure<SwaggerOptions>(configuration.GetSection(nameof(SwaggerOptions)));
            var swaggerApplicationDescription = $"Api for information by {appName}";
            
            services.AddSingleton<SwaggerBuilder>();
            services.AddSingleton<Func<Dictionary<string, string>>>(sp => () => serviceUrlFactory(sp));

            services.AddSwaggerGen(c =>
            {
                if (!string.IsNullOrEmpty(authorityUrl))
                {
                    c.AddSecurityDefinition(SecuritySchemeType.OAuth2.GetDisplayName(), new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.OAuth2,
                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                                {
                                    AuthorizationUrl = new Uri($"{authorityUrl}/connect/authorize"),
                                    TokenUrl = new Uri($"{authorityUrl}/connect/token"),
                                    Scopes = configuration.GetSection("SwaggerOptions:Scopes")
                                        .Get<string[]>()
                                        .ToDictionary(x => x)
                                }
                        }
                    });
                }

                c.SwaggerDoc(
                    _swaggerApplicationVersion,
                    new OpenApiInfo
                    {
                        Version = _swaggerApplicationVersion,
                        Title = appName,
                        Description = swaggerApplicationDescription
                    });

                c.IgnoreObsoleteActions();
            });
        }

        public static void UseOcelotSwagger(this IApplicationBuilder app, Action<SwaggerUIOptions> configAction)
        {
            var config = app.ApplicationServices.GetService<IOptions<SwaggerOptions>>().Value; 
            app.UseSwagger();
            app.UseSwaggerUI(opts =>
            {
                foreach (var (name, service) in config.Services)
                {
                    opts.SwaggerEndpoint(service.Url, name);
                }
                
                opts.OAuthRealm("swagger-ui-realm");
                opts.OAuthClientId(config.ClientId);
                opts.OAuthClientSecret(config.ClientSecret);
                opts.OAuthAppName("Swagger UI");
                opts.DocExpansion(DocExpansion.None);
                opts.OAuthUsePkce();
                
                configAction?.Invoke(opts);
            });

            app.UseMiddleware<SwaggerMiddleware>();
        }
    }
}

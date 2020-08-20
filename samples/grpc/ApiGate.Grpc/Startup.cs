using System.Collections.Generic;
using DevRocks.Ocelot.Grpc;
using DevRocks.Ocelot.Grpc.Grpc;
using DevRocks.Ocelot.Grpc.Swagger;
using GrpcService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

namespace ApiGate.Grpc
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpcConverter(_configuration, () =>
                new GrpcAssemblyResolver().ConfigGrpcAssembly(
                    typeof(GreeterService).Assembly));
            services.AddOcelot();
            services.AddControllers();
            services.AddGrpcOcelotSwagger(
                _configuration,
                "ApiGate.Grpc",
                configAction: cfg =>
                {
                    cfg.SwaggerEndpoint("/greater/swagger.json", "greater", typeof(GreeterService).Assembly);
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context => { await context.Response.WriteAsync("Hello World!"); });
            });
            
            app.UseOcelotSwagger();
            
            var grpcProcessor = app.ApplicationServices.GetRequiredService<GrpcRequestMiddleware>();
            var configuration = new OcelotPipelineConfiguration
            {
                PreQueryStringBuilderMiddleware = async (ctx, next) =>
                {
                    await grpcProcessor.Invoke(ctx, next);
                },
            };
            app.UseOcelot(configuration).Wait();
        }
    }
}

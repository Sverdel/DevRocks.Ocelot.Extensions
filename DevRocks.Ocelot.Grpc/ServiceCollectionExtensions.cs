using System;
using System.Collections.Generic;
using DevRocks.Ocelot.Grpc.Grpc;
using DevRocks.Ocelot.Grpc.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DevRocks.Ocelot.Grpc
{
    public static class ServiceCollectionExtensions
    {
        public static void AddGrpcConverter(this IServiceCollection services,
            IConfiguration configuration,
            Func<GrpcAssemblyResolver> addGrpcAssembly,
            IEnumerable<JsonConverter> converters = null)
        {
            services.Configure<GrpcOptions>(configuration);
            services.AddSingleton<CacheHelper>();
            services.AddSingleton<Func<IEnumerable<JsonConverter>>>(() => converters);
            services.AddSingleton<GrpcRequestMiddleware>();
            services.AddSingleton(resolver => addGrpcAssembly.Invoke());
        }
    }
}

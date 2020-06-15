using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PolicyServer.Runtime.Client;

namespace DevRocks.Ocelot.PolicyServer
{
    public static class EndpointRouteBuilderExtensions
    {
        private static IPolicyServerRuntimeClient _policyClient;

        private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        public static void MapUserPermissions(this IEndpointRouteBuilder builder, string path)
        {
            builder.MapGet(path, async ctx =>
            {
                _policyClient ??= ctx.RequestServices.GetService<IPolicyServerRuntimeClient>();
                var result = await _policyClient.EvaluateAsync(ctx.User);
                var text = JsonConvert.SerializeObject(result, _jsonSerializerSettings);
                ctx.Response.ContentType = MediaTypeNames.Application.Json;
                await ctx.Response.WriteAsync(text);
            }).RequireAuthorization();
        }
    }
}
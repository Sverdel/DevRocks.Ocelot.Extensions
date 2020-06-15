using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Ocelot.Configuration.Repository;
using DevRocks.Ocelot.Grpc.Swagger.Model;

namespace DevRocks.Ocelot.Grpc.Swagger.Middleware
{
    /// <summary>
    /// Middleware определяющий необходимость возврата swagger.json файла и передающий управление builder-у для этого
    /// </summary>
    internal class SwaggerMiddleware
    {
        private readonly IDictionary<string, SwaggerBuilder> _declaredSwaggerBuilders;
        private readonly IInternalConfigurationRepository _ocelotConfigurationsRepository;
        private readonly RequestDelegate _next;
        private readonly Dictionary<string, string> _swaggerDefinitionsCache = new Dictionary<string, string>();

        public SwaggerMiddleware(RequestDelegate next, IDictionary<string, SwaggerBuilder> declaredSwaggerBuilders,
            IInternalConfigurationRepository ocelotConfigurationsRepository)
        {
            _next = next;
            _declaredSwaggerBuilders = declaredSwaggerBuilders;
            _ocelotConfigurationsRepository = ocelotConfigurationsRepository;
        }

        [UsedImplicitly]
        public async Task InvokeAsync(HttpContext httpContext)
        {
            var requestedJson = httpContext.Request.Path.Value.ToUpper();
            if (!_declaredSwaggerBuilders.TryGetValue(requestedJson, out var requestedJsonBuilder))
            {
                await _next(httpContext);
                return;
            }

            if (_swaggerDefinitionsCache.TryGetValue(requestedJson, out var newContent))
            {
                httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(newContent);
                await httpContext.Response.WriteAsync(newContent);
                return;
            }

            var downstreamRoutes = _ocelotConfigurationsRepository.Get().Data.Routes
                .SelectMany(x => x.DownstreamRoute.Select(rr => new OcelotRouteTemplateTuple(rr, x.UpstreamHttpMethod)))
                .ToList();

            var swagger = requestedJsonBuilder.BuildSwaggerJson(downstreamRoutes);
            _swaggerDefinitionsCache[requestedJson] = swagger;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(swagger);
            await httpContext.Response.WriteAsync(swagger);
        }
    }
}
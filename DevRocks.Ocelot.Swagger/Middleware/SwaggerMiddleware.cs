using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DevRocks.Ocelot.Swagger.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Ocelot.Configuration.Repository;

namespace DevRocks.Ocelot.Swagger.Middleware
{
    /// <summary>
    /// Middleware для проксирования определений Swagger-а к сетевым файлам
    /// </summary>
    internal class SwaggerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly SwaggerOptions _options;
        private readonly Func<Dictionary<string, string>> _servicesFactory;
        private readonly SwaggerBuilder _builder;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IInternalConfigurationRepository _configRepository;

        public SwaggerMiddleware(RequestDelegate next,
            IMemoryCache cache,
            IOptions<SwaggerOptions> options,
            Func<Dictionary<string, string>> servicesFactory,
            SwaggerBuilder builder,
            IHttpClientFactory httpClientFactory,
            IInternalConfigurationRepository configRepository)
        {
            _next = next;
            _cache = cache;
            _options = options.Value;
            _servicesFactory = servicesFactory;
            _builder = builder;
            _httpClientFactory = httpClientFactory;
            _configRepository = configRepository;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            var requestedJson = httpContext.Request.Path.Value.ToUpperInvariant();
            if (_options.Services == null || !_options.Services.Values.Any(w => requestedJson.EndsWith(w.Url.ToUpperInvariant())))
            {
                await _next(httpContext);
                return;
            }

            var result = await _cache.GetOrCreateAsync($"{nameof(SwaggerMiddleware)}:{requestedJson}",  async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _options.CacheTimeout;
                    var ocelotConfig = _configRepository.Get().Data;
                    var definition = _options.Services.Values.First(w => requestedJson.EndsWith(w.Url.ToUpperInvariant()));
                    var routes = ocelotConfig.Routes
                        .Where(x => x.DownstreamRoute.Any(v => string.Equals(v.ServiceName, definition.ServiceName, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    var location = _servicesFactory()
                        .GetValueOrDefault(definition.ServiceName.ToUpperInvariant(), string.Empty) + definition.Location;

                    var httpClient = _httpClientFactory.CreateClient();
                    var content = await httpClient.GetStringAsync(location);

                    return _builder.Build(content, routes);
            });

            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(result);
            await httpContext.Response.WriteAsync(result);
        }
    }
}

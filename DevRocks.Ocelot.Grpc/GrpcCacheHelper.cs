using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using DevRocks.Ocelot.Grpc.Internal.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocelot.Cache;
using Ocelot.Middleware;

namespace DevRocks.Ocelot.Grpc
{
    public class CacheHelper
    {
        private readonly IOcelotCache<CachedResponse> _outputCache;
        private readonly ICacheKeyGenerator _cacheGenerator;
        private readonly ILogger<CacheHelper> _logger;

        public CacheHelper(IOcelotCache<CachedResponse> outputCache,
            ICacheKeyGenerator cacheGenerator,
            ILogger<CacheHelper> logger)
        {
            _outputCache = outputCache;
            _cacheGenerator = cacheGenerator;
            _logger = logger;
        }

        public async Task Process(HttpContext  context, Func<HttpContext , Task> next)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            if (!downstreamRoute.IsCached)
            {
                await next.Invoke(context);
                return;
            }

            try
            {
                var downstreamRequest = context.Items.DownstreamRequest();
                var downstreamUrlKey = $"{downstreamRequest.Method}-{downstreamRequest.OriginalString}";
                var downStreamRequestCacheKey = _cacheGenerator.GenerateRequestCacheKey(downstreamRequest);

                _logger.LogDebug("Started checking cache for {Url}", downstreamUrlKey);

                var cached = _outputCache.Get(downStreamRequestCacheKey, downstreamRoute.CacheOptions.Region);

                if (cached != null)
                {
                    _logger.LogDebug("cache entry exists for {Url}. Data length {length}", downstreamUrlKey, cached.Body?.Length ?? 0);

                    context.Response.ContentType = MediaTypeNames.Application.Json;
                    context.Items.UpsertDownstreamResponse(CreateHttpResponseMessage(cached));

                    _logger.LogDebug("finished returned cached response for {Url}", downstreamUrlKey);
                    return;
                }

                _logger.LogDebug("no response cached for {Url}", downstreamUrlKey);

                await next.Invoke(context);

                // prevent caching errors
                var downstreamResponse = context.Items.DownstreamResponse();
                if (downstreamResponse == null || (int)downstreamResponse.StatusCode >= 400)
                {
                    _logger.LogDebug("there was a pipeline error for {Url}", downstreamUrlKey);
                    return;
                }

                cached = await CreateCachedResponse(downstreamResponse);

                _outputCache.Add(downStreamRequestCacheKey, cached,
                    TimeSpan.FromSeconds(downstreamRoute.CacheOptions.TtlSeconds),
                    downstreamRoute.CacheOptions.Region);

                _logger.LogDebug("finished response added to cache for {Url}. Data length {length}", downstreamUrlKey, cached?.Body?.Length ?? 0);
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                throw;
            }
        }

        private static DownstreamResponse CreateHttpResponseMessage(CachedResponse cached)
        {
            if (cached == null)
            {
                return null;
            }

            var streamContent = new GrpcHttpContent(cached.Body);

            foreach (var (key, value) in cached.ContentHeaders)
            {
                streamContent.Headers.TryAddWithoutValidation(key, value);
            }

            return new DownstreamResponse(streamContent, cached.StatusCode, cached.Headers.ToList(), cached.ReasonPhrase);
        }

        private static async Task<CachedResponse> CreateCachedResponse(DownstreamResponse response)
        {
            if (response == null)
            {
                return null;
            }

            var statusCode = response.StatusCode;
            var headers = response.Headers.ToDictionary(v => v.Key, v => v.Values);
            var body = response.Content != null
                ? await response.Content.ReadAsStringAsync()
                : null;

            var contentHeaders = response.Content?.Headers.ToDictionary(v => v.Key, v => v.Value);

            var cached = new CachedResponse(statusCode, headers, body, contentHeaders, response.ReasonPhrase);
            return cached;
        }
    }
}

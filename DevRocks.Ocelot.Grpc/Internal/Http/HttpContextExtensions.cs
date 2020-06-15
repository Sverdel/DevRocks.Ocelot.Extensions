using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ocelot.Configuration;
using Ocelot.Request.Middleware;

namespace DevRocks.Ocelot.Grpc.Internal.Http
{
    internal static class HttpContextExtensions
    {     
        private static Regex HeadersFilter = new Regex("^(X-TUI-.+|X-Correlation-Id|Authorization|Accept-language|Content-type)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        private const string _requestIdHeaderName = "X-Correlation-Id";

        public static string ParseJsonRequest(this HttpContext context,
            IDictionary<string, string> upstreamHeaders = null)
        {
            var o = new JObject();

            if (upstreamHeaders != null && upstreamHeaders.ContainsKey("x-grpc-route-data"))
            {
                // route data
                var nameValues = JsonConvert.DeserializeObject<List<NameAndValue>>(upstreamHeaders["x-grpc-route-data"]); // work with ocelot
                foreach (var nameValue in nameValues)
                {
                    var decodedValue = System.Net.WebUtility.UrlDecode(nameValue.Value);
                    o.Add(nameValue.Name.Trim('{', '}'), decodedValue);
                }
            }

            // query string
            foreach (var (key, value) in context.Request.Query)
            {
                var decodedValue = System.Net.WebUtility.UrlDecode(value.ToString());
                o.Add(key, decodedValue);
            }

            if (upstreamHeaders == null || !upstreamHeaders.ContainsKey("x-grpc-body-data"))
            {
                return JsonConvert.SerializeObject(o);
            }

            // route data
            var json = upstreamHeaders["x-grpc-body-data"]; // work with ocelot
            if (!string.IsNullOrEmpty(json))
            {
                o.Merge(JObject.Parse(json));
            }

            return JsonConvert.SerializeObject(o);
        }

        public static IDictionary<string, string> BuildRequestHeaders(this HttpContext context, DownstreamRoute downstreamRoute, DownstreamRequest downstreamRequest)
        {
            var headers = new Dictionary<string, string>();
            foreach (var key in context.Request.Headers.Keys)
            {
                if (HeadersFilter.IsMatch(key))
                    headers.Add(key, context.Request.Headers[key].FirstOrDefault());
            }

            foreach (var claimToHeader in downstreamRoute.ClaimsToHeaders)
            {
                if (downstreamRequest.Headers.TryGetValues(claimToHeader.ExistingKey, out var values))
                {
                    headers.Add(claimToHeader.ExistingKey, values.FirstOrDefault());
                }
            }

            if (!headers.ContainsKey(_requestIdHeaderName))
            {
                headers[_requestIdHeaderName] = Guid.NewGuid().ToString("N");
            }

            return headers;
        }

        private class NameAndValue
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }
    }
}

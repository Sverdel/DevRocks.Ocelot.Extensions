using System;
using System.Collections.Generic;
using System.Linq;
using Ocelot.Configuration.File;

namespace DevRocks.Ocelot.Grpc.Options
{
    public class GrpcOptions : FileConfiguration
    {
        private HashSet<string> _routeNames;
        private Dictionary<string, TimeSpan?> _timeouts;
        
        public new GrpcGlobalConfiguration GlobalConfiguration { get; set; }
        
        public new IEnumerable<GrpcRoute> Routes { get; set; }

        public bool IsGrpcRoute(string route)
        {
            _routeNames ??= Routes?
                .Where(x => x.IsGrpc).
                Select(x => x.DownstreamPathTemplate.ToUpperInvariant())
                .ToHashSet() ?? new HashSet<string>();
            return _routeNames.Contains(route.ToUpperInvariant());
        }

        public TimeSpan GetRouteTimeout(string route)
        {
            _timeouts ??= Routes?
                              .Where(x => x.IsGrpc)
                              .DistinctBy(x => x.DownstreamPathTemplate.ToUpperInvariant())
                              .ToDictionary(x => x.DownstreamPathTemplate.ToUpperInvariant(), x => x.RequestTimeout)
                          ?? new Dictionary<string, TimeSpan?>();

            return _timeouts.GetValueOrDefault(route.ToUpperInvariant(), GlobalConfiguration?.RequestTimeout) ?? TimeSpan.MaxValue;
        }
    }
}

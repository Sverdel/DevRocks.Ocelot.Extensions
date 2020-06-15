using System.Collections.Generic;
using System.Net.Http;
using Ocelot.Configuration;

namespace DevRocks.Ocelot.Grpc.Swagger.Model
{
    internal class OcelotRouteTemplateTuple
    {
        public DownstreamRoute Route { get; }
        public List<HttpMethod> HttpMethods { get; }

        public OcelotRouteTemplateTuple(DownstreamRoute route, List<HttpMethod> httpMethods)
        {
            Route = route;
            HttpMethods = httpMethods;
        }
    }
}
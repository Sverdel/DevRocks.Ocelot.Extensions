using System.Collections.Generic;
using System.Reflection;
using DevRocks.Ocelot.Swagger.Options;

namespace DevRocks.Ocelot.Grpc.Swagger.Config
{
    public class SwaggerConfig : SwaggerOptions
    {
        internal List<(string Url, string Name, Assembly Assembly)> SwaggerEndPoints { get; }
            = new List<(string Url, string Name, Assembly Assembly)>();

        public void SwaggerEndpoint(string url, string name, Assembly assembly)
            => SwaggerEndPoints.Add((url, name, assembly));
    }
}
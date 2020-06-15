using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Server
    {
        public string Description { get; set; }
        public string Url { get; set; }
        public IDictionary<string, ServerVariable> Variables { get; set; }
    }
}
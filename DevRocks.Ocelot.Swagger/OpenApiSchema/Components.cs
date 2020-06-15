using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Components
    {
        public IDictionary<string, Schema> Schemas { get; set; }
        public IDictionary<string, Response> Responses { get; set; }
        public IDictionary<string, Parameter> Parameters { get; set; }
        public IDictionary<string, object> Examples { get; set; }
        public IDictionary<string, RequestBody> RequestBodies { get; set; }
        public IDictionary<string, Header> Headers { get; set; }
        public IDictionary<string, Link> Links { get; set; }
    }
}
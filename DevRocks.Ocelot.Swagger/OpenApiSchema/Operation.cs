using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Operation
    {
        public IList<string> Tags { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }

        public IList<Parameter> Parameters { get; set; }

        public RequestBody RequestBody { get; set; }

        public Responses Responses { get; set; }
    }
}
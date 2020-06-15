using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class ServerVariable
    {
        public string Description { get; set; }
        public string Default { get; set; }
        public List<string> Enum { get; set; }
    }
}
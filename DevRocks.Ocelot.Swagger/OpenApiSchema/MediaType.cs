using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class MediaType
    {
        public Schema Schema { get; set; }
        public object Example { get; set; }
        public IDictionary<string, object> Examples { get; set; }
        public IDictionary<string, ApiEncoding> Encoding { get; set; }
    }
}
using System.Collections.Generic;
using Microsoft.OpenApi.Models;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class ApiEncoding
    {
        public string ContentType { get; set; }
        public IDictionary<string, Header> Headers { get; set; }
        public ParameterStyle? Style { get; set; }
        public bool? Explode { get; set; }
        public bool? AllowReserved { get; set; }
    }
}

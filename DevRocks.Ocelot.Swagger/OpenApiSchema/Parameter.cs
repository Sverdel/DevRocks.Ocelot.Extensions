using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Parameter
    {
        [JsonProperty("$ref")]
        public string Reference { get; set; }

        public string Name { get; set; }
        public ParameterLocation? In { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public bool AllowEmptyValue { get; set; }
        public ParameterStyle? Style { get; set; }
        public bool Explode { get; set; }
        public bool AllowReserved { get; set; }
        public Schema Schema { get; set; }
        public IDictionary<string, object> Examples { get; set; }
        public object Example { get; set; }
        public IDictionary<string, MediaType> Content { get; set; }
    }
}
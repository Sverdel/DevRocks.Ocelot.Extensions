using System.Collections.Generic;
using Newtonsoft.Json;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class RequestBody
    {
        public bool UnresolvedReference { get; set; }

        [JsonProperty("$ref")]
        public string Reference { get; set; }

        public string Description { get; set; }
        public bool Required { get; set; }
        public IDictionary<string, MediaType> Content { get; set; }
    }
}
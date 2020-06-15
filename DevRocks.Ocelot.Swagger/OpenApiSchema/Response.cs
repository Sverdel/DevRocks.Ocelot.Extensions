using System.Collections.Generic;
using Newtonsoft.Json;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Response
    {
        public string Description { get; set; }
        public IDictionary<string, Header> Headers { get; set; }
        public IDictionary<string, MediaType> Content { get; set; }
        public IDictionary<string, Link> Links { get; set; }
        public bool UnresolvedReference { get; set; }

        [JsonProperty("$ref")]
        public string Reference { get; set; }
    }
}
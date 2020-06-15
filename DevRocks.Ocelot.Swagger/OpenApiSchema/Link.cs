using System.Collections.Generic;
using Newtonsoft.Json;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Link
    {
        public string OperationRef { get; set; }
        public string OperationId { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public object RequestBody { get; set; }
        public string Description { get; set; }
        public Server Server { get; set; }
        public bool UnresolvedReference { get; set; }

        [JsonProperty("$ref")]
        public string Reference { get; set; }
    }
}
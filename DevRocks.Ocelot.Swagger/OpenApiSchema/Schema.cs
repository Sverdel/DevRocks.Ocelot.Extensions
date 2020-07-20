using System.Collections.Generic;
using Newtonsoft.Json;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Schema
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string Format { get; set; }
        public string Description { get; set; }
        public decimal? Maximum { get; set; }
        public bool? ExclusiveMaximum { get; set; }
        public decimal? Minimum { get; set; }
        public bool? ExclusiveMinimum { get; set; }
        public int? MaxLength { get; set; }
        public int? MinLength { get; set; }
        public string Pattern { get; set; }
        public decimal? MultipleOf { get; set; }
        public string Default { get; set; }
        public bool ReadOnly { get; set; }
        public bool WriteOnly { get; set; }
        public ISet<string> Required { get; set; }
        public Schema Items { get; set; }
        public int? MaxItems { get; set; }
        public int? MinItems { get; set; }
        public bool? UniqueItems { get; set; }
        public IDictionary<string, Schema> Properties { get; set; }
        public int? MaxProperties { get; set; }
        public int? MinProperties { get; set; }
        
        public object AdditionalProperties { get; set; }

        [JsonIgnore]
        public bool AdditionalPropertiesAllowed => AdditionalProperties is bool allowed && allowed;

        [JsonIgnore]
        public Schema AdditionalPropertiesSchema => AdditionalProperties is Schema schema ? schema : null;
        
        public object Example { get; set; }
        public IList<object> Enum { get; set; }
        public bool Nullable { get; set; }
        public bool UnresolvedReference { get; set; }

        [JsonProperty("$ref")]
        public string Reference { get; set; }
    }
}

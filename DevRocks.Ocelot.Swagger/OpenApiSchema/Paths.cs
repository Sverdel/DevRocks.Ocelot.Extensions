using System;
using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class Paths : Dictionary<string, PathItem>
    {
        public Paths() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
        
        public Paths(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}

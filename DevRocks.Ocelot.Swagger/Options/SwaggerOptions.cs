using System;
using System.Collections.Generic;

namespace DevRocks.Ocelot.Swagger.Options
{
    public class SwaggerOptions
    {
        public string ClientId { get; set; }
        
        public string ClientSecret { get; set; }
        
        public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromMinutes(10);
        
        public string[] Scopes { get; set; }
        
        public Dictionary<string, Service> Services { get; set; }
        
    }
}

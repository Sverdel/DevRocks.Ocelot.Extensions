using System.Collections.Generic;
using System.Linq;

namespace DevRocks.Ocelot.FileServiceDiscovery
{
    public class ServicesConfig 
    {
        public string DefaultHost { get; set; }
        
        public string DefaultSchema { get; set; }
        
        public Dictionary<string, ServiceConfig> Services { get; set; }

        public Dictionary<string, string> GetServicesUrls()
        {
            return Services
                .Where(x => !x.Value.IsGrpcHost)
                .ToDictionary(
                    x => x.Key.ToUpperInvariant(),
                    x => $"{x.Value.Schema ?? DefaultSchema}://{x.Value.Host ?? DefaultHost}:{x.Value.Port}");
        }
    }
}

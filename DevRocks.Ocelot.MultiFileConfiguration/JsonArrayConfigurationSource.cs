using Microsoft.Extensions.Configuration;

namespace DevRocks.Ocelot.MultiFileConfiguration
{
    public class JsonArrayConfigurationSource : FileConfigurationSource
    {
        public int Offset { get; set; }
        
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new JsonArrayConfigurationProvider(this, Offset);
        }
    }
}

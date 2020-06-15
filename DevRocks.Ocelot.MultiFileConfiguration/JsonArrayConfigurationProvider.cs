using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DevRocks.Ocelot.MultiFileConfiguration
{
    public class JsonArrayConfigurationProvider : FileConfigurationProvider
    {
        private readonly int _offset;

        public JsonArrayConfigurationProvider(JsonArrayConfigurationSource source, int offset) : base(source)
        {
            _offset = offset;
        }

        public override void Load(Stream stream)
        {
            try
            {
                Data = JsonArrayConfigurationFileParser.Parse(stream, _offset);
            }
            catch (JsonException e)
            {
                throw new FormatException("Could not parse the JSON file", e);
            }
        }
    }
}

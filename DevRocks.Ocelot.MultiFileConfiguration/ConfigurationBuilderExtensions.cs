using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace DevRocks.Ocelot.MultiFileConfiguration
{
    public static class ConfigurationBuilderExtensions
    {
        public static void AddOcelotConfig(this IConfigurationBuilder configurationBuilder, string path)
        {
            configurationBuilder.AddJsonFile("ocelot.json", false, true);
            var files = new DirectoryInfo(path)
                .EnumerateFiles("ocelot.*.json")
                .ToList();

            int offset = 1000;
            foreach (var file in files)
            {
                configurationBuilder.AddJsonArrayFile(file.Name, offset, false, true);
                offset += 1000;
            }
        }

        private static void AddJsonArrayFile(this IConfigurationBuilder builder, string path, int offset,
            bool optional,
            bool reloadOnChange)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("File path must be a non-empty string", nameof(path));
            }

            builder.Add((Action<JsonArrayConfigurationSource>)(s =>
            {
                s.FileProvider = null;
                s.Path = path;
                s.Optional = optional;
                s.ReloadOnChange = reloadOnChange;
                s.Offset = offset;
                s.ResolveFileProvider();
            }));
        }
    }
}

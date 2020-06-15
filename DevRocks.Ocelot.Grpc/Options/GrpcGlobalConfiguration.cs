using System;
using Ocelot.Configuration.File;

namespace DevRocks.Ocelot.Grpc.Options
{
    public class GrpcGlobalConfiguration : FileGlobalConfiguration
    {
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.MaxValue;
    }
}

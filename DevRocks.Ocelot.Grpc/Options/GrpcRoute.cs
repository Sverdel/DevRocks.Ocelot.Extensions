using System;
using Ocelot.Configuration.File;

namespace DevRocks.Ocelot.Grpc.Options
{
    public class GrpcRoute : FileRoute
    {
        public bool IsGrpc { get; set; }

        public TimeSpan? RequestTimeout { get; set; }
    }
}

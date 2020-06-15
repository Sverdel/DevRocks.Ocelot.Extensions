using Google.Protobuf.Reflection;

namespace DevRocks.Ocelot.Grpc.Swagger.Model
{
    internal class Descriptor
    {
        public string ServiceName { get; set; }

        public string MethodName { get; set; }

        public MethodDescriptor MethodDescriptor { get; set; }

        public string AssemblyPath { get; set; }

        public string FormalName => $"/{ServiceName}/{MethodName}";
    }
}
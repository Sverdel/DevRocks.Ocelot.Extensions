using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Google.Protobuf.Reflection;

namespace DevRocks.Ocelot.Grpc.Swagger.Model
{
    internal class GrpcServiceDescriptor
    {
        private readonly List<Descriptor> _descriptors = new List<Descriptor>();

        public GrpcServiceDescriptor(Assembly assembly)
        {
            var fileTypes = assembly.GetTypes().Where(type => type.Name.EndsWith("Reflection"));
            foreach (var type in fileTypes)
            {
                const BindingFlags flag = BindingFlags.Static | BindingFlags.Public;
                var property = type.GetProperties(flag).FirstOrDefault(t => t.Name == "Descriptor");
                if (!(property?.GetValue(null) is FileDescriptor fileDescriptor))
                {
                    continue;
                }

                foreach (var svr in fileDescriptor.Services)
                {
                    _descriptors.AddRange(svr.Methods.Select(m => new Descriptor
                    {
                        ServiceName = svr.FullName.ToUpper(),
                        MethodName = m.Name.ToUpper(),
                        MethodDescriptor = m,
                        AssemblyPath = assembly.Location
                    }));
                }
            }
        }

        public IReadOnlyDictionary<string, MethodDescriptor> MethodList(IEnumerable<string> downstreamRoutes)
        {
            return _descriptors
                .Where(w => downstreamRoutes.Contains(w.FormalName))
                .ToDictionary(x => x.FormalName, y => y.MethodDescriptor);
        }

        public IReadOnlyCollection<string> AssembliesPath(IEnumerable<string> downstreamRoutes)
        {
            return _descriptors
                .Where(x => downstreamRoutes.Contains(x.FormalName))
                .Select(x => x.AssemblyPath)
                .ToList();
        }
    }
}
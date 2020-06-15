using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DevRocks.Ocelot.Grpc.Internal.Http
{
    public class GrpcHttpContent : HttpContent
    {
        private readonly string _result;
        private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public GrpcHttpContent(string result)
        {
            _result = result;
        }

        public GrpcHttpContent(object result)
        {
            _result = JsonConvert.SerializeObject(result, _jsonSerializerSettings);
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            // do not dispose it!
            var writer = new StreamWriter(stream);
            await writer.WriteAsync(_result);
            await writer.FlushAsync();
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetBytes(_result).Length;
            return true;
        }

        public async Task WriteStream(Stream stream)
        {
            await SerializeToStreamAsync(stream, null);
        }
    }
}

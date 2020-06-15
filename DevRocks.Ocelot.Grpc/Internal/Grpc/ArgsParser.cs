using Google.Protobuf;
using Grpc.Core;

namespace DevRocks.Ocelot.Grpc.Internal.Grpc
{
    internal static class ArgsParser<T> where T : class, IMessage<T>, new()
    {
        public static readonly MessageParser<T> Parser = new MessageParser<T>(Factory<T>.CreateInstance);
        public static readonly Marshaller<T> Marshaller = Marshallers.Create((arg) => arg.ToByteArray(), Parser.ParseFrom);
    }
}
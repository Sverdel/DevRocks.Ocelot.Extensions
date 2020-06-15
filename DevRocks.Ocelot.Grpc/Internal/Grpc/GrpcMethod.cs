using System;
using System.Collections.Concurrent;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace DevRocks.Ocelot.Grpc.Internal.Grpc
{
    internal class GrpcMethod<TRequest, TResult>
        where TRequest : class, IMessage<TRequest>, new()
        where TResult : class, IMessage<TResult>, new()
    {
        private static readonly ConcurrentDictionary<MethodDescriptor, Method<TRequest, TResult>> _methods
            = new ConcurrentDictionary<MethodDescriptor, Method<TRequest, TResult>>();

        public static Method<TRequest, TResult> GetMethod(MethodDescriptor methodDescriptor)
        {
            if (_methods.TryGetValue(methodDescriptor, out var method))
                return method;

            var mType = 0;
            if (methodDescriptor.IsClientStreaming)
                mType = 1;
            if (methodDescriptor.IsServerStreaming)
                mType += 2;
            var methodType = (MethodType)Enum.ToObject(typeof(MethodType), mType);

            method = new Method<TRequest, TResult>(
                methodType,
                methodDescriptor.Service.FullName,
                methodDescriptor.Name,
                ArgsParser<TRequest>.Marshaller,
                ArgsParser<TResult>.Marshaller);

            _methods.TryAdd(methodDescriptor, method);

            return method;
        }
    }
}
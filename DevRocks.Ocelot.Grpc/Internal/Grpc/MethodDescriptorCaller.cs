using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;

namespace DevRocks.Ocelot.Grpc.Internal.Grpc
{
    internal class MethodDescriptorCaller : ClientBase<MethodDescriptorCaller>
    {
        public MethodDescriptorCaller()
        {
        }

        public MethodDescriptorCaller(Channel channel) : base(channel)
        {
        }

        private MethodDescriptorCaller(ClientBaseConfiguration configuration) : base(configuration)
        {
        }

        protected override MethodDescriptorCaller NewInstance(ClientBaseConfiguration configuration)
        {
            return new MethodDescriptorCaller(configuration);
        }

        public async Task<object> InvokeAsync(MethodDescriptor method, IDictionary<string, string> headers, object requestObject, DateTime? deadline = null)
        {
            object requests;

            if (requestObject != null && typeof(IEnumerable<>).MakeGenericType(method.InputType.ClrType).IsInstanceOfType(requestObject))
            {
                requests = requestObject;
            }
            else
            {
                var ary = Array.CreateInstance(method.InputType.ClrType, 1);
                ary.SetValue(requestObject, 0);
                requests = ary;
            }

            var m = typeof(MethodDescriptorCaller).GetMethod("CallGrpcAsyncCore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (m == null)
            {
                return null;
            }

            var task = (Task<object>)m.MakeGenericMethod(method.InputType.ClrType, method.OutputType.ClrType)
                                        .Invoke(this, new[] { method, headers, requests, deadline });

            return await task;
        }
        
        // ReSharper disable once UnusedMember.Local
        private async Task<object> CallGrpcAsyncCore<TRequest, TResponse>(MethodDescriptor method, IDictionary<string, string> headers, IEnumerable<TRequest> requests, DateTime? deadline = null) 
            where TRequest : class, IMessage<TRequest>, new()
            where TResponse : class, IMessage<TResponse>, new()
        {
            var option = CreateCallOptions(headers, deadline);
            var rpc = GrpcMethod<TRequest, TResponse>.GetMethod(method);
            switch (rpc.Type)
            {
                case MethodType.Unary:
                    return await AsyncUnaryCall(CallInvoker, rpc, option, requests.FirstOrDefault());

                case MethodType.ClientStreaming:
                    return await AsyncClientStreamingCall(CallInvoker, rpc, option, requests);

                case MethodType.ServerStreaming:
                    return await AsyncServerStreamingCall(CallInvoker, rpc, option, requests.FirstOrDefault());

                case MethodType.DuplexStreaming:
                    return await AsyncDuplexStreamingCall(CallInvoker, rpc, option, requests);

                default:
                    throw new NotSupportedException($"MethodType '{rpc.Type}' is not supported.");
            }
        }

        private static CallOptions CreateCallOptions(IDictionary<string, string> headers, DateTime? deadline = null)
        {
            var meta = new Metadata();

            foreach (var (key, value) in headers)
            {
                meta.Add(key, value);
            }

            var option = new CallOptions(meta, deadline);

            return option;
        }

        private static async Task<TResponse> AsyncUnaryCall<TRequest, TResponse>(CallInvoker invoker, Method<TRequest, TResponse> method, CallOptions option, TRequest request) where TRequest : class where TResponse : class
        {
            return await invoker.AsyncUnaryCall(method, null, option, request).ResponseAsync;
        }

        private static async Task<TResponse> AsyncClientStreamingCall<TRequest, TResponse>(CallInvoker invoker, Method<TRequest, TResponse> method, CallOptions option, IEnumerable<TRequest> requests) where TRequest : class where TResponse : class
        {
            using var call = invoker.AsyncClientStreamingCall(method, null, option);
            if (requests != null)
            {
                foreach (var request in requests)
                {
                    await call.RequestStream.WriteAsync(request);
                }
            }

            await call.RequestStream.CompleteAsync();

            return call.ResponseAsync.Result;
        }

        private static async Task<IList<TResponse>> AsyncServerStreamingCall<TRequest, TResponse>(CallInvoker invoker, Method<TRequest, TResponse> method, CallOptions option, TRequest request) where TRequest : class where TResponse : class
        {
            using var call = invoker.AsyncServerStreamingCall(method, null, option, request);
            var responses = new List<TResponse>();

            while (await call.ResponseStream.MoveNext())
            {
                responses.Add(call.ResponseStream.Current);
            }

            return responses;
        }

        private static async Task<IList<TResponse>> AsyncDuplexStreamingCall<TRequest, TResponse>(CallInvoker invoker, Method<TRequest, TResponse> method, CallOptions option, IEnumerable<TRequest> requests) where TRequest : class where TResponse : class
        {
            using var call = invoker.AsyncDuplexStreamingCall(method, null, option);
            if (requests != null)
            {
                foreach (var request in requests)
                {
                    await call.RequestStream.WriteAsync(request);
                }
            }

            await call.RequestStream.CompleteAsync();

            var responses = new List<TResponse>();

            while (await call.ResponseStream.MoveNext())
            {
                responses.Add(call.ResponseStream.Current);
            }

            return responses;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;
using DevRocks.Ocelot.Grpc.Grpc;
using DevRocks.Ocelot.Grpc.Internal.Grpc;
using DevRocks.Ocelot.Grpc.Internal.Http;
using DevRocks.Ocelot.Grpc.Options;
using DevRocks.Ocelot.Grpc.Response;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ocelot.Errors;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Middleware;
using Ocelot.Responses;

namespace DevRocks.Ocelot.Grpc
{
    public class GrpcRequestMiddleware
    {
        private const string _defaultErrorStatus = "processing-error";
        private const HttpStatusCode _defaultErrorCode = HttpStatusCode.InternalServerError;

        private readonly CacheHelper _cacheHelper;
        private GrpcOptions _options;

        private static readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private static readonly ConcurrentDictionary<string, MethodDescriptorCaller> _clients = new ConcurrentDictionary<string, MethodDescriptorCaller>();

        private static readonly Dictionary<StatusCode, string> _statusNames = new Dictionary<StatusCode, string>
        {
            [StatusCode.OK] = "ok",
            [StatusCode.Unauthenticated] = "unauthorized",
            [StatusCode.FailedPrecondition] = "validation-error",
            [StatusCode.PermissionDenied] = "forbidden",
            [StatusCode.Unavailable] = "service-unavailable",
            [StatusCode.Unimplemented] = "not-found",
            [StatusCode.NotFound] = "not-found",
            [StatusCode.DeadlineExceeded] = "request-timeout",
            [StatusCode.AlreadyExists] = "conflict"
        };

        private static readonly Dictionary<StatusCode, HttpStatusCode> _statusCodes =
            new Dictionary<StatusCode, HttpStatusCode>
            {
                [StatusCode.OK] = HttpStatusCode.OK,
                [StatusCode.Unauthenticated] = HttpStatusCode.Unauthorized,
                [StatusCode.FailedPrecondition] = HttpStatusCode.BadRequest,
                [StatusCode.PermissionDenied] = HttpStatusCode.Forbidden,
                [StatusCode.Unavailable] = HttpStatusCode.ServiceUnavailable,
                [StatusCode.Unimplemented] = HttpStatusCode.NotFound,
                [StatusCode.NotFound] = HttpStatusCode.NotFound,
                [StatusCode.AlreadyExists] = HttpStatusCode.Conflict
            };

        public GrpcRequestMiddleware(IOptionsMonitor<GrpcOptions> options, CacheHelper cacheHelper, Func<IEnumerable<JsonConverter>> convertersFactory)
        {
            _cacheHelper = cacheHelper;
            _options = options.CurrentValue;
            options.OnChange(cfg => _options = cfg);
            var converters = convertersFactory?.Invoke();
            if (converters != null)
            {
                foreach (var jsonConverter in converters)
                {
                    _jsonSerializerSettings.Converters.Add(jsonConverter);    
                }
            } 
        }

        public async Task Invoke(HttpContext  context, Func<Task> next)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            var methodPath = downstreamRoute.DownstreamPathTemplate.Value;
            // ignore if the request is not a gRPC content type
            if (!_options.IsGrpcRoute(methodPath))
            {
                await next.Invoke();
                if (context.Items.DownstreamResponse() != null) // if has response - everything is ok, just return
                {
                    return;
                }

                // add human friendly error message for grpc-call
                var errors = context.Items.Errors() ?? new List<Error>();
                var response = new GrpcHttpContent(new
                {
                    ResponseCode = _defaultErrorStatus,
                    Errors = errors.Any()
                        ? errors
                            .Select(x => $"{x.Code}: {x.Message}")
                            .ToArray()
                        : new[]
                        {
                            "Request process error. Either request is invalid or application wasn't configured properly"
                        }
                });

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)_defaultErrorCode;
                await response.WriteStream(context.Response.Body);

                return;
            }

            await _cacheHelper.Process(context, async ctx => await ProcessInternal(ctx, next));
        }

        /// <summary>
        /// Process grpc request
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        private async Task ProcessInternal(HttpContext context, Func<Task> next)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            var methodPath = downstreamRoute.DownstreamPathTemplate.Value;

            try
            {
                var grpcAssemblyResolver = context.RequestServices.GetService<GrpcAssemblyResolver>();
                var methodDescriptor =
                    grpcAssemblyResolver.FindMethodDescriptor(methodPath.Split('/').Last().ToUpperInvariant());

                if (methodDescriptor == null)
                {
                    await next.Invoke();
                    return;
                }

                var downstreamRequest = context.Items.DownstreamRequest();
                var upstreamHeaders = new Dictionary<string, string>
                {
                    {
                        "x-grpc-route-data",
                        JsonConvert.SerializeObject(
                            context.Items.TemplatePlaceholderNameAndValues().Select(x => new { x.Name, x.Value }))
                    },
                    { "x-grpc-body-data", await downstreamRequest.Content.ReadAsStringAsync() }
                };

                var requestData = context.ParseJsonRequest(upstreamHeaders);
                var client = await CreateGrpcClient(context);
                var timeout = _options.GetRouteTimeout(methodPath);
                var deadline = (timeout != TimeSpan.MaxValue) ? DateTime.UtcNow.Add(timeout) : DateTime.MaxValue;
                var requestObject = JsonConvert.DeserializeObject(requestData, methodDescriptor.InputType.ClrType, _jsonSerializerSettings);
                var headers = context.BuildRequestHeaders(downstreamRoute, downstreamRequest);
                var result = await client.InvokeAsync(methodDescriptor, headers, requestObject, deadline);
                var content = new GrpcHttpContent(JsonConvert.SerializeObject(result, _jsonSerializerSettings));
                var response = new OkResponse<GrpcHttpContent>(content);

                response.Data.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
                
                context.Response.ContentType = MediaTypeNames.Application.Json;
                context.Items.UpsertDownstreamResponse(new DownstreamResponse(response.Data, HttpStatusCode.OK, response.Data.Headers, HttpStatusCode.OK.ToString()));
            }
            catch (RpcException e)
            {
                ProcessRpcError(context, e);
            }
            catch (Exception e)
            {
                ProcessError(context, e);
            }
        }

        private static async Task<MethodDescriptorCaller> CreateGrpcClient(HttpContext  context)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            var loadBalancerFactory = context.RequestServices.GetService<ILoadBalancerFactory>();
            var loadBalancerResponse = loadBalancerFactory.Get(downstreamRoute, context.Items.IInternalConfiguration().ServiceProviderConfiguration);
            var serviceHostPort = await loadBalancerResponse.Data.Lease(context);
            var downstreamHost = $"{serviceHostPort.Data.DownstreamHost}:{serviceHostPort.Data.DownstreamPort}";
            var client = _clients.GetOrAdd(downstreamHost, host =>
            {
                var channel = new Channel(downstreamHost, ChannelCredentials.Insecure);
                return new MethodDescriptorCaller(channel);
            });
            return client;
        }
        
        private static void ProcessError(HttpContext  context, Exception e)
        {
            var content = new GrpcHttpContent(new ApiResponse
            {
                ResponseCode = _defaultErrorStatus,
                Errors = new[] { new ApiError(e.Message, e.InnerException?.Message) }
            });
 
            
            var response = new OkResponse<GrpcHttpContent>(new GrpcHttpContent(content));
            response.Data.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            
            context.Response.ContentType = MediaTypeNames.Application.Json;
            context.Items.UpsertDownstreamResponse(new DownstreamResponse(response.Data, _defaultErrorCode, response.Data.Headers, _defaultErrorCode.ToString()));
        }
 
        private static void ProcessRpcError(HttpContext  context, RpcException e)
        {
            var content = new ApiResponse
            {
                ResponseCode = _statusNames.GetValueOrDefault(e.StatusCode, _defaultErrorStatus)
            };
            
            try
            {
                content.Errors = JsonConvert.DeserializeObject<IEnumerable<ApiError>>(e.Status.Detail).ToList();
            }
            catch (Exception)
            {
                content.Errors = new[] { new ApiError(e.Status.Detail, e.InnerException?.Message) };
            }

            var statusCode = _statusCodes.GetValueOrDefault(e.StatusCode, _defaultErrorCode);
            var response = new OkResponse<GrpcHttpContent>(new GrpcHttpContent(content));
            response.Data.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
            
            context.Response.ContentType = MediaTypeNames.Application.Json;
            context.Items.UpsertDownstreamResponse(new DownstreamResponse(response.Data, statusCode, response.Data.Headers, statusCode.ToString()));
        }
    }
}

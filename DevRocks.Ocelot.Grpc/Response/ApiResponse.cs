using System.Collections.Generic;

namespace DevRocks.Ocelot.Grpc.Response
{
    public class ApiResponse
    {
        public string ResponseCode { get; set; }

        public IReadOnlyList<ApiError> Errors { get; set; }
    }
}
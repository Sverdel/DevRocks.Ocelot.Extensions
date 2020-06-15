namespace DevRocks.Ocelot.Grpc.Response
{
    public class ApiError
    {
        public ApiError(string userMessage, string internalMessage = null, string fieldName = null, object fieldValue = null)
        {
            UserMessage = userMessage;
            InternalMessage = internalMessage;
            FieldName = fieldName;
            FieldValue = fieldValue;
        }

        public string FieldName { get; set; }

        public object FieldValue { get; set; }

        public string UserMessage { get; set; }

        public string InternalMessage { get; set; }
    }
}
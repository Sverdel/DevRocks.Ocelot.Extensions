namespace DevRocks.Ocelot.Swagger.Options
{
    public class Service
    {
        public string Url { get; set; }

        public string Location { get; set; } = "/swagger/v1/swagger.json";
        
        public string ServiceName { get; set; }
    }
}

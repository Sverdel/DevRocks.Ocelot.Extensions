namespace DevRocks.Ocelot.FileServiceDiscovery
{
    public class ServiceConfig 
    {
        public string Schema { get; set; }
        
        public string Host { get; set; }
        
        public int Port { get; set; }
        
        public bool IsGrpcHost { get; set; }
    }
}

using Microsoft.OpenApi.Models;

namespace DevRocks.Ocelot.Swagger.OpenApiSchema
{
    public class SwaggerDocument
    {
        public OpenApiInfo Info { get; set; }
        public Paths Paths { get; set; }
        public Components Components { get; set; }
    }
}

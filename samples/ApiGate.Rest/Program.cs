using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiGate.Rest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, configurationBuilder) =>
                {
                    var env = context.HostingEnvironment;
                    configurationBuilder.AddJsonFile("appsettings.json", false, true);
                    configurationBuilder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true);
                    configurationBuilder.AddJsonFile("ocelot.json", false, true);
                    configurationBuilder.AddEnvironmentVariables();
                    
                    context.Configuration = configurationBuilder.Build();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel((context, options) =>
                    {
                        options.ListenAnyIP(5600);
                    }).UseStartup<Startup>();
                });
    }
}

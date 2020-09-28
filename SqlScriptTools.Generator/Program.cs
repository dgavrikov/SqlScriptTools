using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlScriptTools.Generator.Abstractions;
using SqlScriptTools.Generator.ClientGenerator;
using SqlScriptTools.Generator.Exporters;
using SqlScriptTools.Generator.Installer;
using SqlScriptTools.Generator.Services.MsSql;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SqlScriptTools.Generator
{
    class Program
    {
        private static IConfiguration _configuration;
        static int Main()
        {
            try
            {
                Console.WriteLine("Start export Job");
                MainAsync().Wait();
                Console.WriteLine("Finish export Job");
                return 0;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
        }
        static async Task MainAsync()
        {
            IServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            ILogger logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Start export job.");
                IAppGenerator scriptService = serviceProvider.GetService<IAppGenerator>();
                await scriptService.GenerateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }
        }
        private static void ConfigureServices(IServiceCollection services)
        {
            // Build configuration
            _configuration = (IConfiguration)new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .Build();

            services.AddSingleton<IConfiguration>(_configuration);

            // Setting
            services
                .AddConnectionInfo(_configuration)
                .AddExportInfo(_configuration);

            // Logger configuration
            services.AddLogging(logger =>
            {
                logger
                .ClearProviders()
                .AddConfiguration(_configuration.GetSection("Logging"))
                .AddConsole();
            });

            //Services
            services
                .AddScoped<IScriptService, MssqlScriptService>()
                .AddScoped<IExporter, FileExporter>()
                .AddSingleton<IAppGenerator, ConsoleClientGenerator>();

        }
    }
}

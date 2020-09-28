using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlScriptTools.Generator.Settings;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlScriptTools.Generator.Installer
{
    public static class ExportInfoInstaller
    {
        public static IServiceCollection AddExportInfo(
         this IServiceCollection services,
         IConfiguration configuration)
        {
            var exportInfo = new ExportInfo();
            configuration.GetSection(nameof(ExportInfo)).Bind(exportInfo);
            services.AddSingleton(exportInfo);
            return services;
        }
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlScriptTools.Generator.Settings;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;

namespace SqlScriptTools.Generator.Installer
{
    public static class ConnectionInfoInstaller
    {
     public static IServiceCollection AddConnectionInfo(
         this IServiceCollection services, 
         IConfiguration configuration)
        {
            var connectionInfo = new ConnectionInfo();
            configuration.GetSection(nameof(ConnectionInfo)).Bind(connectionInfo);
            services.AddSingleton(connectionInfo);
            return services;
        }
    }
}

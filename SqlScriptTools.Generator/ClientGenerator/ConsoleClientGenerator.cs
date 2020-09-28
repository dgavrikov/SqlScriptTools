using Microsoft.Extensions.Logging;
using SqlScriptTools.Generator.Abstractions;
using System;
using System.Threading.Tasks;

namespace SqlScriptTools.Generator.ClientGenerator
{
    internal class ConsoleClientGenerator:IAppGenerator
    {
        private readonly IScriptService _service;
        private readonly IExporter _exporter;
        private readonly ILogger<ConsoleClientGenerator> _logger;

        public ConsoleClientGenerator(
            IScriptService service, 
            IExporter exporter,
            ILogger<ConsoleClientGenerator> logger)
        {
            _service = service;
            _exporter = exporter;
            _logger = logger;
        }

        public async Task GenerateAsync()
        {
            var exportScriptResult = await _service.GetScriptInfoAsync();

            exportScriptResult.ForEach(SendExporter);
        }

        private void SendExporter(IScriptInfo scriptInfo)
        {
            var result = _exporter.Export(scriptInfo);
            if (!result)
            {
                
            }
        }
    }
}

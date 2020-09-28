using Microsoft.Extensions.Logging;
using SqlScriptTools.Generator.Abstractions;
using SqlScriptTools.Generator.Extensions;
using SqlScriptTools.Generator.Settings;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlScriptTools.Generator.Exporters
{
    internal class FileExporter : IExporter
    {
        private readonly Dictionary<string, bool> _clearDirectoryList = new Dictionary<string, bool>(30);
        private readonly ExportInfo _exportSetting;
        private readonly ILogger<FileExporter> _logger;

        /// <summary>
        /// Export script to filesystem
        /// </summary>
        /// <param name="rootPath">Root directory for export.</param>
        /// <param name="clearDirectory">Clear directory before export</param>
        public FileExporter(
            ExportInfo exportSetting,
            ILogger<FileExporter> logger)
        {
            _exportSetting = exportSetting;
            _logger = logger;

            if (!CheckPath(_exportSetting.Patch))
                throw new DirectoryNotFoundException(_exportSetting.Patch);
        }
        public bool Export(IScriptInfo scriptInfo)
        {
            if (scriptInfo == null)
                return false;

            var exportDirectory = $"{_exportSetting.Patch}" +
                $"\\{scriptInfo.Location.ServerName ??= ""}" +
                $"\\{scriptInfo.Location.DatabaseName ??= ""}" +
                $"\\{scriptInfo.Type??= ""}";

            if (!CheckPath(exportDirectory))
                return false;
            ClearDirectory(exportDirectory);

            var exportFile = GetCurrentPath(string.IsNullOrEmpty(scriptInfo.Schema)
                ? scriptInfo.Name
                : $"{scriptInfo.Schema}.{scriptInfo.Name}");

            _logger?.LogInformation($"{nameof(Export)}: скрипт с именем {exportFile} попытается сохратиться в директорию {exportDirectory}");

            File.WriteAllText($"{exportDirectory}\\{exportFile}.sql", scriptInfo.Body, Encoding.UTF8);
            
            _logger?.LogInformation($"{nameof(Export)}: скрипт с именем {exportFile} сохранился в директорию {exportDirectory}");

            return true;
            
        }
        private static bool CheckPath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return Directory.Exists(path);
        }

        #region Clear directory
        private void ClearDirectory(string path)
        {
            if (!_exportSetting.ClearPatch)
                return;
            if (_clearDirectoryList.ContainsKey(path))
                return;
            try
            {
                var directory = new DirectoryInfo(path);
                directory.GetFiles().ForEach(f =>
                {
                    f.Delete();
                });
                _clearDirectoryList.Add(path, true);
            }
            catch
            {
                _clearDirectoryList.Add(path, false);
            }
            
        }
        #endregion
        private static string GetCurrentPath(string path, string replaceString = "_")
        {
            Regex pattern = new Regex("[\\\\/:*?\"<>|]");
            return pattern.Replace(path, replaceString);
        }
    }
}

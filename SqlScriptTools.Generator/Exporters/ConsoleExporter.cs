using System;
using System.Threading;
using SqlScriptTools.Generator.Abstractions;

namespace SqlScriptTools.Generator.Exporters
{
    internal class ConsoleExporter:IExporter
    {
        public bool Export(IScriptInfo scriptInfo)
        {
                var dt = DateTime.Now.ToString("G");
                var trid = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"{dt}({trid}) script name: {scriptInfo.Name}");
                Console.WriteLine($"{dt}({trid}) script body:");
                Console.WriteLine(scriptInfo.Body);
                Console.WriteLine(new string('=', 32));

                return true;
            
        }
    }
}

using SqlScriptTools.Generator.Abstractions;

namespace SqlScriptTools.Generator.Services
{
    internal class MssqlScriptInfo : IScriptInfo
    {        
        public ScriptInfoLocation Location { get; set; }
        public string Type { get; set; }
        public string Schema { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
    }
}

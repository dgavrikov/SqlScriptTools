namespace SqlScriptTools.Generator.Abstractions
{
    public interface IScriptInfo
    {
        ScriptInfoLocation Location { get; set; }
        string Type { get; set; }
        string Schema { get; set; }
        string Name { get; set; }
        string Body { get; set; }
    }
}

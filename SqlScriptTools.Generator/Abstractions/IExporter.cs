namespace SqlScriptTools.Generator.Abstractions
{
    public interface IExporter
    {
        bool Export(IScriptInfo scriptInfo);
    }
}

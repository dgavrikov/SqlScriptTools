using System.Threading.Tasks;

namespace SqlScriptTools.Generator.Abstractions
{
    public interface IAppGenerator
    {
        Task GenerateAsync();
    }
}

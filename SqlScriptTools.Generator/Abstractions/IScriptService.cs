using System.Collections.Generic;
using System.Threading.Tasks;

namespace SqlScriptTools.Generator.Abstractions
{
    public interface IScriptService
    {
        Task<List<IScriptInfo>> GetScriptInfoAsync();

    }
}

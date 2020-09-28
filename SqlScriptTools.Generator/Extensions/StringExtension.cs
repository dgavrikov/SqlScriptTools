using System;
using System.Collections.Generic;
using System.Text;

namespace SqlScriptTools.Generator.Extensions
{
    internal static class StringExtension
    {
        /// <summary>
        /// Parse String array to Dictionary
        /// </summary>
        /// <param name="args">string array</param>
        /// <param name="keyValueDelimiter">KeyValue delimiter</param>
        /// <returns>Dictionary list</returns>
        public static Dictionary<string, string> ParseToDictionary(
            this string[] args,
            char keyValueDelimiter = '=')
        {
            if ((args == null) || (args.Length <= 0)) return null;
            var result = new Dictionary<string, string>(args.Length);
            Array.ForEach(args, s =>
            {
                var item = s.Split(keyValueDelimiter);
                if (item.Length < 1)
                    return;
                if (result.ContainsKey(item[0]))
                    result[item[0]] = item.Length > 1 ? item[1] : null;
                else
                    result.Add(item[0], item.Length > 1 ? item[1] : null);
            });
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace SqlScriptTools.Generator.Extensions
{
    internal static class IEnumerableExtension
    {
        /// <summary>
        /// Foreach method for collection.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="seq">Collection</param>
        /// <param name="action">Delegate</param>
        public static void ForEach<T>(this IEnumerable<T> seq, Action<T> action)
        {
            foreach (var item in seq)
                action(item);
        }

        /// <summary>
        /// Lazy streaming foreach method.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="seq">Collection</param>
        /// <param name="action">Delegate</param>
        /// <returns>Collection</returns>
        public static IEnumerable<T> ForEachLazy<T>(this IEnumerable<T> seq, Action<T> action)
        {
            foreach (var item in seq)
            {
                action(item);
                yield return item;
            }
        }
    }
}

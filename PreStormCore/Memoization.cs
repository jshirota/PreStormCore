using System;
using System.Collections.Concurrent;

namespace PreStormCore
{
    internal static class Memoization
    {
        public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> f) where T : notnull
        {
            var cache = new ConcurrentDictionary<T, TResult>();
            return x => cache.GetOrAdd(x, _ => f(x));
        }
    }
}

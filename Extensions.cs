using System;

namespace Pinduri
{
    internal static class Extensions
    {
        public static TResult Map<T, TResult>(this T value, Func<T, TResult> fn) { return fn(value); }
        public static T Tap<T>(this T value, Action<T> a) { a(value); return value; }
    }
}

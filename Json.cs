using System;
using System.Linq;

namespace Pinduri
{
    public static class Json
    {
        public static string Serialize(object value) =>
            new (Func<Type, bool> pred, Func<object, string> action)[]
            {
                (pred: t => t == null, action: v => "null"),
                (pred: t => new object[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, float.NaN, float.PositiveInfinity, float.NegativeInfinity }.Contains(value), action: v => FormattableString.Invariant($"\"{v}\"")),
                (pred: t => t == typeof(DateTime), action: v => $"\"{v:o}\""),
                (pred: t => t == typeof(bool), action: v => v.ToString().ToLowerInvariant()),
                (pred: t => t.IsEnum || t == typeof(char) || t == typeof(string), action: v => v.ToString().Select(c => c == '\\' ? "\\\\" : c == '\"' ? "\\\"" : c < ' ' ? $"\\u{(int)c:x4}" : c.ToString()).Map(x => $"\"{string.Concat(x)}\"")),
                (pred: t => t.IsValueType, action: v => FormattableString.Invariant($"{v}")),
                (pred: t => value is System.Collections.IEnumerable, action: v => (v as System.Collections.IEnumerable).Cast<object>().Select(x => Serialize(x)).Map(x => $"[{string.Join(", ", x)}]")),
                (pred: t => true, action: x => value.GetType().GetProperties().Where(x => x.CanRead && !x.GetGetMethod().IsStatic && x.GetIndexParameters().Length == 0).OrderBy(x => x.Name).Select(x => $"\"{x.Name}\":{Serialize(x.GetValue(value))}").Map(x => $"{{ {string.Join(", ", x)} }}")),
            }.First(x => x.pred(value?.GetType())).Map(x => x.action(value));
    }
} // line #21

using System;
using System.Linq;

namespace Pinduri
{
    public static class Wildcard
    {
        public static bool Match(string pattern, string input) =>
            new (Func<bool> p, Func<bool> f)[]
            {
                (p: () => pattern == null || input == null, f: () => Match(pattern ?? "", input ?? "")),
                (p: () => pattern == input, f: () => true),
                (p: () => input == "", f: () => pattern.Trim('*') == ""),
                (p: () => pattern == "", f: () => false),
                (p: () => pattern.StartsWith('?'), f: () => Match(pattern[1..], input[1..])),
                (p: () => pattern.EndsWith('?'), f: () => Match(pattern[0..^1], input[0..^1])),
                (p: () => pattern.StartsWith('*'), f: () => Match(pattern.TrimStart('*'), input) || Match(pattern, input[1..])),
                (p: () => pattern.EndsWith('*'), f: () => Match(pattern.TrimEnd('*'), input) || Match(pattern, input[0..^1])),
                (p: () => pattern[0] == input[0], f: () => Match(pattern[1..], input[1..])),
                (p: () => true, f: () => false),
            }.First(x => x.p()).f();
    }
} // line #23

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pinduri
{
    public static class DiffMerge
    {
        public static IEnumerable<string> Diff(IEnumerable<string> a, IEnumerable<string> b)
        {
            var (aa, ba) = (a.ToArray(), b.ToArray());

            IEnumerable<string> RenderDiff(int[,] c, string[] a, string[] b, int i, int j) =>
            i > 0 && j > 0 && a[i - 1] == b[j - 1] ? RenderDiff(c, a, b, i - 1, j - 1).Append("  " + a[i - 1])
               : j > 0 && (i == 0 || c[i, j - 1] >= c[i - 1, j]) ? RenderDiff(c, a, b, i, j - 1).Append("+ " + b[j - 1])
               : i > 0 && (j == 0 || c[i, j - 1] < c[i - 1, j]) ? RenderDiff(c, a, b, i - 1, j).Append("- " + a[i - 1])
               : new string[0];

            int[,] BuildMatrix(string[] a, string[] b) => new int[a.Length + 1, b.Length + 1].Tap(c => a.Select((x, i) => b.Select((y, j) => y.Tap(y => c[i + 1, j + 1] = a[i] == b[j] ? c[i, j] + 1 : Math.Max(c[i + 1, j], c[i, j + 1]))).ToList()).ToList());

            return RenderDiff(BuildMatrix(aa, ba), aa, ba, aa.Length, ba.Length);
        }

        public static IEnumerable<string> Merge(IEnumerable<string> o, IEnumerable<string> a, IEnumerable<string> b)
        {
            var (oa, aa, ba) = (o.Append("").ToArray(), a.Append("").ToArray(), b.Append("").ToArray());

            IEnumerable<(int, int)> Matches(IEnumerable<string> diff) => diff.Aggregate<string, (int i, int j, IEnumerable<(int, int)> result)>((i: 0, j: 0, result: new (int, int)[0]),
                (a, x) => x.StartsWith("  ") ? (i: a.i + 1, j: a.j + 1, result: a.result.Append((a.i, a.j))) : x.StartsWith("- ") ? (i: a.i + 1, a.j, a.result) : (a.i, j: a.j + 1, a.result)).result;

            (int o, int a, int b)[] Chunks(IEnumerable<(int, int)> a, IEnumerable<(int, int)> b) => a.Select(x => x.Item1).Intersect(b.Select(x => x.Item1)).Select(o => (o, a.First(x => x.Item1 == o).Item2, b.First(x => x.Item1 == o).Item2)).ToArray();

            return Chunks(Matches(Diff(oa, aa)), Matches(Diff(oa, ba))).Map(chunks =>
                chunks.Aggregate((prev: (o: -1, a: -1, b: -1), res: new string[0] as IEnumerable<string>),
                    (acc, curr) => (sa: (from: acc.prev.a + 1, count: curr.a - acc.prev.a - 1), sb: (from: acc.prev.b + 1, count: curr.b - acc.prev.b - 1))
                        .Map(x => (prev: curr, res: x.sa.count == 0 || x.sb.count == 0
                                    ? acc.res.Concat(new ArraySegment<string>(aa, x.sa.from, x.sa.count)).Concat(new ArraySegment<string>(ba, x.sb.from, x.sb.count)).Append(aa[curr.a])
                                    : new ArraySegment<string>(aa, x.sa.from, x.sa.count).SequenceEqual(new ArraySegment<string>(ba, x.sb.from, x.sb.count)) ? acc.res.Concat(new ArraySegment<string>(ba, x.sb.from, x.sb.count)).Append(aa[curr.a])
                                    : acc.res.Append("<<<<<<<<").Concat(new ArraySegment<string>(aa, x.sa.from, x.sa.count)).Append("========").Concat(new ArraySegment<string>(ba, x.sb.from, x.sb.count)).Append(">>>>>>>>").Append(aa[curr.a])))
                ).res).SkipLast(1);
        }
    }
} // line #43

// based on https://en.wikipedia.org/wiki/Longest_common_subsequence_problem#Code_for_the_dynamic_programming_solution 
// and on https://blog.jcoglan.com/2017/05/08/merging-with-diff3/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pinduri.Tests
{
    public class DiffMergeTests
    {
        static void TestDiff(IEnumerable<string> a, IEnumerable<string> b, IEnumerable<string> expected, string message = null)
        {
            var diff = DiffMerge.Diff(a.ToArray(), b.ToArray());
            Assert.AreEqual(true, diff.SequenceEqual(expected), message);
        }

        static void TestMerge(IEnumerable<string> o, IEnumerable<string> a, IEnumerable<string> b, IEnumerable<string> expected, string message = null)
        {
            var merge = DiffMerge.Merge(o, a, b);
            Assert.AreEqual(true, merge.SequenceEqual(expected), message);
        }

        static string[] S(params string[] items) => items;

        public class DiffTest
        {
            public void ShouldDiff_When_BothAreEmpty()
            {
                TestDiff(S(), S(), S());
            }

            public void ShouldDiff_When_OneInputIsEmpty()
            {
                TestDiff(S(), S("a", "b", "c"), S("+ a", "+ b", "+ c"));
            }

            public void ShouldDiff_When_OtherInputIsEmpty()
            {
                TestDiff(S("a", "b", "c"), S(), S("- a", "- b", "- c"));
            }

            public void ShouldDiff_When_Same()
            {
                TestDiff(S("a", "b", "c"), S("a", "b", "c"), S("  a", "  b", "  c"));
            }

            public void ShouldDiff_When_InsertedToTheBeginning()
            {
                TestDiff(S("a", "b", "c"), S("1", "2", "a", "b", "c"), S("+ 1", "+ 2", "  a", "  b", "  c"));
            }

            public void ShouldDiff_When_InsertedToTheEnd()
            {
                TestDiff(S("a", "b", "c"), S("a", "b", "c", "d", "e"), S("  a", "  b", "  c", "+ d", "+ e"));
            }

            public void ShouldDiff_When_InsertedToTheBeginningAndToEnd()
            {
                TestDiff(S("a", "b", "c"), S("1", "2", "a", "b", "c", "d", "e"), S("+ 1", "+ 2", "  a", "  b", "  c", "+ d", "+ e"));
            }

            public void ShouldDiff_When_InsertedToMiddle()
            {
                TestDiff(S("a", "b", "c"), S("a", "b", "1", "2", "c"), S("  a", "  b", "+ 1", "+ 2", "  c"));
            }

            public void ShouldDiff_When_ReplacedInTheBeginning()
            {
                TestDiff(S("a", "b", "c"), S("1", "b", "c"), S("- a", "+ 1", "  b", "  c"));
            }

            public void ShouldDiff_When_ReplacedAtTheEnd()
            {
                TestDiff(S("a", "b", "c"), S("a", "b", "1"), S("  a", "  b", "- c", "+ 1"));
            }

            public void ShouldDiff_When_ReplacedInTheMiddle()
            {
                TestDiff(S("a", "b", "c"), S("a", "1", "c"), S("  a", "- b", "+ 1", "  c"));
            }

            public void ShouldDiff_When_ReplacedAll()
            {
                TestDiff(S("a", "b", "c"), S("d", "e", "f"), S("- a", "- b", "- c", "+ d", "+ e", "+ f"));
            }
        }

        class MergeTest
        {
            public void ShouldMerge_When_OneIsEmpty()
            {
                TestMerge(S(), S(), S(), S());
                TestMerge(S(), S("a"), S(), S("a"));
                TestMerge(S(), S(), S("1"), S("1"));
                TestMerge(S("p"), S(), S(), S());
            }

            public void ShouldMerge_When_BothVersionsAreSame()
            {
                TestMerge(S(), S("a"), S("a"), S("a"));
                TestMerge(S("p"), S("a"), S("a"), S("a"));
                TestMerge(S("p"), S("a", "b"), S("a", "b"), S("a", "b"));
                TestMerge(S("p", "r"), S("p", "a", "b", "r"), S("p", "a", "b", "r"), S("p", "a", "b", "r"));
                TestMerge(S("p", "r"), S("p", "r", "a", "b", "r"), S("p", "r", "a", "b", "r"), S("p", "r", "a", "b", "r"));
            }

            public void ShouldMergeConflict_When_VersionsDiffer_And_Conflict()
            {
                TestMerge(S(), S("a"), S("b"), S("<<<<<<<<", "a", "========", "b", ">>>>>>>>"));
                TestMerge(S(), S("a", "b", "c"), S("a", "c"), S("<<<<<<<<", "a", "b", "c", "========", "a", "c", ">>>>>>>>"));
                TestMerge(S("p"), S("a"), S("b"), S("<<<<<<<<", "a", "========", "b", ">>>>>>>>"));
                TestMerge(S("p", "r"), S("a", "b"), S("b"), S("<<<<<<<<", "a", "b", "========", "b", ">>>>>>>>"));
                TestMerge(S("p", "r"), S("a", "b"), S("b", "a"), S("<<<<<<<<", "a", "b", "========", "b", "a", ">>>>>>>>"));
            }

            public void ShouldMergeConflict_When_VersionsDiffer_And_NotConflict()
            {
                TestMerge(S("p", "r", "s"), S("p", "a", "b", "s"), S("p", "s", "1"), S("p", "a", "b", "s", "1"));
                TestMerge(S("p", "r", "s"), S("p", "a", "b", "r", "s"), S("p", "r", "b", "s"), S("p", "a", "b", "r", "b", "s"));
                TestMerge(S("p", "r", "s"), S("p", "a", "r", "s"), S("p", "r", "s"), S("p", "a", "r", "s"));
                TestMerge(S("p", "r", "s"), S("p", "r", "s"), S("p", "a", "r", "s"), S("p", "a", "r", "s"));
                TestMerge(S("p", "r"), S("p", "a", "r"), S("p", "r"), S("p", "a", "r"));
                TestMerge(S("p", "r"), S("p", "r"), S("p", "a", "r"), S("p", "a", "r"));
            }
        }

        public static void Go()
        {
            new PUnit()
                .Test<DiffTest>()
                .Test<MergeTest>()
                .RunToConsole();
        }
    }
}

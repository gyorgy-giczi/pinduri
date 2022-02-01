using System.Collections.Generic;

namespace Pinduri.Tests
{
    public class WildcardTests
    {
        private static void Test(List<(string pattern, string input, bool expected)> cases)
        {
            cases.ForEach(x => Assert.AreEqual(x.expected, Wildcard.Match(x.pattern, x.input), $"{x}"));
        }

        public void ShouldMatchCorrectly_When_PatternIsNullOrEmpty()
        {
            Test(new()
            {
                (null, null, true),
                (null, "", true),
                (null, "a", false),
                ("", null, true),
                ("", "", true),
                ("", "a", false),
            });
        }

        public void ShouldMatchCorrectly_When_InputIsNullOrEmpty()
        {
            Test(new()
            {
                ("?", null, false),
                ("??", null, false),
                ("a", null, false),
                ("aa", null, false),
                ("*", null, true),
                ("**", null, true),
                ("?", "", false),
                ("??", "", false),
                ("a", "", false),
                ("aa", "", false),
                ("*", "", true),
                ("**", "", true),
            });
        }

        public void ShouldMatchCorrectly_When_ThereIsNoWildcardInThePattern()
        {
            Test(new()
            {
                ("a", "a", true),
                ("a", "b", false),
                ("a", "aa", false),
                ("ab", "ab", true),
                ("ab", "aa", false),
                ("ab", "abx", false),
            });
        }

        public void ShouldMatchCorrectly_When_QuestionMarkIsInThePattern()
        {
            Test(new()
            {
                ("?", null, false),
                ("?", "", false),
                ("?", "a", true),
                ("?", "aa", false),
                ("??", "", false),
                ("??", "a", false),
                ("??", "aa", true),
                ("??", "aaa", false),
                ("a?", "a", false),
                ("a?", "ab", true),
                ("a?", "ba", false),
                ("a?", "abx", false),
                ("?a", "a", false),
                ("?a", "ab", false),
                ("?a", "ba", true),
                ("?a", "xab", false),
                ("?a", "xba", false),
                ("a?b", "", false),
                ("a?b", "a", false),
                ("a?b", "ab", false),
                ("a?b", "axb", true),
                ("a?b", "axxb", false),
            });
        }

        public void ShouldMatchCorrectly_When_AsteriskIsInThePatthern()
        {
            Test(new()
            {
                ("*", null, true),
                ("*", "", true),
                ("*", "a", true),
                ("*", "aa", true),
                ("*", "aaa", true),
                ("*a", "a", true),
                ("*a", "aa", true),
                ("*a", "b", false),
                ("*a", "ba", true),
                ("*a", "ab", false),
                ("a*", "a", true),
                ("a*", "aa", true),
                ("a*", "b", false),
                ("a*", "ba", false),
                ("a*", "ab", true),
                ("a*b", "", false),
                ("a*b", "a", false),
                ("a*b", "b", false),
                ("a*b", "ab", true),
                ("a*b", "axb", true),
                ("a*b", "xab", false),
                ("a*b", "abx", false),
                ("a*b", "xabx", false),
            });
        }

        public void ShouldMatchCorrectly_When_QuestionMarksAndAsterisksAreInThePattern()
        {
            Test(new()
            {
                ("*?", "", false),
                ("*?", "a", true),
                ("*?", "ab", true),
                ("*?", "abx", true),
                ("?*", "", false),
                ("?*", "a", true),
                ("?*", "ab", true),
                ("?*", "abx", true),

                ("a*b?c", "abc", false),
                ("a*b?c", "abxc", true),
                ("a*b?c", "axbxc", true),
                ("a*b?c", "axbxxc", false),
                ("a*b?c", "axbc", false),
                ("a*b?c", "axxbxc", true),
                ("a*b?c", "axxbxxc", false),

                ("a**b??c", "abc", false),
                ("a**b??c", "abxc", false),
                ("a**b??c", "abxxc", true),
                ("a**b??c", "axbc", false),
                ("a**b??c", "axbxc", false),
                ("a**b??c", "axbxxc", true),
                ("a**b??c", "axbxxxc", false),
                ("a**b??c", "axxbxxc", true),
            });
        }

        // TODO: more test cases

        public static void Go()
        {
            new PUnit().Test<WildcardTests>().RunToConsole();
        }
    }
}

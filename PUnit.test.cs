using System;
using System.Collections.Generic;
using System.Linq;

namespace Pinduri.Tests
{
    class Assert
    {
        public static void AreEqual<T>(T expected, T actual, string message = null) { if (!Equals(expected, actual)) { throw new Exception(string.Join(". ", $"Expected '{expected}' but got '{actual}'", message)); } }

        public static void IsNotNull(object actual, string message = null) { if (actual == null) { throw new Exception(string.Join(". ", $"Expected value to be not null", message)); } }

        public static void Throws<T>(Action a, string message = null)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                if (!typeof(T).IsAssignableFrom(e.GetType())) { throw new Exception($"Expected to throw {typeof(T).FullName}, but {e.GetType().FullName} was thrown"); }
                if (message != null && e.Message != message) { throw new Exception($"Expected to throw message {message}, but {e.Message} was thrown"); }
                return;
            }

            throw new Exception($"Expected to throw exception {typeof(T).FullName}");
        }
    }
}

namespace Pinduri.Tests
{
    using System.Threading.Tasks;

    class CustomFactoryTest
    {
        public CustomFactoryTest(string dummy) { }
        public void ShouldRunThisTest() { }
    }

    class AsyncTest
    {
        public async Task ShouldRunThisTest() { await Task.Delay(200); }
        public Task ShouldRunThisTestToo() { return Task.Delay(200); }
        public async Task ShouldFailEvenThisTest() { await Task.Run(() => { throw new Exception("Error in ShouldFailEvenThisTest"); }); }
        public async Task<string> ShouldNotRunThisTest() { return await Task.FromResult("ShouldNotRunThisTest"); }
    }

    class PassFailTests
    {
        public void ShouldFailThisTest() { throw new Exception("Error in ShouldFailThisTest()"); }
        public void ShouldFailThisTestToo() { throw new Exception("Error in ShouldFailThisTestToo()"); }
        public void ShouldPassThisTest() { }
        public void ShouldPassThisTestToo() { }
    }

    class ShouldTestTests
    {
        public void ShouldRunThis() { }
        public void __ShouldNotRunThis() { }
        public void __ShouldRunThisTooTest() { }
        public void __ShouldNotRunThisTooTest__() { }
    }

    class SkipTests
    {
        public void Skip_ShouldNotRunThisTest() { throw new Exception("Skip_ShouldNotRunThisTest ran"); }
        public void ShouldRunThisTest() { }
    }

    class OnlyTests
    {
        public void ShouldNotRunThisTest() { throw new Exception("ShouldNotRunThisTest ran"); }
        public void Only_ShouldRunThisTest() { }
    }

    class BeforeEachErrorTests
    {
        public void BeforeEach() { throw new Exception("Error in BeforeEach()"); }
        public void ShouldNotRunThisTest() { }
        public void ShouldNotRunThisTestEither() { }
    }

    class AfterEachErrorTests
    {
        public void AfterEach() { throw new Exception("Error in AfterEach()"); }
        public void ShouldFailThisTest() { }
        public void ShouldFailWithAggregateException() { throw new Exception("Error in ShouldFailWithAggregateException()"); }
    }

    class BeforeErrorTests
    {
        public void Before() { throw new Exception("Error in Before()"); }
        public void ShouldNotRunThisTest() { }
        public void ShouldNotRunThisTestEither() { }
    }

    class AfterErrorTests
    {
        public void After() { throw new Exception("Error in After()"); }
        public void ShouldFailThisTest() { throw new Exception("Error in ShouldFailThisTest()"); }
        public void ShouldPassThisTest() { }
    }

    class HooksTests
    {
        public List<string> InvokedMethods { get; init; } = new();
        private void Invoked(string msg) { InvokedMethods.Add(msg); }

        public void Before() { Invoked("Before"); }
        public void After() { Invoked("After"); }
        public void BeforeEach() { Invoked("BeforeEach"); }
        public void AfterEach() { Invoked("AfterEach"); }

        public void Skip_ShouldNotRunThisTest() { throw new Exception("Skip_ShouldNotRunThisTest ran"); }
        public void ShouldReportAsPassed() { Invoked("ShouldReportAsPassed"); }
        public void ShouldReportAsFailed() { Invoked("ShouldReportAsFailed"); throw new Exception("Some failure"); }
    }

    class PUnitTests
    {
        public void ShouldUseCustomFactory()
        {
            var instance = new CustomFactoryTest("dummy");
            var container = new { Create = new Func<Type, object>((t) => instance) }; // e.g. new Container().RegisterSingleton(instance);
            var result = new PUnit() { Create = (type) => container.Create(type) }.Test<CustomFactoryTest>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual(instance, result.Instance);
            Assert.AreEqual("ShouldRunThisTest ", result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
            Assert.Throws<MissingMethodException>(() => new PUnit().Test<CustomFactoryTest>().Run(), "No parameterless constructor defined for type 'Pinduri.Tests.CustomFactoryTest'.");
        }

        public void ShouldRunAsyncTests()
        {
            var result = new PUnit().Test<AsyncTest>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual(
                "ShouldRunThisTest ,ShouldRunThisTestToo ,ShouldFailEvenThisTest Error in ShouldFailEvenThisTest",
                result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldReportError_When_TestFails()
        {
            var result = new PUnit().Test<PassFailTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual(
                "ShouldFailThisTest Error in ShouldFailThisTest(),ShouldFailThisTestToo Error in ShouldFailThisTestToo(),ShouldPassThisTest ,ShouldPassThisTestToo ",
                result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldRunTests_When_BeginsWithShouldOrEndsWithTest()
        {
            var result = new PUnit().Test<ShouldTestTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual("ShouldRunThis ,__ShouldRunThisTooTest ", result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldSkipTests_When_MarkedWithSkip()
        {
            var result = new PUnit().Test<SkipTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual("ShouldRunThisTest ", result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldRunOnlyTheTests_When_MarkedWithOnly()
        {
            var result = new PUnit().Test<OnlyTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual("Only_ShouldRunThisTest ", result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldReportErrorANdFailTest_When_BeforeEachFails()
        {
            var result = new PUnit().Test<BeforeEachErrorTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual("ShouldNotRunThisTest Error in BeforeEach(),ShouldNotRunThisTestEither Error in BeforeEach()", result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldReportErrorANdFailTest_When_AfterEachFails()
        {
            var result = new PUnit().Test<AfterEachErrorTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual(
                "ShouldFailThisTest Error in AfterEach(),ShouldFailWithAggregateException One or more errors occurred. (Error in ShouldFailWithAggregateException()) (Error in AfterEach())",
                result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldReportErrorAndNotRunTests_When_BeforeFails()
        {
            var result = new PUnit().Test<BeforeErrorTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.AreEqual("Error in Before()", result.Error.Message);
            Assert.AreEqual(0, result.Results.Count());
        }

        public void ShouldReportError_When_AfterFails()
        {
            var result = new PUnit().Test<AfterErrorTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Error);
            Assert.AreEqual("Error in After()", result.Error.Message);
            Assert.AreEqual("ShouldFailThisTest Error in ShouldFailThisTest(),ShouldPassThisTest ", result.Results.Select(x => $"{x.Name} {x.Error?.Message}").Map(x => string.Join(",", x)));
        }

        public void ShouldRunHooksInCorrectOrder()
        {
            var result = new PUnit().Test<HooksTests>().Run().First();

            Assert.IsNotNull(result);
            Assert.AreEqual(null, result.Error);
            Assert.AreEqual(
                "Before,BeforeEach,ShouldReportAsPassed,AfterEach,BeforeEach,ShouldReportAsFailed,AfterEach,After",
                (result.Instance as HooksTests).InvokedMethods.Map(x => string.Join(",", x)));
        }

        public static void Go()
        {
            new PUnit().Test<PUnitTests>().RunToConsole();

            new PUnit()
                // .Test<CustomFactoryTest>() cannot run directly as it breaks the whole test run
                .Test<AsyncTest>()
                .Test<ShouldTestTests>()
                .Test<PassFailTests>()
                .Test<SkipTests>()
                .Test<OnlyTests>()
                .Test<BeforeEachErrorTests>()
                .Test<AfterEachErrorTests>()
                .Test<BeforeErrorTests>()
                .Test<AfterErrorTests>()
                .Test<HooksTests>()
                .RunToConsole();
        }
    }
}

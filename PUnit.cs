using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pinduri
{
    public class PUnit
    {
        private readonly List<(string Suite, Func<Action<(string, Exception, TimeSpan)>, (object Instance, Exception Error, IEnumerable<(string Name, Exception Error, TimeSpan Time)> Results)> Run)> suites = new();

        public Func<Type, object> Create { get; set; } = (type) => Activator.CreateInstance(type);

        private static Exception Invoke(MethodInfo method, object instance) { try { (method?.Invoke(instance, new object[0]) as System.Threading.Tasks.Task)?.GetAwaiter().GetResult(); return default; } catch (Exception e) { return e is TargetInvocationException ? e.InnerException : e; } }
        private static void Write(ConsoleColor color, string message) { try { Console.ForegroundColor = color; Console.Write(message); } finally { Console.ResetColor(); } }

        public IEnumerable<(string Suite, object Instance, Exception Error, IEnumerable<(string Name, Exception Error, TimeSpan Time)> Results)> Run() =>
            new Action<(string, Exception, TimeSpan)>(x => { }).Map(cb => suites.Select(suite => suite.Run(cb).Map(x => (Suite: suite.Suite, Instance: x.Instance, Error: x.Error, Results: x.Results.ToList().AsEnumerable()))).ToList());

        public IEnumerable<(string Suite, object Instance, Exception Error, IEnumerable<(string Name, Exception Error, TimeSpan Time)> Results)> RunToConsole() =>
            new Func<(string Name, Exception Error, TimeSpan Time), object>(x => x.Error == null
                ? x.Tap(y => Write(ConsoleColor.Green, $"\t\u2713")).Tap(y => Console.Write($" {y.Name} ")).Tap(y => Write(ConsoleColor.Cyan, $"{(int)y.Time.TotalMilliseconds} ms.\n"))
                : x.Tap(y => Write(ConsoleColor.Red, $"\t\u00d7")).Tap(y => Console.Write($" {y.Name} ")).Tap(y => Write(ConsoleColor.Cyan, $"{(int)y.Time.TotalMilliseconds} ms.\n")).Tap(y => Write(ConsoleColor.DarkYellow, $"{y.Error}\n")))
            .Map(reporter => suites.Select(suite => (Suite: suite.Suite, Results: suite.Tap(y => Console.WriteLine(y.Suite)).Run(x => reporter(x)).Tap(x => Write(ConsoleColor.DarkYellow, $"{x.Error}\n")))
            .Map(x => (Suite: x.Suite, Instance: x.Results.Instance, Error: x.Results.Error, Results: x.Results.Results)))
            .ToList()
            .Tap(results => Console.WriteLine($"{results.Sum(x => x.Results.Count())} total, {results.Sum(x => x.Results.Count(y => y.Error == default))} successful, {results.Sum(x => x.Results.Count(y => y.Error != default))} failed\n")));

        public PUnit Test<T>() =>
            new Func<Action<(string, Exception, TimeSpan)>, (object instance, Exception Error, IEnumerable<(string Name, Exception Error, TimeSpan Time)> Results)>((testCompleted) =>
                Create(typeof(T)).Map(instance =>
                    Invoke(typeof(T).GetMethod("Before"), instance)
                    .Map(x => x != null
                        ? (Error: x, Results: new (string Name, Exception Error, TimeSpan Time)[0].AsEnumerable())
                        : (Error: default, Results: CollectTests<T>().Select(test => test.Run(instance).Map(result => (Name: test.Name, Error: result.Error, Time: result.Time)).Tap(x => testCompleted(x))).ToList())
                    )
                    .Map(x => (Instance: instance, Error: x.Error ?? Invoke(typeof(T).GetMethod("After"), instance), Results: x.Results))
            ))
            .Map(x => (Suite: typeof(T).Name, Run: x))
            .Tap(x => suites.Add(x)).Map(x => this);

        private static IEnumerable<(string Name, Func<object, (Exception Error, TimeSpan Time)> Run)> CollectTests<T>() =>
            typeof(T).GetMethods().Where(x => x.Name.StartsWith("Only_") && x.IsPublic && (x.ReturnType == typeof(void) || x.ReturnType == typeof(System.Threading.Tasks.Task)) && x.GetParameters().Length == 0)
                .Map(x => x.Count() == 0 ? typeof(T).GetMethods().Where(x => (x.Name.EndsWith("Test") || x.Name.StartsWith("Should")) && !x.Name.StartsWith("Skip_") && x.IsPublic && (x.ReturnType == typeof(void) || x.ReturnType == typeof(System.Threading.Tasks.Task)) && x.GetParameters().Length == 0) : x)
                .Select(test => (Name: test.Name, Run: new Func<object, Exception>(i => new[] { Invoke(typeof(T).GetMethod("BeforeEach"), i) ?? Invoke(test, i), Invoke(typeof(T).GetMethod("AfterEach"), i) }.Where(x => x != null).Map(x => x.Count() > 1 ? new AggregateException(x) : x.FirstOrDefault()))))
                .Select(test => (Name: test.Name, Run: new Func<object, (Exception, TimeSpan)>(i => System.Diagnostics.Stopwatch.StartNew().Map(w => (Error: test.Run(i), Time: w.Elapsed)))));
    }
} // line #48

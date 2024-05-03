using System;
using System.Collections.Generic;
using System.Linq;

namespace Pinduri
{
    public static class SnakeGame
    {
        internal record struct Game(IEnumerable<(int x, int y)> snake, (int x, int y) food);
        internal static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> val, int len) => val.Count() <= len ? new[] { val } : new[] { val.Take(len) }.Concat(Chunk(val.Skip(len), len));
        internal static IEnumerable<T> Infinite<T>(T state, Func<T, T> func) { while (true) yield return state = func(state); }
        internal static T WithDelay<T>(this T val, int milliSeconds) { System.Threading.Thread.Sleep(milliSeconds); return val; }
        internal static Func<(int, int), Func<(int dx, int dy)>> CreateDirection = ((int dx, int dy) dir) => () => dir = Console.KeyAvailable ? Console.ReadKey().Key.Map(k => k == ConsoleKey.LeftArrow ? (-1, 0) : k == ConsoleKey.RightArrow ? (1, 0) : k == ConsoleKey.UpArrow ? (0, -1) : k == ConsoleKey.DownArrow ? (0, 1) : dir) : dir;
        internal static bool SnakeHitsItself(IEnumerable<(int x, int y)> snake) => snake.Skip(1).Contains(snake.First());
        internal static IEnumerable<T> Print<T>(IEnumerable<T> val) => val.Tap(x => Console.CursorVisible = false).Tap(x => Console.SetCursorPosition(0, 0)).Select(x => x.Tap(x => Console.WriteLine(x))).ToList();
        internal static Func<int, int, Func<(int, int)>, Func<Game, Game>> CreateLogic = (int width, int height, Func<(int dx, int dy)> direction) => (Game state) => direction().Map(dir => state.snake.First().Map(h => (x: (h.x + dir.dx + width) % width, y: (h.y + dir.dy + height) % height))).Map(h => state.snake.Prepend(h).ToList()).Map(snake => snake.First() == state.food ? new Game(snake, (Random.Shared.Next(width), Random.Shared.Next(height))) : new Game(snake.SkipLast(1), state.food));
        internal static Func<int, int, Func<Game, IEnumerable<string>>> CreateRenderer = (int width, int height) => (Game state) => Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Select(x => (x, y))).Select(c => c == state.food ? "\u00B7" : state.snake.Contains(c) ? "O" : " ").Chunk(width).Select(x => $"|{string.Concat(x)}|");
        public static void Play(int width = 12, int height = 12, int speed = 5) => (initialState: new Game(new[] { (x: width / 2, y: height / 2) }, (1, 1)), logic: CreateLogic(width, height, CreateDirection((1, 0))), renderer: CreateRenderer(width, height)).Map(g => Infinite(g.initialState, g.logic).TakeWhile(x => !SnakeHitsItself(x.snake)).Select(x => Print(g.renderer(x))).Select(x => x.WithDelay(1000 / speed)).Count());
    }
} // line #20

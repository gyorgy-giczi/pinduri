using System;
using System.Linq;

namespace Pinduri
{
    internal class WebSite
    {
        private static HttpServer.Middleware DefaultPage(string page = "index.html") => (ctx, next) => ctx.Map(x => x.Get<Uri>("Request.Url").LocalPath.EndsWith("/") ? x.Set("Request.Url", new UriBuilder(x.Get<Uri>("Request.Url")).Tap(x => x.Path = x.Path + page).Uri) : x).Map(x => next(x));
        private static HttpServer.Middleware DisableOptions() => (ctx, next) => ctx.Get<string>("Request.method") == "OPTIONS" ? ctx.Set("Response.Status", "405") : next(ctx);
        private static HttpServer.Middleware LastVisited() => (ctx, next) => ctx.Append(new ("Response.Headers.Set-Cookie", $"PreviousLastVisited={ctx.Get<string>("Request.Cookies.LastVisited")}")).Append(new ("Response.Headers.Set-Cookie", $"LastVisited={DateTime.Now.ToString("u")}" )).Map(x => next(x));
        private static HttpServer.Middleware NotFound() => (ctx, next) => ctx.StringContent("<h1>404 - No shrubberies", "404");
        private static HttpServer.Middleware RequestCounter(string sessionVariable = "RequestCounter") => (ctx, next) => next(ctx.Set($"Session.{sessionVariable}", (ctx.Get<int?>($"Session.{sessionVariable}") ?? 0) + 1));
        private static HttpServer.Middleware ViewContext() => (ctx, next) => ctx.OrderBy(x => x.Key).Select(x => $"<tr><td>{x.Key}</td><td>{x.Value}</td></tr>").Prepend("<h1>Context</h1><table>").Map(x => string.Concat(x)).Map(x => ctx.StringContent(x));
        private static HttpServer.Middleware ThrowError(string message) => (ctx, next) => throw new Exception(message);

        public static void Run(string rootPath, int port = 55445)
        {
            var server = HttpServer.Start(port,
                new[] {
                    HttpServer.RequestLogger(),
                    HttpServer.ErrorHandler(),
                    DisableOptions(), // demonstrates simple middleware
                    DefaultPage("index.html"), // demonstrates context manipulation

                    HttpServer.CookieHandler(),
                    LastVisited(), // demonstrates cookies

                    HttpServer.Session(HttpServer.CreateInMemorySessionStore()),
                    RequestCounter(), // demonstrates session

                    ThrowError("ROARR!").ForRoute("*", "/error"), // demonstrates error page
                    ViewContext().ForRoute("GET", "/context"), // shows context (request and session), unsafe :-)
                    NotFound().ForRoute("GET", "/not-found"),

                    HttpServer.StaticFile(rootPath).ForRoute("GET", "/"),
                    NotFound(),
                }
            );

            Console.Error.WriteLine($"server started at http://{server.Server.LocalEndPoint}{Environment.NewLine}Root path is {rootPath}");
            Console.ReadLine();

            server.Stop();
        }
    }
}

using System;

namespace Pinduri
{
    internal class WebSite
    {
        private static HttpServer.Middleware DefaultPage(string page = "index.html") => (ctx, next) => ctx.Map(x => x.Get<Uri>("Request.Url").LocalPath.EndsWith("/") ? x.Set("Request.Url", new UriBuilder(x.Get<Uri>("Request.Url")).Tap(x => x.Path = x.Path + page).Uri) : x).Map(x => next(x));
        private static HttpServer.Middleware DisableOptions() => (ctx, next) => ctx.Get<string>("Request.method") == "OPTIONS" ? ctx.Set("Response.Status", "405") : next(ctx);
        private static HttpServer.Middleware ThrowError(string message) => (ctx, next) => throw new Exception(message);
        private static HttpServer.Middleware NotFound() => (ctx, next) => ctx.StringContent("<h1>404 - No shrubberies", "404");

        public static void Run(string rootPath, int port = 55445)
        {
            var server = HttpServer.Start(port,
                new[] {
                    HttpServer.RequestLogger(),
                    HttpServer.ErrorHandler(),
                    DisableOptions(),
                    DefaultPage("index.html"),
                    ThrowError("ROARR!").ForRoute("*", "/error"),
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

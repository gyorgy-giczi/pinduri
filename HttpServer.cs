using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TcpListener = System.Net.Sockets.TcpListener;
using Context = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>>;

namespace Pinduri
{
    public static partial class HttpServer // core
    {
        public delegate Context Middleware(Context context, Func<Context, Context> next);

        private static IEnumerable<string> AsEnumerable(this StreamReader reader) { while (!reader.EndOfStream) { yield return reader.ReadLine(); }; }
        internal static IEnumerable<KeyValuePair<string, string>> ParseKeyValueList(string value, char separator = ';') => (value ?? "").Trim().Split(separator, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2).Append("").ToArray().Map(x => new KeyValuePair<string, string>(x[0], x[1])));
        public static Context Set(this Context ctx, string key, object val) => ctx.Where(x => !string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Append(new KeyValuePair<string, object>(key, val));
        public static TValue Get<TValue>(this Context ctx, string key) => ctx.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Map(x => x.Key != default ? (TValue)x.Value : default(TValue));

        public static TcpListener Start(int port, params Middleware[] middlewares) =>
            middlewares.Prepend(ParseQueryString()).Prepend(ParseUrl(Environment.MachineName, port)).Map(middlewares => BuildMiddlewareChain(middlewares))
            .Map(pipeline => new TcpListener(System.Net.IPAddress.Any, port)
                .Tap(x => x.Start())
                .Tap(x => AcceptConnection(x, stream => HandleRequest(stream, pipeline))));

        private static void AcceptConnection(TcpListener listener, Action<Stream> callback) => System.Threading.Tasks.Task.Run(async () => (await listener.AcceptTcpClientAsync()).Tap(x => AcceptConnection(listener, callback)).Tap(x => callback(x.GetStream())).Tap(x => x.Close()));

        private static void HandleRequest(Stream stream, Func<Context, Context> pipeline) =>
            ReadContext(stream)
            .Map(ctx => pipeline.Invoke(ctx))
            .Map(ctx => WriteContext(ctx, stream))
            .Tap(ctx => ctx.Select(x => x.Value).OfType<IDisposable>().Select(x => x.Tap(x => x.Dispose())));

        internal static Context ReadContext(Stream stream) =>
            new StreamReader(stream, System.Text.Encoding.GetEncoding("iso-8859-1")).Map(reader => new KeyValuePair<string, object>[0].AsEnumerable()
                .Map(ctx => reader.AsEnumerable().TakeWhile(x => x != "").ToArray().Map(headerLines => (headerLines.FirstOrDefault() ?? " ").Split(' ').Map(parts => new[] { (Key: "Method", Value: parts[0]), (Key: "Url", Value: parts[1]) }.Aggregate(ctx, (a, x) => a.Set($"Request.{x.Key}", x.Value)))
                    .Map(ctx => headerLines.Skip(1).Select(header => header.Split(':', 2).Map(x => (Key: x[0].Trim(), Value: x[1].Trim()))).Aggregate(ctx, (a, x) => a.Set($"Request.Headers.{x.Key}", x.Value)))))
                .Map(ctx => ctx.Set("Request.RawBody", Convert.ToInt32(ctx.Get<string>("Request.Headers.Content-Length") ?? "0").Map(contentLength => new char[contentLength].Map(buffer => (buffer, bytesRead: reader.Read(buffer, 0, contentLength))).Map(x => x.buffer.Take(x.bytesRead))).Select(x => (byte)x).ToArray()))
                .Map(ctx => ctx.Set("Response.BodyStream", new MemoryStream()))
            );

        internal static Context WriteContext(Context ctx, Stream stream) =>
            ctx.Map(ctx => (contentLength: ctx.Get<Stream>("Response.BodyStream").Map(x => x == null ? 0 : x.Length), status: ctx.Get<object>("Response.Status") ?? "200 OK"))
                .Map(x => ctx.Set("Response.Status", x.status).Set("Response.Headers.Content-Length", x.contentLength))
                .Tap(ctx => new Stream[0]
                    .Append(ctx.Where(x => x.Key.StartsWith("Response.Headers.", StringComparison.OrdinalIgnoreCase)).Select(x => $"{x.Key.Substring(17)}: {x.Value}").Prepend($"HTTP/1.0 {ctx.Get<object>("Response.Status")}").Append("\r\n").Map(x => string.Join("\r\n", x)).Map(headers => System.Text.Encoding.ASCII.GetBytes(headers)).Map(x => new MemoryStream(x)))
                    .Map(x => ctx.Get<Stream>("Response.BodyStream").Map(s => s == null ? x : x.Append(s)))
                    .Select(x => x.Tap(x => x.Seek(0, SeekOrigin.Begin)).Tap(x => x.CopyTo(stream))).ToList());

        internal static Func<Context, Context> BuildMiddlewareChain(IEnumerable<Middleware> middlewares) => (middlewares ?? new Middleware[0]).Reverse().Aggregate(new Func<Context, Context>(ctx => ctx), (a, x) => ctx => x(ctx, ctx => a(ctx)));
        internal static Middleware ParseUrl(string host = "localhost", int port = 80) => (ctx, next) => ctx.Get<object>("Request.Url").Map(x => x != null && x is Uri uri ? uri : ((string)x).Map(x => x ?? "").Map(x => new Uri(x.Contains("://") ? x : $"http://{host}:{port}/{x.TrimStart('/')}"))).Map(x => ctx.Set("Request.Url", x)).Map(x => next(x));
        internal static Middleware ParseQueryString() => (ctx, next) => (ctx.Get<Uri>("Request.url") ?? new Uri("http://localhost/")).Query.TrimStart('?').Map(x => ParseKeyValueList(x, '&')).Select(x => (Key: Uri.UnescapeDataString(x.Key), Value: Uri.UnescapeDataString(x.Value))).Aggregate(ctx, (a, x) => a.Set($"Request.Query.{x.Key}", x.Value)).Map(x => next(x));
        public static Middleware Combine(params Middleware[] middlewares) => (ctx, next) => BuildMiddlewareChain(middlewares.Append((ctx, _) => next(ctx))).Invoke(ctx);
        public static Middleware EndPipeline() => (ctx, _) => ctx;
        public static Middleware When(this Middleware middleware, Predicate<Context> condition) => (ctx, next) => condition(ctx) ? middleware(ctx, next) : next(ctx);
    }

    public static partial class HttpServer // middlewares
    {
        public static Context BinaryContent(this Context ctx, byte[] content, string status = "200", string contentType = null) => ctx.Set("Response.Status", status).Map(ctx => contentType != null ? ctx.Set("Response.Headers.Content-Type", contentType) : ctx).Set("Response.BodyStream", new MemoryStream(content ?? new byte[0]));
        public static Context StringContent(this Context ctx, string content, string status = "200", string contentType = null) => ctx.BinaryContent(System.Text.Encoding.UTF8.GetBytes(content ?? ""), status, contentType);
        public static Middleware ErrorHandler(TextWriter textWriter = default, Func<Exception, string> errorPage = default) => (ctx, next) => { try { return next(ctx); } catch (Exception e) { (textWriter ?? Console.Error).WriteLine(e); return ctx.Where(x => !x.Key.StartsWith("Response.")).StringContent((errorPage ?? (e => $"<h1>500 - Internal Server Error</h1><pre>{e}"))(e), "500", "text/html"); } };
        public static Middleware ForRoute(this Middleware middleware, string method, string path) => middleware.When(ctx => method.Split(',').Select(x => x.Trim()).Map(x => x.Contains("*") || x.Contains(ctx.Get<string>("Request.Method"))) && ctx.Get<Uri>("Request.Url")?.LocalPath.Trim('/').StartsWith(path.TrimStart('/'), StringComparison.OrdinalIgnoreCase) == true).When(ctx => method != null && path != null);

        public static Middleware StaticFile(string root, Func<string, string> mimeType = default, Func<string, byte[]> readFile = default)
        {
            mimeType = mimeType ?? ParseKeyValueList("html=text/html;png=image/png;jpg=image/jpg;css=text/css", ';').ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase).Map(mimeMap => new Func<string, string>(x => (x ?? "").Trim('.').Map(x => mimeMap.ContainsKey(x) ? mimeMap[x] : "application/octet-stream")));
            bool IsSubPathOf(string subPath, string basePath) => Path.GetRelativePath(basePath, subPath).Map(rel => !rel.StartsWith('.') && !Path.IsPathRooted(rel));
            bool IsValid(string path, string root) => IsSubPathOf(path, Path.GetFullPath(root ?? ".")) && File.Exists(path);
            return (ctx, next) => Path.Combine(Path.GetFullPath(root ?? "."), ctx.Get<Uri>("Request.Url")?.LocalPath.Trim('/', '\\') ?? "").Map(path => IsValid(path, root) ? ctx.BinaryContent((readFile ?? File.ReadAllBytes)(path), "200", mimeType(Path.GetExtension(path))) : ctx.Set("Response.Status", 404));//.Map(x => next(x));
        }

        public static Middleware RequestLogger(TextWriter writer = default, Func<DateTime> now = default)
        {
            (writer, now) = (writer ?? Console.Out, now ?? new Func<DateTime>(() => DateTime.UtcNow));
            writer.WriteLine($"#Fields: date time cs-method cs-uri sc-status sc-bytes time-taken");
            return (ctx, next) => System.Diagnostics.Stopwatch.StartNew().Map(sw => next(ctx).Tap(ctx => writer.WriteLine($"{now().ToString("u")} {ctx.Get<string>("Request.Method").Map(x => string.IsNullOrEmpty(x) ? "-" : x)} {(ctx.Get<Uri>("Request.Url")?.PathAndQuery).Map(x => string.IsNullOrEmpty(x) ? "-" : x)} {(ctx.Get<object>("Response.status")?.ToString().Replace(" ", "+")).Map(x => string.IsNullOrEmpty(x) ? "-" : x)} {ctx.Get<Stream>("Response.BodyStream")?.Length.ToString() ?? "-"} {sw.ElapsedMilliseconds}")));
        }
    }
} // line #79

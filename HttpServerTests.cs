using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Context = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>>;
using static Pinduri.HttpServer;

namespace Pinduri.Tests
{
    public class HttpServerTests
    {
        private static readonly Func<Context, Context> DefaultNext = ctx => ctx;

        private static Context NewContext() => new KeyValuePair<string, object>[0];

        public static string Stringify(Context items, bool stringifyByteArrays = true) =>
            items == null ? "null"
            : items
                .OrderBy(x => x.Key)
                .Select(e =>
                    e.Map(x => x.Value switch
                        {
                            null => "<null>",
                            Uri uri => uri.OriginalString,
                            string[] lines => string.Join(Environment.NewLine, lines),
                            byte[] bytes when bytes.Length < 1000 && stringifyByteArrays => Convert.ToHexString(bytes) + " " + System.Text.Encoding.GetEncoding("iso-8859-1").GetString(bytes),
                            byte[] bytes when bytes.Length < 1000 => Convert.ToHexString(bytes),
                            byte[] bytes when bytes.Length >= 1000 => $"Length: {bytes.Length}",
                            Stream stream => $"Length: {stream.Length}",
                            _ => x.Value.ToString()
                        }
                    )
                    .Map(s => $"{e.Key} ({e.Value?.GetType().Name}): {s}")
                )
                .Map(x => string.Join(Environment.NewLine, x));

        private class ReadContextTest
        {
            private static Stream ToStream(IEnumerable<string> header, byte[] body = null) =>
                new MemoryStream()
                    .Tap(s => s.Write(System.Text.Encoding.ASCII.GetBytes(string.Join("\r\n", header) + "\r\n\r\n")))
                    .Tap(s => s.Write(body ?? new byte[0]))
                    .Tap(s => s.Seek(0, SeekOrigin.Begin));

            public void ShouldRead_When_RequestIsEmpty()
            {
                var expected = new[] 
                {
                    "Request.Method (String): ",
                    "Request.RawBody (Byte[]):  ",
                    "Request.Url (String): ",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(new MemoryStream());
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldRead_When_RequestHasNoRequestLine()
            {
                var header = new[] 
                {
                    "",
                };

                var expected = new[] 
                {
                    "Request.Method (String): ",
                    "Request.RawBody (Byte[]):  ",
                    "Request.Url (String): ",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldRead_When_RequestHasOnlyRequestLine()
            {
                var header = new[] 
                {
                    "GET /index.html HTTP/1.0",
                };

                var expected = new[] 
                {
                    "Request.Method (String): GET",
                    "Request.RawBody (Byte[]):  ",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldRead_When_RequestHasHeaders()
            {
                var header = new[] 
                {
                    "GET /index.html HTTP/1.0",
                    "Some-Header: some-header-value;some-attribute=some-attribute-value",
                };

                var expected = new[] 
                {
                    "Request.Headers.Some-Header (String): some-header-value;some-attribute=some-attribute-value",
                    "Request.Method (String): GET",
                    "Request.RawBody (Byte[]):  ",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldRead_When_RequestHasAsciiBody()
            {
                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                    "Content-Length: 5",
                };

                var expected = new[] 
                {
                    "Request.Headers.Content-Length (String): 5",
                    "Request.Method (String): POST",
                    "Request.RawBody (Byte[]): 726F617272 roarr",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, System.Text.Encoding.ASCII.GetBytes("roarr")));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldRead_When_RequestHasUtf16Body()
            {
                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                    "Content-Length: 10",
                };

                var expected = new[] 
                {
                    "Request.Headers.Content-Length (String): 10",
                    "Request.Method (String): POST",
                    "Request.RawBody (Byte[]): 72006F00610072007200 r\0o\0a\0r\0r\0",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, System.Text.Encoding.Unicode.GetBytes("roarr")));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldRead_When_RequestHasBinaryBody()
            {
                var body = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                    $"Content-Length: {body.Length}",
                };

                var expected = new[] 
                {
                    $"Request.Headers.Content-Length (String): {body.Length}",
                    "Request.Method (String): POST",
                    $"Request.RawBody (Byte[]): {Convert.ToHexString(body)}",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, body));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result, stringifyByteArrays: false));
                Assert.AreEqual(true, result.Get<byte[]>("Request.RawBody").Zip(body).All(x => x.First == x.Second));
            }

            public void ShouldRead_When_BodyIsLarge()
            {
                var body = new byte[64 * 1024 * 1024]; // 64MB
                new Random().NextBytes(body);

                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                    $"Content-Length: {body.Length}",
                };

                var expected = new[] 
                {
                    $"Request.Headers.Content-Length (String): {body.Length}",
                    "Request.Method (String): POST",
                    $"Request.RawBody (Byte[]): Length: {body.Length}",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, body));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result, stringifyByteArrays: false));
                Assert.AreEqual(true, result.Get<byte[]>("Request.RawBody").Zip(body).All(x => x.First == x.Second));
            }

            public void ShouldRespectContentLengthHeader()
            {
                var body = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                    $"Content-Length: 18",
                };

                var expected = new[] 
                {
                    $"Request.Headers.Content-Length (String): 18",
                    "Request.Method (String): POST",
                    $"Request.RawBody (Byte[]): 000102030405060708090A0B0C0D0E0F1011",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, body));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result, stringifyByteArrays: false));
                Assert.AreEqual(true, result.Get<byte[]>("Request.RawBody").Zip(body).All(x => x.First == x.Second));
            }

            public void ShouldNotReadBody_When_ContentLengthheaderIsMissing()
            {
                var body = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();

                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                };

                var expected = new[] 
                {
                    "Request.Method (String): POST",
                    $"Request.RawBody (Byte[]): ",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, body));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result, stringifyByteArrays: false));
                Assert.AreEqual(true, result.Get<byte[]>("Request.RawBody").Zip(body).All(x => x.First == x.Second));
            }

            public void Skip_ShouldFail_When_BodyLengthIsLessThanContentLengthHeader()
            {
                // TODO: not implemented, so this test fails
                var body = Enumerable.Range(0, 10).Select(x => (byte)x).ToArray();

                var header = new[] 
                {
                    "POST /index.html HTTP/1.0",
                    "Content-Length: 11",
                };

                var expected = new[] 
                {
                    "Request.Headers.Content-Length: 11",
                    "Request.Method (String): POST",
                    $"Request.RawBody (Byte[]): ",
                    "Request.Url (String): /index.html",
                    "Response.BodyStream (MemoryStream): Length: 0",
                };

                var result = HttpServer.ReadContext(ToStream(header, body));
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result, stringifyByteArrays: false));
                Assert.AreEqual(true, result.Get<byte[]>("Request.RawBody").Zip(body).All(x => x.First == x.Second));
            }
        }

        private class WriteContextTest
        {
            private static string Read(Stream stream)
            {
                stream.Seek(0, SeekOrigin.Begin);
                return new StreamReader(stream, System.Text.Encoding.GetEncoding("ISO-8859-1")).ReadToEnd();
            }
            
            public void ShouldSetStatusTo200OK_When_StatusIsNotSet()
            {
                var ctx = NewContext();
                var stream = new MemoryStream();
                var expected = new string[]
                {
                    "HTTP/1.0 200 OK",
                    "Content-Length: 0",
                    "",
                    "",
                };

                HttpServer.WriteContext(ctx, stream);

                Assert.AreEqual(string.Join("\r\n", expected), Read(stream));
            }

            public void ShouldWriteStatus_When_StatusIsSet()
            {
                var ctx = NewContext().Set("Response.Status", "123 - Roarr");
                var stream = new MemoryStream();
                var expected = new string[]
                {
                    "HTTP/1.0 123 - Roarr",
                    "Content-Length: 0",
                    "",
                    "",
                };

                HttpServer.WriteContext(ctx, stream);

                Assert.AreEqual(string.Join("\r\n", expected), Read(stream));
            }

            public void ShouldWriteHeaders_When_HeaderContainsOnlyAsciiCharacters()
            {
                var headerValue = string.Concat(Enumerable.Range(0, 127).Select(x => (char)x));
                var ctx = NewContext()
                    .Set("Response.Headers.Header1", "headervalue1")
                    .Set("Response.Headers.Header2", headerValue);
                var stream = new MemoryStream();
                var expected = new string[]
                {
                    "HTTP/1.0 200 OK",
                    "Header1: headervalue1",
                    $"Header2: {headerValue}",
                    "Content-Length: 0",
                    "",
                    "",
                };

                HttpServer.WriteContext(ctx, stream);

                Assert.AreEqual(string.Join("\r\n", expected), Read(stream));
            }

            public void ShouldWriteHeaders_When_ThereAreMultipleOnesWithSameName()
            {
                var headerValue = string.Concat(Enumerable.Range(0, 127).Select(x => (char)x));
                var ctx = NewContext()
                    .Set("Response.Headers.Header1", "headervalue1")
                    .Append(new KeyValuePair<string, object>("Response.Headers.Header1", "headervalue2"))
                    .Append(new KeyValuePair<string, object>("Response.Headers.Header1", "headervalue3"));
                var stream = new MemoryStream();
                var expected = new string[]
                {
                    "HTTP/1.0 200 OK",
                    "Header1: headervalue1",
                    "Header1: headervalue2",
                    "Header1: headervalue3",
                    "Content-Length: 0",
                    "",
                    "",
                };

                HttpServer.WriteContext(ctx, stream);

                Assert.AreEqual(string.Join("\r\n", expected), Read(stream));
            }

            public void ShouldFindHeadersCaseInsensitive()
            {
                var ctx = NewContext()
                    .Set("RESPONSE.HEADERS.HEADER1", "headervalue1")
                    .Set("response.headers.header2", "HEADERVALUE2");
                var stream = new MemoryStream();
                var expected = new string[]
                {
                    "HTTP/1.0 200 OK",
                    "HEADER1: headervalue1",
                    "header2: HEADERVALUE2",
                    "Content-Length: 0",
                    "",
                    "",
                };

                HttpServer.WriteContext(ctx, stream);

                Assert.AreEqual(string.Join("\r\n", expected), Read(stream));
            }

            public void ShouldWriteBody()
            {
                var data = Enumerable.Range(0, 255).Select(x => (byte)x).ToArray();
                var ctx = NewContext().Set("Response.BodyStream", new MemoryStream(data));
                var stream = new MemoryStream();
                var expected = new string[]
                {
                    "HTTP/1.0 200 OK",
                    "Content-Length: 255",
                    "",
                    $"{string.Concat(data.Select(x => (char)x))}",
                };

                HttpServer.WriteContext(ctx, stream);

                Assert.AreEqual(string.Join("\r\n", expected), Read(stream));
            }
        }

        private class MiddlewareChainTest
        {
            public void ShouldBuildChain_When_MiddlewaresIsNull()
            {
                IEnumerable<HttpServer.Middleware> middlewares = null;
                var result = HttpServer.BuildMiddlewareChain(middlewares);
                Assert.IsNotNull(result);
            }

            public void ShouldBuildChain_When_MiddlewaresIsEmpty()
            {
                IEnumerable<HttpServer.Middleware> middlewares = new HttpServer.Middleware[0];
                var result = HttpServer.BuildMiddlewareChain(middlewares);
                Assert.IsNotNull(result);
            }

            public void ShouldMiddlewaresBeChainedInCorrectOrder()
            {
                var callLog = new List<string>();
                var middlewares = Enumerable.Range(1, 3).Select<int, HttpServer.Middleware>(id => (ctx, next) =>
                {
                    var value = ctx.Get<int>("value");
                    callLog.Add($"{id}: begin {value}");
                    var newContext = ctx.Set("value", value + 1);

                    var result = next(newContext);

                    var newValue = result.Get<int>("value");

                    callLog.Add($"{id}: end {newValue}");
                    return result.Set("value", newValue + 3);
                });

                var expected = new[]
                {
                    "1: begin 1",
                    "2: begin 2",
                    "3: begin 3",
                    "3: end 4",
                    "2: end 7",
                    "1: end 10"
                };

                var result = HttpServer.BuildMiddlewareChain(middlewares);
                Assert.IsNotNull(result);

                var ctx = result(new[] { new KeyValuePair<string, object>("value", 1) });

                Assert.AreEqual(string.Join(Environment.NewLine, expected), string.Join(Environment.NewLine, callLog));
                Assert.AreEqual(13, ctx.Get<int>("value"));
            }
        }

        private class ParseUrlTest
        {
            public void ShouldUseDefaultUrl_When_UrlDoesNotExist()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext();
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost:80/",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseProvidedDefaultHost_When_UrlDoesNotExist()
            {
                var target = HttpServer.ParseUrl(host: "roarr.org");
                var ctx = NewContext();
                var expected = new[] 
                {
                    "Request.Url (Uri): http://roarr.org:80/",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseProvidedDefaultPort_When_UrlDoesNotExist()
            {
                var target = HttpServer.ParseUrl(port: 8081);
                var ctx = NewContext();
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost:8081/",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseProvidedDefaultHostAndPort_When_UrlDoesNotExist()
            {
                var target = HttpServer.ParseUrl(host: "roarr.org", port: 8081);
                var ctx = NewContext();
                var expected = new[] 
                {
                    "Request.Url (Uri): http://roarr.org:8081/",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseDefaultUrl_When_UrlIsEmpty()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", "");
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost:80/",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldReplaceStringWithUri_When_UrlIsWellFormedString()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", "https://username:password@hostname:12345/some-path?param1=%25%20%26%20%3f%20%3d#some-fragment");
                var expected = new[] 
                {
                    "Request.Url (Uri): https://username:password@hostname:12345/some-path?param1=%25%20%26%20%3f%20%3d#some-fragment",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldKeepExistingUri_When_UrlIsUri()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", new Uri("https://username:password@hostname:12345/some-path?param1=%25%20%26%20%3f%20%3d#some-fragment"));
                var expected = new[] 
                {
                    "Request.Url (Uri): https://username:password@hostname:12345/some-path?param1=%25%20%26%20%3f%20%3d#some-fragment",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseDefaultBaseUrl_When_UrlIsRelativeWithoutTrailingSlash()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", "some-path");
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost:80/some-path",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseDefaultBaseUrl_When_UrlIsRelativeWithTrailingSlash()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", "/some-path");
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost:80/some-path",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseProvidedDefaultBaseUrl_When_UrlIsRelativeWithoutTrailingSlash()
            {
                var target = HttpServer.ParseUrl(host: "roarr.org", port: 8081);
                var ctx = NewContext().Set("Request.Url", "some-path");
                var expected = new[] 
                {
                    "Request.Url (Uri): http://roarr.org:8081/some-path",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldUseProvidedDefaultBaseUrl_When_UrlIsRelativeWithTrailingSlash()
            {
                var target = HttpServer.ParseUrl(host: "roarr.org", port: 8081);
                var ctx = NewContext().Set("Request.Url", "/some-path");
                var expected = new[] 
                {
                    "Request.Url (Uri): http://roarr.org:8081/some-path",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }



            public void ShouldUseDefaultBaseUrl_When_UrlIsRelativeAndHasQuery()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", "/some-path?param1=%25%20%26%20%3f%20%3d#some-fragment");
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost:80/some-path?param1=%25%20%26%20%3f%20%3d#some-fragment",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldThrowUriFormatException_When_UrlIsInvalid()
            {
                var target = HttpServer.ParseUrl();
                var ctx = NewContext().Set("Request.Url", "://");

                Assert.Throws<UriFormatException>(() => target(ctx, DefaultNext));
            }
        }

        private class ParseQueryStringTest
        {
            public void ShouldParse_When_UrlDoesNotExist()
            {
                var target = HttpServer.ParseQueryString();
                var ctx = NewContext();
                var expected = "";
                Assert.IsNotNull(target);

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(expected, Stringify(result));
            }

            public void ShouldParse_When_UrlHasNoQueryParameters()
            {
                var target = HttpServer.ParseQueryString();
                var ctx = NewContext().Set("Request.Url", new Uri("http://localhost/?"));
                var expected = new[] 
                {
                    "Request.Url (Uri): http://localhost/?",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldParse_When_UrlHasOneQueryParameter()
            {
                var target = HttpServer.ParseQueryString();
                var ctx = NewContext().Set("Request.Url", new Uri("http://localhost/?roarr=ROARR!"));
                var expected = new[] 
                {
                    "Request.Query.roarr (String): ROARR!",
                    "Request.Url (Uri): http://localhost/?roarr=ROARR!",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldParse_When_UrlHasQueryParameters()
            {
                var target = HttpServer.ParseQueryString();
                var ctx = NewContext().Set("Request.Url", new Uri("http://localhost/?roarr=ROARR!&param2=value2"));
                var expected = new[] 
                {
                    "Request.Query.param2 (String): value2",
                    "Request.Query.roarr (String): ROARR!",
                    "Request.Url (Uri): http://localhost/?roarr=ROARR!&param2=value2",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldParse_When_UrlHasEscapedQueryParameters()
            {
                var target = HttpServer.ParseQueryString();
                var ctx = NewContext().Set("Request.Url", new Uri("http://localhost/?roarr=%25%20%26%20%3f%20%3d"));

                var expected = new[] 
                {
                    "Request.Query.roarr (String): % & ? =",
                    "Request.Url (Uri): http://localhost/?roarr=%25%20%26%20%3f%20%3d",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldParse_When_UrlHasFragment()
            {
                var target = HttpServer.ParseQueryString();
                var ctx = NewContext().Set("Request.Url", new Uri("http://localhost/?roarr=%25%20%26%20%3f%20%3d#some-fragment"));
                var expected = new[] 
                {
                    "Request.Query.roarr (String): % & ? =",
                    "Request.Url (Uri): http://localhost/?roarr=%25%20%26%20%3f%20%3d#some-fragment",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }
        }

        private class CombineTest
        {
            private sealed class Counter { public int Value; }

            private static Middleware CreateMiddleware(int id, Counter r) => (ctx, next) => next(ctx.Set($"{++r.Value} Middleware #{id} before", null)).Set($"{++r.Value} Middleware #{id} after", null);

            public void ShouldCombine_When_MiddlewaresIsNull()
            {
                var result = HttpServer.Combine(null);
                Assert.IsNotNull(result);
            }

            public void ShouldCombine_When_MiddlewaresIsEmpty()
            {
                var result = HttpServer.Combine(new HttpServer.Middleware[0]);
                Assert.IsNotNull(result);
            }

            public void ShouldExecuteMiddlewaresInCorrectOrder()
            {
                var counter = new Counter();
                var middlewares = Enumerable.Range(1, 3).Select(x => CreateMiddleware(x, counter)).ToArray();
                var expected = new[] 
                {
                    "1 Middleware #1 before (): <null>",
                    "2 Middleware #2 before (): <null>",
                    "3 Middleware #3 before (): <null>",
                    "4 Next (): <null>",
                    "5 Middleware #3 after (): <null>",
                    "6 Middleware #2 after (): <null>",
                    "7 Middleware #1 after (): <null>",
                };

                var target = HttpServer.Combine(middlewares);
                Assert.IsNotNull(target);

                var result = target(NewContext(), ctx => ctx.Set($"{++counter.Value} Next", null));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExecuteMiddlewaresInCorrectOrder_When_ContainsEndPipeline()
            {
                var counter = new Counter();
                var middlewares = new[] 
                {
                    CreateMiddleware(1, counter),
                    CreateMiddleware(2, counter),
                    HttpServer.EndPipeline(),
                    CreateMiddleware(3, counter),
                };

                var expected = new[] 
                {
                    "1 Middleware #1 before (): <null>",
                    "2 Middleware #2 before (): <null>",
                    "3 Middleware #2 after (): <null>",
                    "4 Middleware #1 after (): <null>",
                };

                var target = HttpServer.Combine(middlewares);
                Assert.IsNotNull(target);

                var result = target(NewContext(), ctx => ctx.Set($"{++counter.Value} Next", null));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExecuteMiddlewaresInCorrectOrder_When_HasCombinedMiddlewares()
            {
                var counter = new Counter();
                var middlewares = new[] 
                {
                    CreateMiddleware(1, counter),
                    HttpServer.Combine(CreateMiddleware(101, counter), CreateMiddleware(102, counter)),
                    CreateMiddleware(2, counter),
                };

                var expected = new[] 
                {
                    "1 Middleware #1 before (): <null>",
                    "2 Middleware #101 before (): <null>",
                    "3 Middleware #102 before (): <null>",
                    "4 Middleware #2 before (): <null>",
                    "5 Next (): <null>",
                    "6 Middleware #2 after (): <null>",
                    "7 Middleware #102 after (): <null>",
                    "8 Middleware #101 after (): <null>",
                    "9 Middleware #1 after (): <null>",
                };

                var target = HttpServer.Combine(middlewares);
                Assert.IsNotNull(target);

                var result = target(NewContext(), ctx => ctx.Set($"{++counter.Value} Next", null));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExecuteMiddlewaresInCorrectOrder_When_HasCombinedMiddlewares_And_EndPipeline()
            {
                var counter = new Counter();
                var middlewares = new[] 
                {
                    CreateMiddleware(1, counter),
                    HttpServer.Combine(CreateMiddleware(101, counter), CreateMiddleware(102, counter), EndPipeline(), CreateMiddleware(103, counter)),
                    CreateMiddleware(2, counter),
                };

                var expected = new[] 
                {
                    "1 Middleware #1 before (): <null>",
                    "2 Middleware #101 before (): <null>",
                    "3 Middleware #102 before (): <null>",
                    "4 Middleware #102 after (): <null>",
                    "5 Middleware #101 after (): <null>",
                    "6 Middleware #1 after (): <null>",
                };

                var target = HttpServer.Combine(middlewares);
                Assert.IsNotNull(target);

                var result = target(NewContext(), ctx => ctx.Set($"{++counter.Value} Next", null));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }
        }

        private class EndPipelineTest
        {
            public void ShouldNotExecuteNext()
            {
                var target = HttpServer.EndPipeline();
                Assert.IsNotNull(target);

                var result = target(NewContext(), ctx => throw new Exception("Should not be executed"));

                Assert.AreEqual("", Stringify(result));
            }

            public void ShouldReturnTheSameContextInstance()
            {
                var target = HttpServer.EndPipeline();
                var ctx = NewContext();

                var result = target(ctx, ctx => throw new Exception("Should not be executed"));

                Assert.AreEqual(true, object.ReferenceEquals(ctx, result));
            }
        }

        private class WhenTest
        {
            public void ShouldExecuteMiddlewareThenNext_When_ConditionIsTrue()
            {
                int counter = 0;
                HttpServer.Middleware middleware = (ctx, next) => next(ctx.Set("Middleware.Before", ++counter)).Set("Middleware.After", ++counter);
                var expected = new[] 
                {
                    "Middleware.After (Int32): 3",
                    "Middleware.Before (Int32): 1",
                    "Next (Int32): 2",
                };

                var target = HttpServer.When(middleware, ctx => true);

                var result = target(NewContext(), ctx => ctx.Set("Next", ++counter));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExecuteMiddleware_When_ConditionIsTrueAndMiddlwareSkipsNext()
            {
                int counter = 0;
                HttpServer.Middleware middleware = (ctx, next) => ctx.Set("Middleware.Before", ++counter).Set("Middleware.After", ++counter);
                var expected = new[] 
                {
                    "Middleware.After (Int32): 2",
                    "Middleware.Before (Int32): 1",
                };

                var target = HttpServer.When(middleware, ctx => true);

                var result = target(NewContext(), ctx => ctx.Set("Next", ++counter));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExecuteOnlyNext_When_ConditionIsFalse()
            {
                int counter = 0;
                HttpServer.Middleware middleware = (ctx, next) => next(ctx.Set("Middleware.Before", ++counter)).Set("Middleware.After", ++counter);
                var expected = new[] 
                {
                    "Next (Int32): 1",
                };

                var target = HttpServer.When(middleware, ctx => false);

                var result = target(NewContext(), ctx => ctx.Set("Next", ++counter));

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }
        }

        private class BinaryContentTest
        {
            public void ShouldSetStatusAndBodyStream_When_ContentIsNull()
            {
                var content = (byte[])null;
                var result = NewContext().BinaryContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldSetStatusAndBodyStream_When_ContentIsEmpty()
            {
                var content = new byte[0];
                var result = NewContext().BinaryContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual(Convert.ToHexString(content), Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldSetBodyStream_When_ContentIsNotEmpty()
            {
                var content = Enumerable.Range(0, 256).Select(x => (byte)x).ToArray();
                var result = NewContext().BinaryContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 256",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual(Convert.ToHexString(content), Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldSetStatusAndBodyStream_When_StatusIsSpecified()
            {
                var content = new byte[0];
                var result = NewContext().BinaryContent(content: content, status: "ROARR");
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Status (String): ROARR",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual(Convert.ToHexString(content), Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldSetContentTypeAndStatusAndBodyStream_When_ContentTypeIsSpecified()
            {
                var content = new byte[0];
                var result = NewContext().BinaryContent(content: content, contentType: "text/roarr");
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Headers.Content-Type (String): text/roarr",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual(Convert.ToHexString(content), Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }
        }

        private class StringContentTest
        {
            public void ShouldSetStatusAndBodyStream_When_ContentIsNull()
            {
                var content = (string)null;
                var result = NewContext().StringContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldSetStatusAndBodyStream_When_ContentIsEmpty()
            {
                var content = "";
                var result = NewContext().StringContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual("", Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldSetBodyStream_When_ContentIsNotEmpty()
            {
                var content = "roarr";
                var result = NewContext().StringContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 5",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual("726F617272", Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldSetStatusAndBodyStream_When_StatusIsSpecified()
            {
                var content = "";
                var result = NewContext().StringContent(content: content, status: "ROARR");
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Status (String): ROARR",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual("", Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldSetContentTypeAndStatusAndBodyStream_When_ContentTypeIsSpecified()
            {
                var content = "";
                var result = NewContext().StringContent(content: content, contentType: "text/roarr");
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 0",
                    "Response.Headers.Content-Type (String): text/roarr",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual("", Convert.ToHexString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }

            public void ShouldEncodeInUtf8()
            {
                var content = string.Concat(Enumerable.Range(0, 384).Select(x => (char)x));
                var result = NewContext().StringContent(content: content);
                var expected = new[]
                {
                    "Response.BodyStream (MemoryStream): Length: 640",
                    "Response.Status (String): 200",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                Assert.AreEqual(content, System.Text.Encoding.UTF8.GetString(result.Get<MemoryStream>("Response.BodyStream").ToArray()));
            }
        }

        private class ErrorHandlerTest
        {
            public void ShouldNotChangeContext_When_ExceptionIsNotThrown()
            {
                var textWriter = new StringWriter();
                var errorPage = new Func<Exception, string>(e => "roarr");
                var target = HttpServer.ErrorHandler(textWriter, errorPage: errorPage);
                var next = DefaultNext;

                var ctx = NewContext()
                    .Set("Request.Roarr", "ROARR")
                    .Set("Response.Roarr", "ROARR")
                    .Set("Response.Status", "123 Roarr");

                var expected = Stringify(ctx);

                var result = target(ctx, next);
                Assert.AreEqual(expected, Stringify(result));
                Assert.AreEqual(ctx, result);
                Assert.AreEqual("", textWriter.ToString());
            }

            public void ShouldOverwriteResponse_When_ExceptionIsThrown()
            {
                var textWriter = new StringWriter();
                var errorPage = new Func<Exception, string>(e => "roarr");
                var target = HttpServer.ErrorHandler(textWriter, errorPage: errorPage);
                var exception = new Exception("Roarr");
                var next = new Func<Context, Context>(ctx => throw exception);

                var ctx = NewContext()
                    .Set("Request.Roarr", "ROARR")
                    .Set("Response.BodyStream", new MemoryStream())
                    .Set("Response.Headers.Content-Type", "text/roarr")
                    .Set("Response.Roarr", "ROARR")
                    .Set("Response.Status", "123 Roarr");

                var expected = new[]
                {
                    "Request.Roarr (String): ROARR",
                    "Response.BodyStream (MemoryStream): Length: 5",
                    "Response.Headers.Content-Type (String): text/html",
                    "Response.Status (String): 500",
                };

                var result = target(ctx, next);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldWriteException_When_ExceptionIsThrown()
            {
                var textWriter = new StringWriter();
                var target = HttpServer.ErrorHandler(textWriter);
                var exception = new Exception("Roarr");

                var result = target(NewContext(), ctx => throw exception);
                Assert.AreEqual(exception.ToString().Trim(), textWriter.ToString().Trim());
            }

            public void ShouldUseDefaultErrorPage_When_ExceptionIsThrown()
            {
                var textWriter = new StringWriter();
                var target = HttpServer.ErrorHandler(textWriter, errorPage: null);
                var exception = new Exception("Roarr");
                var next = new Func<Context, Context>(ctx => throw exception);

                var expected = new[]
                {
                    $"Response.BodyStream (MemoryStream): Length: {(Environment.NewLine == "\r\n" ? 437 : 425 )}", // TODO: this is fragile, work it around
                    "Response.Headers.Content-Type (String): text/html",
                    "Response.Status (String): 500",
                };

                var result = target(NewContext(), next);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }
        }

        private class ForRouteTest
        {
            private static void Test(List<((string Method, string Path) Route, (string Method, string Path) Request, bool Executed, bool NextExecuted)> cases)
            {
                cases.ForEach(x =>
                {
                    var target = new Middleware((ctx, next) => next(ctx.Set("Executed", true))).ForRoute(x.Route.Method, x.Route.Path);
                    var ctx = NewContext()
                        .Map(ctx => x.Request.Method != null ? ctx.Set("Request.Method", x.Request.Method) : ctx)
                        .Map(ctx => x.Request.Path != null ? ctx.Set("Request.Url", new Uri("http://localhost" + x.Request.Path)) : ctx);

                    var next = new Func<Context, Context>(ctx => ctx.Set("NextExecuted", true));
                    var expected = new string[]
                    {
                        "Executed (Boolean): " + x.Executed.ToString(),
                        "NextExecuted (Boolean): " + x.NextExecuted.ToString(),
                    }
                    .Append(x.Request.Method != null ? "Request.Method (String): " + x.Request.Method : null)
                    .Append(x.Request.Path != null ? "Request.Url (Uri): http://localhost" + x.Request.Path : null)
                    .Where(x => x != null).ToArray();

                    var result = target(ctx.Set("Executed", false).Set("NextExecuted", false), next);
                    Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result), x.ToString());
                });
            }

            public void ShouldExecute_When_BothMethodAndPathMatch()
            {
                Test(new()
                { 
                    // exact verb match
                    ( ("", "/"), ("", "/"), true, true),
                    ( ("GET", ""), ("GET", ""), true, true),
                    ( ("GET", "/"), ("GET", ""), true, true),
                    ( ("GET", "/"), ("GET", "/"), true, true),
                    ( ("GET", "/"), ("GET", "/index.html"), true, true),
                    ( ("GET", "/"), ("GET", "/path/to/index.html"), true, true),
                    ( ("GET", "/"), ("GET", "/path/to/index.html?roarr=1"), true, true),
                    ( ("GET", "/path"), ("GET", "/path/to/index.html"), true, true),
                    ( ("GET", "/path/"), ("GET", "/path/to/index.html"), true, true),
                    ( ("GET", "/path/to"), ("GET", "/path/to/index.html"), true, true),
                    ( ("GET", "/path/to/"), ("GET", "/path/to/index.html"), true, true),
                    ( ("GET", "/PaTh/tO/"), ("GET", "/paTH/to/index.html"), true, true),

                    // verb in list
                    ( ("GET,POST,DELETE", "/"), ("GET", "/index.html"), true, true),
                    ( ("GET  , POST", "/"), ("GET", "/index.html"), true, true),
                    ( ("POST,GET", "/"), ("GET", "/index.html"), true, true),
                    ( ("POST,DELETE,GET", "/"), ("GET", "/index.html"), true, true),

                    // wildcard in the list
                    ( ("*", "/"), (null, "/index.html"), true, true),
                    ( ("*", "/"), ("", "/index.html"), true, true),
                    ( ("*", "/"), ("GET", "/index.html"), true, true),
                    ( ("  *   ", "/"), ("GET", "/index.html"), true, true),
                    ( ("*,POST,DELETE", "/"), ("GET", "/index.html"), true, true),
                    ( ("  * , POST , DELETE  ", "/"), ("GET", "/index.html"), true, true),
                    ( ("*, POST, DELETE", "/"), ("GET", "/index.html"), true, true),
                    ( ("POST, *, DELETE", "/"), ("GET", "/index.html"), true, true),
                    ( ("POST, DELETE, *", "/"), ("GET", "/index.html"), true, true),
                });
            }

            public void ShouldNotExecute_When_MethodDoesNotMatch()
            {
                Test(new()
                {
                    ( (null, "/"), ("", "/index.html"), false, true),
                    ( (null, "/"), (null, "/index.html"), false, true),
                    ( ("", "/"), (null, "/index.html"), false, true),
                    ( ("", "/"), ("GET", "/index.html"), false, true),
                    ( ("get", "/"), ("GET", "/index.html"), false, true),
                    ( ("POST", "/"), ("GET", "/index.html"), false, true),
                    ( ("GET", "/"), (null, "/index.html"), false, true),
                    ( ("GET", "/"), ("", "/index.html"), false, true),
                });
            }

            public void ShouldNotExecute_When_PathDoesNotMatch()
            {
                Test(new()
                {
                    ( ("GET", null), ("GET", null), false, true),
                    ( ("GET", null), ("GET", ""), false, true),
                    ( ("GET", ""), ("GET", null), false, true),
                    ( ("GET", "/"), ("GET", null), false, true),
                    ( ("GET", "/path"), ("GET", null), false, true),
                    ( ("GET", "/path"), ("GET", ""), false, true),
                    ( ("GET", "/path"), ("GET", "/index.html"), false, true),
                    ( ("GET", "/path/"), ("GET", "/index.html"), false, true),
                    ( ("GET", "/path/to"), ("GET", "/index.html"), false, true),
                    ( ("GET", "/path"), ("GET", "/roarr/to/index.html"), false, true),
                    ( ("GET", "/path/"), ("GET", "/roarr/to/index.html"), false, true),
                    ( ("GET", "/path/to"), ("GET", "/path/roarr/index.html"), false, true),
                    ( ("GET", "/path/to/"), ("GET", "/path/roarr/index.html"), false, true),
                });
            }
        }

        private class StaticFileTest
        {
            public void ShouldReturn404_When_RootAndUrlAreNull()
            {
                var target = HttpServer.StaticFile(null);
                var expected = new string[]
                {
                    "Response.Status (Int32): 404",
                };

                var result = target(NewContext(), DefaultNext);

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldReturn404_When_UrlISNull()
            {
                var target = HttpServer.StaticFile("./roarr");
                var expected = new string[]
                {
                    "Response.Status (Int32): 404",
                };

                var result = target(NewContext(), DefaultNext);

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldReturn404_When_RootDoesNotExists()
            {
                var target = HttpServer.StaticFile($"./{Guid.NewGuid()}");
                var ctx = NewContext().Set("Request.Url", new Uri("http://localhost:80/index.html"));
                var expected = new string[]
                {
                    "Request.Url (Uri): http://localhost:80/index.html",
                    "Response.Status (Int32): 404",
                };

                var result = target(ctx, DefaultNext);

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }
        }

        private class RequestLoggerTest
        {
            private static string TrimLastNumberFromTheEndOfTheLines(string value) => string.Join(Environment.NewLine, value.Split(Environment.NewLine).Select(x => x.TrimEnd("0123456789".ToCharArray()))).Trim('\r', '\n');

            public void ShouldLogHeaderOnCreation()
            {
                var textWriter = new StringWriter();
                var now = new Func<DateTime>(() => new DateTime(2023, 4, 5, 6, 7, 8));
                var target = HttpServer.RequestLogger(textWriter, now);
                var expected = new[]
                {
                    "#Fields: date time cs-method cs-uri sc-status sc-bytes time-taken",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), TrimLastNumberFromTheEndOfTheLines(textWriter.ToString()));
            }

            public void ShouldLogAvailableInformation()
            {
                var textWriter = new StringWriter();
                var now = new Func<DateTime>(() => new DateTime(2023, 4, 5, 6, 7, 8));
                var target = HttpServer.RequestLogger(textWriter, now);

                var expected = new[]
                {
                    "#Fields: date time cs-method cs-uri sc-status sc-bytes time-taken",
                    "2023-04-05 06:07:08Z - - - - ",
                    "2023-04-05 06:07:08Z GET - - - ",
                    "2023-04-05 06:07:08Z - / - - ",
                    "2023-04-05 06:07:08Z - /home?roarr=roarr-value&roarr2=roarr-value-2 - - ",
                    "2023-04-05 06:07:08Z - - 404+Not+Found - ",
                    "2023-04-05 06:07:08Z - - - 5 ",
                    "2023-04-05 06:07:08Z GET /home?roarr=roarr-value&roarr2=roarr-value-2 404+Not+Found 5 ",
                };

                target(NewContext(), DefaultNext);
                target(NewContext().Set("Request.Method", "GET"), DefaultNext);
                target(NewContext().Set("Request.Url", new Uri("http://localhost:12345/")), DefaultNext);
                target(NewContext().Set("Request.Url", new Uri("http://localhost:12345/home?roarr=roarr-value&roarr2=roarr-value-2")), DefaultNext);
                target(NewContext().Set("Response.Status", "404 Not Found"), DefaultNext);
                target(NewContext().Set("Response.BodyStream", new MemoryStream(new byte[] { 1, 2, 3, 4, 5, })), DefaultNext);
                target(NewContext()
                    .Set("Request.Method", "GET")
                    .Set("Request.Url", new Uri("http://localhost:12345/home?roarr=roarr-value&roarr2=roarr-value-2"))
                    .Set("Response.Status", "404 Not Found")
                    .Set("Response.BodyStream", new MemoryStream(new byte[] { 1, 2, 3, 4, 5, })), DefaultNext);

                Assert.AreEqual(string.Join(Environment.NewLine, expected), TrimLastNumberFromTheEndOfTheLines(textWriter.ToString()));
            }

            public void ShouldLogDashForMissingValues()
            {
                var textWriter = new StringWriter();
                var now = new Func<DateTime>(() => new DateTime(2023, 4, 5, 6, 7, 8));
                var target = HttpServer.RequestLogger(textWriter, now);

                var expected = new[]
                {
                    "#Fields: date time cs-method cs-uri sc-status sc-bytes time-taken",
                    "2023-04-05 06:07:08Z - - - - ",
                    "2023-04-05 06:07:08Z - - - - ",
                    "2023-04-05 06:07:08Z - - - - ",
                };

                target(NewContext(), DefaultNext);
                target(NewContext()
                    .Set("Request.Method", "")
                    .Set("Request.Url", null)
                    .Set("Response.Status", "")
                    .Set("Response.BodyStream", null), DefaultNext);
                target(NewContext()
                    .Set("Request.Method", null)
                    .Set("Request.Url", null)
                    .Set("Response.Status", null)
                    .Set("Response.BodyStream", null), DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), TrimLastNumberFromTheEndOfTheLines(textWriter.ToString()));
            }
        }

       private class CookieHandlerTest
        {
            public void ShouldNotExpandCookies_When_CookieHeaderIsNull()
            {
                var target = HttpServer.CookieHandler();
                var ctx = NewContext().Set("Request.Headers.Cookie", null);
                var expected = Stringify(ctx);

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldNotExpandCookies_When_CookieHeaderIsEmpty()
            {
                var target = HttpServer.CookieHandler();
                var ctx = NewContext().Set("Request.Headers.Cookie", "");
                var expected = Stringify(ctx);

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_CookieHeaderIsNotEmpty()
            {
                var target = HttpServer.CookieHandler();
                var ctx = NewContext().Set("Request.Headers.Cookie", "roarr=some-value");
                var expected = new[]
                {
                    "Request.Cookies.roarr (String): some-value",
                    "Request.Headers.Cookie (String): roarr=some-value",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_CookieHeaderHasMultipleCookies()
            {
                var target = HttpServer.CookieHandler();
                var ctx = NewContext().Set("Request.Headers.Cookie", "roarr=some-value;roarr2=some-other-value");
                var expected = new[]
                {
                    "Request.Cookies.roarr (String): some-value",
                    "Request.Cookies.roarr2 (String): some-other-value",
                    "Request.Headers.Cookie (String): roarr=some-value;roarr2=some-other-value",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_CookiesHaveNoValue()
            {
                var target = HttpServer.CookieHandler();
                var ctx = NewContext().Set("Request.Headers.Cookie", "roarr;roarr2=;roarr3;");
                var expected = new[]
                {
                    "Request.Cookies.roarr (String): ",
                    "Request.Cookies.roarr2 (String): ",
                    "Request.Cookies.roarr3 (String): ",
                    "Request.Headers.Cookie (String): roarr;roarr2=;roarr3;",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_ValueIsUrlEncoded()
            {
                var target = HttpServer.CookieHandler();
                var cookieValue = string.Concat(Enumerable.Range(0, 256).Select(x => (char)x));
                var ctx = NewContext().Set("Request.Headers.Cookie", "roarr=" + Uri.EscapeDataString(cookieValue));
                var expected = new[]
                {
                    "Request.Cookies.roarr (String): " + cookieValue,
                    "Request.Headers.Cookie (String): roarr=%00%01%02%03%04%05%06%07%08%09%0A%0B%0C%0D%0E%0F%10%11%12%13%14%15%16%17%18%19%1A%1B%1C%1D%1E%1F%20%21%22%23%24%25%26%27%28%29%2A%2B%2C-.%2F0123456789%3A%3B%3C%3D%3E%3F%40ABCDEFGHIJKLMNOPQRSTUVWXYZ%5B%5C%5D%5E_%60abcdefghijklmnopqrstuvwxyz%7B%7C%7D~%7F%C2%80%C2%81%C2%82%C2%83%C2%84%C2%85%C2%86%C2%87%C2%88%C2%89%C2%8A%C2%8B%C2%8C%C2%8D%C2%8E%C2%8F%C2%90%C2%91%C2%92%C2%93%C2%94%C2%95%C2%96%C2%97%C2%98%C2%99%C2%9A%C2%9B%C2%9C%C2%9D%C2%9E%C2%9F%C2%A0%C2%A1%C2%A2%C2%A3%C2%A4%C2%A5%C2%A6%C2%A7%C2%A8%C2%A9%C2%AA%C2%AB%C2%AC%C2%AD%C2%AE%C2%AF%C2%B0%C2%B1%C2%B2%C2%B3%C2%B4%C2%B5%C2%B6%C2%B7%C2%B8%C2%B9%C2%BA%C2%BB%C2%BC%C2%BD%C2%BE%C2%BF%C3%80%C3%81%C3%82%C3%83%C3%84%C3%85%C3%86%C3%87%C3%88%C3%89%C3%8A%C3%8B%C3%8C%C3%8D%C3%8E%C3%8F%C3%90%C3%91%C3%92%C3%93%C3%94%C3%95%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%9B%C3%9C%C3%9D%C3%9E%C3%9F%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%A8%C3%A9%C3%AA%C3%AB%C3%AC%C3%AD%C3%AE%C3%AF%C3%B0%C3%B1%C3%B2%C3%B3%C3%B4%C3%B5%C3%B6%C3%B7%C3%B8%C3%B9%C3%BA%C3%BB%C3%BC%C3%BD%C3%BE%C3%BF",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_ValueContainsSemicolonAndEqualSign()
            {
                var target = HttpServer.CookieHandler();
                var cookieValue = "value;a=b";
                var ctx = NewContext().Set("Request.Headers.Cookie", "roarr=" + Uri.EscapeDataString(cookieValue));
                var expected = new[]
                {
                    "Request.Cookies.roarr (String): " + cookieValue,
                    "Request.Headers.Cookie (String): roarr=value%3Ba%3Db",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_KeyIsUrlEncoded()
            {
                var target = HttpServer.CookieHandler();
                var cookieKey = string.Concat(Enumerable.Range(0, 256).Select(x => (char)x));
                var ctx = NewContext().Set("Request.Headers.Cookie", Uri.EscapeDataString(cookieKey) + "=roarr");
                var expected = new[]
                {
                    "Request.Cookies." + cookieKey + " (String): roarr",
                    "Request.Headers.Cookie (String): %00%01%02%03%04%05%06%07%08%09%0A%0B%0C%0D%0E%0F%10%11%12%13%14%15%16%17%18%19%1A%1B%1C%1D%1E%1F%20%21%22%23%24%25%26%27%28%29%2A%2B%2C-.%2F0123456789%3A%3B%3C%3D%3E%3F%40ABCDEFGHIJKLMNOPQRSTUVWXYZ%5B%5C%5D%5E_%60abcdefghijklmnopqrstuvwxyz%7B%7C%7D~%7F%C2%80%C2%81%C2%82%C2%83%C2%84%C2%85%C2%86%C2%87%C2%88%C2%89%C2%8A%C2%8B%C2%8C%C2%8D%C2%8E%C2%8F%C2%90%C2%91%C2%92%C2%93%C2%94%C2%95%C2%96%C2%97%C2%98%C2%99%C2%9A%C2%9B%C2%9C%C2%9D%C2%9E%C2%9F%C2%A0%C2%A1%C2%A2%C2%A3%C2%A4%C2%A5%C2%A6%C2%A7%C2%A8%C2%A9%C2%AA%C2%AB%C2%AC%C2%AD%C2%AE%C2%AF%C2%B0%C2%B1%C2%B2%C2%B3%C2%B4%C2%B5%C2%B6%C2%B7%C2%B8%C2%B9%C2%BA%C2%BB%C2%BC%C2%BD%C2%BE%C2%BF%C3%80%C3%81%C3%82%C3%83%C3%84%C3%85%C3%86%C3%87%C3%88%C3%89%C3%8A%C3%8B%C3%8C%C3%8D%C3%8E%C3%8F%C3%90%C3%91%C3%92%C3%93%C3%94%C3%95%C3%96%C3%97%C3%98%C3%99%C3%9A%C3%9B%C3%9C%C3%9D%C3%9E%C3%9F%C3%A0%C3%A1%C3%A2%C3%A3%C3%A4%C3%A5%C3%A6%C3%A7%C3%A8%C3%A9%C3%AA%C3%AB%C3%AC%C3%AD%C3%AE%C3%AF%C3%B0%C3%B1%C3%B2%C3%B3%C3%B4%C3%B5%C3%B6%C3%B7%C3%B8%C3%B9%C3%BA%C3%BB%C3%BC%C3%BD%C3%BE%C3%BF=roarr",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldExpandCookies_When_KeyContainsSemicolonAndEqualSign()
            {
                var target = HttpServer.CookieHandler();
                var cookieKey = "key;a=b";
                var ctx = NewContext().Set("Request.Headers.Cookie", Uri.EscapeDataString(cookieKey) + "=roarr");
                var expected = new[]
                {
                    "Request.Cookies." + cookieKey + " (String): roarr",
                    "Request.Headers.Cookie (String): key%3Ba%3Db=roarr",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldTrimValueAndKey()
            {
                var target = HttpServer.CookieHandler();
                var ctx = NewContext().Set("Request.Headers.Cookie", " roarr  =   some-value    ");
                var expected = new[]
                {
                    "Request.Cookies.roarr (String): some-value",
                    "Request.Headers.Cookie (String):  roarr  =   some-value    ",
                };

                var result = target(ctx, DefaultNext);
                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }
        }

        private class SessionTest
        {
            public void ShouldStoreNotThrowError_When_SessionIdIsNull()
            {
                var target = HttpServer.CreateInMemorySessionStore();
                target.Store(null, NewContext());
            }

            public void ShouldStoreNotThrowError_When_SessionIsNull()
            {
                var target = HttpServer.CreateInMemorySessionStore();
                target.Store("roarr", null);

                var result = target.Load("roarr");
                Assert.AreEqual(null, result);
            }

            public void ShouldGetEmptySession_When_SessionIdIsNull()
            {
                var target = HttpServer.CreateInMemorySessionStore();
                var result = target.Load(null);

                Assert.AreEqual("", Stringify(result));
            }

            public void ShouldGetEmptySession_When_SessionIdIsNullEvenIfStoredBefore()
            {
                var target = HttpServer.CreateInMemorySessionStore();
                target.Store(null, NewContext().Set("roarr", "some-value"));
                var result = target.Load(null);

                Assert.AreEqual("", Stringify(result));
            }

            public void ShouldGetEmptySession_When_SessionIdIsEmpty()
            {
                var target = HttpServer.CreateInMemorySessionStore();

                var result = target.Load("");
                Assert.AreEqual("", Stringify(result));
            }

            public void ShouldGetEmptySession_When_SessionIdIsEmptyEvenIfStoredBefore()
            {
                var target = HttpServer.CreateInMemorySessionStore();
                target.Store("", NewContext().Set("roarr", "some-value"));

                var result = target.Load("");
                Assert.AreEqual("", Stringify(result));
            }

            public void ShouldGetEmptySession_When_SessionDoesNotExist()
            {
                var target = HttpServer.CreateInMemorySessionStore();
                var result = target.Load("roarr");

                Assert.AreEqual("", Stringify(result));
            }

            public void ShouldGetSession_When_SessionExist()
            {
                var sessionId = "roarr";
                var session = NewContext().Set("roarr1", "some-roarr-value");
                var target = HttpServer.CreateInMemorySessionStore();
                target.Store(sessionId, session);
                var result = target.Load("roarr");

                Assert.AreEqual(Stringify(session), Stringify(result));
            }

            public void ShouldGenerateNewSessionId_When_SessionIdCookieIsMissing()
            {
                Func<string> idGenerator = () => "roarr";
                var store = HttpServer.CreateInMemorySessionStore();
                var target = HttpServer.Session(store, idGenerator);
                var ctx = NewContext();
                var next = new Func<Context, Context>(result =>
                {
                    var expected = new[]
                    {
                        "Request.SessionId (String): roarr",
                        "Response.Headers.Set-Cookie (String): SessionId=roarr",
                    };

                    Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                    return ctx;
                });

                target(ctx, next);
            }

            public void ShouldGenerateNewSessionId_When_SessionIdCookieIsEmpty()
            {
                Func<string> idGenerator = () => "roarr";
                var store = HttpServer.CreateInMemorySessionStore();
                var target = HttpServer.Session(store, idGenerator);
                var ctx = NewContext().Set("Request.Headers.Cookie", "SessionId=");
                var next = new Func<Context, Context>(result =>
                {
                    var expected = new[]
                    {
                        "Request.Headers.Cookie (String): SessionId=",
                        "Request.SessionId (String): roarr",
                        "Response.Headers.Set-Cookie (String): SessionId=roarr",
                    };

                    Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                    return ctx;
                });

                target(ctx, next);
            }

            public void ShouldUseSessionIdFromCookie()
            {
                var store = HttpServer.CreateInMemorySessionStore();
                var target = HttpServer.Session(store);
                var ctx = NewContext().Set("Request.Cookies.SessionId", "roarr");
                var next = new Func<Context, Context>(result =>
                {
                    var expected = new[]
                    {
                        "Request.Cookies.SessionId (String): roarr",
                        "Request.SessionId (String): roarr",
                    };

                    Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
                    return ctx;
                });

                target(ctx, next);
            }

            public void ShouldStoreSessionValues()
            {
                var store = HttpServer.CreateInMemorySessionStore();
                var target = HttpServer.Session(store);
                var ctx = NewContext().Set("Request.Cookies.SessionId", "roarr");
                var next = new Func<Context, Context>(result =>
                {
                    return ctx.Set("Session.Roarr", "some-session-value").Set("Session.Roarr2", "other-session-value");
                });

                target(ctx, next);

                var result = store.Load("roarr");

                var expected = new[]
                {
                    "Roarr (String): some-session-value",
                    "Roarr2 (String): other-session-value",
                };

                Assert.AreEqual(string.Join(Environment.NewLine, expected), Stringify(result));
            }

            public void ShouldSessionValuesBeCleanedAfterExecution()
            {
                var sessionId = "roarr";
                var store = HttpServer.CreateInMemorySessionStore();
                var target = HttpServer.Session(store);

                var expectedInside = new[]
                {
                    $"Request.Cookies.SessionId (String): {sessionId}",
                    $"Request.SessionId (String): {sessionId}",
                    "Session.SessionKey (String): some-session-value",
                };

                var expectedOutside = new[]
                {
                    $"Request.Cookies.SessionId (String): {sessionId}",
                    $"Request.SessionId (String): {sessionId}",
                };

                var next = new Func<Context, Context>(ctx =>
                {
                    Assert.AreEqual(string.Join(Environment.NewLine, expectedInside), Stringify(ctx));
                    return ctx;
                });

                store.Store(sessionId, NewContext().Set("SessionKey", "some-session-value"));

                var result = target(NewContext().Set("Request.Cookies.SessionId", sessionId), next);
                Assert.AreEqual(string.Join(Environment.NewLine, expectedOutside), Stringify(result));
            }
        }

        public static void Go()
        {
            new PUnit()
                .Test<ReadContextTest>()
                .Test<WriteContextTest>()
                .Test<MiddlewareChainTest>()
                .Test<ParseUrlTest>()
                .Test<ParseQueryStringTest>()
                .Test<CombineTest>()
                .Test<EndPipelineTest>()
                .Test<WhenTest>()

                .Test<BinaryContentTest>()
                .Test<StringContentTest>()
                .Test<ErrorHandlerTest>()
                .Test<ForRouteTest>()
                .Test<StaticFileTest>()
                .Test<RequestLoggerTest>()
                .Test<CookieHandlerTest>()
                .Test<SessionTest>()

                .RunToConsole();
        }
    }
}

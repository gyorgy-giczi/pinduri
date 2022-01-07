using System;
using System.Collections.Generic;
using System.Linq;

namespace Pinduri.Tests
{
    public class JsonTests
    {
        private class PropertyTest
        {
            public string ShouldIgnoreThisPublicField = null;
            private string ShouldIgnoreThisPrivateProperty { get; set; }
            internal string ShouldIgnoreThisInternalProperty { get; set; }
            protected string ShouldIgnoreThisProtectedProperty { get; set; }
            protected internal string ShouldIgnoreThisProtectedInternalProperty { get; set; }
            public string ShouldIgnoreThisWriteOnlyProperty { set { } }
            public static string ShouldIgnoreThisStaticProperty { get; set; }
            public string this[int index] { get { return "should ignore indexer"; } }

            public string ShouldSerializeThisReadWriteProperty { get; set; }
            public string ShouldSerializeThisReadOnlyProperty { get; }
        }

        private class Empty
        {
        }

        private class Employee
        {
            public string Name { get; set; }
            public List<Employee> ManagedEmployees { get; set; }
        }

        public void ShouldSerializeNullValues()
        {
            Assert.AreEqual("null", Json.Serialize((string)null));
            Assert.AreEqual("null", Json.Serialize((int?)null));
            Assert.AreEqual("null", Json.Serialize((double?)null));
            Assert.AreEqual("null", Json.Serialize((float?)null));
            Assert.AreEqual("null", Json.Serialize((decimal?)null));
            Assert.AreEqual("null", Json.Serialize((DateTime?)null));
            Assert.AreEqual("null", Json.Serialize((ConsoleColor?)null));
            Assert.AreEqual("null", Json.Serialize((object)null));
            Assert.AreEqual("null", Json.Serialize((JsonTests)null));
            Assert.AreEqual("null", Json.Serialize((char?)null));
            Assert.AreEqual("null", Json.Serialize((Guid?)null));
            Assert.AreEqual("null", Json.Serialize(((string, string)?)null));
        }

        public void ShouldSerializeDBNull()
        {
            Assert.AreEqual("null", Json.Serialize(DBNull.Value));
        }

        public void ShouldSerializeEmptyString()
        {
            Assert.AreEqual("\"\"", Json.Serialize(string.Empty));
        }

        public void ShouldSerializeString()
        {
            Assert.AreEqual("\"roarr\"", Json.Serialize("roarr"));
        }

        public void ShouldSerializeStringWithSpecialCharacters()
        {
            var obj = "\\ \"" + string.Concat(Enumerable.Range(0, 0x21).Select(x => (char)x));

            string result = Json.Serialize(obj);
            Assert.AreEqual("\"\\\\ \\\"\\u0000\\u0001\\u0002\\u0003\\u0004\\u0005\\u0006\\u0007\\u0008\\u0009\\u000a\\u000b\\u000c\\u000d\\u000e\\u000f\\u0010\\u0011\\u0012\\u0013\\u0014\\u0015\\u0016\\u0017\\u0018\\u0019\\u001a\\u001b\\u001c\\u001d\\u001e\\u001f \"", result);
        }

        public void ShouldSerializeBoolean()
        {
            Assert.AreEqual("true", Json.Serialize(true));
            Assert.AreEqual("false", Json.Serialize(false));
        }

        public void ShouldSerializeDouble()
        {
            Assert.AreEqual("3.14", Json.Serialize(3.14));
            Assert.AreEqual("-1.7976931348623157E+308", Json.Serialize(double.MinValue));
            Assert.AreEqual("1.7976931348623157E+308", Json.Serialize(double.MaxValue));
        }

        public void ShouldSerializeFloat()
        {
            Assert.AreEqual("3.14", Json.Serialize(3.14f));
            Assert.AreEqual("-3.4028235E+38", Json.Serialize(float.MinValue));
            Assert.AreEqual("3.4028235E+38", Json.Serialize(float.MaxValue));
        }

        public void ShouldSerializeDecimal()
        {
            Assert.AreEqual("3.14", Json.Serialize(3.14m));
        }

        public void ShouldSerializeInfinity()
        {
            Assert.AreEqual("\"Infinity\"", Json.Serialize(double.PositiveInfinity));
            Assert.AreEqual("\"-Infinity\"", Json.Serialize(double.NegativeInfinity));
            Assert.AreEqual("\"Infinity\"", Json.Serialize(float.PositiveInfinity));
            Assert.AreEqual("\"-Infinity\"", Json.Serialize(float.NegativeInfinity));
        }

        public void ShouldSerializeNaN()
        {
            Assert.AreEqual("\"NaN\"", Json.Serialize(double.NaN));
        }

        public void ShouldSerializeUniversalDateTime()
        {
            Assert.AreEqual("\"2021-11-25T10:56:37.6780000Z\"", Json.Serialize(DateTime.Parse("2021-11-25 14:56:37.678 +04:00").ToUniversalTime()));
        }

        public void ShouldSerializeTimeSpan()
        {
            Assert.AreEqual("\"14:56:37.6780000\"", Json.Serialize(TimeSpan.Parse("14:56:37.678")));
        }

        public void ShouldSerializeDateTimeOffset()
        {
            Assert.AreEqual("\"2021-11-25T14:56:37.6780000+04:00\"", Json.Serialize(DateTimeOffset.Parse("2021-11-25 14:56:37.678 +04:00")));
        }

        public void ShouldSerializeGuid()
        {
            Assert.AreEqual("\"01234567-89ab-cdef-0123-456789abcdef\"", Json.Serialize(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef")));
        }

        public void ShouldSerializeEnum()
        {
            Assert.AreEqual("\"Green\"", Json.Serialize(ConsoleColor.Green));
        }

        public void ShouldSerializeEnum_With_Flags()
        {
            Assert.AreEqual("\"Alt, Shift, Control\"", Json.Serialize(ConsoleModifiers.Control | ConsoleModifiers.Alt | ConsoleModifiers.Shift));
        }

        public void ShouldSerializeTuple()
        {
            Assert.AreEqual("[1, \"Bar\", \"Green\"]", Json.Serialize((Roarr: 1, Foo: "Bar", Color: ConsoleColor.Green)));
        }

        public void ShouldSerializeDictionary()
        {
            Assert.AreEqual("{ \"stringKey\":\"stringVal\", \"42\":42, \"Green\":\"Green\" }", Json.Serialize(new Dictionary<object, object>() { { "stringKey", "stringVal" }, { 42, 42 }, { ConsoleColor.Green, ConsoleColor.Green } }));
            Assert.AreEqual("{ \"a\":\"a\", \"b\":\"b\" }", Json.Serialize(new Dictionary<string, string>() { { "a", "a" }, { "b", "b" } }));
            Assert.AreEqual("{ \"1\":1, \"2\":2 }", Json.Serialize(new Dictionary<int, int>() { { 1, 1 }, { 2, 2 } }));
        }

        public void ShouldSerializeUri()
        {
            Assert.AreEqual("\"http://tempuri.org:12345/roarr?foo=bar\"", Json.Serialize(new Uri("http://tempuri.org:12345/roarr?foo=bar")));
        }

        public void ShouldSerializeEnumerable()
        {
            Assert.AreEqual("[-2, -1, 0, 1, 2]", Json.Serialize(Enumerable.Range(-2, 5)));
            Assert.AreEqual("[-2, -1, 0, 1, 2]", Json.Serialize(new List<int>(Enumerable.Range(-2, 5))));
            Assert.AreEqual("[-2, -1, 0, 1, 2]", Json.Serialize(new Queue<int>(Enumerable.Range(-2, 5))));
            Assert.AreEqual("[2, 1, 0, -1, -2]", Json.Serialize(new Stack<int>(Enumerable.Range(-2, 5))));
            Assert.AreEqual("[\"1\", 2, 3.3, \"Green\"]", Json.Serialize(new List<object>() { "1", 2, 3.3, ConsoleColor.Green }));
        }

        public void ShouldSerializeArray()
        {
            var obj = new object[]
            {
                "roarr",
                42,
                true,
                new int[] { 1, 2, 3 },
            };

            string result = Json.Serialize(obj);
            Assert.AreEqual("[\"roarr\", 42, true, [1, 2, 3]]", result);
        }

        public void ShouldSerializeCharArray()
        {
            Assert.AreEqual("[\"r\", \"o\", \"a\", \"r\", \"r\"]", Json.Serialize("roarr".ToCharArray()));
        }

        public void ShouldSerializeOnlyPublicProperties()
        {
            Assert.AreEqual("{ \"ShouldSerializeThisReadOnlyProperty\":null, \"ShouldSerializeThisReadWriteProperty\":null }", Json.Serialize(new PropertyTest()));
        }

        public void ShouldSerializeEmptyObject()
        {
            // NOTE: 2 spaces between braces is valid, so it is accepted
            Assert.AreEqual("{  }", Json.Serialize(new Empty()));
        }

        public void ShouldSerializeAnonymousObject()
        {
            var obj = new
            {
                stringProperty = "roarr",
                intProperty = 42,
                nullProperty = (object)null,
                booleanProperty = true,
                intArrayProperty = new int[] { 1, 2, 3 },
            };

            string result = Json.Serialize(obj);
            Assert.AreEqual("{ \"booleanProperty\":true, \"intArrayProperty\":[1, 2, 3], \"intProperty\":42, \"nullProperty\":null, \"stringProperty\":\"roarr\" }", result);
        }

        public void ShouldSerializeObjectTree()
        {
            var obj = new Employee()
            {
                Name = "Rob Director",
                ManagedEmployees = new List<Employee>()
                {
                    new Employee()
                    {
                        Name = "Felix CTO",
                        ManagedEmployees = new List<Employee>()
                        {
                            new Employee() { Name = "Gyorgy Engineer", },
                            new Employee() { Name = "Teal'c Jaffa", },
                        }
                    },
                    new Employee()
                    {
                        Name = "Brünnhilda CFO",
                        ManagedEmployees = new List<Employee>(),
                    },
                },
            };

            string result = Json.Serialize(obj);
            Assert.AreEqual("{ \"ManagedEmployees\":[{ \"ManagedEmployees\":[{ \"ManagedEmployees\":null, \"Name\":\"Gyorgy Engineer\" }, { \"ManagedEmployees\":null, \"Name\":\"Teal'c Jaffa\" }], \"Name\":\"Felix CTO\" }, { \"ManagedEmployees\":[], \"Name\":\"Brünnhilda CFO\" }], \"Name\":\"Rob Director\" }", result);
        }

        public static void Go()
        {
            new PUnit().Test<JsonTests>().RunToConsole();
        }
    }
}

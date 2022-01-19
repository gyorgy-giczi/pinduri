using System;

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

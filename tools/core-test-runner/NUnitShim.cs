// NUnit 없이 Assets/Tests/EditMode 테스트를 그대로 컴파일하기 위한 최소 대체 구현.
// tools/run-core-tests.sh 전용 — Unity 프로젝트에는 포함되지 않는다 (Assets 밖).
using System;
using System.Collections;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public readonly object[] Arguments;
        public TestCaseAttribute(params object[] args) { Arguments = args; }
    }

    public delegate void TestDelegate();

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        private static string M(string message, string detail)
        {
            return string.IsNullOrEmpty(message) ? detail : message + " — " + detail;
        }

        private static bool NumEq(object a, object b)
        {
            if (a == null || b == null) return a == b;
            if (IsNumeric(a) && IsNumeric(b))
            {
                if (a is double || a is float || b is double || b is float) return Convert.ToDouble(a) == Convert.ToDouble(b);
                if (a is ulong || b is ulong) return Convert.ToUInt64(a) == Convert.ToUInt64(b);
                return Convert.ToInt64(a) == Convert.ToInt64(b);
            }
            if (a is IEnumerable && b is IEnumerable && !(a is string))
            {
                IEnumerator ea = ((IEnumerable)a).GetEnumerator(), eb = ((IEnumerable)b).GetEnumerator();
                while (true)
                {
                    bool ma = ea.MoveNext(), mb = eb.MoveNext();
                    if (ma != mb) return false;
                    if (!ma) return true;
                    if (!NumEq(ea.Current, eb.Current)) return false;
                }
            }
            return a.Equals(b);
        }

        private static bool IsNumeric(object o)
        {
            return o is int || o is long || o is short || o is byte || o is sbyte || o is uint || o is ulong || o is ushort || o is double || o is float;
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!NumEq(expected, actual)) throw new AssertionException(M(message, "expected <" + expected + "> but was <" + actual + ">"));
        }

        public static void AreNotEqual(object notExpected, object actual, string message = null)
        {
            if (NumEq(notExpected, actual)) throw new AssertionException(M(message, "did not expect <" + actual + ">"));
        }

        public static void IsTrue(bool condition, string message = null) { if (!condition) throw new AssertionException(M(message, "expected true")); }
        public static void IsFalse(bool condition, string message = null) { if (condition) throw new AssertionException(M(message, "expected false")); }
        public static void IsNull(object o, string message = null) { if (o != null) throw new AssertionException(M(message, "expected null but was <" + o + ">")); }
        public static void IsNotNull(object o, string message = null) { if (o == null) throw new AssertionException(M(message, "expected not null")); }
        public static void Fail(string message = null) { throw new AssertionException(message ?? "Fail"); }

        public static void Greater(long a, long b, string message = null) { if (!(a > b)) throw new AssertionException(M(message, a + " > " + b + " expected")); }
        public static void GreaterOrEqual(long a, long b, string message = null) { if (!(a >= b)) throw new AssertionException(M(message, a + " >= " + b + " expected")); }
        public static void Less(long a, long b, string message = null) { if (!(a < b)) throw new AssertionException(M(message, a + " < " + b + " expected")); }
        public static void LessOrEqual(long a, long b, string message = null) { if (!(a <= b)) throw new AssertionException(M(message, a + " <= " + b + " expected")); }

        public static T Throws<T>(TestDelegate code, string message = null) where T : Exception
        {
            try { code(); }
            catch (T e) { return e; }
            catch (Exception e) { throw new AssertionException(M(message, "expected " + typeof(T).Name + " but got " + e.GetType().Name + ": " + e.Message)); }
            throw new AssertionException(M(message, "expected " + typeof(T).Name + " but nothing was thrown"));
        }
    }
}

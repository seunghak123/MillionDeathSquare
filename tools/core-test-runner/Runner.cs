// 콘솔 테스트 러너: 어셈블리 안의 [Test] 메서드를 리플렉션으로 찾아 실행한다.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class CoreTestRunner
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        string filter = args.Length > 0 ? args[0] : null;
        int passed = 0, failed = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var types = typeof(CoreTestRunner).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0 || m.GetCustomAttributes(typeof(TestCaseAttribute), false).Length > 0))
            .OrderBy(t => t.FullName);
        foreach (Type type in types)
        {
            MethodInfo setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes(typeof(SetUpAttribute), false).Length > 0);
            foreach (MethodInfo method in type.GetMethods().OrderBy(m => m.MetadataToken))
            {
                bool isTest = method.GetCustomAttributes(typeof(TestAttribute), false).Length > 0;
                var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), false).Cast<TestCaseAttribute>().ToArray();
                if (!isTest && cases.Length == 0) continue;
                object[][] argSets = cases.Length > 0 ? cases.Select(c => c.Arguments).ToArray() : new[] { new object[0] };
                foreach (object[] argSet in argSets)
                {
                    string name = type.Name + "." + method.Name + (argSet.Length > 0 ? "(" + string.Join(",", argSet.Select(a => Convert.ToString(a))) + ")" : "");
                    if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try
                    {
                        object instance = Activator.CreateInstance(type);
                        if (setUp != null) setUp.Invoke(instance, null);
                        method.Invoke(instance, argSet);
                        passed++;
                        Console.WriteLine("  PASS " + name);
                    }
                    catch (TargetInvocationException e)
                    {
                        failed++;
                        Exception inner = e.InnerException ?? e;
                        Console.WriteLine("  FAIL " + name + "\n       " + inner.GetType().Name + ": " + inner.Message);
                        string trace = inner.StackTrace ?? "";
                        foreach (string line in trace.Split('\n').Take(4)) Console.WriteLine("       " + line.Trim());
                    }
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("Core tests: " + passed + " passed, " + failed + " failed (" + watch.ElapsedMilliseconds + " ms)");
        return failed == 0 ? 0 : 1;
    }
}

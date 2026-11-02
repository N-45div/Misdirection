// A tiny test runner so the engine-free game logic (Assets/Misdirection/Scripts/Core) can be
// tested under Mono without Unity or NUnit. Run via tools/tests/test_csharp_core.py.
using System;
using System.Linq;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

public static class Assert
{
    public static void True(bool condition, string message = "expected true")
    {
        if (!condition) throw new Exception(message);
    }

    public static void False(bool condition, string message = "expected false")
    {
        if (condition) throw new Exception(message);
    }

    public static void Equal<T>(T expected, T actual, string what = "value")
    {
        if (!Equals(expected, actual)) throw new Exception(what + ": expected " + expected + ", got " + actual);
    }

    public static void Near(double expected, double actual, double tolerance = 1e-4, string what = "value")
    {
        if (Math.Abs(expected - actual) > tolerance) throw new Exception(what + ": expected " + expected + " ± " + tolerance + ", got " + actual);
    }
}

public static class MiniTestRunner
{
    public static int Main(string[] args)
    {
        var tests = Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0)
            .OrderBy(m => m.DeclaringType.Name + "." + m.Name)
            .ToList();
        int failed = 0;
        foreach (MethodInfo test in tests)
        {
            string name = test.DeclaringType.Name + "." + test.Name;
            try
            {
                test.Invoke(null, null);
                Console.WriteLine("PASS " + name);
            }
            catch (TargetInvocationException e)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + e.InnerException.Message);
            }
        }
        Console.WriteLine("RESULT " + (tests.Count - failed) + "/" + tests.Count + " passed");
        return failed == 0 ? 0 : 1;
    }
}

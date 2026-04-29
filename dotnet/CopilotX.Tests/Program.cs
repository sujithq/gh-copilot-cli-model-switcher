using System.Reflection;
using Xunit;

namespace CopilotX.Tests;

public static class Program
{
    public static async Task<int> Main()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var factAttribute = typeof(FactAttribute);

        var testMethods = assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttributes(factAttribute, inherit: true).Any())
                .Select(method => (type, method)))
            .ToList();

        var passed = 0;
        var failed = 0;

        foreach (var (type, method) in testMethods)
        {
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(type);
                var result = method.Invoke(instance, null);

                if (result is Task task)
                {
                    await task;
                }

                Console.WriteLine($"PASS {type.Name}.{method.Name}");
                passed++;
            }
            catch (Exception ex)
            {
                var actual = ex is TargetInvocationException tie && tie.InnerException != null
                    ? tie.InnerException
                    : ex;

                Console.WriteLine($"FAIL {type.Name}.{method.Name}: {actual.Message}");
                failed++;
            }
            finally
            {
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        Console.WriteLine($"Total: {testMethods.Count}, Passed: {passed}, Failed: {failed}");
        return failed == 0 ? 0 : 1;
    }
}

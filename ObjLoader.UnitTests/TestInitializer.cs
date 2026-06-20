using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ObjLoader.UnitTests;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var assemblyName = new AssemblyName(args.Name).Name;
            if (assemblyName == null) return null;

            foreach (var searchPath in GetYmm4SearchPaths())
            {
                var path = Path.Combine(searchPath, assemblyName + ".dll");
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }
            return null;
        };
    }

    private static IEnumerable<string> GetYmm4SearchPaths()
    {
        var envPath = Environment.GetEnvironmentVariable("YMM4DirPath");
        if (!string.IsNullOrEmpty(envPath))
            yield return envPath;

        yield return @"C:\Program Files\YukkuriMovieMaker_v4_Lite_Dev";
        yield return @"C:\Program Files\YukkuriMovieMaker4";
        yield return @"C:\Program Files (x86)\YukkuriMovieMaker4";
    }
}

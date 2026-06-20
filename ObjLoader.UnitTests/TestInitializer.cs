using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ObjLoader.UnitTests
{
    public static class TestInitializer
    {
        [ModuleInitializer]
        public static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var assemblyName = new AssemblyName(args.Name).Name;
                if (assemblyName == null) return null;

                var ymm4Path = @"C:\Program Files\YukkuriMovieMaker_v4_Lite_Dev";
                var path = Path.Combine(ymm4Path, assemblyName + ".dll");
                
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
                return null;
            };
        }
    }
}

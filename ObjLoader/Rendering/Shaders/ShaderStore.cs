using System.IO;
using System.Reflection;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace ObjLoader.Rendering.Shaders
{
    internal static class ShaderStore
    {
        private static byte[]? _cachedVertexShaderByteCode;
        private static byte[]? _cachedPixelShaderByteCode;
        private static byte[]? _cachedGridPixelShaderByteCode;
        private static byte[]? _cachedGridVertexShaderByteCode;
        private static readonly object _lock = new object();

        public static (Blob? Blob, string? Error) Compile(string source, string entryPoint, string profile)
        {
            try
            {
                var blob = Compiler.Compile(source, entryPoint, "CustomShader", profile, ShaderFlags.OptimizationLevel3, EffectFlags.None);
                return (blob, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public static (byte[] VS, byte[] PS, byte[] GridVS, byte[] GridPS) GetByteCodes()
        {
            lock (_lock)
            {
                if (_cachedVertexShaderByteCode == null)
                {
                    _cachedVertexShaderByteCode = LoadShaderResource("ObjLoaderVS.cso");
                }

                if (_cachedPixelShaderByteCode == null)
                {
                    _cachedPixelShaderByteCode = LoadShaderResource("ObjLoaderPS.cso");
                }

                if (_cachedGridVertexShaderByteCode == null)
                {
                    _cachedGridVertexShaderByteCode = LoadShaderResource("GridVS.cso");
                }

                if (_cachedGridPixelShaderByteCode == null)
                {
                    _cachedGridPixelShaderByteCode = LoadShaderResource("GridPS.cso");
                }

                return (_cachedVertexShaderByteCode!, _cachedPixelShaderByteCode!, _cachedGridVertexShaderByteCode!, _cachedGridPixelShaderByteCode!);
            }
        }

        private static byte[] LoadShaderResource(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(resourceName))
            {
                throw new FileNotFoundException($"Embedded shader resource not found: {fileName}");
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new IOException($"Failed to load shader resource stream: {resourceName}");
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
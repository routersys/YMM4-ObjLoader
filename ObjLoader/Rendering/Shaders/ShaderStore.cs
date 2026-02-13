using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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

        private static readonly ConcurrentDictionary<string, (byte[]? ByteCode, string? Error)> _compilationCache = new();

        public static (byte[]? ByteCode, string? Error) Compile(string source, string entryPoint, string profile)
        {
            var cacheKey = ComputeCacheKey(source, entryPoint, profile);

            if (_compilationCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            try
            {
                using var blob = Compiler.Compile(source, entryPoint, "CustomShader", profile, ShaderFlags.OptimizationLevel3, EffectFlags.None);
                var byteCode = blob.AsBytes().ToArray();
                _compilationCache.TryAdd(cacheKey, (byteCode, null));
                return (byteCode, null);
            }
            catch (Exception ex)
            {
                _compilationCache.TryAdd(cacheKey, (null, ex.Message));
                return (null, ex.Message);
            }
        }

        public static void ClearCache()
        {
            _compilationCache.Clear();
        }

        private static string ComputeCacheKey(string source, string entryPoint, string profile)
        {
            var combined = source + "\0" + entryPoint + "\0" + profile;
            var bytes = Encoding.UTF8.GetBytes(combined);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
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
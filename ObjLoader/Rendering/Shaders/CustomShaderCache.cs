using ObjLoader.Rendering.Shaders.Interfaces;
using System.IO;

namespace ObjLoader.Rendering.Shaders
{
    internal sealed class CustomShaderCache : IShaderCache, IDisposable
    {
        private readonly record struct CacheEntry(CompiledShaderSet? Shaders, DateTime LastWriteTime);

        private readonly IShaderLoader _loader;
        private readonly IShaderCompiler _compiler;
        private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

        internal CustomShaderCache(IShaderLoader loader, IShaderCompiler compiler)
        {
            _loader = loader;
            _compiler = compiler;
        }

        public CompiledShaderSet? Resolve(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var lastWriteTime = File.GetLastWriteTimeUtc(path);

            if (_cache.TryGetValue(path, out var entry) && entry.LastWriteTime == lastWriteTime)
                return entry.Shaders;

            Evict(path);

            var source = _loader.Load(path);
            var compiled = source is not null ? _compiler.Compile(source) : null;

            _cache[path] = new CacheEntry(compiled, lastWriteTime);
            return compiled;
        }

        private void Evict(string path)
        {
            if (_cache.Remove(path, out var evicted))
                evicted.Shaders?.Dispose();
        }

        public void Dispose()
        {
            foreach (var entry in _cache.Values)
                entry.Shaders?.Dispose();
            _cache.Clear();
        }
    }
}
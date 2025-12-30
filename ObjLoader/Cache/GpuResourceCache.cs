using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ObjLoader.Cache
{
    internal static class GpuResourceCache
    {
        private static readonly ConcurrentDictionary<string, GpuResourceCacheItem> _cache = new();

        public static bool TryGetValue(string key, [NotNullWhen(true)] out GpuResourceCacheItem? item)
        {
            if (_cache.TryGetValue(key, out var val))
            {
                item = val;
                return true;
            }
            item = null;
            return false;
        }

        public static void AddOrUpdate(string key, GpuResourceCacheItem item)
        {
            _cache.AddOrUpdate(key, item, (k, old) => {
                old.Dispose();
                return item;
            });
        }

        public static void Clear()
        {
            foreach (var item in _cache.Values)
            {
                item.Dispose();
            }
            _cache.Clear();
        }
    }
}
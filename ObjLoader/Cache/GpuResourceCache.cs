using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Vortice.Direct3D11;

namespace ObjLoader.Cache
{
    internal static class GpuResourceCache
    {
        private static readonly ConcurrentDictionary<string, GpuResourceCacheItem> _cache = new();
        private static readonly object _cleanupLock = new();

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
            _cache.AddOrUpdate(key, item, (k, oldValue) =>
            {
                if (oldValue.Device != item.Device)
                {
                    try { oldValue.Dispose(); } catch { }
                }
                return item;
            });
        }

        public static void Clear()
        {
            lock (_cleanupLock)
            {
                foreach (var kvp in _cache)
                {
                    try
                    {
                        kvp.Value?.Dispose();
                    }
                    catch { }
                }
                _cache.Clear();
            }
        }

        public static void ClearForDevice(ID3D11Device device)
        {
            lock (_cleanupLock)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in _cache)
                {
                    if (kvp.Value?.Device == device)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    if (_cache.TryRemove(key, out var item))
                    {
                        try { item?.Dispose(); } catch { }
                    }
                }
            }
        }

        public static void CleanupInvalidResources()
        {
            lock (_cleanupLock)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in _cache)
                {
                    var item = kvp.Value;
                    if (item == null || item.Device == null)
                    {
                        keysToRemove.Add(kvp.Key);
                        continue;
                    }

                    try
                    {
                        var reason = item.Device.DeviceRemovedReason;
                        if (reason.Failure)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                    catch
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    if (_cache.TryRemove(key, out var item))
                    {
                        try { item?.Dispose(); } catch { }
                    }
                }
            }
        }
    }
}
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Vortice.Direct3D11;

namespace ObjLoader.Cache
{
    internal sealed class GpuResourceCache : IGpuResourceCache, IDisposable
    {
        private static readonly Lazy<GpuResourceCache> _instance = new Lazy<GpuResourceCache>(() => new GpuResourceCache());

        private readonly ConcurrentDictionary<string, GpuResourceCacheItem> _cache = new();
        private readonly object _cleanupLock = new();
        private bool _disposed;

        public static GpuResourceCache Instance => _instance.Value;

        private GpuResourceCache()
        {
        }

        public bool TryGetValue(string key, [NotNullWhen(true)] out GpuResourceCacheItem? item)
        {
            if (_disposed)
            {
                item = null;
                return false;
            }

            if (string.IsNullOrEmpty(key))
            {
                item = null;
                return false;
            }

            if (_cache.TryGetValue(key, out var val))
            {
                item = val;
                return true;
            }

            item = null;
            return false;
        }

        public void AddOrUpdate(string key, GpuResourceCacheItem item)
        {
            if (_disposed) return;
            if (string.IsNullOrEmpty(key)) return;
            if (item == null) return;

            _cache.AddOrUpdate(key, item, (k, oldValue) =>
            {
                if (!ReferenceEquals(oldValue, item))
                {
                    SafeDispose(oldValue);
                }
                return item;
            });
        }

        public void Remove(string key)
        {
            if (_disposed) return;
            if (string.IsNullOrEmpty(key)) return;

            if (_cache.TryRemove(key, out var item))
            {
                SafeDispose(item);
            }
        }

        public void Clear()
        {
            lock (_cleanupLock)
            {
                var keys = _cache.Keys.ToList();
                foreach (var key in keys)
                {
                    if (_cache.TryRemove(key, out var item))
                    {
                        SafeDispose(item);
                    }
                }
            }
        }

        public void ClearForDevice(ID3D11Device device)
        {
            if (device == null) return;

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
                        SafeDispose(item);
                    }
                }
            }
        }

        public void CleanupInvalidResources()
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
                        SafeDispose(item);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }
    }
}
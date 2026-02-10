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
        private int _disposed;

        public static GpuResourceCache Instance => _instance.Value;

        private GpuResourceCache()
        {
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public bool TryGetValue(string key, [NotNullWhen(true)] out GpuResourceCacheItem? item)
        {
            if (IsDisposed || string.IsNullOrEmpty(key))
            {
                item = null;
                return false;
            }

            if (_cache.TryGetValue(key, out var val) && val != null)
            {
                item = val;
                return true;
            }

            item = null;
            return false;
        }

        public void AddOrUpdate(string key, GpuResourceCacheItem item)
        {
            if (item == null) return;
            if (string.IsNullOrEmpty(key)) return;

            if (IsDisposed)
            {
                SafeDispose(item);
                return;
            }

            _cache.AddOrUpdate(key, item, (_, oldValue) =>
            {
                if (!ReferenceEquals(oldValue, item))
                {
                    SafeDispose(oldValue);
                }
                return item;
            });

            if (IsDisposed && _cache.TryRemove(key, out var removed))
            {
                SafeDispose(removed);
            }
        }

        public void Remove(string key)
        {
            if (IsDisposed) return;
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
                var snapshot = _cache.ToArray();
                foreach (var kvp in snapshot)
                {
                    if (_cache.TryRemove(kvp.Key, out var item))
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
                var snapshot = _cache.ToArray();
                foreach (var kvp in snapshot)
                {
                    if (kvp.Value?.Device == device)
                    {
                        if (_cache.TryRemove(kvp.Key, out var item))
                        {
                            SafeDispose(item);
                        }
                    }
                }
            }
        }

        public void CleanupInvalidResources()
        {
            lock (_cleanupLock)
            {
                var snapshot = _cache.ToArray();
                foreach (var kvp in snapshot)
                {
                    bool shouldRemove = false;
                    var item = kvp.Value;

                    if (item == null || item.Device == null)
                    {
                        shouldRemove = true;
                    }
                    else
                    {
                        try
                        {
                            var reason = item.Device.DeviceRemovedReason;
                            if (reason.Failure)
                            {
                                shouldRemove = true;
                            }
                        }
                        catch
                        {
                            shouldRemove = true;
                        }
                    }

                    if (shouldRemove && _cache.TryRemove(kvp.Key, out var removed))
                    {
                        SafeDispose(removed);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
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
using ObjLoader.Infrastructure;
using ObjLoader.Settings;
using ObjLoader.Utilities.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Vortice.Direct3D11;

namespace ObjLoader.Cache.Gpu
{
    internal sealed class GpuResourceCache : IGpuResourceCache, IDisposable
    {
        private static readonly Lazy<GpuResourceCache> _instance = new Lazy<GpuResourceCache>(() => new GpuResourceCache());

        private readonly ConcurrentDictionary<string, GpuResourceCacheItem> _cache = new();
        private readonly ConcurrentDictionary<string, Timer> _ttlTimers = new();
        private readonly Lock _cleanupLock = new();
        private int _disposed;

        public static GpuResourceCache Instance => _instance.Value;

        private GpuResourceCache()
        {
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public int Count => _cache.Count;

        public long TotalEstimatedBytes
        {
            get
            {
                long total = 0;
                foreach (var kvp in _cache)
                {
                    total += kvp.Value?.EstimatedGpuBytes ?? 0;
                }
                return total;
            }
        }

        public List<GpuCacheSnapshot> GetSnapshot()
        {
            var result = new List<GpuCacheSnapshot>();
            foreach (var kvp in _cache)
            {
                var item = kvp.Value;
                if (item != null)
                {
                    result.Add(new GpuCacheSnapshot
                    {
                        Key = kvp.Key,
                        EstimatedGpuMB = item.EstimatedGpuBytes / (1024.0 * 1024.0),
                        PartCount = item.Parts?.Length ?? 0
                    });
                }
            }
            return result;
        }

        public bool TryGetValue(string key, [NotNullWhen(true)] out GpuResourceCacheItem? item)
        {
            if (IsDisposed || string.IsNullOrEmpty(key))
            {
                item = null;
                return false;
            }

            if (_cache.TryGetValue(key, out var val) && val != null)
            {
                CancelTtl(key);
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

            CancelTtl(key);

            GpuResourceCacheItem? itemToDispose = null;

            _cache.AddOrUpdate(key, item, (_, oldValue) =>
            {
                if (!ReferenceEquals(oldValue, item))
                {
                    ResourceTracker.Instance.Unregister(key);
                    itemToDispose = oldValue;
                }
                return item;
            });

            if (itemToDispose != null)
            {
                SafeDispose(itemToDispose);
            }

            ResourceTracker.Instance.Register(key, "GpuResourceCacheItem", item, item.EstimatedGpuBytes);

            if (IsDisposed && _cache.TryRemove(key, out var removed))
            {
                CancelTtl(key);
                ResourceTracker.Instance.Unregister(key);
                SafeDispose(removed);
            }
        }

        public void Remove(string key)
        {
            if (IsDisposed) return;
            if (string.IsNullOrEmpty(key)) return;

            CancelTtl(key);

            if (_cache.TryRemove(key, out var item))
            {
                ResourceTracker.Instance.Unregister(key);
                SafeDispose(item);
            }
        }

        public void ScheduleRelease(string key)
        {
            if (IsDisposed || string.IsNullOrEmpty(key)) return;
            if (!_cache.ContainsKey(key)) return;

            CancelTtl(key);

            var delay = TimeSpan.FromSeconds(Math.Max(0.0, ModelSettings.Instance.D3DResourceReleaseDelay));

            Timer? timer = null;
            timer = new Timer(_ =>
            {
                lock (_cleanupLock)
                {
                    if (_ttlTimers.TryRemove(key, out Timer? removedTimer))
                    {
                        removedTimer?.Dispose();
                    }

                    if (!IsDisposed && _cache.TryRemove(key, out var item))
                    {
                        ResourceTracker.Instance.Unregister(key);
                        SafeDispose(item);
                    }
                }
            }, null, delay, Timeout.InfiniteTimeSpan);
            _ttlTimers[key] = timer;
        }

        private void CancelTtl(string key)
        {
            if (_ttlTimers.TryRemove(key, out var existing))
            {
                existing.Dispose();
            }
        }

        public void Clear()
        {
            lock (_cleanupLock)
            {
                foreach (var kvp in _ttlTimers)
                {
                    kvp.Value.Dispose();
                }
                _ttlTimers.Clear();

                var snapshot = _cache.ToArray();
                foreach (var kvp in snapshot)
                {
                    if (_cache.TryRemove(kvp.Key, out var item))
                    {
                        ResourceTracker.Instance.Unregister(kvp.Key);
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
                        CancelTtl(kvp.Key);
                        if (_cache.TryRemove(kvp.Key, out var item))
                        {
                            ResourceTracker.Instance.Unregister(kvp.Key);
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
                        catch (Exception ex)
                        {
                            Logger<GpuResourceCache>.Instance.Error("Failed to check device removed reason", ex);
                            shouldRemove = true;
                        }
                    }

                    if (shouldRemove)
                    {
                        CancelTtl(kvp.Key);
                        if (_cache.TryRemove(kvp.Key, out var removed))
                        {
                            ResourceTracker.Instance.Unregister(kvp.Key);
                            SafeDispose(removed);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Clear();
            ResourceTracker.Instance.UnregisterAll();
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Logger<GpuResourceCache>.Instance.Error("Dispose failed", ex);
            }
        }
    }
}
using ObjLoader.Settings;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Core.Resources
{
    internal sealed class D3DResourcesPool
    {
        private static readonly Dictionary<nint, PoolEntry> _pool = new();
        private static readonly object _globalLock = new();

        private sealed class PoolEntry
        {
            public readonly D3DResources Resources;
            public int RefCount;
            public Timer? ReleaseTimer;
            public int Generation;
            public bool IsDisposed;

            public PoolEntry(D3DResources resources)
            {
                Resources = resources;
            }
        }

        public static D3DResources Acquire(ID3D11Device device)
        {
            var key = device.NativePointer;

            lock (_globalLock)
            {
                if (!_pool.TryGetValue(key, out var entry) || entry.IsDisposed)
                {
                    if (entry != null && entry.IsDisposed)
                    {
                        _pool.Remove(key);
                    }
                    entry = new PoolEntry(new D3DResources(device));
                    _pool[key] = entry;
                }

                entry.RefCount++;
                entry.Generation++;
                entry.ReleaseTimer?.Dispose();
                entry.ReleaseTimer = null;

                return entry.Resources;
            }
        }

        public static void Release(ID3D11Device device)
        {
            var key = device.NativePointer;

            lock (_globalLock)
            {
                if (!_pool.TryGetValue(key, out var entry)) return;

                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    int gen = ++entry.Generation;
                    entry.ReleaseTimer?.Dispose();
                    var delay = TimeSpan.FromSeconds(ModelSettings.Instance.D3DResourceReleaseDelay);
                    entry.ReleaseTimer = new Timer(_ =>
                    {
                        lock (_globalLock)
                        {
                            if (entry.RefCount <= 0 && entry.Generation == gen && !entry.IsDisposed)
                            {
                                entry.IsDisposed = true;
                                _pool.Remove(key);
                                entry.Resources.Dispose();
                                entry.ReleaseTimer?.Dispose();
                                entry.ReleaseTimer = null;
                            }
                        }
                    }, null, delay, Timeout.InfiniteTimeSpan);
                }
            }
        }

        public static void ClearAll()
        {
            lock (_globalLock)
            {
                foreach (var kvp in _pool)
                {
                    kvp.Value.ReleaseTimer?.Dispose();
                    kvp.Value.ReleaseTimer = null;
                    if (!kvp.Value.IsDisposed)
                    {
                        kvp.Value.IsDisposed = true;
                        kvp.Value.Resources.Dispose();
                    }
                }
                _pool.Clear();
            }
        }
    }
}
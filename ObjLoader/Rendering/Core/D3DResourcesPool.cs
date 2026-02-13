using ObjLoader.Settings;
using System.Collections.Concurrent;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Core
{
    internal sealed class D3DResourcesPool
    {
        private static readonly ConcurrentDictionary<nint, PoolEntry> _pool = new();

        private sealed class PoolEntry
        {
            public readonly D3DResources Resources;
            public int RefCount;
            public Timer? ReleaseTimer;
            public int Generation;
            public bool IsDisposed;
            public readonly object Lock = new();

            public PoolEntry(D3DResources resources)
            {
                Resources = resources;
            }
        }

        public static D3DResources Acquire(ID3D11Device device)
        {
            var key = device.NativePointer;

            while (true)
            {
                var entry = _pool.GetOrAdd(key, _ => new PoolEntry(new D3DResources(device)));

                lock (entry.Lock)
                {
                    if (entry.IsDisposed)
                    {
                        _pool.TryRemove(key, out PoolEntry? _removed);
                        continue;
                    }

                    entry.RefCount++;
                    entry.Generation++;
                    entry.ReleaseTimer?.Dispose();
                    entry.ReleaseTimer = null;
                }

                return entry.Resources;
            }
        }

        public static void Release(ID3D11Device device)
        {
            var key = device.NativePointer;
            if (!_pool.TryGetValue(key, out var entry)) return;

            lock (entry.Lock)
            {
                entry.RefCount--;
                if (entry.RefCount <= 0)
                {
                    int gen = ++entry.Generation;
                    entry.ReleaseTimer?.Dispose();
                    var delay = TimeSpan.FromSeconds(ModelSettings.Instance.D3DResourceReleaseDelay);
                    entry.ReleaseTimer = new Timer(_ =>
                    {
                        lock (entry.Lock)
                        {
                            if (entry.RefCount <= 0 && entry.Generation == gen && !entry.IsDisposed)
                            {
                                entry.IsDisposed = true;
                                _pool.TryRemove(key, out PoolEntry? _removed);
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
            foreach (var kvp in _pool)
            {
                lock (kvp.Value.Lock)
                {
                    kvp.Value.ReleaseTimer?.Dispose();
                    kvp.Value.ReleaseTimer = null;
                    if (!kvp.Value.IsDisposed)
                    {
                        kvp.Value.IsDisposed = true;
                        kvp.Value.Resources.Dispose();
                    }
                }
            }
            _pool.Clear();
        }
    }
}
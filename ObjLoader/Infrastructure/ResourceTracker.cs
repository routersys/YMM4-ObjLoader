using System.Collections.Concurrent;

namespace ObjLoader.Infrastructure
{
    internal sealed class ResourceTracker : IDisposable
    {
        private static readonly Lazy<ResourceTracker> _instance = new Lazy<ResourceTracker>(() => new ResourceTracker());
        private readonly ConcurrentDictionary<string, ResourceAllocation> _allocations = new();
        private readonly ConcurrentDictionary<string, List<ResourceAllocation>> _disposedHistory = new();
        private readonly object _statsLock = new();
        private long _totalAllocations;
        private long _totalDisposals;
        private long _totalEstimatedBytes;
        private int _disposed;

        public static ResourceTracker Instance => _instance.Value;

        private ResourceTracker()
        {
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public void Register(string key, string resourceType, IDisposable resource, long estimatedSizeBytes = 0)
        {
            if (IsDisposed) return;
            if (string.IsNullOrEmpty(key)) return;
            if (resource == null) return;

            var allocation = new ResourceAllocation(key, resourceType, resource, estimatedSizeBytes);

            _allocations.AddOrUpdate(key, allocation, (_, oldAlloc) =>
            {
                if (!oldAlloc.IsDisposed)
                {
                    oldAlloc.MarkDisposed();
                    RecordDisposal(key, oldAlloc);
                }
                return allocation;
            });

            lock (_statsLock)
            {
                _totalAllocations++;
                _totalEstimatedBytes += estimatedSizeBytes;
            }
        }

        public void Unregister(string key)
        {
            if (IsDisposed) return;
            if (string.IsNullOrEmpty(key)) return;

            if (_allocations.TryRemove(key, out var allocation))
            {
                allocation.MarkDisposed();
                RecordDisposal(key, allocation);

                lock (_statsLock)
                {
                    _totalDisposals++;
                    _totalEstimatedBytes -= allocation.EstimatedSizeBytes;
                    if (_totalEstimatedBytes < 0) _totalEstimatedBytes = 0;
                }
            }
        }

        public void UnregisterAll()
        {
            if (IsDisposed) return;

            var snapshot = _allocations.ToArray();
            int removedCount = 0;

            foreach (var kvp in snapshot)
            {
                if (_allocations.TryRemove(kvp.Key, out var allocation))
                {
                    allocation.MarkDisposed();
                    RecordDisposal(kvp.Key, allocation);
                    removedCount++;
                }
            }

            lock (_statsLock)
            {
                _totalDisposals += removedCount;
                _totalEstimatedBytes = 0;
            }
        }

        public List<ResourceAllocation> GetLeakedResources(TimeSpan maxAge)
        {
            var leaked = new List<ResourceAllocation>();
            if (IsDisposed) return leaked;

            foreach (var kvp in _allocations)
            {
                try
                {
                    var alloc = kvp.Value;
                    if (alloc != null && !alloc.IsDisposed && alloc.Age > maxAge && alloc.IsAlive())
                    {
                        leaked.Add(alloc);
                    }
                }
                catch
                {
                }
            }

            return leaked;
        }

        public List<ResourceAllocation> GetOrphanedResources()
        {
            var orphaned = new List<ResourceAllocation>();
            if (IsDisposed) return orphaned;

            var keysToRemove = new List<string>();

            foreach (var kvp in _allocations)
            {
                try
                {
                    var alloc = kvp.Value;
                    if (alloc != null && !alloc.IsDisposed && !alloc.IsAlive())
                    {
                        orphaned.Add(alloc);
                        keysToRemove.Add(kvp.Key);
                    }
                }
                catch
                {
                }
            }

            foreach (var key in keysToRemove)
            {
                _allocations.TryRemove(key, out _);
            }

            return orphaned;
        }

        public ResourceTrackerStats GetStats()
        {
            int activeCount = 0;
            long activeBytes = 0;
            int orphanedCount = 0;

            foreach (var kvp in _allocations)
            {
                try
                {
                    var alloc = kvp.Value;
                    if (alloc != null && !alloc.IsDisposed)
                    {
                        if (alloc.IsAlive())
                        {
                            activeCount++;
                            activeBytes += alloc.EstimatedSizeBytes;
                        }
                        else
                        {
                            orphanedCount++;
                        }
                    }
                }
                catch
                {
                }
            }

            lock (_statsLock)
            {
                return new ResourceTrackerStats
                {
                    ActiveResources = activeCount,
                    OrphanedResources = orphanedCount,
                    TotalAllocations = _totalAllocations,
                    TotalDisposals = _totalDisposals,
                    EstimatedActiveBytes = activeBytes
                };
            }
        }

        public bool IsTracked(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _allocations.ContainsKey(key);
        }

        public ResourceAllocation? GetAllocation(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            _allocations.TryGetValue(key, out var alloc);
            return alloc;
        }

        private void RecordDisposal(string key, ResourceAllocation allocation)
        {
            try
            {
                _disposedHistory.AddOrUpdate(
                    key,
                    _ => new List<ResourceAllocation> { allocation },
                    (_, list) =>
                    {
                        lock (list)
                        {
                            list.Add(allocation);
                            while (list.Count > 10)
                            {
                                list.RemoveAt(0);
                            }
                        }
                        return list;
                    });
            }
            catch
            {
            }
        }

        public void PurgeHistory()
        {
            if (IsDisposed) return;
            _disposedHistory.Clear();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _allocations.Clear();
            _disposedHistory.Clear();
        }
    }

    internal struct ResourceTrackerStats
    {
        public int ActiveResources;
        public int OrphanedResources;
        public long TotalAllocations;
        public long TotalDisposals;
        public long EstimatedActiveBytes;
    }
}
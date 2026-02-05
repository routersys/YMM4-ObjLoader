using System.Diagnostics.CodeAnalysis;

namespace ObjLoader.Cache
{
    internal interface IGpuResourceCache
    {
        bool TryGetValue(string key, [NotNullWhen(true)] out GpuResourceCacheItem? item);
        void AddOrUpdate(string key, GpuResourceCacheItem item);
        void Remove(string key);
        void Clear();
        void ClearForDevice(Vortice.Direct3D11.ID3D11Device device);
        void CleanupInvalidResources();
    }
}
using System.Diagnostics;

namespace ObjLoader.Infrastructure
{
    internal sealed class ResourceAllocation
    {
        public string Key { get; }
        public string ResourceType { get; }
        public DateTime CreatedAt { get; }
        public string StackTrace { get; }
        public long EstimatedSizeBytes { get; }
        public WeakReference<IDisposable> ResourceReference { get; }
        public bool IsDisposed { get; private set; }

        public ResourceAllocation(string key, string resourceType, IDisposable resource, long estimatedSizeBytes)
        {
            Key = key ?? string.Empty;
            ResourceType = resourceType ?? "Unknown";
            CreatedAt = DateTime.UtcNow;
            EstimatedSizeBytes = Math.Max(0, estimatedSizeBytes);
            ResourceReference = new WeakReference<IDisposable>(resource ?? throw new ArgumentNullException(nameof(resource)));
            IsDisposed = false;

            try
            {
                StackTrace = new StackTrace(2, true).ToString();
            }
            catch
            {
                StackTrace = string.Empty;
            }
        }

        public void MarkDisposed()
        {
            IsDisposed = true;
        }

        public bool IsAlive()
        {
            if (IsDisposed) return false;
            try
            {
                return ResourceReference.TryGetTarget(out _);
            }
            catch
            {
                return false;
            }
        }

        public TimeSpan Age => DateTime.UtcNow - CreatedAt;
    }
}
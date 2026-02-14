using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.ViewModels.Splitter;

namespace ObjLoader.Services.Models
{
    internal class ModelLoadResult : IDisposable
    {
        public ObjModel? Model { get; set; }
        public GpuResourceCacheItem? Resource { get; set; }
        public double Scale { get; set; }
        public double Height { get; set; }
        public List<PartItem> Parts { get; set; } = new List<PartItem>();

        public void Dispose()
        {
            Resource?.Dispose();
        }
    }
}

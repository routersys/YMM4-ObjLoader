namespace ObjLoader.Cache
{
    internal sealed class GpuCacheSnapshot
    {
        public string Key { get; set; } = string.Empty;
        public double EstimatedGpuMB { get; set; }
        public int PartCount { get; set; }
    }
}
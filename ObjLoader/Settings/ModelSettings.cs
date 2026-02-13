using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Utilities;
using Vortice.DXGI;
using YukkuriMovieMaker.Plugin;

namespace ObjLoader.Settings
{
    public class ModelSettings : SettingsBase<ModelSettings>
    {
        public override string Name => Texts.Settings_3DModel;
        public override SettingsCategory Category => SettingsCategory.Shape;
        public override bool HasSettingView => true;
        public override object SettingView => new Views.ModelSettingsView { DataContext = new ViewModels.ModelSettingsViewModel(this) };
        public static ModelSettings Instance => Default;

        public const int DefaultMaxFileSizeMB = 500;
        public const int DefaultMaxGpuMemoryPerModelMB = 2048;
        public const int DefaultMaxTotalGpuMemoryMB = 8192;
        public const int DefaultMaxVertices = 10_000_000;
        public const int DefaultMaxIndices = 30_000_000;
        public const int DefaultMaxParts = 10000;
        public const int MinFileSizeMB = 10;
        public const int MaxFileSizeMBLimit = 10240;
        public const int MinGpuMemoryMB = 64;
        public const int MaxGpuMemoryMBLimit = 32768;
        public const int MinVertices = 10_000;
        public const int MaxVerticesLimit = 100_000_000;
        public const int MinIndices = 30_000;
        public const int MaxIndicesLimit = 300_000_000;
        public const int MinParts = 10;
        public const int MaxPartsLimit = 50_000;
        public const double DefaultD3DResourceReleaseDelay = 5.0;

        private double _d3dResourceReleaseDelay = DefaultD3DResourceReleaseDelay;
        public double D3DResourceReleaseDelay
        {
            get => _d3dResourceReleaseDelay;
            set => Set(ref _d3dResourceReleaseDelay, Math.Max(0.0, value));
        }

        private bool _isSandboxEnforced = false;
        public bool IsSandboxEnforced
        {
            get => _isSandboxEnforced;
            set => Set(ref _isSandboxEnforced, value);
        }

        private List<string> _allowedRoots = new List<string>();
        public List<string> AllowedRoots
        {
            get => _allowedRoots;
            set => Set(ref _allowedRoots, value ?? new List<string>());
        }

        private bool _enableAutoAudit = false;
        public bool EnableAutoAudit
        {
            get => _enableAutoAudit;
            set => Set(ref _enableAutoAudit, value);
        }

        private double _auditIntervalMinutes = 5.0;
        public double AuditIntervalMinutes
        {
            get => _auditIntervalMinutes;
            set => Set(ref _auditIntervalMinutes, Math.Max(0.5, value));
        }

        private double _leakThresholdMinutes = 30.0;
        public double LeakThresholdMinutes
        {
            get => _leakThresholdMinutes;
            set => Set(ref _leakThresholdMinutes, Math.Max(1.0, value));
        }

        private int _maxFileSizeMB = DefaultMaxFileSizeMB;
        public int MaxFileSizeMB
        {
            get => _maxFileSizeMB;
            set => Set(ref _maxFileSizeMB, Math.Clamp(value, MinFileSizeMB, MaxFileSizeMBLimit));
        }

        private int _maxGpuMemoryPerModelMB = DefaultMaxGpuMemoryPerModelMB;
        public int MaxGpuMemoryPerModelMB
        {
            get => _maxGpuMemoryPerModelMB;
            set => Set(ref _maxGpuMemoryPerModelMB, Math.Clamp(value, MinGpuMemoryMB, MaxGpuMemoryMBLimit));
        }

        private int _maxTotalGpuMemoryMB = DefaultMaxTotalGpuMemoryMB;
        public int MaxTotalGpuMemoryMB
        {
            get => _maxTotalGpuMemoryMB;
            set => Set(ref _maxTotalGpuMemoryMB, Math.Clamp(value, MinGpuMemoryMB, MaxGpuMemoryMBLimit));
        }

        private int _maxVertices = DefaultMaxVertices;
        public int MaxVertices
        {
            get => _maxVertices;
            set => Set(ref _maxVertices, Math.Clamp(value, MinVertices, MaxVerticesLimit));
        }

        private int _maxIndices = DefaultMaxIndices;
        public int MaxIndices
        {
            get => _maxIndices;
            set => Set(ref _maxIndices, Math.Clamp(value, MinIndices, MaxIndicesLimit));
        }

        private int _maxParts = DefaultMaxParts;
        public int MaxParts
        {
            get => _maxParts;
            set => Set(ref _maxParts, Math.Clamp(value, MinParts, MaxPartsLimit));
        }

        public long MaxFileSizeBytes => (long)_maxFileSizeMB * 1024L * 1024L;
        public long MaxGpuMemoryPerModelBytes => (long)_maxGpuMemoryPerModelMB * 1024L * 1024L;
        public long MaxTotalGpuMemoryBytes => (long)_maxTotalGpuMemoryMB * 1024L * 1024L;

        public override void Initialize()
        {
            try
            {
                if (_isSandboxEnforced)
                    FileSystemSandbox.Instance.Enable();
                else
                    FileSystemSandbox.Instance.Disable();

                FileSystemSandbox.Instance.ClearAllowedRoots();
                if (_allowedRoots != null)
                {
                    foreach (var root in _allowedRoots)
                    {
                        if (!string.IsNullOrWhiteSpace(root))
                            FileSystemSandbox.Instance.AddAllowedRoot(root);
                    }
                }

                if (DXGI.CreateDXGIFactory1(out IDXGIFactory1? factory).Success && factory != null)
                {
                    using (factory)
                    {
                        long maxDedicatedVideoMemory = 0;
                        long maxSharedSystemMemory = 0;

                        for (int i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
                        {
                            using (adapter)
                            {
                                var desc = adapter.Description1;
                                if ((desc.Flags & AdapterFlags.Software) == 0)
                                {
                                    if ((long)desc.DedicatedVideoMemory > maxDedicatedVideoMemory)
                                    {
                                        maxDedicatedVideoMemory = (long)desc.DedicatedVideoMemory;
                                        maxSharedSystemMemory = (long)desc.SharedSystemMemory;
                                    }
                                }
                            }
                        }

                        long clampTargetMemory = 0;
                        if (maxDedicatedVideoMemory > 512 * 1024 * 1024)
                        {
                            clampTargetMemory = maxDedicatedVideoMemory;
                        }
                        else if (maxSharedSystemMemory > 0)
                        {
                            clampTargetMemory = maxSharedSystemMemory;
                        }

                        if (clampTargetMemory > 0)
                        {
                            int maxMB = (int)(clampTargetMemory / (1024 * 1024));
                            if (maxMB <= DefaultMaxTotalGpuMemoryMB)
                            {
                                MaxTotalGpuMemoryMB = Math.Min(MaxTotalGpuMemoryMB, maxMB);
                            }
                        }
                    }
                }

                ResourceAuditor.Instance.SetLeakThreshold(TimeSpan.FromMinutes(Math.Max(1.0, _leakThresholdMinutes)));

                if (_enableAutoAudit)
                {
                    ResourceAuditor.Instance.Start(TimeSpan.FromMinutes(Math.Max(0.5, _auditIntervalMinutes)));
                }
                else
                {
                    ResourceAuditor.Instance.Stop();
                }
            }
            catch
            {
            }
        }

        public bool IsFileSizeAllowed(long fileBytes)
        {
            return fileBytes <= MaxFileSizeBytes;
        }

        public bool IsGpuMemoryPerModelAllowed(long gpuBytes)
        {
            return gpuBytes <= MaxGpuMemoryPerModelBytes;
        }

        public bool IsVertexCountAllowed(int count)
        {
            return count <= _maxVertices;
        }

        public bool IsIndexCountAllowed(int count)
        {
            return count <= _maxIndices;
        }

        public bool IsPartCountAllowed(int count)
        {
            return count <= _maxParts;
        }

        public string ValidateModelComplexity(string fileName, int vertexCount, int indexCount, int partCount)
        {
            if (!IsVertexCountAllowed(vertexCount))
            {
                return string.Format(Texts.VertexCountExceeded, fileName, FormatCount(vertexCount), FormatCount(_maxVertices));
            }
            if (!IsIndexCountAllowed(indexCount))
            {
                return string.Format(Texts.IndexCountExceeded, fileName, FormatCount(indexCount), FormatCount(_maxIndices));
            }
            if (!IsPartCountAllowed(partCount))
            {
                return string.Format(Texts.PartCountExceeded, fileName, FormatCount(partCount), FormatCount(_maxParts));
            }
            return string.Empty;
        }

        private static string FormatCount(int value)
        {
            return value.ToString("N0");
        }
    }
}
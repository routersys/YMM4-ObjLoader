using System.IO;
using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using ObjLoader.Utilities;
using ObjLoader.Views.Controls;
using ObjLoader.ViewModels.Settings;
using Vortice.DXGI;
using YukkuriMovieMaker.Plugin;
using ObjLoader.Cache.Core;
using ObjLoader.Utilities.Logging;

namespace ObjLoader.Settings
{
    public class ModelSettings : SettingsBase<ModelSettings>
    {
        public override string Name => Texts.Settings_3DModel;
        public override SettingsCategory Category => SettingsCategory.Shape;
        public override bool HasSettingView => true;
        public override object SettingView => new ModelSettingsView { DataContext = new ModelSettingsViewModel(this) };
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

        public double D3DResourceReleaseDelay
        {
            get;
            set => Set(ref field, Math.Max(0.0, value));
        } = DefaultD3DResourceReleaseDelay;

        public bool IsSandboxEnforced
        {
            get;
            set => Set(ref field, value);
        } = false;

        public List<string> AllowedRoots
        {
            get;
            set => Set(ref field, value ?? new List<string>());
        } = new List<string>();

        public bool EnableAutoAudit
        {
            get;
            set => Set(ref field, value);
        } = false;

        public double AuditIntervalMinutes
        {
            get;
            set => Set(ref field, Math.Max(0.5, value));
        } = 5.0;

        public double LeakThresholdMinutes
        {
            get;
            set => Set(ref field, Math.Max(1.0, value));
        } = 30.0;

        public int MaxFileSizeMB
        {
            get;
            set => Set(ref field, Math.Clamp(value, MinFileSizeMB, MaxFileSizeMBLimit));
        } = DefaultMaxFileSizeMB;

        public int MaxGpuMemoryPerModelMB
        {
            get;
            set => Set(ref field, Math.Clamp(value, MinGpuMemoryMB, Math.Min(MaxGpuMemoryMBLimit, MaxTotalGpuMemoryMB)));
        } = DefaultMaxGpuMemoryPerModelMB;

        public int MaxTotalGpuMemoryMB
        {
            get;
            set
            {
                int clamped = Math.Clamp(value, MinGpuMemoryMB, MaxGpuMemoryMBLimit);
                if (Set(ref field, clamped))
                {
                    if (MaxGpuMemoryPerModelMB > field)
                    {
                        MaxGpuMemoryPerModelMB = field;
                    }
                }
            }
        } = DefaultMaxTotalGpuMemoryMB;

        public int MaxVertices
        {
            get;
            set => Set(ref field, Math.Clamp(value, MinVertices, MaxVerticesLimit));
        } = DefaultMaxVertices;

        public int MaxIndices
        {
            get;
            set => Set(ref field, Math.Clamp(value, MinIndices, MaxIndicesLimit));
        } = DefaultMaxIndices;

        public int MaxParts
        {
            get;
            set => Set(ref field, Math.Clamp(value, MinParts, MaxPartsLimit));
        } = DefaultMaxParts;

        public long MaxFileSizeBytes => (long)MaxFileSizeMB * 1024L * 1024L;
        public long MaxGpuMemoryPerModelBytes => (long)MaxGpuMemoryPerModelMB * 1024L * 1024L;
        public long MaxTotalGpuMemoryBytes => (long)MaxTotalGpuMemoryMB * 1024L * 1024L;

        public override void Initialize()
        {
            try
            {
                if (IsSandboxEnforced)
                    FileSystemSandbox.Instance.Enable();
                else
                    FileSystemSandbox.Instance.Disable();

                FileSystemSandbox.Instance.ClearAllowedRoots();
                foreach (var root in AllowedRoots)
                {
                    if (!string.IsNullOrWhiteSpace(root))
                        FileSystemSandbox.Instance.AddAllowedRoot(root);
                }
            }
            catch (Exception ex)
            {
                Logger<ModelSettings>.Instance.Error("Sandbox setup failed", ex);
            }

            try
            {
                AdjustGpuMemoryLimit();
            }
            catch (Exception ex)
            {
                Logger<ModelSettings>.Instance.Error("GPU info retrieval failed", ex);
            }

            try
            {
                ResourceAuditor.Instance.SetLeakThreshold(TimeSpan.FromMinutes(Math.Max(1.0, LeakThresholdMinutes)));

                if (EnableAutoAudit)
                {
                    ResourceAuditor.Instance.Start(TimeSpan.FromMinutes(Math.Max(0.5, AuditIntervalMinutes)));
                }
                else
                {
                    ResourceAuditor.Instance.Stop();
                }
            }
            catch (Exception ex)
            {
                Logger<ModelSettings>.Instance.Error("Auditor setup failed", ex);
            }
        }

        private void AdjustGpuMemoryLimit()
        {
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
                    if (maxDedicatedVideoMemory > 512L * 1024L * 1024L)
                    {
                        clampTargetMemory = maxDedicatedVideoMemory;
                    }
                    else if (maxSharedSystemMemory > 0)
                    {
                        clampTargetMemory = maxSharedSystemMemory;
                    }

                    if (clampTargetMemory > 0)
                    {
                        long maxMB = clampTargetMemory / (1024L * 1024L);
                        if (maxMB <= DefaultMaxTotalGpuMemoryMB)
                        {
                            MaxTotalGpuMemoryMB = Math.Min(MaxTotalGpuMemoryMB, (int)maxMB);
                        }
                    }
                }
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
            return count <= MaxVertices;
        }

        public bool IsIndexCountAllowed(int count)
        {
            return count <= MaxIndices;
        }

        public bool IsPartCountAllowed(int count)
        {
            return count <= MaxParts;
        }

        public string ValidateModelComplexity(string fileName, int vertexCount, int indexCount, int partCount)
        {
            if (!IsVertexCountAllowed(vertexCount))
            {
                return string.Format(Texts.VertexCountExceeded, fileName, FormatCount(vertexCount), FormatCount(MaxVertices));
            }
            if (!IsIndexCountAllowed(indexCount))
            {
                return string.Format(Texts.IndexCountExceeded, fileName, FormatCount(indexCount), FormatCount(MaxIndices));
            }
            if (!IsPartCountAllowed(partCount))
            {
                return string.Format(Texts.PartCountExceeded, fileName, FormatCount(partCount), FormatCount(MaxParts));
            }
            return string.Empty;
        }

        public List<string> CacheIndexPaths
        {
            get;
            set => Set(ref field, value ?? new List<string>());
        } = new List<string>();

        public CacheIndex GetCacheIndex()
        {
            var aggregatedIndex = new CacheIndex();

            foreach (var path in CacheIndexPaths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    var loadedIndex = CacheIndex.FromBinary(data);
                    foreach (var kvp in loadedIndex.Entries)
                    {
                        aggregatedIndex.Entries[kvp.Key] = kvp.Value;
                    }
                }
                catch (Exception ex)
                {
                    Logger<ModelSettings>.Instance.Error($"Failed to load index from '{path}'", ex);
                }
            }
            return aggregatedIndex;
        }

        public const int MaxCacheEntries = 10000;

        public void SaveCacheIndex(CacheIndex index)
        {
            if (index == null)
            {
                CacheIndexPaths = new List<string>();
                Save();
                return;
            }

            if (index.Entries.Count > MaxCacheEntries)
            {
                var keysToRemove = index.Entries.OrderBy(x => x.Value.LastAccessTime)
                                                .Take(index.Entries.Count - MaxCacheEntries)
                                                .Select(x => x.Key)
                                                .ToList();
                foreach (var key in keysToRemove)
                {
                    index.Entries.Remove(key);
                }

                UserNotification.ShowInfo(string.Format(Texts.CacheEntryLimitReached, MaxCacheEntries), Texts.ErrorTitle);
            }

            var newPaths = new List<string>();

            var groupedEntries = index.Entries.GroupBy(kvp =>
            {
                string? originalDir = Path.GetDirectoryName(kvp.Key);
                return string.IsNullOrEmpty(originalDir) ? string.Empty : originalDir;
            });

            foreach (var group in groupedEntries)
            {
                if (string.IsNullOrEmpty(group.Key)) continue;

                string cacheDir = Path.Combine(group.Key, ".cache");
                string indexFile = Path.Combine(cacheDir, "CacheIndex.dat");

                try
                {
                    if (!Directory.Exists(cacheDir))
                    {
                        var di = Directory.CreateDirectory(cacheDir);
                        di.Attributes |= FileAttributes.Hidden;
                    }

                    var partialIndex = new CacheIndex();
                    partialIndex.Version = index.Version;
                    foreach (var kvp in group)
                    {
                        partialIndex.Entries[kvp.Key] = kvp.Value;
                    }

                    File.WriteAllBytes(indexFile, partialIndex.ToBinary());
                    newPaths.Add(indexFile);
                }
                catch (Exception ex)
                {
                    Logger<ModelSettings>.Instance.Error($"Failed to save partial index to '{indexFile}'", ex);
                }
            }
            CacheIndexPaths = newPaths;
            Save();
        }

        private static string FormatCount(int value)
        {
            return value.ToString("N0");
        }
    }
}
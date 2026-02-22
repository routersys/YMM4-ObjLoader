using System.IO;
using ObjLoader.Localization;
using ObjLoader.Utilities;

namespace ObjLoader.Cache
{
    public class CacheIndex
    {
        public int Version { get; set; } = 2;
        public Dictionary<string, CacheEntry> Entries { get; set; } = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        public class CacheEntry
        {
            public string ModelHash { get; set; } = string.Empty;
            public string OriginalPath { get; set; } = string.Empty;
            public string CacheRootPath { get; set; } = string.Empty;

            public long TotalSize { get; set; }
            public DateTime LastAccessTime { get; set; }
            public bool IsSplit { get; set; }
            public int PartsCount { get; set; }
            public string ExtensionProviderId { get; set; } = string.Empty;
            public bool HasExtensionCache { get; set; }
        }

        public byte[] ToBinary()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(Version);
                bw.Write(Entries.Count);
                foreach (var kvp in Entries)
                {
                    bw.Write(kvp.Key);
                    var entry = kvp.Value;
                    bw.Write(entry.ModelHash);
                    bw.Write(entry.OriginalPath);
                    bw.Write(entry.CacheRootPath);
                    bw.Write(entry.TotalSize);
                    bw.Write(entry.LastAccessTime.ToBinary());
                    bw.Write(entry.IsSplit);
                    bw.Write(entry.PartsCount);
                    bw.Write(entry.ExtensionProviderId);
                    bw.Write(entry.HasExtensionCache);
                }
                return ms.ToArray();
            }
        }

        public static CacheIndex FromBinary(byte[] data)
        {
            var index = new CacheIndex();
            if (data == null || data.Length == 0) return index;

            try
            {
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms))
                {
                    index.Version = br.ReadInt32();
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        string key = br.ReadString();
                        var entry = new CacheEntry();
                        entry.ModelHash = br.ReadString();
                        entry.OriginalPath = br.ReadString();
                        entry.CacheRootPath = br.ReadString();
                        entry.TotalSize = br.ReadInt64();
                        entry.LastAccessTime = DateTime.FromBinary(br.ReadInt64());
                        entry.IsSplit = br.ReadBoolean();
                        entry.PartsCount = br.ReadInt32();

                        if (index.Version >= 2)
                        {
                            entry.ExtensionProviderId = br.ReadString();
                            entry.HasExtensionCache = br.ReadBoolean();
                        }

                        index.Entries[key] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CacheIndex.FromBinary: Deserialization failed: {ex.Message}");
                UserNotification.ShowWarning(Texts.CacheIndexDeserializationFailed, Texts.ErrorTitle);
                return new CacheIndex();
            }
            return index;
        }
    }
}
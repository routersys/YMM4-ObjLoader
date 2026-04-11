using ObjLoader.Cache.Extensions;
using ObjLoader.Cache.Storage;
using ObjLoader.Cache.Streaming;
using ObjLoader.Cache.Utilities;
using ObjLoader.Core.Models;
using ObjLoader.Settings;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace ObjLoader.Cache.Core
{
    public class ModelCache
    {
        private const int MaxThumbnailSize = 10 * 1024 * 1024;
        private const int MaxTexturePathLength = 32_767;
        private const string CacheDirName = ".cache";

        private readonly List<IExtensionCacheProvider> _extensionProviders;

        public ModelCache()
        {
            _extensionProviders = Generated.ExtensionCacheProviderRegistry.GetProviders();
        }

        public IStreamingCacheWriter CreateStreamingWriter(string path, CacheHeader header, byte[] thumbnail, bool? forceSplit = null)
        {
            string root = Path.GetDirectoryName(path) ?? string.Empty;
            string hash = ComputePathHash(path);
            string cacheDir = Path.Combine(root, CacheDirName);
            string modelCacheDir = Path.Combine(cacheDir, hash);

            if (!Directory.Exists(cacheDir))
            {
                var di = Directory.CreateDirectory(cacheDir);
                di.Attributes |= FileAttributes.Hidden;
            }
            if (!Directory.Exists(modelCacheDir))
            {
                Directory.CreateDirectory(modelCacheDir);
            }

            bool isSplit = forceSplit ?? (DiskTypeDetector.GetDiskType(root) == DiskType.Hdd);
            var writer = new StreamingCacheWriter(modelCacheDir, isSplit);

            writer.WriteHeader(header);
            writer.WriteThumbnail(thumbnail);

            return writer;
        }

        public void FinalizeStreamingCache(string path, StreamingCacheWriter writer, ObjModel model)
        {
            try
            {
                writer.Commit();

                string root = Path.GetDirectoryName(path) ?? string.Empty;
                string hash = ComputePathHash(path);
                string modelCacheDir = Path.Combine(root, CacheDirName, hash);

                IExtensionCacheProvider? activeProvider = null;
                foreach (var p in _extensionProviders)
                {
                    if (p.HasExtensionData(model))
                    {
                        activeProvider = p;
                        break;
                    }
                }

                long totalSize = writer.GetTotalSize();
                int partsCount = writer.GetPartsCount();

                string extFile = Path.Combine(modelCacheDir, "ext.bin");
                if (activeProvider != null)
                {
                    using var fsExt = new FileStream(extFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var bwExt = new BinaryWriter(fsExt);
                    activeProvider.WriteExtensionData(bwExt, model);
                    totalSize += new FileInfo(extFile).Length;
                }
                else if (File.Exists(extFile))
                {
                    File.Delete(extFile);
                }

                bool isSplit = writer.GetPartsCount() > 1;

                var index = ModelSettings.Instance.GetCacheIndex();
                index.Entries[path] = new CacheIndex.CacheEntry
                {
                    ModelHash = hash,
                    OriginalPath = path,
                    CacheRootPath = Path.Combine(CacheDirName, hash),
                    TotalSize = totalSize,
                    LastAccessTime = DateTime.Now,
                    IsSplit = isSplit,
                    PartsCount = partsCount,
                    ExtensionProviderId = activeProvider?.ProviderId ?? string.Empty,
                    HasExtensionCache = activeProvider != null
                };
                ModelSettings.Instance.SaveCacheIndex(index);

                string legacyPath = path + ".bin";
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch { }
        }

        public void SaveThumbnailFile(string path, byte[] thumbnail)
        {
            try
            {
                string root = Path.GetDirectoryName(path) ?? string.Empty;
                string hash = ComputePathHash(path);
                string modelCacheDir = Path.Combine(root, CacheDirName, hash);

                if (!Directory.Exists(modelCacheDir)) return;

                string thumbPath = Path.Combine(modelCacheDir, "thumb.png");
                File.WriteAllBytes(thumbPath, thumbnail);
            }
            catch { }
        }

        public byte[] LoadThumbnailFile(string path)
        {
            try
            {
                var index = ModelSettings.Instance.GetCacheIndex();
                string cacheDir = string.Empty;

                if (index.Entries.TryGetValue(path, out var e))
                {
                    string root = Path.GetDirectoryName(path) ?? string.Empty;
                    cacheDir = Path.Combine(root, CacheDirName, e.ModelHash);
                }

                if (string.IsNullOrEmpty(cacheDir))
                {
                    string hash = ComputePathHash(path);
                    string root = Path.GetDirectoryName(path) ?? string.Empty;
                    cacheDir = Path.Combine(root, CacheDirName, hash);
                }

                string thumbPath = Path.Combine(cacheDir, "thumb.png");
                if (File.Exists(thumbPath))
                {
                    return File.ReadAllBytes(thumbPath);
                }
            }
            catch { }
            return Array.Empty<byte>();
        }

        public bool TryLoad(string path, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion, out ObjModel model)
        {
            model = new ObjModel();
            byte[]? loadedThumbnail = null;
            string? legacyMigrationPath = null;

            try
            {
                var index = ModelSettings.Instance.GetCacheIndex();
                CacheIndex.CacheEntry? entry = null;
                string cachePath = string.Empty;
                bool isSplit = false;

                if (index.Entries.TryGetValue(path, out var e))
                {
                    string root = Path.GetDirectoryName(path) ?? string.Empty;
                    string cacheDir = Path.Combine(root, CacheDirName, e.ModelHash);
                    if (Directory.Exists(cacheDir))
                    {
                        entry = e;
                        cachePath = cacheDir;
                        isSplit = e.IsSplit;
                    }
                }

                if (string.IsNullOrEmpty(cachePath))
                {
                    string legacyPath = path + ".bin";
                    if (File.Exists(legacyPath))
                    {
                        cachePath = legacyPath;
                        legacyMigrationPath = legacyPath;
                        isSplit = false;
                    }
                    else
                    {
                        string hash = ComputePathHash(path);
                        string root = Path.GetDirectoryName(path) ?? string.Empty;
                        string possibleDir = Path.Combine(root, CacheDirName, hash);

                        if (Directory.Exists(possibleDir))
                        {
                            cachePath = possibleDir;
                            if (File.Exists(Path.Combine(cachePath, "header.bin")))
                            {
                                isSplit = true;
                            }
                            else if (File.Exists(Path.Combine(cachePath, "model.bin")))
                            {
                                isSplit = false;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                if (Directory.Exists(cachePath) && !CacheRepairUtil.ValidateCacheIntegrity(cachePath))
                {
                    CacheRepairUtil.RepairIfNeeded(cachePath);
                    return false;
                }

                Stream stream;
                if (isSplit)
                {
                    stream = new MultiFileStream(cachePath);
                }
                else
                {
                    string filePath = Directory.Exists(cachePath) ? Path.Combine(cachePath, "model.bin") : cachePath;
                    stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                }

                string fileHash = ComputeFileHash(path);

                using (stream)
                using (var br = new BinaryReader(stream))
                {
                    var header = ReadHeader(br);
                    if (!header.IsValid(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion, fileHash)) return false;

                    model = ReadBody(br, stream, out loadedThumbnail);

                    if (entry != null && entry.HasExtensionCache && !string.IsNullOrEmpty(entry.ExtensionProviderId))
                    {
                        var provider = _extensionProviders.FirstOrDefault(p => p.ProviderId == entry.ExtensionProviderId);
                        if (provider != null)
                        {
                            string extDir = Directory.Exists(cachePath) ? cachePath : (Path.GetDirectoryName(cachePath) ?? string.Empty);
                            string extFile = Path.Combine(extDir, "ext.bin");
                            if (File.Exists(extFile))
                            {
                                var modelToLoad = model;
                                model.ExtensionLoadTask = Task.Run(() =>
                                {
                                    try
                                    {
                                        using var fsExt = new FileStream(extFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                                        using var brExt = new BinaryReader(fsExt);
                                        provider.ReadExtensionData(brExt, modelToLoad);
                                    }
                                    catch
                                    {
                                    }
                                });
                            }
                        }
                    }

                    if (model.Vertices.Length > 0 && !index.Entries.ContainsKey(path) && legacyMigrationPath == null)
                    {
                        try
                        {
                            long totalSize = 0;
                            int partsCount = 1;

                            if (isSplit)
                            {
                                var files = Directory.GetFiles(cachePath, "part.*.bin");
                                foreach (var f in files) totalSize += new FileInfo(f).Length;
                                partsCount = files.Length;
                            }
                            else
                            {
                                string filePath = Directory.Exists(cachePath) ? Path.Combine(cachePath, "model.bin") : cachePath;
                                if (File.Exists(filePath)) totalSize = new FileInfo(filePath).Length;
                            }

                            string hash = ComputePathHash(path);
                            index.Entries[path] = new CacheIndex.CacheEntry
                            {
                                ModelHash = hash,
                                OriginalPath = path,
                                CacheRootPath = Path.Combine(CacheDirName, hash),
                                TotalSize = totalSize,
                                LastAccessTime = DateTime.Now,
                                IsSplit = isSplit,
                                PartsCount = partsCount
                            };
                            ModelSettings.Instance.SaveCacheIndex(index);
                        }
                        catch { }
                    }
                }

                if (legacyMigrationPath != null && model.Vertices.Length > 0 && loadedThumbnail != null)
                {
                    try
                    {
                        Save(path, model, loadedThumbnail, originalTimestamp, parserId, parserVersion, pluginVersion);
                        if (File.Exists(legacyMigrationPath))
                        {
                            File.Delete(legacyMigrationPath);
                        }
                    }
                    catch { }
                }

                return model.Vertices.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public bool Convert(string path, bool toSplit)
        {
            try
            {
                var index = ModelSettings.Instance.GetCacheIndex();
                string cacheFile = string.Empty;
                string extDir = string.Empty;

                if (index.Entries.TryGetValue(path, out var e))
                {
                    if (e.IsSplit == toSplit) return true;

                    string root = Path.GetDirectoryName(path) ?? string.Empty;
                    string cacheDir = Path.Combine(root, CacheDirName, e.ModelHash);
                    if (Directory.Exists(cacheDir))
                    {
                        extDir = cacheDir;
                        if (e.IsSplit)
                        {
                            cacheFile = Path.Combine(cacheDir, "part.0.bin");
                        }
                        else
                        {
                            cacheFile = Path.Combine(cacheDir, "model.bin");
                        }
                    }
                }

                Stream stream;
                if (e!.IsSplit)
                {
                    stream = new MultiFileStream(Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, CacheDirName, e.ModelHash));
                }
                else
                {
                    stream = new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                }

                CacheHeader header;
                byte[] thumbnail;
                ObjModel model;

                using (stream)
                using (var br = new BinaryReader(stream))
                {
                    header = ReadHeader(br);
                    model = ReadBody(br, stream, out thumbnail);
                }

                if (e.HasExtensionCache && !string.IsNullOrEmpty(e.ExtensionProviderId))
                {
                    var provider = _extensionProviders.FirstOrDefault(p => p.ProviderId == e.ExtensionProviderId);
                    if (provider != null)
                    {
                        if (string.IsNullOrEmpty(extDir) && !string.IsNullOrEmpty(cacheFile))
                        {
                            extDir = Path.GetDirectoryName(cacheFile) ?? string.Empty;
                        }
                        string extFile = Path.Combine(extDir, "ext.bin");
                        if (File.Exists(extFile))
                        {
                            using var fsExt = new FileStream(extFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using var brExt = new BinaryReader(fsExt);
                            provider.ReadExtensionData(brExt, model);
                        }
                    }
                }

                Save(path, model, thumbnail, DateTime.FromBinary(header.Timestamp), header.ParserId, header.ParserVersion, header.PluginVersion, toSplit);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] GetThumbnail(string path, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion)
        {
            try
            {
                var thumbFile = LoadThumbnailFile(path);
                if (thumbFile.Length > 0) return thumbFile;
            }
            catch { }

            try
            {
                var index = ModelSettings.Instance.GetCacheIndex();
                string cacheFile = string.Empty;

                if (index.Entries.TryGetValue(path, out var e))
                {
                    string root = Path.GetDirectoryName(path) ?? string.Empty;
                    string cacheDir = Path.Combine(root, CacheDirName, e.ModelHash);
                    if (Directory.Exists(cacheDir))
                    {
                        if (e.IsSplit)
                        {
                            cacheFile = Path.Combine(cacheDir, "part.0.bin");
                        }
                        else
                        {
                            cacheFile = Path.Combine(cacheDir, "model.bin");
                        }
                    }
                }

                if (string.IsNullOrEmpty(cacheFile) || !File.Exists(cacheFile))
                {
                    string legacyPath = path + ".bin";
                    if (File.Exists(legacyPath))
                    {
                        cacheFile = legacyPath;
                    }
                    else
                    {
                        string hash = ComputePathHash(path);
                        string root = Path.GetDirectoryName(path) ?? string.Empty;
                        string possibleDir = Path.Combine(root, CacheDirName, hash);
                        if (Directory.Exists(possibleDir))
                        {
                            if (File.Exists(Path.Combine(possibleDir, "part.0.bin"))) cacheFile = Path.Combine(possibleDir, "part.0.bin");
                            else if (File.Exists(Path.Combine(possibleDir, "model.bin"))) cacheFile = Path.Combine(possibleDir, "model.bin");
                        }
                    }
                }

                if (string.IsNullOrEmpty(cacheFile) || !File.Exists(cacheFile)) return Array.Empty<byte>();

                string fileHash = ComputeFileHash(path);

                using var fs = new FileStream(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                using var br = new BinaryReader(fs);

                var header = ReadHeader(br);
                if (!header.IsValid(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion, fileHash)) return Array.Empty<byte>();

                int thumbLen = br.ReadInt32();
                if (thumbLen > 0 && thumbLen <= MaxThumbnailSize)
                {
                    return br.ReadBytes(thumbLen);
                }
            }
            catch
            {
            }

            return Array.Empty<byte>();
        }

        public void Save(string path, ObjModel model, byte[] thumbnail, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion, bool? forceSplit = null)
        {
            try
            {
                string root = Path.GetDirectoryName(path) ?? string.Empty;
                string hash = ComputePathHash(path);
                string cacheDir = Path.Combine(root, CacheDirName);
                string modelCacheDir = Path.Combine(cacheDir, hash);

                if (!Directory.Exists(cacheDir))
                {
                    var di = Directory.CreateDirectory(cacheDir);
                    di.Attributes |= FileAttributes.Hidden;
                }
                if (!Directory.Exists(modelCacheDir))
                {
                    Directory.CreateDirectory(modelCacheDir);
                }

                bool isSplit = forceSplit ?? (DiskTypeDetector.GetDiskType(root) == DiskType.Hdd);

                string fileHash = ComputeFileHash(path);
                var header = new CacheHeader(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion, fileHash);

                long totalSize = 0;
                int partsCount = 1;

                if (!isSplit)
                {
                    string tempPath = Path.Combine(modelCacheDir, "model.bin.tmp");
                    string finalPath = Path.Combine(modelCacheDir, "model.bin");

                    WriteCacheFileSingle(tempPath, header, model, thumbnail);
                    File.Move(tempPath, finalPath, true);
                    totalSize = new FileInfo(finalPath).Length;

                    CleanUpSplitFiles(modelCacheDir);
                }
                else
                {
                    CleanUpSplitFiles(modelCacheDir);
                    partsCount = WriteCacheFileSplit(modelCacheDir, header, model, thumbnail, out totalSize);
                    string singleFile = Path.Combine(modelCacheDir, "model.bin");
                    if (File.Exists(singleFile)) File.Delete(singleFile);
                }

                IExtensionCacheProvider? activeProvider = null;
                foreach (var p in _extensionProviders)
                {
                    if (p.HasExtensionData(model))
                    {
                        activeProvider = p;
                        break;
                    }
                }

                string extFile = Path.Combine(modelCacheDir, "ext.bin");
                if (activeProvider != null)
                {
                    using var fsExt = new FileStream(extFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var bwExt = new BinaryWriter(fsExt);
                    activeProvider.WriteExtensionData(bwExt, model);
                    totalSize += new FileInfo(extFile).Length;
                }
                else if (File.Exists(extFile))
                {
                    File.Delete(extFile);
                }

                var index = ModelSettings.Instance.GetCacheIndex();
                index.Entries[path] = new CacheIndex.CacheEntry
                {
                    ModelHash = hash,
                    OriginalPath = path,
                    CacheRootPath = Path.Combine(CacheDirName, hash),
                    TotalSize = totalSize,
                    LastAccessTime = DateTime.Now,
                    IsSplit = isSplit,
                    PartsCount = partsCount,
                    ExtensionProviderId = activeProvider?.ProviderId ?? string.Empty,
                    HasExtensionCache = activeProvider != null
                };
                ModelSettings.Instance.SaveCacheIndex(index);

                string legacyPath = path + ".bin";
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch
            {
            }
        }

        private void CleanUpSplitFiles(string dir)
        {
            var files = Directory.GetFiles(dir, "part.*.bin");
            foreach (var f in files) File.Delete(f);
        }

        private string ComputePathHash(string path)
        {
            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private string ComputeFileHash(string path)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "");
            }
            catch
            {
                return string.Empty;
            }
        }

        private CacheHeader ReadHeader(BinaryReader br)
        {
            int signature = br.ReadInt32();
            int version = br.ReadInt32();
            long timestamp = br.ReadInt64();
            string path = br.ReadString();
            string parserId = br.ReadString();
            int parserVersion = br.ReadInt32();
            string pluginVersion = br.ReadString();
            string fileHash = string.Empty;

            if (version >= 6)
            {
                fileHash = br.ReadString();
            }

            return new CacheHeader(signature, version, timestamp, path, parserId, parserVersion, pluginVersion, fileHash);
        }

        private unsafe ObjModel ReadBody(BinaryReader br, Stream stream, out byte[] thumbnail)
        {
            var limits = ModelSettings.Instance;

            int thumbLen = br.ReadInt32();
            if (thumbLen < 0 || thumbLen > MaxThumbnailSize)
                throw new InvalidDataException($"Invalid thumbnail length: {thumbLen}");

            if (thumbLen > 0)
            {
                thumbnail = br.ReadBytes(thumbLen);
            }
            else
            {
                thumbnail = Array.Empty<byte>();
            }

            int vCount = br.ReadInt32();
            int iCount = br.ReadInt32();
            int pCount = br.ReadInt32();

            if (vCount < 0 || vCount > limits.MaxVertices)
                throw new InvalidDataException($"Invalid vertex count: {vCount}");
            if (iCount < 0 || iCount > limits.MaxIndices)
                throw new InvalidDataException($"Invalid index count: {iCount}");
            if (pCount < 0 || pCount > limits.MaxParts)
                throw new InvalidDataException($"Invalid part count: {pCount}");

            var parts = new List<ModelPart>(pCount);
            for (int i = 0; i < pCount; i++)
            {
                int tLen = br.ReadInt32();
                if (tLen < 0 || tLen > MaxTexturePathLength)
                    throw new InvalidDataException($"Invalid texture path length: {tLen}");
                var tBytes = br.ReadBytes(tLen);
                string texPath = Encoding.UTF8.GetString(tBytes);
                int iOff = br.ReadInt32();
                int iCnt = br.ReadInt32();
                Vector4 col = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                parts.Add(new ModelPart { TexturePath = texPath, IndexOffset = iOff, IndexCount = iCnt, BaseColor = col });
            }

            Vector3 center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            float scale = br.ReadSingle();

            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);

            fixed (ObjVertex* pV = vertices)
            {
                var span = new Span<byte>(pV, vCount * sizeof(ObjVertex));
                int totalRead = 0;
                while (totalRead < span.Length)
                {
                    int read = stream.Read(span.Slice(totalRead));
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead != span.Length)
                    throw new InvalidDataException($"Expected {span.Length} vertex bytes, read {totalRead}");
            }

            fixed (int* pI = indices)
            {
                var span = new Span<byte>(pI, iCount * sizeof(int));
                int totalRead = 0;
                while (totalRead < span.Length)
                {
                    int read = stream.Read(span.Slice(totalRead));
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead != span.Length)
                    throw new InvalidDataException($"Expected {span.Length} index bytes, read {totalRead}");
            }

            return new ObjModel
            {
                Vertices = vertices,
                Indices = indices,
                Parts = parts,
                ModelCenter = center,
                ModelScale = scale
            };
        }

        private unsafe void WriteCacheFileSingle(string path, CacheHeader header, ObjModel model, byte[] thumbnail)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);
            WriteData(bw, header, model, thumbnail);
        }

        private unsafe int WriteCacheFileSplit(string dir, CacheHeader header, ObjModel model, byte[] thumbnail, out long totalSize)
        {
            totalSize = 0;
            int partIndex = 0;
            const int ChunkSize = 256 * 1024;

            using (var splitter = new SplitStream(dir, ChunkSize))
            using (var bw = new BinaryWriter(splitter))
            {
                WriteData(bw, header, model, thumbnail);
                totalSize = splitter.TotalLength;
                partIndex = splitter.PartCount;
            }
            return partIndex;
        }

        private unsafe void WriteData(BinaryWriter bw, CacheHeader header, ObjModel model, byte[] thumbnail)
        {
            bw.Write(header.Signature);
            bw.Write(header.Version);
            bw.Write(header.Timestamp);
            bw.Write(header.OriginalPath);
            bw.Write(header.ParserId);
            bw.Write(header.ParserVersion);
            bw.Write(header.PluginVersion);
            bw.Write(header.FileHash);

            bw.Write(thumbnail.Length);
            if (thumbnail.Length > 0)
            {
                bw.Write(thumbnail);
            }

            bw.Write(model.Vertices.Length);
            bw.Write(model.Indices.Length);
            bw.Write(model.Parts.Count);

            foreach (var part in model.Parts)
            {
                var textureBytes = Encoding.UTF8.GetBytes(part.TexturePath ?? string.Empty);
                bw.Write(textureBytes.Length);
                bw.Write(textureBytes);
                bw.Write(part.IndexOffset);
                bw.Write(part.IndexCount);
                bw.Write(part.BaseColor.X);
                bw.Write(part.BaseColor.Y);
                bw.Write(part.BaseColor.Z);
                bw.Write(part.BaseColor.W);
            }

            bw.Write(model.ModelCenter.X);
            bw.Write(model.ModelCenter.Y);
            bw.Write(model.ModelCenter.Z);
            bw.Write(model.ModelScale);

            fixed (ObjVertex* pV = model.Vertices)
            {
                var span = new ReadOnlySpan<byte>(pV, model.Vertices.Length * sizeof(ObjVertex));
                bw.Write(span);
            }

            fixed (int* pI = model.Indices)
            {
                var span = new ReadOnlySpan<byte>(pI, model.Indices.Length * sizeof(int));
                bw.Write(span);
            }
        }

        private class MultiFileStream : Stream
        {
            private readonly string _baseDir;
            private int _currentIndex = 0;
            private FileStream? _currentStream;
            private long _position = 0;

            public MultiFileStream(string baseDir)
            {
                _baseDir = baseDir;
                OpenNextStream();
            }

            private void OpenNextStream()
            {
                _currentStream?.Dispose();
                string path = Path.Combine(_baseDir, $"part.{_currentIndex}.bin");
                if (File.Exists(path))
                {
                    _currentStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                }
                else
                {
                    _currentStream = null;
                }
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => _position; set => throw new NotSupportedException(); }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(new Span<byte>(buffer, offset, count));
            }

            public override int Read(Span<byte> buffer)
            {
                if (_currentStream == null) return 0;

                int totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    int read = _currentStream.Read(buffer.Slice(totalRead));
                    if (read == 0)
                    {
                        _currentIndex++;
                        OpenNextStream();
                        if (_currentStream == null) break;
                    }
                    else
                    {
                        totalRead += read;
                        _position += read;
                    }
                }
                return totalRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (origin == SeekOrigin.Begin)
                {
                    long delta = offset - _position;
                    return Seek(delta, SeekOrigin.Current);
                }

                if (origin == SeekOrigin.Current && offset >= 0)
                {
                    long toSkip = offset;
                    while (toSkip > 0)
                    {
                        if (_currentStream == null) break;
                        long currentRem = _currentStream.Length - _currentStream.Position;
                        if (toSkip <= currentRem)
                        {
                            _currentStream.Seek(toSkip, SeekOrigin.Current);
                            _position += toSkip;
                            toSkip = 0;
                        }
                        else
                        {
                            _currentStream.Seek(0, SeekOrigin.End);
                            _position += currentRem;
                            toSkip -= currentRem;
                            _currentIndex++;
                            OpenNextStream();
                        }
                    }
                    return _position;
                }
                throw new NotSupportedException();
            }

            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try
                    {
                        _currentStream?.Dispose();
                    }
                    catch
                    {
                    }
                }
                base.Dispose(disposing);
            }
        }

        private class SplitStream : Stream
        {
            private readonly string _dir;
            private readonly int _chunkSize;
            private int _partIndex = 0;
            private FileStream? _currentStream;
            private long _totalLength = 0;

            public long TotalLength => _totalLength;
            public int PartCount => _partIndex;

            public SplitStream(string dir, int chunkSize)
            {
                _dir = dir;
                _chunkSize = chunkSize;
                NextPart();
            }

            private void NextPart()
            {
                _currentStream?.Dispose();
                string path = Path.Combine(_dir, $"part.{_partIndex}.bin");
                _currentStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                _partIndex++;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _totalLength;
            public override long Position { get => _totalLength; set => throw new NotSupportedException(); }

            public override void Flush() => _currentStream?.Flush();

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                Write(new ReadOnlySpan<byte>(buffer, offset, count));
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                int written = 0;
                while (written < buffer.Length)
                {
                    if (_currentStream!.Length >= _chunkSize)
                    {
                        NextPart();
                    }

                    int remainingInChunk = (int)(_chunkSize - _currentStream!.Length);
                    int toWrite = Math.Min(remainingInChunk, buffer.Length - written);

                    _currentStream!.Write(buffer.Slice(written, toWrite));
                    written += toWrite;
                    _totalLength += toWrite;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try
                    {
                        _currentStream?.Dispose();
                    }
                    catch
                    {
                    }
                }
                base.Dispose(disposing);
            }
        }
    }
}
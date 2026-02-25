using System.IO;
using System.Reflection;
using ObjLoader.Cache.Core;
using ObjLoader.Cache.Streaming;
using ObjLoader.Core.Interfaces;
using ObjLoader.Core.Models;
using ObjLoader.Localization;
using ObjLoader.Settings;
using ObjLoader.Utilities;

namespace ObjLoader.Parsers
{
    public partial class ObjModelLoader
    {
        private const string DefaultPluginVersion = "1.0.0";
        private static readonly string PluginVersion;
        private readonly List<IModelParser> _parsers;
        private readonly ModelCache _cache;
        private readonly Dictionary<string, List<IModelParser>> _extensionMap;
        private readonly Dictionary<Type, int> _parserVersions;

        static ObjModelLoader()
        {
            try
            {
                PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? DefaultPluginVersion;
            }
            catch
            {
                PluginVersion = DefaultPluginVersion;
            }
        }

        public ObjModelLoader()
        {
            _cache = new ModelCache();
            _parsers = new List<IModelParser>();
            _extensionMap = new Dictionary<string, List<IModelParser>>(StringComparer.OrdinalIgnoreCase);
            _parserVersions = new Dictionary<Type, int>();

            LoadGeneratedParsers();
        }

        private IModelParser? GetParser(string path)
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return null;

            if (ShouldForceAssimp(ext))
            {
                var assimpParser = _parsers.FirstOrDefault(p => p.GetType().Name == "AssimpParser");
                if (assimpParser != null && assimpParser.CanParse(ext))
                {
                    return assimpParser;
                }
            }

            if (_extensionMap.TryGetValue(ext, out var mappedParsers))
            {
                var parser = mappedParsers.FirstOrDefault(p => p.GetType().Name != "AssimpParser" && p.CanParse(ext));
                if (parser != null) return parser;

                parser = mappedParsers.FirstOrDefault(p => p.CanParse(ext));
                if (parser != null) return parser;
            }

            return _parsers.FirstOrDefault(p => p.CanParse(ext));
        }

        private bool ShouldForceAssimp(string ext)
        {
            var settings = PluginSettings.Instance;
            return ext.AsSpan() switch
            {
                var s when s.Equals(".obj", StringComparison.OrdinalIgnoreCase) => settings.AssimpObj,
                var s when s.Equals(".glb", StringComparison.OrdinalIgnoreCase) => settings.AssimpGlb,
                var s when s.Equals(".gltf", StringComparison.OrdinalIgnoreCase) => settings.AssimpGlb,
                var s when s.Equals(".ply", StringComparison.OrdinalIgnoreCase) => settings.AssimpPly,
                var s when s.Equals(".stl", StringComparison.OrdinalIgnoreCase) => settings.AssimpStl,
                var s when s.Equals(".3mf", StringComparison.OrdinalIgnoreCase) => settings.Assimp3mf,
                var s when s.Equals(".pmx", StringComparison.OrdinalIgnoreCase) => settings.AssimpPmx,
                _ => false
            };
        }

        private bool ValidateFileSize(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                var modelSettings = ModelSettings.Instance;
                if (!modelSettings.IsFileSizeAllowed(fileInfo.Length))
                {
                    long sizeMB = fileInfo.Length / (1024L * 1024L);
                    string message = string.Format(
                        Texts.FileSizeExceeded,
                        Path.GetFileName(path),
                        sizeMB,
                        modelSettings.MaxFileSizeMB);
                    UserNotification.ShowWarning(message, Texts.ResourceLimitTitle);
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ValidateModelComplexity(string path, ObjModel model)
        {
            var modelSettings = ModelSettings.Instance;
            string fileName = Path.GetFileName(path);
            int partCount = model.Parts?.Count ?? 0;
            string error = modelSettings.ValidateModelComplexity(fileName, model.Vertices.Length, model.Indices.Length, partCount);
            if (!string.IsNullOrEmpty(error))
            {
                UserNotification.ShowWarning(error, Texts.ResourceLimitTitle);
                return false;
            }
            return true;
        }

        public ObjModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new ObjModel();

            if (!ValidateFileSize(path))
            {
                return new ObjModel();
            }

            var parser = GetParser(path);
            var parserId = parser?.GetType().Name ?? string.Empty;
            var parserVersion = parser != null && _parserVersions.TryGetValue(parser.GetType(), out var v) ? v : 1;
            var fileInfo = new FileInfo(path);

            if (_cache.TryLoad(path, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion, out var cachedModel))
            {
                if (!ValidateModelComplexity(path, cachedModel))
                {
                    return new ObjModel();
                }
                return cachedModel;
            }

            if (parser is IStreamingModelParser streamingParser && streamingParser.SupportsStreaming)
            {
                return LoadWithStreaming(path, streamingParser, parserId, parserVersion, fileInfo);
            }

            var model = parser?.Parse(path) ?? new ObjModel();

            if (model.Vertices.Length > 0)
            {
                if (!ValidateModelComplexity(path, model))
                {
                    return new ObjModel();
                }

                var thumb = ThumbnailUtil.CreateThumbnail(model);
                _cache.Save(path, model, thumb, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion);
                if (thumb.Length > 0)
                {
                    _cache.SaveThumbnailFile(path, thumb);
                }
            }

            return model;
        }

        private ObjModel LoadWithStreaming(string path, IStreamingModelParser streamingParser, string parserId, int parserVersion, FileInfo fileInfo)
        {
            StreamingCacheWriter? writer = null;
            try
            {
                string fileHash = ComputeFileHashForStreaming(path);
                var header = new CacheHeader(fileInfo.LastWriteTimeUtc.ToBinary(), path, parserId, parserVersion, PluginVersion, fileHash);
                var emptyThumb = Array.Empty<byte>();

                writer = (StreamingCacheWriter)_cache.CreateStreamingWriter(path, header, emptyThumb);

                var lightModel = streamingParser.StreamToCache(path, writer);

                _cache.FinalizeStreamingCache(path, writer, lightModel);

                lightModel = null;

                if (!_cache.TryLoad(path, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion, out var fullModel))
                {
                    return LoadWithFallback(path, streamingParser, parserId, parserVersion, fileInfo);
                }

                if (!ValidateModelComplexity(path, fullModel))
                {
                    return new ObjModel();
                }

                try
                {
                    var thumb = ThumbnailUtil.CreateThumbnail(fullModel);
                    if (thumb.Length > 0)
                    {
                        _cache.SaveThumbnailFile(path, thumb);
                    }
                }
                catch { }

                return fullModel;
            }
            catch
            {
                writer?.Rollback();

                return LoadWithFallback(path, streamingParser, parserId, parserVersion, fileInfo);
            }
        }

        private ObjModel LoadWithFallback(string path, IModelParser parser, string parserId, int parserVersion, FileInfo fileInfo)
        {
            try
            {
                var model = parser.Parse(path);
                if (model.Vertices.Length > 0)
                {
                    if (!ValidateModelComplexity(path, model))
                    {
                        return new ObjModel();
                    }

                    var thumb = ThumbnailUtil.CreateThumbnail(model);
                    _cache.Save(path, model, thumb, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion);
                    if (thumb.Length > 0)
                    {
                        _cache.SaveThumbnailFile(path, thumb);
                    }
                }
                return model;
            }
            catch
            {
                return new ObjModel();
            }
        }

        private static string ComputeFileHashForStreaming(string path)
        {
            try
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "");
            }
            catch
            {
                return string.Empty;
            }
        }

        public byte[] GetThumbnail(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Array.Empty<byte>();

            var thumbFile = _cache.LoadThumbnailFile(path);
            if (thumbFile.Length > 0) return thumbFile;

            var parser = GetParser(path);
            var parserId = parser?.GetType().Name ?? string.Empty;
            var parserVersion = parser != null && _parserVersions.TryGetValue(parser.GetType(), out var v) ? v : 1;
            var fileInfo = new FileInfo(path);

            var thumb = _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion);
            if (thumb.Length > 0) return thumb;

            var model = Load(path);
            thumbFile = _cache.LoadThumbnailFile(path);
            if (thumbFile.Length > 0) return thumbFile;

            return _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion);
        }

        public List<byte[]> GetSplitThumbnails(string path)
        {
            var model = Load(path);
            var thumbnails = new List<byte[]>();

            if (model.Parts != null && model.Parts.Count > 0)
            {
                foreach (var part in model.Parts)
                {
                    thumbnails.Add(ThumbnailUtil.CreateThumbnail(model, 256, 256, part.IndexOffset, part.IndexCount));
                }
            }
            else
            {
                thumbnails.Add(ThumbnailUtil.CreateThumbnail(model, 256, 256));
            }

            return thumbnails;
        }

        public List<byte[]> GetPartThumbnails(string path, HashSet<int> partIndices)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || partIndices == null || partIndices.Count == 0)
            {
                return new List<byte[]>();
            }

            var model = Load(path);
            var thumbnails = new List<byte[]>();

            if (model.Parts != null && model.Parts.Count > 0)
            {
                for (int i = 0; i < model.Parts.Count; i++)
                {
                    if (partIndices.Contains(i))
                    {
                        var part = model.Parts[i];
                        thumbnails.Add(ThumbnailUtil.CreateThumbnail(model, 256, 256, part.IndexOffset, part.IndexCount));
                    }
                }
            }
            else if (partIndices.Contains(0))
            {
                thumbnails.Add(ThumbnailUtil.CreateThumbnail(model, 256, 256));
            }

            return thumbnails;
        }
    }
}
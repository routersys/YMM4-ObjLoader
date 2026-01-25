using System.IO;
using System.Reflection;
using ObjLoader.Cache;
using ObjLoader.Core;
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
            return ext.ToLowerInvariant() switch
            {
                ".obj" => settings.AssimpObj,
                ".glb" => settings.AssimpGlb,
                ".gltf" => settings.AssimpGlb,
                ".ply" => settings.AssimpPly,
                ".stl" => settings.AssimpStl,
                ".3mf" => settings.Assimp3mf,
                ".pmx" => settings.AssimpPmx,
                _ => false
            };
        }

        public ObjModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new ObjModel();

            var parser = GetParser(path);
            var parserId = parser?.GetType().Name ?? string.Empty;
            var parserVersion = parser != null && _parserVersions.TryGetValue(parser.GetType(), out var v) ? v : 1;
            var fileInfo = new FileInfo(path);

            if (_cache.TryLoad(path, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion, out var cachedModel))
            {
                return cachedModel;
            }

            var model = parser?.Parse(path) ?? new ObjModel();

            if (model.Vertices.Length > 0)
            {
                var thumb = ThumbnailUtil.CreateThumbnail(model);
                _cache.Save(path, model, thumb, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion);
            }

            return model;
        }

        public byte[] GetThumbnail(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Array.Empty<byte>();

            var parser = GetParser(path);
            var parserId = parser?.GetType().Name ?? string.Empty;
            var parserVersion = parser != null && _parserVersions.TryGetValue(parser.GetType(), out var v) ? v : 1;
            var fileInfo = new FileInfo(path);

            var thumb = _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc, parserId, parserVersion, PluginVersion);
            if (thumb.Length > 0) return thumb;

            var model = Load(path);
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
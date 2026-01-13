using System.IO;
using System.Reflection;
using ObjLoader.Core;
using ObjLoader.Cache;
using ObjLoader.Utilities;
using ObjLoader.Settings;

namespace ObjLoader.Parsers
{
    public class ObjModelLoader
    {
        private const string DefaultPluginVersion = "2.3.0";
        private static readonly string PluginVersion;

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

        private readonly List<IModelParser> _parsers;
        private readonly ModelCache _cache;

        public ObjModelLoader()
        {
            _cache = new ModelCache();
            _parsers = new List<IModelParser>
            {
                new StlParser(),
                new PmxParser(),
                new PmdParser(),
                new GlbParser(),
                new ThreeMfParser(),
                new PlyParser(),
                new WavefrontObjParser(),
                new AssimpParser()
            };
        }

        private IModelParser? GetParser(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            bool forceAssimp = false;
            if (ext == ".obj") forceAssimp = PluginSettings.Instance.AssimpObj;
            else if (ext == ".glb" || ext == ".gltf") forceAssimp = PluginSettings.Instance.AssimpGlb;
            else if (ext == ".ply") forceAssimp = PluginSettings.Instance.AssimpPly;
            else if (ext == ".stl") forceAssimp = PluginSettings.Instance.AssimpStl;
            else if (ext == ".3mf") forceAssimp = PluginSettings.Instance.Assimp3mf;
            else if (ext == ".pmx") forceAssimp = PluginSettings.Instance.AssimpPmx;

            IModelParser? parser = null;
            if (forceAssimp)
            {
                parser = _parsers.OfType<AssimpParser>().FirstOrDefault();
                if (parser != null && !parser.CanParse(ext))
                {
                    parser = null;
                }
            }

            if (parser == null)
            {
                parser = _parsers.FirstOrDefault(p => p.CanParse(ext));
            }

            return parser;
        }

        public ObjModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new ObjModel();

            var parser = GetParser(path);
            var parserId = parser?.GetType().Name ?? string.Empty;

            var fileInfo = new FileInfo(path);
            if (_cache.TryLoad(path, fileInfo.LastWriteTimeUtc, parserId, PluginVersion, out var cachedModel))
            {
                return cachedModel;
            }

            var model = parser?.Parse(path) ?? new ObjModel();

            if (model.Vertices.Length > 0)
            {
                var thumb = ThumbnailUtil.CreateThumbnail(model);
                _cache.Save(path, model, thumb, fileInfo.LastWriteTimeUtc, parserId, PluginVersion);
            }

            return model;
        }

        public byte[] GetThumbnail(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Array.Empty<byte>();

            var parser = GetParser(path);
            var parserId = parser?.GetType().Name ?? string.Empty;

            var fileInfo = new FileInfo(path);
            var thumb = _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc, parserId, PluginVersion);
            if (thumb.Length > 0) return thumb;

            var model = Load(path);
            return _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc, parserId, PluginVersion);
        }
    }
}
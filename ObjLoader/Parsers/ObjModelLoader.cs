using System.IO;
using ObjLoader.Core;
using ObjLoader.Cache;
using ObjLoader.Utilities;

namespace ObjLoader.Parsers
{
    public class ObjModelLoader
    {
        private readonly List<IModelParser> _parsers;
        private readonly ModelCache _cache;

        public ObjModelLoader()
        {
            _cache = new ModelCache();
            _parsers = new List<IModelParser>
            {
                new StlParser(),
                new PmxParser(),
                new GlbParser(),
                new ThreeMfParser(),
                new PlyParser(),
                new WavefrontObjParser(),
                new AssimpParser()
            };
        }

        public ObjModel Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new ObjModel();

            var fileInfo = new FileInfo(path);
            if (_cache.TryLoad(path, fileInfo.LastWriteTimeUtc, out var cachedModel))
            {
                return cachedModel;
            }

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var parser = _parsers.FirstOrDefault(p => p.CanParse(ext));

            var model = parser?.Parse(path) ?? new ObjModel();

            if (model.Vertices.Length > 0)
            {
                var thumb = ThumbnailUtil.CreateThumbnail(model);
                _cache.Save(path, model, thumb, fileInfo.LastWriteTimeUtc);
            }

            return model;
        }

        public byte[] GetThumbnail(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Array.Empty<byte>();

            var fileInfo = new FileInfo(path);
            var thumb = _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc);
            if (thumb.Length > 0) return thumb;

            var model = Load(path);
            return _cache.GetThumbnail(path, fileInfo.LastWriteTimeUtc);
        }
    }
}
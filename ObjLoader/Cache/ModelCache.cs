using System.IO;
using System.Numerics;
using System.Text;
using ObjLoader.Core;
using ObjLoader.Settings;

namespace ObjLoader.Cache
{
    public class ModelCache
    {
        private const int MaxThumbnailSize = 10 * 1024 * 1024;
        private const int MaxTexturePathLength = 32_767;

        public bool TryLoad(string path, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion, out ObjModel model)
        {
            model = new ObjModel();
            var cachePath = path + ".bin";

            if (!File.Exists(cachePath)) return false;

            try
            {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                using var br = new BinaryReader(fs);

                var header = ReadHeader(br);
                if (!header.IsValid(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion)) return false;

                model = ReadBody(br, fs);
                return model.Vertices.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public byte[] GetThumbnail(string path, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion)
        {
            var cachePath = path + ".bin";
            if (!File.Exists(cachePath)) return Array.Empty<byte>();

            try
            {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                using var br = new BinaryReader(fs);

                var header = ReadHeader(br);
                if (!header.IsValid(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion)) return Array.Empty<byte>();

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

        public void Save(string path, ObjModel model, byte[] thumbnail, DateTime originalTimestamp, string parserId, int parserVersion, string pluginVersion)
        {
            var cachePath = path + ".bin";
            var tempPath = cachePath + ".tmp";

            try
            {
                var header = new CacheHeader(originalTimestamp.ToBinary(), path, parserId, parserVersion, pluginVersion);
                WriteCacheFile(tempPath, header, model, thumbnail);

                File.Move(tempPath, cachePath, true);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
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

            return new CacheHeader(signature, version, timestamp, path, parserId, parserVersion, pluginVersion);
        }

        private unsafe ObjModel ReadBody(BinaryReader br, FileStream fs)
        {
            var limits = ModelSettings.Instance;

            int thumbLen = br.ReadInt32();
            if (thumbLen < 0 || thumbLen > MaxThumbnailSize)
                throw new InvalidDataException($"Invalid thumbnail length: {thumbLen}");
            if (thumbLen > 0) fs.Seek(thumbLen, SeekOrigin.Current);

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

            long requiredVertexBytes = (long)vCount * sizeof(ObjVertex);
            long requiredIndexBytes = (long)iCount * sizeof(int);
            long remainingBytes = fs.Length - fs.Position;
            if (remainingBytes < requiredVertexBytes + requiredIndexBytes)
                throw new InvalidDataException($"File too small: need {requiredVertexBytes + requiredIndexBytes} bytes, have {remainingBytes}");

            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);

            fixed (ObjVertex* pV = vertices)
            {
                var span = new Span<byte>(pV, vCount * sizeof(ObjVertex));
                int bytesRead = fs.Read(span);
                if (bytesRead != span.Length)
                    throw new InvalidDataException($"Expected {span.Length} vertex bytes, read {bytesRead}");
            }

            fixed (int* pI = indices)
            {
                var span = new Span<byte>(pI, iCount * sizeof(int));
                int bytesRead = fs.Read(span);
                if (bytesRead != span.Length)
                    throw new InvalidDataException($"Expected {span.Length} index bytes, read {bytesRead}");
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

        private unsafe void WriteCacheFile(string tempPath, CacheHeader header, ObjModel model, byte[] thumbnail)
        {
            var limits = ModelSettings.Instance;

            if (model.Vertices.Length > limits.MaxVertices)
                throw new InvalidOperationException($"Vertex count {model.Vertices.Length} exceeds limit {limits.MaxVertices}");
            if (model.Indices.Length > limits.MaxIndices)
                throw new InvalidOperationException($"Index count {model.Indices.Length} exceeds limit {limits.MaxIndices}");
            if (model.Parts.Count > limits.MaxParts)
                throw new InvalidOperationException($"Part count {model.Parts.Count} exceeds limit {limits.MaxParts}");

            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);

            bw.Write(header.Signature);
            bw.Write(header.Version);
            bw.Write(header.Timestamp);
            bw.Write(header.OriginalPath);
            bw.Write(header.ParserId);
            bw.Write(header.ParserVersion);
            bw.Write(header.PluginVersion);

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
    }
}
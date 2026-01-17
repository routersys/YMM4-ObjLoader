using System.IO;
using System.Numerics;
using System.Text;
using ObjLoader.Core;

namespace ObjLoader.Cache
{
    public class ModelCache
    {
        private const int Signature = 0x4A424F04;
        private const int Version = 4;

        public bool TryLoad(string path, DateTime originalTimestamp, string parserId, string pluginVersion, out ObjModel model)
        {
            model = new ObjModel();
            var cachePath = path + ".bin";

            if (!File.Exists(cachePath)) return false;

            var cacheInfo = new FileInfo(cachePath);
            if (cacheInfo.LastWriteTimeUtc < originalTimestamp) return false;

            try
            {
                model = LoadBinary(cachePath, path, parserId, pluginVersion);
                return model.Vertices.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public byte[] GetThumbnail(string path, DateTime originalTimestamp, string parserId, string pluginVersion)
        {
            var cachePath = path + ".bin";
            if (!File.Exists(cachePath)) return Array.Empty<byte>();

            var cacheInfo = new FileInfo(cachePath);
            if (cacheInfo.LastWriteTimeUtc < originalTimestamp) return Array.Empty<byte>();

            try
            {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                if (br.ReadInt32() != Signature) return Array.Empty<byte>();
                if (br.ReadInt32() != Version) return Array.Empty<byte>();
                br.ReadInt64();

                var storedPath = br.ReadString();
                if (!string.Equals(path, storedPath, StringComparison.OrdinalIgnoreCase)) return Array.Empty<byte>();

                var storedParserId = br.ReadString();
                if (!string.Equals(parserId, storedParserId, StringComparison.Ordinal)) return Array.Empty<byte>();

                var storedPluginVersion = br.ReadString();
                if (!string.Equals(pluginVersion, storedPluginVersion, StringComparison.Ordinal)) return Array.Empty<byte>();

                int thumbLen = br.ReadInt32();
                if (thumbLen > 0)
                {
                    return br.ReadBytes(thumbLen);
                }
            }
            catch { }

            return Array.Empty<byte>();
        }

        public void Save(string path, ObjModel model, byte[] thumbnail, DateTime originalTimestamp, string parserId, string pluginVersion)
        {
            try
            {
                SaveBinary(path + ".bin", path, model, thumbnail, originalTimestamp, parserId, pluginVersion);
            }
            catch { }
        }

        private unsafe void SaveBinary(string cachePath, string originalPath, ObjModel model, byte[] thumbnail, DateTime originalTimestamp, string parserId, string pluginVersion)
        {
            using var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);

            bw.Write(Signature);
            bw.Write(Version);
            bw.Write(originalTimestamp.ToBinary());
            bw.Write(originalPath);
            bw.Write(parserId);
            bw.Write(pluginVersion);

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

        private unsafe ObjModel LoadBinary(string cachePath, string originalPath, string parserId, string pluginVersion)
        {
            using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (br.ReadInt32() != Signature) throw new Exception();
            if (br.ReadInt32() != Version) throw new Exception();
            br.ReadInt64();

            var storedPath = br.ReadString();
            if (!string.Equals(originalPath, storedPath, StringComparison.OrdinalIgnoreCase)) throw new Exception();

            var storedParserId = br.ReadString();
            if (!string.Equals(parserId, storedParserId, StringComparison.Ordinal)) throw new Exception();

            var storedPluginVersion = br.ReadString();
            if (!string.Equals(pluginVersion, storedPluginVersion, StringComparison.Ordinal)) throw new Exception();

            int thumbLen = br.ReadInt32();
            if (thumbLen > 0) fs.Seek(thumbLen, SeekOrigin.Current);

            int vCount = br.ReadInt32();
            int iCount = br.ReadInt32();
            int pCount = br.ReadInt32();

            var parts = new List<ModelPart>(pCount);
            for (int i = 0; i < pCount; i++)
            {
                int tLen = br.ReadInt32();
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
                if (fs.Read(span) != span.Length) throw new Exception();
            }

            fixed (int* pI = indices)
            {
                var span = new Span<byte>(pI, iCount * sizeof(int));
                if (fs.Read(span) != span.Length) throw new Exception();
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
    }
}
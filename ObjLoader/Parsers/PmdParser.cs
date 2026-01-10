using System.IO;
using System.Numerics;
using System.Text;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class PmdParser : IModelParser
    {
        static PmdParser()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public bool CanParse(string extension) => extension.ToLowerInvariant() == ".pmd";

        public unsafe ObjModel Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadBytes(3);
            if (Encoding.ASCII.GetString(magic) != "Pmd") return new ObjModel();

            float ver = br.ReadSingle();

            var encoding = Encoding.GetEncoding(932);

            string name = ReadString(br, 20, encoding);
            string comment = ReadString(br, 256, encoding);

            int vCount = br.ReadInt32();
            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 n = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector2 uv = new Vector2(br.ReadSingle(), br.ReadSingle());

                br.ReadInt16();
                br.ReadInt16();
                br.ReadByte();
                br.ReadByte();

                vertices[i] = new ObjVertex { Position = p, Normal = n, TexCoord = uv };
            }

            int iCount = br.ReadInt32();
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);
            for (int i = 0; i < iCount; i++)
            {
                indices[i] = br.ReadUInt16();
            }

            int mCount = br.ReadInt32();
            var parts = new List<ModelPart>(mCount);
            int indexOffset = 0;

            for (int i = 0; i < mCount; i++)
            {
                Vector4 diff = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 spec = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float specPow = br.ReadSingle();
                Vector3 amb = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                byte toonIdx = br.ReadByte();
                byte edgeFlag = br.ReadByte();
                int faceCount = br.ReadInt32();
                string texPath = ReadString(br, 20, encoding);

                if (!string.IsNullOrEmpty(texPath))
                {
                    if (texPath.Contains('*'))
                    {
                        texPath = texPath.Split('*')[0];
                    }

                    texPath = texPath.Replace('\\', Path.DirectorySeparatorChar);
                    if (!Path.IsPathRooted(texPath))
                        texPath = Path.Combine(Path.GetDirectoryName(path) ?? "", texPath);
                }

                Vector3 partMin = new Vector3(float.MaxValue);
                Vector3 partMax = new Vector3(float.MinValue);

                if (indexOffset + faceCount <= indices.Length)
                {
                    for (int k = 0; k < faceCount; k++)
                    {
                        int vIdx = indices[indexOffset + k];
                        if (vIdx >= 0 && vIdx < vertices.Length)
                        {
                            var p = vertices[vIdx].Position;
                            partMin = Vector3.Min(partMin, p);
                            partMax = Vector3.Max(partMax, p);
                        }
                    }
                }

                parts.Add(new ModelPart
                {
                    TexturePath = texPath,
                    IndexOffset = indexOffset,
                    IndexCount = faceCount,
                    BaseColor = diff,
                    Center = faceCount > 0 ? (partMin + partMax) * 0.5f : Vector3.Zero
                });

                indexOffset += faceCount;
            }

            ModelHelper.CalculateBounds(vertices, out Vector3 c, out float s);
            return new ObjModel { Vertices = vertices, Indices = indices, Parts = parts, ModelCenter = c, ModelScale = s, Name = name, Comment = comment };
        }

        private string ReadString(BinaryReader br, int length, Encoding encoding)
        {
            var bytes = br.ReadBytes(length);
            int zeroIndex = Array.IndexOf(bytes, (byte)0);
            return encoding.GetString(bytes, 0, zeroIndex >= 0 ? zeroIndex : bytes.Length);
        }
    }
}
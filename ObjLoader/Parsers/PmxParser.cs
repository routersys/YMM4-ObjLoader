using System.IO;
using System.Numerics;
using System.Text;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class PmxParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".pmx";

        public unsafe ObjModel Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadBytes(4);
            if (Encoding.ASCII.GetString(magic) != "PMX ") return new ObjModel();

            float ver = br.ReadSingle();
            byte globalCount = br.ReadByte();
            var globals = br.ReadBytes(globalCount);

            Encoding encoding = globals[0] == 0 ? Encoding.Unicode : Encoding.UTF8;
            int addUvCount = globals[1];
            int vertexIdxSize = globals[2];
            int textureIdxSize = globals[3];
            int materialIdxSize = globals[4];
            int boneIdxSize = globals[5];
            int morphIdxSize = globals[6];
            int rigidIdxSize = globals[7];

            int len = br.ReadInt32();
            fs.Seek(len, SeekOrigin.Current);
            len = br.ReadInt32();
            fs.Seek(len, SeekOrigin.Current);
            len = br.ReadInt32();
            fs.Seek(len, SeekOrigin.Current);
            len = br.ReadInt32();
            fs.Seek(len, SeekOrigin.Current);

            int vCount = br.ReadInt32();
            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 n = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector2 uv = new Vector2(br.ReadSingle(), br.ReadSingle());

                byte weightType = br.ReadByte();
                switch (weightType)
                {
                    case 0: fs.Seek(boneIdxSize, SeekOrigin.Current); break;
                    case 1: fs.Seek(boneIdxSize * 2 + 4, SeekOrigin.Current); break;
                    case 2: fs.Seek(boneIdxSize * 4 + 16, SeekOrigin.Current); break;
                    case 3: fs.Seek(boneIdxSize * 2 + 4 + 36, SeekOrigin.Current); break;
                    case 4: fs.Seek(boneIdxSize * 4 + 16, SeekOrigin.Current); break;
                }
                float edge = br.ReadSingle();
                vertices[i] = new ObjVertex { Position = p, Normal = n, TexCoord = uv };
            }

            int iCount = br.ReadInt32();
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);

            if (vertexIdxSize == 1)
            {
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadByte();
            }
            else if (vertexIdxSize == 2)
            {
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadUInt16();
            }
            else
            {
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadInt32();
            }

            int tCount = br.ReadInt32();
            var texturePaths = new string[tCount];
            for (int i = 0; i < tCount; i++)
            {
                len = br.ReadInt32();
                var bytes = br.ReadBytes(len);
                string tPath = encoding.GetString(bytes);
                if (tPath.Contains("*")) tPath = "";
                else
                {
                    tPath = tPath.Replace('\\', Path.DirectorySeparatorChar);
                    if (!Path.IsPathRooted(tPath))
                        tPath = Path.Combine(Path.GetDirectoryName(path) ?? "", tPath);
                }
                texturePaths[i] = tPath;
            }

            int mCount = br.ReadInt32();
            var parts = new List<ModelPart>(mCount);
            int indexOffset = 0;

            for (int i = 0; i < mCount; i++)
            {
                len = br.ReadInt32();
                fs.Seek(len, SeekOrigin.Current);
                len = br.ReadInt32();
                fs.Seek(len, SeekOrigin.Current);

                Vector4 diff = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 spec = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float specPow = br.ReadSingle();
                Vector3 amb = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                byte drawMode = br.ReadByte();
                Vector4 edgeCol = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float edgeSize = br.ReadSingle();

                int texIdx = -1;
                if (textureIdxSize == 1) texIdx = br.ReadSByte();
                else if (textureIdxSize == 2) texIdx = br.ReadInt16();
                else texIdx = br.ReadInt32();

                int sphereIdx = -1;
                if (textureIdxSize == 1) sphereIdx = br.ReadSByte();
                else if (textureIdxSize == 2) sphereIdx = br.ReadInt16();
                else sphereIdx = br.ReadInt32();

                byte sphereMode = br.ReadByte();
                byte sharedToon = br.ReadByte();

                if (sharedToon == 0)
                {
                    if (textureIdxSize == 1) br.ReadSByte();
                    else if (textureIdxSize == 2) br.ReadInt16();
                    else br.ReadInt32();
                }
                else
                {
                    br.ReadByte();
                }

                len = br.ReadInt32();
                fs.Seek(len, SeekOrigin.Current);

                int faceCount = br.ReadInt32();

                string texPath = "";
                if (texIdx >= 0 && texIdx < tCount) texPath = texturePaths[texIdx];

                parts.Add(new ModelPart
                {
                    TexturePath = texPath,
                    IndexOffset = indexOffset,
                    IndexCount = faceCount,
                    BaseColor = diff
                });

                indexOffset += faceCount;
            }

            ModelHelper.CalculateBounds(vertices, out Vector3 c, out float s);
            return new ObjModel { Vertices = vertices, Indices = indices, Parts = parts, ModelCenter = c, ModelScale = s };
        }
    }
}
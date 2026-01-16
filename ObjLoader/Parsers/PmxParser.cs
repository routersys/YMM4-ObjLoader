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

            if (globals.Length < 8)
            {
                var newGlobals = new byte[8];
                Array.Copy(globals, newGlobals, globals.Length);
                globals = newGlobals;
            }

            Encoding encoding = globals[0] == 0 ? Encoding.Unicode : Encoding.UTF8;
            int addUvCount = globals[1];
            int vertexIdxSize = globals[2];
            int textureIdxSize = globals[3];
            int materialIdxSize = globals[4];
            int boneIdxSize = globals[5];
            int morphIdxSize = globals[6];
            int rigidIdxSize = globals[7];

            int len = br.ReadInt32();
            string name = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

            len = br.ReadInt32();
            string nameEn = encoding.GetString(br.ReadBytes(len));

            len = br.ReadInt32();
            string comment = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

            len = br.ReadInt32();
            string commentEn = encoding.GetString(br.ReadBytes(len));

            int vCount = br.ReadInt32();
            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 n = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector2 uv = new Vector2(br.ReadSingle(), br.ReadSingle());

                if (addUvCount > 0)
                {
                    int skip = addUvCount * 16;
                    for (int k = 0; k < skip; k++) br.ReadByte();
                }

                byte weightType = br.ReadByte();
                int weightSkip = 0;
                switch (weightType)
                {
                    case 0: weightSkip = boneIdxSize; break;
                    case 1: weightSkip = boneIdxSize * 2 + 4; break;
                    case 2: weightSkip = boneIdxSize * 4 + 16; break;
                    case 3: weightSkip = boneIdxSize * 2 + 4 + 36; break;
                    case 4: weightSkip = boneIdxSize * 4 + 16; break;
                }

                for (int k = 0; k < weightSkip; k++) br.ReadByte();

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
                var mNameBytes = br.ReadBytes(len);
                string mName = encoding.GetString(mNameBytes).Trim().Replace("\0", "");
                len = br.ReadInt32();
                var mNameEn = br.ReadBytes(len);

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
                var memo = br.ReadBytes(len);

                int faceCount = br.ReadInt32();

                string texPath = "";
                if (texIdx >= 0 && texIdx < tCount) texPath = texturePaths[texIdx];

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
                    Name = mName,
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
    }
}
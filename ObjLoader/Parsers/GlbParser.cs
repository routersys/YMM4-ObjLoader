using System.IO;
using System.Numerics;
using System.Text;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class GlbParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".glb" || extension == ".gltf";

        public unsafe ObjModel Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt32() != 0x46546C67) return new ObjModel();
            br.ReadUInt32();
            br.ReadUInt32();

            int jsonLen = br.ReadInt32();
            if (br.ReadUInt32() != 0x4E4F534A) return new ObjModel();
            var jsonBytes = br.ReadBytes(jsonLen);
            var jsonStr = Encoding.UTF8.GetString(jsonBytes);

            int binLen = br.ReadInt32();
            if (br.ReadUInt32() != 0x004E4942) return new ObjModel();
            var binBytes = br.ReadBytes(binLen);

            int GetJsonInt(string key, int startIdx = 0)
            {
                int idx = jsonStr.IndexOf("\"" + key + "\"", startIdx);
                if (idx == -1) return -1;
                int colon = jsonStr.IndexOf(':', idx);
                int comma = jsonStr.IndexOf(',', colon);
                int brace = jsonStr.IndexOf('}', colon);
                int end = (comma == -1) ? brace : (brace == -1 ? comma : Math.Min(comma, brace));
                string val = jsonStr.Substring(colon + 1, end - colon - 1).Trim();
                int.TryParse(val, out int res);
                return res;
            }

            int posKey = jsonStr.IndexOf("\"POSITION\"");
            if (posKey == -1) return new ObjModel();
            int posAccIdx = GetJsonInt(":", posKey - 1);

            int normKey = jsonStr.IndexOf("\"NORMAL\"");
            int normAccIdx = -1;
            if (normKey != -1) normAccIdx = GetJsonInt(":", normKey - 1);

            int uvKey = jsonStr.IndexOf("\"TEXCOORD_0\"");
            int uvAccIdx = -1;
            if (uvKey != -1) uvAccIdx = GetJsonInt(":", uvKey - 1);

            int indicesKey = jsonStr.IndexOf("\"indices\"", posKey);
            if (indicesKey == -1) indicesKey = jsonStr.LastIndexOf("\"indices\"", posKey);
            int indicesAccIdx = GetJsonInt(":", indicesKey - 1);

            (int bv, int off, int count, int compType) GetAccessorInfo(int accIdx)
            {
                int accArray = jsonStr.IndexOf("\"accessors\"");
                int curr = accArray;
                for (int i = 0; i <= accIdx; i++) curr = jsonStr.IndexOf('{', curr + 1);

                int bv = GetJsonInt("bufferView", curr);
                int off = GetJsonInt("byteOffset", curr);
                if (off == -1) off = 0;
                int count = GetJsonInt("count", curr);
                int ct = GetJsonInt("componentType", curr);
                return (bv, off, count, ct);
            }

            (int off, int len) GetBufferViewInfo(int bvIdx)
            {
                int bvArray = jsonStr.IndexOf("\"bufferViews\"");
                int curr = bvArray;
                for (int i = 0; i <= bvIdx; i++) curr = jsonStr.IndexOf('{', curr + 1);

                int off = GetJsonInt("byteOffset", curr);
                if (off == -1) off = 0;
                int len = GetJsonInt("byteLength", curr);
                return (off, len);
            }

            var posInfo = GetAccessorInfo(posAccIdx);
            var posBv = GetBufferViewInfo(posInfo.bv);

            var indInfo = GetAccessorInfo(indicesAccIdx);
            var indBv = GetBufferViewInfo(indInfo.bv);

            int vCount = posInfo.count;
            int iCount = indInfo.count;

            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);

            fixed (byte* bin = binBytes)
            {
                byte* pBase = bin + posBv.off + posInfo.off;
                for (int i = 0; i < vCount; i++)
                {
                    vertices[i].Position = *(Vector3*)(pBase + i * 12);
                }

                if (normAccIdx != -1)
                {
                    var normInfo = GetAccessorInfo(normAccIdx);
                    var normBv = GetBufferViewInfo(normInfo.bv);
                    byte* nBase = bin + normBv.off + normInfo.off;
                    for (int i = 0; i < vCount; i++) vertices[i].Normal = *(Vector3*)(nBase + i * 12);
                }

                if (uvAccIdx != -1)
                {
                    var uvInfo = GetAccessorInfo(uvAccIdx);
                    var uvBv = GetBufferViewInfo(uvInfo.bv);
                    byte* uBase = bin + uvBv.off + uvInfo.off;
                    for (int i = 0; i < vCount; i++) vertices[i].TexCoord = *(Vector2*)(uBase + i * 8);
                }

                byte* iBase = bin + indBv.off + indInfo.off;
                if (indInfo.compType == 5123)
                {
                    for (int i = 0; i < iCount; i++) indices[i] = *(ushort*)(iBase + i * 2);
                }
                else if (indInfo.compType == 5125)
                {
                    for (int i = 0; i < iCount; i++) indices[i] = *(int*)(iBase + i * 4);
                }
                else
                {
                    for (int i = 0; i < iCount; i++) indices[i] = *(byte*)(iBase + i);
                }
            }

            if (normAccIdx == -1) ModelHelper.CalculateNormals(vertices, indices);
            ModelHelper.CalculateBounds(vertices, out Vector3 c, out float s);
            var parts = new List<ModelPart> { new ModelPart { TexturePath = string.Empty, IndexOffset = 0, IndexCount = indices.Length, BaseColor = Vector4.One } };
            return new ObjModel { Vertices = vertices, Indices = indices, Parts = parts, ModelCenter = c, ModelScale = s };
        }
    }
}
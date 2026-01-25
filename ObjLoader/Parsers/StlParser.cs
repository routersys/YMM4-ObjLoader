using ObjLoader.Attributes;
using ObjLoader.Core;
using System.IO;
using System.Numerics;
using System.Text;

namespace ObjLoader.Parsers
{
    [ModelParser(1, ".stl")]
    public class StlParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".stl";

        public unsafe ObjModel Parse(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 84) return new ObjModel();

            bool isAscii = true;
            for (int i = 0; i < 80 && i < bytes.Length; i++)
            {
                if (bytes[i] == 0) { isAscii = false; break; }
            }

            if (isAscii)
            {
                string start = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 100)).TrimStart();
                if (!start.StartsWith("solid", StringComparison.OrdinalIgnoreCase)) isAscii = false;
            }

            if (isAscii) return ParseAscii(path);

            int count = BitConverter.ToInt32(bytes, 80);
            if (bytes.Length < 84 + count * 50) return new ObjModel();

            int totalV = count * 3;
            var rawPositions = GC.AllocateUninitializedArray<Vector3>(totalV, true);
            var rawNormals = GC.AllocateUninitializedArray<Vector3>(totalV, true);

            fixed (byte* ptr = bytes)
            {
                byte* d = ptr + 84;
                for (int i = 0; i < count; i++)
                {
                    Vector3 n = *(Vector3*)d;
                    d += 12;
                    Vector3 v1 = *(Vector3*)d;
                    d += 12;
                    Vector3 v2 = *(Vector3*)d;
                    d += 12;
                    Vector3 v3 = *(Vector3*)d;
                    d += 12 + 2;

                    int idx = i * 3;
                    rawPositions[idx] = v1; rawPositions[idx + 1] = v2; rawPositions[idx + 2] = v3;
                    rawNormals[idx] = n; rawNormals[idx + 1] = n; rawNormals[idx + 2] = n;
                }
            }

            return ProcessVertices(rawPositions, rawNormals, totalV);
        }

        private ObjModel ParseAscii(string path)
        {
            var rawPositions = new List<Vector3>();
            var rawNormals = new List<Vector3>();

            using (var reader = new StreamReader(path))
            {
                string? line;
                Vector3 currentNormal = Vector3.Zero;

                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    if (parts[0] == "facet" && parts.Length >= 4 && parts[1] == "normal")
                    {
                        float.TryParse(parts[2], out float nx);
                        float.TryParse(parts[3], out float ny);
                        float.TryParse(parts[4], out float nz);
                        currentNormal = new Vector3(nx, ny, nz);
                    }
                    else if (parts[0] == "vertex" && parts.Length >= 4)
                    {
                        float.TryParse(parts[1], out float x);
                        float.TryParse(parts[2], out float y);
                        float.TryParse(parts[3], out float z);
                        rawPositions.Add(new Vector3(x, y, z));
                        rawNormals.Add(currentNormal);
                    }
                }
            }

            return ProcessVertices(rawPositions.ToArray(), rawNormals.ToArray(), rawPositions.Count);
        }

        private ObjModel ProcessVertices(Vector3[] rawPositions, Vector3[] rawNormals, int totalV)
        {
            var pSort = new int[totalV];
            for (int i = 0; i < totalV; i++) pSort[i] = i;

            Array.Sort(pSort, (a, b) => {
                var va = rawPositions[a];
                var vb = rawPositions[b];
                int c = va.X.CompareTo(vb.X);
                if (c != 0) return c;
                c = va.Y.CompareTo(vb.Y);
                if (c != 0) return c;
                return va.Z.CompareTo(vb.Z);
            });

            var vertices = new List<ObjVertex>(totalV);
            var indices = new int[totalV];

            if (totalV > 0)
            {
                int uniqueIdx = 0;
                int currentPIdx = pSort[0];
                var currP = rawPositions[currentPIdx];
                var currN = rawNormals[currentPIdx];
                vertices.Add(new ObjVertex { Position = currP, Normal = currN, TexCoord = Vector2.Zero, Color = Vector4.One });
                indices[currentPIdx] = 0;

                for (int i = 1; i < totalV; i++)
                {
                    int pIdx = pSort[i];
                    var p = rawPositions[pIdx];

                    if (p != currP)
                    {
                        uniqueIdx++;
                        currP = p;
                        currN = rawNormals[pIdx];
                        vertices.Add(new ObjVertex { Position = currP, Normal = currN, TexCoord = Vector2.Zero, Color = Vector4.One });
                    }
                    else
                    {
                        vertices[uniqueIdx] = new ObjVertex { Position = vertices[uniqueIdx].Position, Normal = vertices[uniqueIdx].Normal + rawNormals[pIdx], TexCoord = Vector2.Zero, Color = Vector4.One };
                    }
                    indices[pIdx] = uniqueIdx;
                }

                bool recalcNormals = false;
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (vertices[i].Normal.LengthSquared() < 1e-6f)
                    {
                        recalcNormals = true;
                    }
                    else
                    {
                        vertices[i] = new ObjVertex { Position = vertices[i].Position, Normal = Vector3.Normalize(vertices[i].Normal), TexCoord = Vector2.Zero, Color = Vector4.One };
                    }
                }

                if (recalcNormals)
                {
                    var vArray = vertices.ToArray();
                    ModelHelper.CalculateNormals(vArray, indices);
                    vertices = new List<ObjVertex>(vArray);
                }
            }

            var verts = vertices.ToArray();
            ModelHelper.CalculateBounds(verts, out Vector3 c, out float s);
            var parts = new List<ModelPart> { new ModelPart { TexturePath = string.Empty, IndexOffset = 0, IndexCount = indices.Length, BaseColor = Vector4.One, Center = c } };
            return new ObjModel { Vertices = verts, Indices = indices, Parts = parts, ModelCenter = c, ModelScale = s };
        }
    }
}
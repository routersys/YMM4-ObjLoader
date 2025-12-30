using System.Numerics;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    internal static class ModelHelper
    {
        public static unsafe void CalculateNormals(ObjVertex[] vertices, int[] indices)
        {
            fixed (ObjVertex* pVerts = vertices)
            fixed (int* pInds = indices)
            {
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int i1 = pInds[i];
                    int i2 = pInds[i + 1];
                    int i3 = pInds[i + 2];

                    Vector3 p1 = pVerts[i1].Position;
                    Vector3 p2 = pVerts[i2].Position;
                    Vector3 p3 = pVerts[i3].Position;

                    Vector3 edge1 = p2 - p1;
                    Vector3 edge2 = p3 - p1;
                    Vector3 normal = Vector3.Cross(edge1, edge2);

                    pVerts[i1].Normal += normal;
                    pVerts[i2].Normal += normal;
                    pVerts[i3].Normal += normal;
                }

                int len = vertices.Length;
                for (int i = 0; i < len; i++)
                {
                    var n = pVerts[i].Normal;
                    float lenSq = n.LengthSquared();
                    if (lenSq > 1e-6f)
                    {
                        pVerts[i].Normal = n / MathF.Sqrt(lenSq);
                    }
                }
            }
        }

        public static void CalculateBounds(ObjVertex[] vertices, out Vector3 center, out float scale)
        {
            if (vertices.Length == 0)
            {
                center = Vector3.Zero;
                scale = 1.0f;
                return;
            }

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            if (Vector.IsHardwareAccelerated && vertices.Length >= Vector<float>.Count)
            {
                CalculateBoundsSimd(vertices, ref min, ref max);
            }
            else
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = vertices[i].Position;
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            center = (min + max) * 0.5f;
            Vector3 size = max - min;
            float maxSize = Math.Max(size.X, Math.Max(size.Y, size.Z));
            scale = maxSize > 1e-6f ? 1.5f / maxSize : 1.0f;
        }

        private static unsafe void CalculateBoundsSimd(ObjVertex[] vertices, ref Vector3 min, ref Vector3 max)
        {
            var minX = new Vector<float>(float.MaxValue);
            var minY = new Vector<float>(float.MaxValue);
            var minZ = new Vector<float>(float.MaxValue);
            var maxX = new Vector<float>(float.MinValue);
            var maxY = new Vector<float>(float.MinValue);
            var maxZ = new Vector<float>(float.MinValue);

            int vecSize = Vector<float>.Count;
            int len = vertices.Length;
            int i = 0;

            fixed (ObjVertex* p = vertices)
            {
                byte* ptr = (byte*)p;
                int stride = sizeof(ObjVertex);

                for (; i <= len - vecSize; i += vecSize)
                {
                    for (int j = 0; j < vecSize; j++)
                    {
                        var v = *(ObjVertex*)(ptr + (i + j) * stride);

                        if (v.Position.X < minX[j])
                        {
                            var temp = new float[vecSize];
                            minX.CopyTo(temp);
                            temp[j] = v.Position.X;
                            minX = new Vector<float>(temp);
                        }
                        if (v.Position.Y < minY[j])
                        {
                            var temp = new float[vecSize];
                            minY.CopyTo(temp);
                            temp[j] = v.Position.Y;
                            minY = new Vector<float>(temp);
                        }
                        if (v.Position.Z < minZ[j])
                        {
                            var temp = new float[vecSize];
                            minZ.CopyTo(temp);
                            temp[j] = v.Position.Z;
                            minZ = new Vector<float>(temp);
                        }

                        if (v.Position.X > maxX[j])
                        {
                            var temp = new float[vecSize];
                            maxX.CopyTo(temp);
                            temp[j] = v.Position.X;
                            maxX = new Vector<float>(temp);
                        }
                        if (v.Position.Y > maxY[j])
                        {
                            var temp = new float[vecSize];
                            maxY.CopyTo(temp);
                            temp[j] = v.Position.Y;
                            maxY = new Vector<float>(temp);
                        }
                        if (v.Position.Z > maxZ[j])
                        {
                            var temp = new float[vecSize];
                            maxZ.CopyTo(temp);
                            temp[j] = v.Position.Z;
                            maxZ = new Vector<float>(temp);
                        }
                    }
                }
            }

            for (int k = 0; k < vecSize; k++)
            {
                if (minX[k] < min.X) min.X = minX[k];
                if (minY[k] < min.Y) min.Y = minY[k];
                if (minZ[k] < min.Z) min.Z = minZ[k];
                if (maxX[k] > max.X) max.X = maxX[k];
                if (maxY[k] > max.Y) max.Y = maxY[k];
                if (maxZ[k] > max.Z) max.Z = maxZ[k];
            }

            for (; i < len; i++)
            {
                var p = vertices[i].Position;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
    }
}
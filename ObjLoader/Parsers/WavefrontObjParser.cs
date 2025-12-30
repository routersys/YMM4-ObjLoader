using System.IO;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class WavefrontObjParser : IModelParser
    {
        public bool CanParse(string extension) => string.IsNullOrEmpty(extension) || extension == ".obj";

        public unsafe ObjModel Parse(string path)
        {
            using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            byte* basePointer = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePointer);
            long fileSize = new FileInfo(path).Length;

            int processorCount = Environment.ProcessorCount;
            var chunkBoundaries = new long[processorCount + 1];
            long chunkSize = fileSize / processorCount;
            chunkBoundaries[0] = 0;
            chunkBoundaries[processorCount] = fileSize;

            for (int i = 1; i < processorCount; i++)
            {
                long pos = i * chunkSize;
                while (pos < fileSize && *(basePointer + pos) != '\n') pos++;
                if (pos < fileSize) pos++;
                chunkBoundaries[i] = pos;
            }

            var counts = new Counts[processorCount];

            Parallel.For(0, processorCount, i =>
            {
                counts[i] = CountChunk(basePointer, chunkBoundaries[i], chunkBoundaries[i + 1]);
            });

            var offsets = new Counts[processorCount];
            int totalV = 0, totalVt = 0, totalVn = 0, totalF = 0;

            for (int i = 0; i < processorCount; i++)
            {
                offsets[i].V = totalV;
                offsets[i].Vt = totalVt;
                offsets[i].Vn = totalVn;
                offsets[i].F = totalF;

                totalV += counts[i].V;
                totalVt += counts[i].Vt;
                totalVn += counts[i].Vn;
                totalF += counts[i].F;
            }

            Vector3* rawV = (Vector3*)NativeMemory.Alloc((nuint)totalV, (nuint)sizeof(Vector3));
            Vector2* rawVt = (Vector2*)NativeMemory.Alloc((nuint)(totalVt > 0 ? totalVt : 1), (nuint)sizeof(Vector2));
            Vector3* rawVn = (Vector3*)NativeMemory.Alloc((nuint)(totalVn > 0 ? totalVn : 1), (nuint)sizeof(Vector3));

            var sortArray = GC.AllocateUninitializedArray<SortableVertex>(totalF * 3, true);

            var mtlLibs = new string[processorCount];

            Parallel.For(0, processorCount, i =>
            {
                mtlLibs[i] = ParseChunk(basePointer, chunkBoundaries[i], chunkBoundaries[i + 1],
                    rawV + offsets[i].V,
                    rawVt + offsets[i].Vt,
                    rawVn + offsets[i].Vn,
                    sortArray,
                    offsets[i].F * 3);
            });

            accessor.SafeMemoryMappedViewHandle.ReleasePointer();

            Array.Sort(sortArray);

            int uniqueCount = 0;
            if (sortArray.Length > 0)
            {
                uniqueCount = 1;
                for (int i = 1; i < sortArray.Length; i++)
                {
                    if (sortArray[i].CompareTo(sortArray[i - 1]) != 0)
                    {
                        uniqueCount++;
                    }
                }
            }

            var vertices = GC.AllocateUninitializedArray<ObjVertex>(uniqueCount, true);
            var indices = GC.AllocateUninitializedArray<int>(sortArray.Length, true);

            if (uniqueCount > 0)
            {
                int currentIdx = 0;

                var first = sortArray[0];
                GetVertexData(first.V, first.Vt, first.Vn, totalV, totalVt, totalVn, rawV, rawVt, rawVn, out Vector3 p, out Vector2 uv, out Vector3 n);
                vertices[0] = new ObjVertex { Position = p, TexCoord = uv, Normal = n };
                indices[first.OriginalIndex] = 0;

                for (int i = 1; i < sortArray.Length; i++)
                {
                    var curr = sortArray[i];
                    var prev = sortArray[i - 1];

                    if (curr.CompareTo(prev) != 0)
                    {
                        currentIdx++;
                        GetVertexData(curr.V, curr.Vt, curr.Vn, totalV, totalVt, totalVn, rawV, rawVt, rawVn, out p, out uv, out n);
                        vertices[currentIdx] = new ObjVertex { Position = p, TexCoord = uv, Normal = n };
                    }
                    indices[curr.OriginalIndex] = currentIdx;
                }
            }

            NativeMemory.Free(rawV);
            NativeMemory.Free(rawVt);
            NativeMemory.Free(rawVn);

            if (totalVn == 0 && indices.Length > 0)
            {
                ModelHelper.CalculateNormals(vertices, indices);
            }

            ModelHelper.CalculateBounds(vertices, out Vector3 center, out float scale);

            string texturePath = string.Empty;
            foreach (var lib in mtlLibs)
            {
                if (!string.IsNullOrEmpty(lib))
                {
                    try
                    {
                        string mtlPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, lib);
                        if (File.Exists(mtlPath))
                        {
                            texturePath = ParseMtlForTexture(mtlPath);
                            if (!string.IsNullOrEmpty(texturePath))
                            {
                                if (!Path.IsPathRooted(texturePath))
                                {
                                    texturePath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, texturePath);
                                }
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            var parts = new List<ModelPart> { new ModelPart { TexturePath = texturePath, IndexOffset = 0, IndexCount = indices.Length, BaseColor = Vector4.One } };
            return new ObjModel
            {
                Vertices = vertices,
                Indices = indices,
                Parts = parts,
                ModelCenter = center,
                ModelScale = scale
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void GetVertexData(int vIdx, int vtIdx, int vnIdx, int vCount, int vtCount, int vnCount,
            Vector3* v, Vector2* vt, Vector3* vn, out Vector3 p, out Vector2 uv, out Vector3 n)
        {
            p = Vector3.Zero;
            if (vIdx < 0) vIdx = vCount + vIdx + 1;
            if (vIdx > 0 && vIdx <= vCount) p = v[vIdx - 1];

            uv = Vector2.Zero;
            if (vtIdx < 0) vtIdx = vtCount + vtIdx + 1;
            if (vtIdx > 0 && vtIdx <= vtCount) uv = vt[vtIdx - 1];

            n = Vector3.Zero;
            if (vnIdx < 0) vnIdx = vnCount + vnIdx + 1;
            if (vnIdx > 0 && vnIdx <= vnCount) n = vn[vnIdx - 1];
        }

        private static unsafe Counts CountChunk(byte* start, long startOffset, long endOffset)
        {
            var counts = new Counts();
            byte* ptr = start + startOffset;
            byte* end = start + endOffset;

            while (ptr < end)
            {
                while (ptr < end && *ptr <= 32) ptr++;
                if (ptr >= end) break;

                if (*ptr == '#')
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                    continue;
                }

                byte c1 = *ptr;
                ptr++;

                if (c1 == 'v')
                {
                    byte c2 = *ptr;
                    if (c2 == ' ') counts.V++;
                    else if (c2 == 't') counts.Vt++;
                    else if (c2 == 'n') counts.Vn++;
                    while (ptr < end && *ptr != '\n') ptr++;
                }
                else if (c1 == 'f')
                {
                    if (*ptr <= 32)
                    {
                        int vInFace = 0;
                        while (ptr < end && *ptr != '\n')
                        {
                            while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                            if (ptr >= end || *ptr == '\n') break;
                            vInFace++;
                            while (ptr < end && *ptr != ' ' && *ptr != '\n') ptr++;
                        }
                        if (vInFace >= 3)
                        {
                            counts.F += (vInFace - 2);
                        }
                    }
                    else
                    {
                        while (ptr < end && *ptr != '\n') ptr++;
                    }
                }
                else
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                }
            }
            return counts;
        }

        private static unsafe string ParseChunk(byte* start, long startOffset, long endOffset,
            Vector3* vPtr, Vector2* vtPtr, Vector3* vnPtr,
            SortableVertex[] sortArray, int sortStartIndex)
        {
            string mtlLib = string.Empty;
            byte* ptr = start + startOffset;
            byte* end = start + endOffset;

            Vector3* currV = vPtr;
            Vector2* currVt = vtPtr;
            Vector3* currVn = vnPtr;
            int currentSortIdx = sortStartIndex;

            while (ptr < end)
            {
                while (ptr < end && *ptr <= 32) ptr++;
                if (ptr >= end) break;

                if (*ptr == '#')
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                    continue;
                }

                byte c1 = *ptr;
                ptr++;

                if (c1 == 'v')
                {
                    byte c2 = *ptr;
                    if (c2 == ' ')
                    {
                        *currV++ = new Vector3(ParseFloat(ref ptr, end), ParseFloat(ref ptr, end), ParseFloat(ref ptr, end));
                    }
                    else if (c2 == 't')
                    {
                        ptr++;
                        *currVt++ = new Vector2(ParseFloat(ref ptr, end), 1.0f - ParseFloat(ref ptr, end));
                    }
                    else if (c2 == 'n')
                    {
                        ptr++;
                        *currVn++ = new Vector3(ParseFloat(ref ptr, end), ParseFloat(ref ptr, end), ParseFloat(ref ptr, end));
                    }
                    else
                    {
                        while (ptr < end && *ptr != '\n') ptr++;
                    }
                }
                else if (c1 == 'f')
                {
                    if (*ptr <= 32)
                    {
                        int v1 = 0, vt1 = 0, vn1 = 0;
                        int v2 = 0, vt2 = 0, vn2 = 0;
                        int v3 = 0, vt3 = 0, vn3 = 0;

                        ParseVertexIndex(ref ptr, end, out v1, out vt1, out vn1);
                        ParseVertexIndex(ref ptr, end, out v2, out vt2, out vn2);
                        ParseVertexIndex(ref ptr, end, out v3, out vt3, out vn3);

                        sortArray[currentSortIdx] = new SortableVertex(v1, vt1, vn1, currentSortIdx);
                        currentSortIdx++;
                        sortArray[currentSortIdx] = new SortableVertex(v2, vt2, vn2, currentSortIdx);
                        currentSortIdx++;
                        sortArray[currentSortIdx] = new SortableVertex(v3, vt3, vn3, currentSortIdx);
                        currentSortIdx++;

                        while (true)
                        {
                            while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                            if (ptr >= end || *ptr == '\n') break;

                            v2 = v3; vt2 = vt3; vn2 = vn3;
                            ParseVertexIndex(ref ptr, end, out v3, out vt3, out vn3);

                            sortArray[currentSortIdx] = new SortableVertex(v1, vt1, vn1, currentSortIdx);
                            currentSortIdx++;
                            sortArray[currentSortIdx] = new SortableVertex(v2, vt2, vn2, currentSortIdx);
                            currentSortIdx++;
                            sortArray[currentSortIdx] = new SortableVertex(v3, vt3, vn3, currentSortIdx);
                            currentSortIdx++;
                        }
                    }
                    else
                    {
                        while (ptr < end && *ptr != '\n') ptr++;
                    }
                }
                else if (c1 == 'm')
                {
                    if (IsKeyword(ptr, "tllib"))
                    {
                        ptr += 5;
                        while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
                        var s = ptr;
                        while (ptr < end && *ptr > 32 && *ptr != '\n') ptr++;
                        var len = (int)(ptr - s);
                        if (len > 0) mtlLib = Encoding.UTF8.GetString(s, len);
                    }
                    else
                    {
                        while (ptr < end && *ptr != '\n') ptr++;
                    }
                }
                else
                {
                    while (ptr < end && *ptr != '\n') ptr++;
                }
            }
            return mtlLib;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ParseVertexIndex(ref byte* ptr, byte* end, out int v, out int vt, out int vn)
        {
            while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;

            v = ParseInt(ref ptr, end);
            vt = 0;
            vn = 0;

            if (ptr < end && *ptr == '/')
            {
                ptr++;
                if (ptr < end && *ptr != '/')
                {
                    vt = ParseInt(ref ptr, end);
                }
                if (ptr < end && *ptr == '/')
                {
                    ptr++;
                    vn = ParseInt(ref ptr, end);
                }
            }
        }

        private static unsafe bool IsKeyword(byte* ptr, string keyword)
        {
            for (int i = 0; i < keyword.Length; i++)
            {
                if (*(ptr + i) != keyword[i]) return false;
            }
            return true;
        }

        private static string ParseMtlForTexture(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sr = new StreamReader(fs);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    var trim = line.Trim();
                    if (trim.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trim.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) return parts[1];
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float ParseFloat(ref byte* ptr, byte* end)
        {
            while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
            if (ptr >= end) return 0.0f;

            bool neg = false;
            if (*ptr == '-')
            {
                neg = true;
                ptr++;
            }
            else if (*ptr == '+')
            {
                ptr++;
            }

            long num = 0;
            long div = 1;
            bool decimalFound = false;

            while (ptr < end)
            {
                byte c = *ptr;
                if (c >= '0' && c <= '9')
                {
                    num = num * 10 + (c - '0');
                    if (decimalFound) div *= 10;
                }
                else if (c == '.')
                {
                    decimalFound = true;
                }
                else if (c == 'e' || c == 'E')
                {
                    ptr++;
                    int exp = ParseInt(ref ptr, end);
                    float baseVal = (float)num / div;
                    return neg ? -baseVal * MathF.Pow(10, exp) : baseVal * MathF.Pow(10, exp);
                }
                else
                {
                    break;
                }
                ptr++;
            }

            return neg ? (float)-num / div : (float)num / div;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ParseInt(ref byte* ptr, byte* end)
        {
            while (ptr < end && *ptr <= 32 && *ptr != '\n') ptr++;
            if (ptr >= end) return 0;

            bool neg = false;
            if (*ptr == '-')
            {
                neg = true;
                ptr++;
            }
            else if (*ptr == '+')
            {
                ptr++;
            }

            int num = 0;
            while (ptr < end)
            {
                byte c = *ptr;
                if (c >= '0' && c <= '9')
                {
                    num = num * 10 + (c - '0');
                }
                else
                {
                    break;
                }
                ptr++;
            }

            return neg ? -num : num;
        }
    }
}
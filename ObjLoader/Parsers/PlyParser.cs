using System.Buffers;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class PlyParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".ply";

        public ObjModel Parse(string path)
        {
            if (!File.Exists(path)) return new ObjModel();

            string cachePath = path + ".bin";

            try
            {
                if (File.Exists(cachePath))
                {
                    var plyInfo = new FileInfo(path);
                    var cacheInfo = new FileInfo(cachePath);
                    if (cacheInfo.LastWriteTimeUtc >= plyInfo.LastWriteTimeUtc)
                    {
                        var cached = LoadFromCache(cachePath);
                        if (cached != null) return cached;
                    }
                }
            }
            catch { }

            ObjModel model = new ObjModel();
            bool loaded = false;

            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var reader = new PlyReader(stream);
                model = reader.Read();
                loaded = model.Vertices != null && model.Vertices.Length > 0;
            }
            catch
            {
                loaded = false;
            }

            if (loaded)
            {
                if (model.Parts != null)
                {
                    var dir = Path.GetDirectoryName(path);
                    for (int i = 0; i < model.Parts.Count; i++)
                    {
                        var part = model.Parts[i];
                        if (part.BaseColor.W == 0) part.BaseColor = Vector4.One;

                        if (!string.IsNullOrEmpty(part.TexturePath) && dir != null)
                        {
                            string texPath = Path.Combine(dir, part.TexturePath);
                            if (File.Exists(texPath))
                            {
                                part.TexturePath = texPath;
                            }
                        }
                        model.Parts[i] = part;
                    }
                }

                SaveToCache(cachePath, model);
                return model;
            }

            return new ObjModel();
        }

        private void SaveToCache(string path, ObjModel model)
        {
            try
            {
                using var fs = File.Create(path);
                using var bw = new BinaryWriter(fs);
                bw.Write("PLYCACHE_V5");
                bw.Write(model.ModelCenter.X); bw.Write(model.ModelCenter.Y); bw.Write(model.ModelCenter.Z);
                bw.Write(model.ModelScale);

                int vCount = model.Vertices?.Length ?? 0;
                bw.Write(vCount);
                if (vCount > 0)
                {
                    foreach (var v in model.Vertices!)
                    {
                        bw.Write(v.Position.X); bw.Write(v.Position.Y); bw.Write(v.Position.Z);
                        bw.Write(v.Normal.X); bw.Write(v.Normal.Y); bw.Write(v.Normal.Z);
                        bw.Write(v.TexCoord.X); bw.Write(v.TexCoord.Y);
                        bw.Write(v.Color.X); bw.Write(v.Color.Y); bw.Write(v.Color.Z); bw.Write(v.Color.W);
                    }
                }

                int iCount = model.Indices?.Length ?? 0;
                bw.Write(iCount);
                if (iCount > 0)
                {
                    foreach (var idx in model.Indices!) bw.Write(idx);
                }

                int pCount = model.Parts?.Count ?? 0;
                bw.Write(pCount);
                if (pCount > 0)
                {
                    foreach (var p in model.Parts!)
                    {
                        bw.Write(p.TexturePath ?? "");
                        bw.Write(p.IndexOffset);
                        bw.Write(p.IndexCount);
                        bw.Write(p.BaseColor.X); bw.Write(p.BaseColor.Y); bw.Write(p.BaseColor.Z); bw.Write(p.BaseColor.W);
                    }
                }
            }
            catch { }
        }

        private ObjModel? LoadFromCache(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);
                if (br.ReadString() != "PLYCACHE_V5") return null;

                var center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float scale = br.ReadSingle();

                int vCount = br.ReadInt32();
                var vertices = new ObjVertex[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    vertices[i] = new ObjVertex
                    {
                        Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        Normal = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                        TexCoord = new Vector2(br.ReadSingle(), br.ReadSingle()),
                        Color = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                    };
                }

                int iCount = br.ReadInt32();
                var indices = new int[iCount];
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadInt32();

                int pCount = br.ReadInt32();
                var parts = new List<ModelPart>();
                for (int i = 0; i < pCount; i++)
                {
                    parts.Add(new ModelPart
                    {
                        TexturePath = br.ReadString(),
                        IndexOffset = br.ReadInt32(),
                        IndexCount = br.ReadInt32(),
                        BaseColor = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                    });
                }

                return new ObjModel { Vertices = vertices, Indices = indices, Parts = parts, ModelCenter = center, ModelScale = scale };
            }
            catch { return null; }
        }

        private class PlyReader
        {
            private readonly Stream _stream;
            private readonly BinaryReader _binReader;
            private bool _isBinary;
            private bool _isBigEndian;
            private int _vertexCount;
            private int _faceCount;
            private string _textureFile = "";
            private readonly List<PlyProperty> _vertexProps = new List<PlyProperty>();
            private readonly List<PlyProperty> _faceProps = new List<PlyProperty>();

            public PlyReader(Stream stream)
            {
                _stream = stream;
                _binReader = new BinaryReader(stream);
            }

            public ObjModel Read()
            {
                if (!ParseHeader()) return new ObjModel();

                var vertices = new ObjVertex[_vertexCount];
                var indices = new List<int>(_faceCount * 3);

                try
                {
                    if (_isBinary) ReadBinaryData(vertices, indices);
                    else ReadAsciiData(vertices, indices);
                }
                catch
                {
                }

                int vLength = vertices.Length;
                if (indices.Count > 0 && vLength > 0)
                {
                    var validIndices = new List<int>(indices.Count);
                    for (int i = 0; i < indices.Count; i += 3)
                    {
                        if (i + 2 >= indices.Count) break;
                        int i1 = indices[i];
                        int i2 = indices[i + 1];
                        int i3 = indices[i + 2];

                        if (i1 >= 0 && i1 < vLength && i2 >= 0 && i2 < vLength && i3 >= 0 && i3 < vLength)
                        {
                            validIndices.Add(i1);
                            validIndices.Add(i2);
                            validIndices.Add(i3);
                        }
                    }
                    indices = validIndices;
                }

                bool hasNormals = false;
                for (int i = 0; i < _vertexCount; i++)
                {
                    if (vertices[i].Color.W < 0.001f) vertices[i].Color = new Vector4(vertices[i].Color.X, vertices[i].Color.Y, vertices[i].Color.Z, 1.0f);
                    if (vertices[i].Normal.LengthSquared() > 0.001f) hasNormals = true;
                }

                if (!hasNormals && indices.Count > 0 && vLength > 0)
                {
                    try
                    {
                        ModelHelper.CalculateNormals(vertices, indices.ToArray());
                    }
                    catch { }
                }

                ModelHelper.CalculateBounds(vertices, out Vector3 center, out float scale);

                var parts = new List<ModelPart>
                {
                    new ModelPart
                    {
                        TexturePath = _textureFile,
                        IndexOffset = 0,
                        IndexCount = indices.Count,
                        BaseColor = Vector4.One
                    }
                };

                return new ObjModel
                {
                    Vertices = vertices,
                    Indices = indices.ToArray(),
                    Parts = parts,
                    ModelCenter = center,
                    ModelScale = scale
                };
            }

            private bool ParseHeader()
            {
                _stream.Position = 0;
                string currentElement = "";

                while (true)
                {
                    string line = ReadLineFromStream();
                    if (line == null) break;

                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line == "end_header") return true;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    if (parts[0] == "format")
                    {
                        if (parts.Length >= 2)
                        {
                            if (parts[1].Contains("binary_little_endian")) _isBinary = true;
                            else if (parts[1].Contains("binary_big_endian")) { _isBinary = true; _isBigEndian = true; }
                        }
                    }
                    else if (parts[0] == "comment")
                    {
                        if (parts.Length >= 3 && (parts[1] == "TextureFile" || parts[1] == "Texture"))
                            _textureFile = parts[2].Trim('"');
                    }
                    else if (parts[0] == "element")
                    {
                        if (parts.Length >= 3)
                        {
                            currentElement = parts[1];
                            int.TryParse(parts[2], out int count);
                            if (currentElement == "vertex") _vertexCount = count;
                            else if (currentElement == "face") _faceCount = count;
                        }
                    }
                    else if (parts[0] == "property")
                    {
                        if (currentElement == "vertex") _vertexProps.Add(ParseProperty(parts));
                        else if (currentElement == "face") _faceProps.Add(ParseProperty(parts));
                    }
                }
                return false;
            }

            private string ReadLineFromStream()
            {
                var bytes = new List<byte>();
                int b;
                while ((b = _stream.ReadByte()) != -1)
                {
                    if (b == '\n') break;
                    bytes.Add((byte)b);
                }
                if (bytes.Count == 0 && b == -1) return null!;
                return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            }

            private PlyProperty ParseProperty(string[] parts)
            {
                string name = parts[parts.Length - 1];
                string normalizedName = NormalizePropertyName(name);

                if (parts.Length >= 4 && parts[1] == "list")
                {
                    return new PlyProperty { Name = normalizedName, Type = PlyType.List, CountType = GetType(parts[2]), ItemType = GetType(parts[3]) };
                }
                return new PlyProperty { Name = normalizedName, Type = GetType(parts[1]) };
            }

            private string NormalizePropertyName(string name)
            {
                name = name.ToLowerInvariant();
                if (name == "r" || name == "diffuse_red") return "red";
                if (name == "g" || name == "diffuse_green") return "green";
                if (name == "b" || name == "diffuse_blue") return "blue";
                if (name == "a" || name == "diffuse_alpha" || name == "opacity") return "alpha";
                if (name == "u" || name == "s" || name == "tx" || name == "texture_u") return "u";
                if (name == "v" || name == "t" || name == "ty" || name == "texture_v") return "v";
                return name;
            }

            private PlyType GetType(string typeStr)
            {
                return typeStr switch
                {
                    "char" or "int8" => PlyType.Char,
                    "uchar" or "uint8" => PlyType.UChar,
                    "short" or "int16" => PlyType.Short,
                    "ushort" or "uint16" => PlyType.UShort,
                    "int" or "int32" => PlyType.Int,
                    "uint" or "uint32" => PlyType.UInt,
                    "float" or "float32" => PlyType.Float,
                    "double" or "float64" => PlyType.Double,
                    _ => PlyType.Float,
                };
            }

            private void ReadAsciiData(ObjVertex[] vertices, List<int> indices)
            {
                using var reader = new StreamReader(_stream, Encoding.ASCII, false, 65536, true);

                int readV = 0;
                while (readV < _vertexCount)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var span = line.AsSpan();
                        int pIdx = 0;
                        Vector3 pos = Vector3.Zero;
                        Vector3 norm = Vector3.Zero;
                        Vector2 uv = Vector2.Zero;
                        Vector4 col = Vector4.One;
                        float r = 1, g = 1, b = 1, a = 1;
                        bool hasColor = false;

                        foreach (var prop in _vertexProps)
                        {
                            span = TrimLeft(span);
                            int end = span.IndexOfAny(' ', '\t');
                            var valSpan = end == -1 ? span : span.Slice(0, end);
                            if (valSpan.Length > 0)
                            {
                                float val = FastParseFloat(valSpan);
                                switch (prop.Name)
                                {
                                    case "x": pos.X = val; break;
                                    case "y": pos.Y = val; break;
                                    case "z": pos.Z = val; break;
                                    case "nx": norm.X = val; break;
                                    case "ny": norm.Y = val; break;
                                    case "nz": norm.Z = val; break;
                                    case "u": uv.X = val; break;
                                    case "v": uv.Y = val; break;
                                    case "red": r = val / 255.0f; hasColor = true; break;
                                    case "green": g = val / 255.0f; hasColor = true; break;
                                    case "blue": b = val / 255.0f; hasColor = true; break;
                                    case "alpha": a = val / 255.0f; hasColor = true; break;
                                }
                            }
                            if (end == -1) break;
                            span = span.Slice(end + 1);
                            pIdx++;
                        }
                        if (hasColor) col = new Vector4(r, g, b, a);
                        vertices[readV] = new ObjVertex { Position = pos, Normal = norm, TexCoord = uv, Color = col };
                        readV++;
                    }
                    catch { }
                }

                int readF = 0;
                while (readF < _faceCount)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var span = line.AsSpan();
                        bool processed = false;
                        foreach (var prop in _faceProps)
                        {
                            if (prop.Type == PlyType.List)
                            {
                                span = TrimLeft(span);
                                int end = span.IndexOfAny(' ', '\t');
                                var countSpan = end == -1 ? span : span.Slice(0, end);
                                if (countSpan.Length > 0 && int.TryParse(countSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                                {
                                    if (end != -1) span = span.Slice(end + 1);
                                    int v0 = 0, vPrev = 0;
                                    for (int k = 0; k < count; k++)
                                    {
                                        span = TrimLeft(span);
                                        end = span.IndexOfAny(' ', '\t');
                                        var idxSpan = end == -1 ? span : span.Slice(0, end);
                                        int vIdx = int.Parse(idxSpan, NumberStyles.Integer, CultureInfo.InvariantCulture);
                                        if (k == 0) v0 = vIdx;
                                        else if (k >= 2) { indices.Add(v0); indices.Add(vPrev); indices.Add(vIdx); }
                                        vPrev = vIdx;
                                        if (end == -1) break;
                                        span = span.Slice(end + 1);
                                    }
                                    processed = true;
                                }
                            }
                            else
                            {
                                span = TrimLeft(span);
                                int end = span.IndexOfAny(' ', '\t');
                                if (end != -1) span = span.Slice(end + 1);
                                else span = ReadOnlySpan<char>.Empty;
                            }
                        }
                        if (processed) readF++;
                    }
                    catch { }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ReadOnlySpan<char> TrimLeft(ReadOnlySpan<char> span)
            {
                int start = 0;
                while (start < span.Length && char.IsWhiteSpace(span[start])) start++;
                return span.Slice(start);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float FastParseFloat(ReadOnlySpan<char> span)
            {
                float.TryParse(span, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float result);
                return result;
            }

            private void ReadBinaryData(ObjVertex[] vertices, List<int> indices)
            {
                for (int i = 0; i < _vertexCount; i++)
                {
                    Vector3 pos = Vector3.Zero;
                    Vector3 norm = Vector3.Zero;
                    Vector2 uv = Vector2.Zero;
                    Vector4 col = Vector4.One;
                    float r = 1, g = 1, b = 1, a = 1;
                    bool hasColor = false;

                    foreach (var prop in _vertexProps)
                    {
                        double val = ReadBinaryValue(prop.Type);
                        switch (prop.Name)
                        {
                            case "x": pos.X = (float)val; break;
                            case "y": pos.Y = (float)val; break;
                            case "z": pos.Z = (float)val; break;
                            case "nx": norm.X = (float)val; break;
                            case "ny": norm.Y = (float)val; break;
                            case "nz": norm.Z = (float)val; break;
                            case "u": uv.X = (float)val; break;
                            case "v": uv.Y = (float)val; break;
                            case "red": r = (float)val / 255.0f; hasColor = true; break;
                            case "green": g = (float)val / 255.0f; hasColor = true; break;
                            case "blue": b = (float)val / 255.0f; hasColor = true; break;
                            case "alpha": a = (float)val / 255.0f; hasColor = true; break;
                        }
                    }
                    if (hasColor) col = new Vector4(r, g, b, a);
                    vertices[i] = new ObjVertex { Position = pos, Normal = norm, TexCoord = uv, Color = col };
                }

                for (int i = 0; i < _faceCount; i++)
                {
                    foreach (var prop in _faceProps)
                    {
                        if (prop.Type == PlyType.List)
                        {
                            int count = (int)ReadBinaryValue(prop.CountType);
                            int v0 = 0, vPrev = 0;
                            for (int k = 0; k < count; k++)
                            {
                                int vIdx = (int)ReadBinaryValue(prop.ItemType);
                                if (k == 0) v0 = vIdx;
                                else if (k >= 2) { indices.Add(v0); indices.Add(vPrev); indices.Add(vIdx); }
                                vPrev = vIdx;
                            }
                        }
                        else
                        {
                            ReadBinaryValue(prop.Type);
                        }
                    }
                }
            }

            private double ReadBinaryValue(PlyType type)
            {
                switch (type)
                {
                    case PlyType.Char: return _binReader.ReadSByte();
                    case PlyType.UChar: return _binReader.ReadByte();
                    case PlyType.Short: return ReadInt16();
                    case PlyType.UShort: return ReadUInt16();
                    case PlyType.Int: return ReadInt32();
                    case PlyType.UInt: return ReadUInt32();
                    case PlyType.Float: return ReadSingle();
                    case PlyType.Double: return ReadDouble();
                    default: return 0;
                }
            }

            private short ReadInt16()
            {
                var val = _binReader.ReadInt16();
                return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
            }

            private ushort ReadUInt16()
            {
                var val = _binReader.ReadUInt16();
                return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
            }

            private int ReadInt32()
            {
                var val = _binReader.ReadInt32();
                return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
            }

            private uint ReadUInt32()
            {
                var val = _binReader.ReadUInt32();
                return _isBigEndian ? System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(val) : val;
            }

            private float ReadSingle()
            {
                var bytes = _binReader.ReadBytes(4);
                if (_isBigEndian) Array.Reverse(bytes);
                return BitConverter.ToSingle(bytes, 0);
            }

            private double ReadDouble()
            {
                var bytes = _binReader.ReadBytes(8);
                if (_isBigEndian) Array.Reverse(bytes);
                return BitConverter.ToDouble(bytes, 0);
            }

            private enum PlyType { Char, UChar, Short, UShort, Int, UInt, Float, Double, List }

            private class PlyProperty { public string Name = ""; public PlyType Type; public PlyType CountType; public PlyType ItemType; }
        }
    }
}
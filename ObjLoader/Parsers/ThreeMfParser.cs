using System.IO.Compression;
using System.Numerics;
using System.Xml;
using ObjLoader.Core;

namespace ObjLoader.Parsers
{
    public class ThreeMfParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".3mf";

        public ObjModel Parse(string path)
        {
            try
            {
                using var archive = ZipFile.OpenRead(path);
                var modelEntry = archive.GetEntry("3D/3dmodel.model");
                if (modelEntry == null) return new ObjModel();

                using var stream = modelEntry.Open();
                using var reader = XmlReader.Create(stream);

                var verts = new List<ObjVertex>();
                var inds = new List<int>();

                var colorMap = new Dictionary<string, Vector4>();
                string currentPid = "";

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.LocalName == "vertex")
                        {
                            float x = float.Parse(reader.GetAttribute("x") ?? "0");
                            float y = float.Parse(reader.GetAttribute("y") ?? "0");
                            float z = float.Parse(reader.GetAttribute("z") ?? "0");
                            verts.Add(new ObjVertex { Position = new Vector3(x, y, z), Color = Vector4.One });
                        }
                        else if (reader.LocalName == "triangle")
                        {
                            int v1 = int.Parse(reader.GetAttribute("v1") ?? "0");
                            int v2 = int.Parse(reader.GetAttribute("v2") ?? "0");
                            int v3 = int.Parse(reader.GetAttribute("v3") ?? "0");

                            inds.Add(v1);
                            inds.Add(v2);
                            inds.Add(v3);

                            string pid = reader.GetAttribute("pid") ?? "";
                            string p1 = reader.GetAttribute("p1") ?? "";

                            if (!string.IsNullOrEmpty(pid) && !string.IsNullOrEmpty(p1))
                            {
                                string key = pid + ":" + p1;
                                if (colorMap.TryGetValue(key, out var col))
                                {
                                    if (v1 < verts.Count) { var v = verts[v1]; v.Color = col; verts[v1] = v; }
                                    if (v2 < verts.Count) { var v = verts[v2]; v.Color = col; verts[v2] = v; }
                                    if (v3 < verts.Count) { var v = verts[v3]; v.Color = col; verts[v3] = v; }
                                }
                            }
                        }
                        else if (reader.LocalName == "base")
                        {
                            string val = reader.GetAttribute("displaycolor") ?? "#FFFFFFFF";
                            if (ParseColor(val, out var col))
                            {
                                if (!string.IsNullOrEmpty(currentPid))
                                {
                                    colorMap[currentPid + ":" + colorMap.Count] = col;
                                }
                            }
                        }
                        else if (reader.LocalName == "basematerials")
                        {
                            currentPid = reader.GetAttribute("id") ?? "";
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.LocalName == "basematerials")
                        {
                            currentPid = "";
                        }
                    }
                }

                var vArray = verts.ToArray();
                var iArray = inds.ToArray();
                ModelHelper.CalculateNormals(vArray, iArray);
                ModelHelper.CalculateBounds(vArray, out Vector3 c, out float s);
                var parts = new List<ModelPart> { new ModelPart { TexturePath = string.Empty, IndexOffset = 0, IndexCount = iArray.Length, BaseColor = Vector4.One } };
                return new ObjModel { Vertices = vArray, Indices = iArray, Parts = parts, ModelCenter = c, ModelScale = s };
            }
            catch
            {
                return new ObjModel();
            }
        }

        private bool ParseColor(string hex, out Vector4 color)
        {
            color = Vector4.One;
            if (string.IsNullOrEmpty(hex) || hex.Length < 7) return false;

            try
            {
                int start = hex.StartsWith("#") ? 1 : 0;

                if (hex.Length == 9)
                {
                    byte r = Convert.ToByte(hex.Substring(start, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(start + 2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(start + 4, 2), 16);
                    byte a = Convert.ToByte(hex.Substring(start + 6, 2), 16);
                    color = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
                    return true;
                }
                else if (hex.Length == 7)
                {
                    byte r = Convert.ToByte(hex.Substring(start, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(start + 2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(start + 4, 2), 16);
                    color = new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, 1.0f);
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
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

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "vertex")
                        {
                            float x = float.Parse(reader.GetAttribute("x") ?? "0");
                            float y = float.Parse(reader.GetAttribute("y") ?? "0");
                            float z = float.Parse(reader.GetAttribute("z") ?? "0");
                            verts.Add(new ObjVertex { Position = new Vector3(x, y, z) });
                        }
                        else if (reader.Name == "triangle")
                        {
                            inds.Add(int.Parse(reader.GetAttribute("v1") ?? "0"));
                            inds.Add(int.Parse(reader.GetAttribute("v2") ?? "0"));
                            inds.Add(int.Parse(reader.GetAttribute("v3") ?? "0"));
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
    }
}
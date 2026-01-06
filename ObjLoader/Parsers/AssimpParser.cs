using System.IO;
using Assimp;
using ObjLoader.Core;
using System.Numerics;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Parsers
{
    public class AssimpParser : IModelParser
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".blend", ".dae", ".fbx", ".x", ".3ds", ".dxf", ".ifc", ".ase", ".ac", ".ms3d", ".cob", ".scn", ".bvh",
            ".csm", ".xml", ".irrmesh", ".irr", ".mdl", ".md2", ".md3", ".pk3", ".mdc", ".md5mesh", ".smd", ".vta",
            ".ogex", ".3d", ".b3d", ".q3d", ".q3s", ".nff", ".off", ".raw", ".ter", ".hmp", ".ndo", ".lwo", ".lws",
            ".lxo", ".xgl", ".zgl"
        };

        public bool CanParse(string extension)
        {
            return SupportedExtensions.Contains(extension);
        }

        public ObjModel Parse(string path)
        {
            if (!File.Exists(path)) return new ObjModel();

            using var context = new AssimpContext();
            try
            {
                var steps = PostProcessSteps.Triangulate |
                           PostProcessSteps.GenerateNormals |
                           PostProcessSteps.FlipUVs |
                           PostProcessSteps.CalculateTangentSpace |
                           PostProcessSteps.MakeLeftHanded |
                           PostProcessSteps.FlipWindingOrder |
                           PostProcessSteps.GlobalScale;

                var scene = context.ImportFile(path, steps);
                if (scene == null || !scene.HasMeshes) return new ObjModel();

                var vertices = new List<ObjVertex>();
                var indices = new List<int>();
                var parts = new List<ModelPart>();

                var modelDir = Path.GetDirectoryName(path) ?? string.Empty;

                ProcessNode(scene.RootNode, Matrix4x4.Identity, scene, vertices, indices, parts, modelDir);

                var verticesArr = vertices.ToArray();
                var indicesArr = indices.ToArray();

                ModelHelper.CalculateBounds(verticesArr, out Vector3 center, out float scale);

                return new ObjModel
                {
                    Vertices = verticesArr,
                    Indices = indicesArr,
                    Parts = parts,
                    ModelCenter = center,
                    ModelScale = scale
                };
            }
            catch
            {
                return new ObjModel();
            }
        }

        private void ProcessNode(Node node, Matrix4x4 parentTransform, Scene scene, List<ObjVertex> vertices, List<int> indices, List<ModelPart> parts, string modelDir)
        {
            var localTransform = ToNumerics(node.Transform);
            var globalTransform = localTransform * parentTransform;

            if (node.HasMeshes)
            {
                foreach (var meshIndex in node.MeshIndices)
                {
                    var mesh = scene.Meshes[meshIndex];
                    int vertexOffset = vertices.Count;

                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        var vRaw = mesh.Vertices[i];
                        var nRaw = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 0, 0);
                        var tRaw = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : new Vector3D(0, 0, 0);
                        var cRaw = mesh.HasVertexColors(0) ? mesh.VertexColorChannels[0][i] : new Color4D(1, 1, 1, 1);

                        var pos = Vector3.Transform(new Vector3(vRaw.X, vRaw.Y, vRaw.Z), globalTransform);
                        var normal = Vector3.TransformNormal(new Vector3(nRaw.X, nRaw.Y, nRaw.Z), globalTransform);

                        vertices.Add(new ObjVertex
                        {
                            Position = pos,
                            Normal = normal,
                            TexCoord = new Vector2(tRaw.X, tRaw.Y),
                            Color = new Vector4(cRaw.R, cRaw.G, cRaw.B, cRaw.A)
                        });
                    }

                    int startIndex = indices.Count;
                    var partIndices = mesh.GetIndices();
                    for (int i = 0; i < partIndices.Length; i++)
                    {
                        indices.Add(partIndices[i] + vertexOffset);
                    }

                    string texPath = string.Empty;
                    Vector4 baseColor = Vector4.One;
                    float metallic = 0.0f;
                    float roughness = 1.0f;

                    if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
                    {
                        var mat = scene.Materials[mesh.MaterialIndex];

                        if (mat.HasColorDiffuse)
                        {
                            var md = mat.ColorDiffuse;
                            if (md.R > 0 || md.G > 0 || md.B > 0)
                            {
                                baseColor = new Vector4(md.R, md.G, md.B, md.A);
                            }
                        }

                        if (mat.HasTextureDiffuse)
                        {
                            var slot = mat.TextureDiffuse;
                            var rawPath = slot.FilePath;

                            if (!string.IsNullOrEmpty(rawPath))
                            {
                                if (rawPath.StartsWith("*"))
                                {
                                    if (int.TryParse(rawPath.Substring(1), out int texIndex) && texIndex >= 0 && texIndex < scene.TextureCount)
                                    {
                                        var embeddedTex = scene.Textures[texIndex];
                                        var ext = ".png";
                                        if (!string.IsNullOrEmpty(embeddedTex.CompressedFormatHint))
                                        {
                                            ext = "." + embeddedTex.CompressedFormatHint;
                                        }

                                        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{ext}");

                                        if (embeddedTex.IsCompressed)
                                        {
                                            File.WriteAllBytes(tempPath, embeddedTex.CompressedData);
                                            texPath = tempPath;
                                        }
                                    }
                                }
                                else
                                {
                                    var cleanPath = rawPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                                    var fileName = Path.GetFileName(cleanPath);

                                    var candidates = new List<string>
                                    {
                                        rawPath,
                                        cleanPath
                                    };

                                    if (!Path.IsPathRooted(cleanPath))
                                    {
                                        candidates.Add(Path.Combine(modelDir, cleanPath));
                                    }

                                    candidates.Add(Path.Combine(modelDir, fileName));
                                    candidates.Add(Path.Combine(modelDir, "textures", fileName));
                                    candidates.Add(Path.Combine(modelDir, "Textures", fileName));
                                    candidates.Add(Path.Combine(modelDir, "texture", fileName));
                                    candidates.Add(Path.Combine(modelDir, "Texture", fileName));

                                    foreach (var p in candidates)
                                    {
                                        if (File.Exists(p))
                                        {
                                            texPath = p;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    parts.Add(new ModelPart
                    {
                        IndexOffset = startIndex,
                        IndexCount = indices.Count - startIndex,
                        BaseColor = baseColor,
                        TexturePath = texPath,
                        Metallic = metallic,
                        Roughness = roughness,
                        Center = vertices.Count > 0 ? vertices[vertexOffset].Position : Vector3.Zero
                    });
                }
            }

            foreach (var child in node.Children)
            {
                ProcessNode(child, globalTransform, scene, vertices, indices, parts, modelDir);
            }
        }

        private Matrix4x4 ToNumerics(Assimp.Matrix4x4 m)
        {
            return new Matrix4x4(m.A1, m.A2, m.A3, m.A4,
                                 m.B1, m.B2, m.B3, m.B4,
                                 m.C1, m.C2, m.C3, m.C4,
                                 m.D1, m.D2, m.D3, m.D4);
        }
    }
}
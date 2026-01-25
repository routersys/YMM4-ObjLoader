using Assimp;
using ObjLoader.Attributes;
using ObjLoader.Core;
using System.IO;
using System.Numerics;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Parsers
{
    [ModelParser(1, ".3d", ".3ds", ".3mf", ".ac", ".ac3d", ".acc", ".amj", ".ase", ".ask", ".b3d", ".blend", ".bvh", ".cms", ".cob", ".dae", ".dxf", ".enff", ".fbx", ".glb", ".gltf", ".hmb", ".ifc", ".irr", ".irrmesh", ".lwo", ".lws", ".lxo", ".md2", ".md3", ".md5", ".mdc", ".mdl", ".mesh", ".mot", ".ms3d", ".ndo", ".nff", ".obj", ".off", ".ogex", ".ply", ".pmx", ".prj", ".q3o", ".q3s", ".raw", ".scn", ".sib", ".smd", ".stl", ".stp", ".ter", ".uc", ".vta", ".x", ".x3d", ".xgl", ".xml", ".zgl")]
    public class AssimpParser : IModelParser
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".blend", ".dae", ".fbx", ".x", ".3ds", ".dxf", ".ifc", ".ase", ".ac", ".ms3d", ".cob", ".scn", ".bvh",
            ".csm", ".xml", ".irrmesh", ".irr", ".mdl", ".md2", ".md3", ".pk3", ".mdc", ".md5mesh", ".smd", ".vta",
            ".ogex", ".3d", ".b3d", ".q3d", ".q3s", ".nff", ".off", ".raw", ".ter", ".hmp", ".ndo", ".lwo", ".lws",
            ".lxo", ".xgl", ".zgl",
            ".obj", ".glb", ".gltf", ".ply", ".stl", ".3mf", ".pmx"
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
                           PostProcessSteps.GlobalScale |
                           PostProcessSteps.ValidateDataStructure;

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

                    int uvChannelIndex = 0;
                    for (int c = 0; c < mesh.TextureCoordinateChannelCount; c++)
                    {
                        if (mesh.HasTextureCoords(c))
                        {
                            uvChannelIndex = c;
                            break;
                        }
                    }

                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        var vRaw = mesh.Vertices[i];
                        var nRaw = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 0, 0);
                        var tRaw = mesh.HasTextureCoords(uvChannelIndex) ? mesh.TextureCoordinateChannels[uvChannelIndex][i] : new Vector3D(0, 0, 0);
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
                            baseColor = new Vector4(md.R, md.G, md.B, md.A);
                        }

                        var texTypes = new[] { TextureType.Diffuse, (TextureType)12, TextureType.Unknown, TextureType.Emissive };
                        foreach (var type in texTypes)
                        {
                            if (mat.GetMaterialTextureCount(type) > 0)
                            {
                                if (mat.GetMaterialTexture(type, 0, out var slot))
                                {
                                    texPath = FindTexture(scene, slot, modelDir);
                                    if (!string.IsNullOrEmpty(texPath)) break;
                                }
                            }
                        }
                    }

                    var partMin = new Vector3(float.MaxValue);
                    var partMax = new Vector3(float.MinValue);
                    bool hasVerts = false;

                    for (int k = startIndex; k < indices.Count; k++)
                    {
                        var vIdx = indices[k];
                        if (vIdx >= 0 && vIdx < vertices.Count)
                        {
                            var p = vertices[vIdx].Position;
                            partMin = Vector3.Min(partMin, p);
                            partMax = Vector3.Max(partMax, p);
                            hasVerts = true;
                        }
                    }

                    var partName = mesh.Name;
                    if (string.IsNullOrEmpty(partName)) partName = node.Name;

                    parts.Add(new ModelPart
                    {
                        Name = partName,
                        IndexOffset = startIndex,
                        IndexCount = indices.Count - startIndex,
                        BaseColor = baseColor,
                        TexturePath = texPath,
                        Metallic = metallic,
                        Roughness = roughness,
                        Center = hasVerts ? (partMin + partMax) * 0.5f : Vector3.Zero
                    });
                }
            }

            foreach (var child in node.Children)
            {
                ProcessNode(child, globalTransform, scene, vertices, indices, parts, modelDir);
            }
        }

        private string FindTexture(Scene scene, TextureSlot slot, string modelDir)
        {
            var rawPath = slot.FilePath;
            if (string.IsNullOrEmpty(rawPath)) return string.Empty;

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
                        return tempPath;
                    }
                    else
                    {

                    }
                }
                return string.Empty;
            }

            var cleanPath = rawPath;
            try
            {
                cleanPath = Uri.UnescapeDataString(rawPath);
            }
            catch { }

            if (cleanPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                cleanPath = cleanPath.Substring(7);
                if (Path.DirectorySeparatorChar == '\\' && cleanPath.StartsWith("/") && cleanPath.Length > 2 && cleanPath[2] == ':')
                {
                    cleanPath = cleanPath.Substring(1);
                }
            }

            cleanPath = cleanPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            var candidates = new List<string>();
            candidates.Add(cleanPath);
            if (rawPath != cleanPath) candidates.Add(rawPath);

            var fileName = Path.GetFileName(cleanPath);

            if (!string.IsNullOrEmpty(modelDir))
            {
                try { candidates.Add(Path.Combine(modelDir, cleanPath)); } catch { }

                if (!string.IsNullOrEmpty(fileName))
                {
                    candidates.Add(Path.Combine(modelDir, fileName));

                    var subDirs = new[] { "textures", "Textures", "images", "Images", "texture", "Texture", "tex", "Tex" };
                    foreach (var sub in subDirs)
                    {
                        candidates.Add(Path.Combine(modelDir, sub, fileName));
                    }

                    var parentDir = Directory.GetParent(modelDir)?.FullName;
                    if (parentDir != null)
                    {
                        candidates.Add(Path.Combine(parentDir, fileName));
                        foreach (var sub in subDirs)
                        {
                            candidates.Add(Path.Combine(parentDir, sub, fileName));
                        }
                    }
                }
            }

            foreach (var p in candidates)
            {
                if (File.Exists(p)) return p;
            }

            if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(modelDir))
            {
                var noExt = Path.GetFileNameWithoutExtension(fileName);
                var altExts = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tif", ".tiff", ".dds" };

                foreach (var ext in altExts)
                {
                    var newName = noExt + ext;
                    var tryPath = Path.Combine(modelDir, newName);
                    if (File.Exists(tryPath)) return tryPath;
                }
            }

            return string.Empty;
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
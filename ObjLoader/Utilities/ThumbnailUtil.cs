using ObjLoader.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ObjLoader.Utilities
{
    public static class ThumbnailUtil
    {
        public static byte[] CreateThumbnail(ObjModel model, int width = 64, int height = 64, int indexOffset = 0, int indexCount = -1, HashSet<int>? visiblePartIndices = null)
        {
            if (model.Vertices.Length == 0) return Array.Empty<byte>();

            byte[] result = Array.Empty<byte>();

            void Generate()
            {
                try
                {
                    var group = new Model3DGroup();

                    var light = new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1));
                    group.Children.Add(light);
                    group.Children.Add(new AmbientLight(Color.FromRgb(50, 50, 50)));

                    int targetStart = indexOffset;
                    int targetCount = indexCount == -1 ? model.Indices.Length : indexCount;
                    int targetEnd = targetStart + targetCount;

                    if (targetEnd > model.Indices.Length) targetEnd = model.Indices.Length;
                    if (targetStart >= targetEnd) return;

                    var min = new Vector3D(double.MaxValue, double.MaxValue, double.MaxValue);
                    var max = new Vector3D(double.MinValue, double.MinValue, double.MinValue);
                    bool hasVertices = false;

                    var parts = model.Parts != null && model.Parts.Count > 0
                        ? model.Parts
                        : new List<ModelPart> { new ModelPart { IndexOffset = 0, IndexCount = model.Indices.Length, BaseColor = System.Numerics.Vector4.One } };

                    for (int i = 0; i < parts.Count; i++)
                    {
                        if (visiblePartIndices != null && !visiblePartIndices.Contains(i)) continue;

                        var part = parts[i];
                        int pStart = part.IndexOffset;
                        int pEnd = pStart + part.IndexCount;

                        int iStart = Math.Max(targetStart, pStart);
                        int iEnd = Math.Min(targetEnd, pEnd);

                        if (iStart < iEnd)
                        {
                            var mesh = new MeshGeometry3D();
                            var vertexMap = new Dictionary<int, int>();

                            for (int j = iStart; j < iEnd; j++)
                            {
                                int originalIndex = model.Indices[j];

                                if (!vertexMap.TryGetValue(originalIndex, out int newIndex))
                                {
                                    newIndex = mesh.Positions.Count;
                                    vertexMap[originalIndex] = newIndex;

                                    var v = model.Vertices[originalIndex];
                                    var p = new Point3D(v.Position.X, v.Position.Y, v.Position.Z);
                                    mesh.Positions.Add(p);
                                    mesh.Normals.Add(new Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
                                    mesh.TextureCoordinates.Add(new Point(v.TexCoord.X, v.TexCoord.Y));

                                    if (p.X < min.X) min.X = p.X;
                                    if (p.Y < min.Y) min.Y = p.Y;
                                    if (p.Z < min.Z) min.Z = p.Z;
                                    if (p.X > max.X) max.X = p.X;
                                    if (p.Y > max.Y) max.Y = p.Y;
                                    if (p.Z > max.Z) max.Z = p.Z;
                                    hasVertices = true;
                                }

                                mesh.TriangleIndices.Add(newIndex);
                            }

                            if (mesh.Positions.Count > 0)
                            {
                                Material material;

                                if (!string.IsNullOrEmpty(part.TexturePath) && File.Exists(part.TexturePath))
                                {
                                    try
                                    {
                                        var bitmap = new BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.UriSource = new Uri(part.TexturePath);
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.EndInit();
                                        bitmap.Freeze();

                                        var brush = new ImageBrush(bitmap) { ViewportUnits = BrushMappingMode.Absolute };
                                        material = new DiffuseMaterial(brush);
                                    }
                                    catch
                                    {
                                        var c = part.BaseColor;
                                        if (c.W == 0) c.W = 1.0f;
                                        material = new DiffuseMaterial(new SolidColorBrush(Color.FromScRgb(c.W, c.X, c.Y, c.Z)));
                                    }
                                }
                                else
                                {
                                    var c = part.BaseColor;
                                    if (c.W == 0) c.W = 1.0f;
                                    material = new DiffuseMaterial(new SolidColorBrush(Color.FromScRgb(c.W, c.X, c.Y, c.Z)));
                                }

                                var geoModel = new GeometryModel3D(mesh, material);
                                group.Children.Add(geoModel);
                            }
                        }
                    }

                    if (!hasVertices)
                    {
                        var centerV = model.ModelCenter;
                        min = new Vector3D(centerV.X - 0.5, centerV.Y - 0.5, centerV.Z - 0.5);
                        max = new Vector3D(centerV.X + 0.5, centerV.Y + 0.5, centerV.Z + 0.5);
                    }

                    var size = max - min;
                    var center = min + (size * 0.5);
                    var radius = Math.Max(Math.Max(size.X, size.Y), size.Z) * 0.5;

                    if (radius <= 0) radius = 1.0;

                    var distance = radius * 2.5;
                    var camera = new PerspectiveCamera(new Point3D(center.X, center.Y, center.Z + distance), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);
                    camera.NearPlaneDistance = radius * 0.1;
                    camera.FarPlaneDistance = radius * 5.0;

                    var viewport = new Viewport3D
                    {
                        Camera = camera,
                        Width = width,
                        Height = height
                    };
                    viewport.Children.Add(new ModelVisual3D { Content = group });

                    viewport.Measure(new Size(width, height));
                    viewport.Arrange(new Rect(0, 0, width, height));

                    var bitmapResult = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmapResult.Render(viewport);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapResult));

                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    result = ms.ToArray();
                }
                catch
                {
                }
            }

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                Generate();
            }
            else
            {
                var t = new Thread(Generate);
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();
            }

            return result;
        }
    }
}
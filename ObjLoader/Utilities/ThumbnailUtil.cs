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
        public static byte[] CreateThumbnail(ObjModel model, int width = 64, int height = 64)
        {
            if (model.Vertices.Length == 0) return Array.Empty<byte>();

            byte[] result = Array.Empty<byte>();

            void Generate()
            {
                try
                {
                    var mesh = new MeshGeometry3D();

                    foreach (var v in model.Vertices)
                    {
                        mesh.Positions.Add(new Point3D(v.Position.X, v.Position.Y, v.Position.Z));
                        mesh.Normals.Add(new Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
                    }

                    foreach (var i in model.Indices)
                    {
                        mesh.TriangleIndices.Add(i);
                    }

                    var material = new DiffuseMaterial(Brushes.White);
                    var geometryModel = new GeometryModel3D(mesh, material);
                    var group = new Model3DGroup();
                    group.Children.Add(geometryModel);

                    var light = new DirectionalLight(Colors.White, new Vector3D(-1, -1, -1));
                    group.Children.Add(light);
                    group.Children.Add(new AmbientLight(Color.FromRgb(50, 50, 50)));

                    var center = new Point3D(model.ModelCenter.X, model.ModelCenter.Y, model.ModelCenter.Z);
                    var radius = 1.0 / model.ModelScale;
                    var distance = radius * 2.5;
                    var camera = new PerspectiveCamera(new Point3D(center.X, center.Y, center.Z + distance), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 45);

                    var viewport = new Viewport3D
                    {
                        Camera = camera,
                        Width = width,
                        Height = height
                    };
                    viewport.Children.Add(new ModelVisual3D { Content = group });

                    viewport.Measure(new Size(width, height));
                    viewport.Arrange(new Rect(0, 0, width, height));

                    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(viewport);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));

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
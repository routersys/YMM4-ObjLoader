using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Rendering;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using D3D11 = Vortice.Direct3D11;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace ObjLoader.ViewModels
{
    public class CameraWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;

        private double _camX, _camY, _camZ;
        private double _targetX, _targetY, _targetZ;

        private double _viewRadius = 15;
        private double _viewTheta = 45 * Math.PI / 180;
        private double _viewPhi = 45 * Math.PI / 180;
        private Point3D _viewCenter = new Point3D(0, 0, 0);

        private double _gizmoRadius = 6.0;

        private double _modelScale = 1.0;
        private double _aspectRatio = 16.0 / 9.0;
        private int _viewportWidth = 100;
        private int _viewportHeight = 100;

        private bool _isGridVisible = true;
        private Point _lastMousePos;
        private bool _isRotatingView;
        private bool _isPanningView;
        private bool _isDraggingTarget;
        private bool _isDraggingCamera;
        private string _hoveredDirectionName = "";
        private bool _isTargetFixed = true;

        private DispatcherTimer? _animationTimer;
        private double _animTargetTheta, _animTargetPhi;
        private double _animStartTheta, _animStartPhi;
        private double _animProgress;

        public PerspectiveCamera Camera { get; }
        public PerspectiveCamera GizmoCamera { get; }

        public MeshGeometry3D GridGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D CameraVisualGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D CameraHandleGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D TargetVisualGeometry { get; private set; } = new MeshGeometry3D();

        public Model3DGroup ViewCubeModel { get; private set; } = new Model3DGroup();
        public GeometryModel3D? CubeFaceXPos, CubeFaceXNeg, CubeFaceYPos, CubeFaceYNeg, CubeFaceZPos, CubeFaceZNeg;
        public GeometryModel3D? CornerFRT, CornerFLT, CornerBRT, CornerBLT;
        public GeometryModel3D? CornerFRB, CornerFLB, CornerBRB, CornerBLB;

        public string HoveredDirectionName { get => _hoveredDirectionName; set => Set(ref _hoveredDirectionName, value); }

        public double CamX { get => _camX; set { Set(ref _camX, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double CamY { get => _camY; set { Set(ref _camY, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double CamZ { get => _camZ; set { Set(ref _camZ, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double TargetX { get => _targetX; set { Set(ref _targetX, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double TargetY { get => _targetY; set { Set(ref _targetY, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double TargetZ { get => _targetZ; set { Set(ref _targetZ, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }

        public bool IsGridVisible { get => _isGridVisible; set { Set(ref _isGridVisible, value); UpdateGrid(); } }
        public bool IsTargetFixed { get => _isTargetFixed; set { if (Set(ref _isTargetFixed, value)) OnPropertyChanged(nameof(IsTargetFree)); } }
        public bool IsTargetFree => !_isTargetFixed;

        public ActionCommand ResetCommand { get; }

        private WriteableBitmap? _sceneImage;
        public WriteableBitmap? SceneImage { get => _sceneImage; private set => Set(ref _sceneImage, value); }

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private ID3D11Texture2D? _renderTarget;
        private ID3D11RenderTargetView? _rtv;
        private ID3D11Texture2D? _depthStencil;
        private ID3D11DepthStencilView? _dsv;
        private ID3D11Texture2D? _stagingTexture;
        private D3DResources? _d3dResources;
        private GpuResourceCacheItem? _modelResource;

        public CameraWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;
            _loader = new ObjModelLoader();

            Camera = new PerspectiveCamera { FieldOfView = 45, NearPlaneDistance = 0.01, FarPlaneDistance = 100000 };
            GizmoCamera = new PerspectiveCamera { FieldOfView = 45, NearPlaneDistance = 0.1, FarPlaneDistance = 100 };

            _camX = _parameter.CameraX.Values[0].Value;
            _camY = _parameter.CameraY.Values[0].Value;
            _camZ = _parameter.CameraZ.Values[0].Value;
            _targetX = _parameter.TargetX.Values[0].Value;
            _targetY = _parameter.TargetY.Values[0].Value;
            _targetZ = _parameter.TargetZ.Values[0].Value;

            double sw = _parameter.ScreenWidth.Values[0].Value;
            double sh = _parameter.ScreenHeight.Values[0].Value;
            if (sh > 0) _aspectRatio = sw / sh;

            ResetCommand = new ActionCommand(_ => true, _ => ResetSceneCamera());

            InitializeD3D();
            LoadModel();
            UpdateGrid();
            UpdateViewportCamera();
            UpdateSceneCameraVisual();
            CreateViewCube();
        }

        private void InitializeD3D()
        {
            var result = D3D11.D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, new[] { FeatureLevel.Level_11_0 }, out _device, out _context);
            if (result.Failure || _device == null) return;
            _d3dResources = new D3DResources(_device!);
        }

        public void ResizeViewport(int width, int height)
        {
            if (width < 1 || height < 1) return;
            _viewportWidth = width;
            _viewportHeight = height;

            _rtv?.Dispose();
            _renderTarget?.Dispose();
            _dsv?.Dispose();
            _depthStencil?.Dispose();
            _stagingTexture?.Dispose();

            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CPUAccessFlags = CpuAccessFlags.None
            };
            _renderTarget = _device!.CreateTexture2D(texDesc);
            _rtv = _device.CreateRenderTargetView(_renderTarget);

            var depthDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CPUAccessFlags = CpuAccessFlags.None
            };
            _depthStencil = _device.CreateTexture2D(depthDesc);
            _dsv = _device.CreateDepthStencilView(_depthStencil);

            var stagingDesc = texDesc;
            stagingDesc.Usage = ResourceUsage.Staging;
            stagingDesc.BindFlags = BindFlags.None;
            stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
            _stagingTexture = _device.CreateTexture2D(stagingDesc);

            SceneImage = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            UpdateD3DScene();
        }

        private void UpdateD3DScene()
        {
            if (_device == null || _context == null || _rtv == null || _d3dResources == null || _modelResource == null || SceneImage == null) return;

            _context.OMSetRenderTargets(_rtv, _dsv);
            _context.ClearRenderTargetView(_rtv, new Color4(0.13f, 0.13f, 0.13f, 1.0f));
            _context.ClearDepthStencilView(_dsv!, DepthStencilClearFlags.Depth, 1.0f, 0);

            _context.RSSetState(_d3dResources.RasterizerState);
            _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
            _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);

            _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);

            _context.IASetInputLayout(_d3dResources.InputLayout);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            int stride = Unsafe.SizeOf<ObjVertex>();
            int offset = 0;
            _context.IASetVertexBuffers(0, 1, new[] { _modelResource.VertexBuffer }, new[] { stride }, new[] { offset });
            _context.IASetIndexBuffer(_modelResource.IndexBuffer, Format.R32_UInt, 0);

            _context.VSSetShader(_d3dResources.VertexShader);
            _context.PSSetShader(_d3dResources.PixelShader);
            _context.PSSetSamplers(0, new[] { _d3dResources.SamplerState });

            double y = _viewRadius * Math.Cos(_viewPhi);
            double hRadius = _viewRadius * Math.Sin(_viewPhi);
            double x = hRadius * Math.Sin(_viewTheta);
            double z = hRadius * Math.Cos(_viewTheta);
            var viewPos = new Vector3((float)(x + _viewCenter.X), (float)(y + _viewCenter.Y), (float)(z + _viewCenter.Z));
            var targetPos = new Vector3((float)_viewCenter.X, (float)_viewCenter.Y, (float)_viewCenter.Z);

            var view = Matrix4x4.CreateLookAt(viewPos, targetPos, Vector3.UnitY);
            float aspect = (float)_viewportWidth / _viewportHeight;
            var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(45 * Math.PI / 180), aspect, 0.1f, 10000.0f);

            var normalize = Matrix4x4.CreateTranslation(-_modelResource.ModelCenter) * Matrix4x4.CreateScale(_modelResource.ModelScale);

            Matrix4x4 axisConversion = Matrix4x4.Identity;
            var settings = PluginSettings.Instance;
            switch (settings.CoordinateSystem)
            {
                case CoordinateSystem.RightHandedZUp:
                    axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0));
                    break;
                case CoordinateSystem.LeftHandedYUp:
                    axisConversion = Matrix4x4.CreateScale(1, 1, -1);
                    break;
                case CoordinateSystem.LeftHandedZUp:
                    axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1);
                    break;
            }

            float scale = (float)(_parameter.Scale.Values[0].Value / 100.0);
            float rx = (float)(_parameter.RotationX.Values[0].Value * Math.PI / 180.0);
            float ry = (float)(_parameter.RotationY.Values[0].Value * Math.PI / 180.0);
            float rz = (float)(_parameter.RotationZ.Values[0].Value * Math.PI / 180.0);
            float tx = (float)_parameter.X.Values[0].Value;
            float ty = (float)_parameter.Y.Values[0].Value;
            float tz = (float)_parameter.Z.Values[0].Value;

            var placement = Matrix4x4.CreateScale(scale) *
                            Matrix4x4.CreateRotationZ(rz) *
                            Matrix4x4.CreateRotationX(rx) *
                            Matrix4x4.CreateRotationY(ry) *
                            Matrix4x4.CreateTranslation(tx, ty, tz);

            var world = normalize * axisConversion * placement;
            var wvp = world * view * proj;

            for (int i = 0; i < _modelResource.Parts.Length; i++)
            {
                var part = _modelResource.Parts[i];
                var texView = _modelResource.PartTextures[i];
                bool hasTexture = texView != null;

                _context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { hasTexture ? texView! : _d3dResources.WhiteTextureView! });

                var partColor = part.BaseColor;

                ConstantBufferData cbData = new ConstantBufferData
                {
                    WorldViewProj = Matrix4x4.Transpose(wvp),
                    World = Matrix4x4.Transpose(world),
                    LightPos = new Vector4(1, 1, 1, 0),
                    BaseColor = partColor,
                    AmbientColor = new Vector4(0.2f, 0.2f, 0.2f, 1),
                    LightColor = new Vector4(0.8f, 0.8f, 0.8f, 1),
                    CameraPos = new Vector4(viewPos, 1),
                    LightEnabled = 1.0f,
                    DiffuseIntensity = 1.0f,
                    SpecularIntensity = 0.5f,
                    Shininess = 30.0f
                };

                D3D11.MappedSubresource mapped;
                _context.Map(_d3dResources.ConstantBuffer, 0, MapMode.WriteDiscard, D3D11.MapFlags.None, out mapped);
                unsafe
                {
                    Unsafe.Copy(mapped.DataPointer.ToPointer(), ref cbData);
                }
                _context.Unmap(_d3dResources.ConstantBuffer, 0);

                _context.VSSetConstantBuffers(0, new[] { _d3dResources.ConstantBuffer });
                _context.PSSetConstantBuffers(0, new[] { _d3dResources.ConstantBuffer });

                _context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
            }

            _context.CopyResource(_stagingTexture, _renderTarget);
            var map = _context.Map(_stagingTexture!, 0, MapMode.Read, D3D11.MapFlags.None);

            try
            {
                SceneImage.Lock();
                unsafe
                {
                    var srcPtr = (byte*)map.DataPointer;
                    var dstPtr = (byte*)SceneImage.BackBuffer;
                    var height = _viewportHeight;
                    var widthInBytes = _viewportWidth * 4;
                    var srcPitch = map.RowPitch;
                    var dstPitch = SceneImage.BackBufferStride;

                    for (int r = 0; r < height; r++)
                    {
                        Buffer.MemoryCopy(
                           srcPtr + (r * srcPitch),
                           dstPtr + (r * dstPitch),
                           dstPitch,
                           widthInBytes);
                    }
                }
                SceneImage.AddDirtyRect(new Int32Rect(0, 0, _viewportWidth, _viewportHeight));
                SceneImage.Unlock();
            }
            finally
            {
                _context.Unmap(_stagingTexture!, 0);
            }
        }

        private unsafe void LoadModel()
        {
            var path = _parameter.FilePath;
            if (string.IsNullOrEmpty(path)) return;

            var model = _loader.Load(path);
            if (model.Vertices.Length == 0) return;

            var vDesc = new BufferDescription(model.Vertices.Length * Unsafe.SizeOf<ObjVertex>(), BindFlags.VertexBuffer, ResourceUsage.Immutable);
            ID3D11Buffer vb;
            fixed (ObjVertex* p = model.Vertices) vb = _device!.CreateBuffer(vDesc, new SubresourceData(p));

            var iDesc = new BufferDescription(model.Indices.Length * sizeof(int), BindFlags.IndexBuffer, ResourceUsage.Immutable);
            ID3D11Buffer ib;
            fixed (int* p = model.Indices) ib = _device.CreateBuffer(iDesc, new SubresourceData(p));

            var parts = model.Parts.ToArray();
            var partTextures = new ID3D11ShaderResourceView?[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (File.Exists(parts[i].TexturePath))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(parts[i].TexturePath);
                        using var ms = new MemoryStream(bytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        var conv = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                        var pixels = new byte[conv.PixelWidth * conv.PixelHeight * 4];
                        conv.CopyPixels(pixels, conv.PixelWidth * 4, 0);

                        var tDesc = new Texture2DDescription { Width = conv.PixelWidth, Height = conv.PixelHeight, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource };
                        fixed (byte* p = pixels)
                        {
                            using var t = _device.CreateTexture2D(tDesc, new[] { new SubresourceData(p, conv.PixelWidth * 4) });
                            partTextures[i] = _device.CreateShaderResourceView(t);
                        }
                    }
                    catch { }
                }
            }

            _modelResource = new GpuResourceCacheItem(_device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale);

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (var v in model.Vertices)
            {
                double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }
            double sx = maxX - minX; double sy = maxY - minY; double sz = maxZ - minZ;
            _modelScale = Math.Max(sx, Math.Max(sy, sz));
            if (_modelScale < 0.1) _modelScale = 1.0;
            _viewRadius = _modelScale * 2.5;

            UpdateViewportCamera();
            UpdateD3DScene();
        }

        private void ResetSceneCamera()
        {
            CamX = 0; CamY = 0;
            CamZ = -_modelScale * 2.0;
            TargetX = 0; TargetY = 0; TargetZ = 0;
            _viewRadius = _modelScale * 3.0;
            AnimateView(0, Math.PI / 4);
        }

        private void CreateViewCube()
        {
            ViewCubeModel = new Model3DGroup();

            var red = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 60, 60)));
            var darkRed = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 0, 0)));
            var green = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 255, 60)));
            var darkGreen = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 150, 0)));
            var blue = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 255)));
            var darkBlue = new DiffuseMaterial(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 150)));
            var gray = new DiffuseMaterial(Brushes.Gray);

            var centerMesh = new MeshGeometry3D();
            AddCubeToMesh(centerMesh, new Point3D(0, 0, 0), 0.7);
            ViewCubeModel.Children.Add(new GeometryModel3D(centerMesh, gray));

            CubeFaceXPos = CreateFace(new Vector3D(1, 0, 0), red, "右 (Right)");
            ViewCubeModel.Children.Add(CubeFaceXPos);
            CubeFaceXNeg = CreateFace(new Vector3D(-1, 0, 0), darkRed, "左 (Left)");
            ViewCubeModel.Children.Add(CubeFaceXNeg);
            CubeFaceYPos = CreateFace(new Vector3D(0, 1, 0), green, "上 (Top)");
            ViewCubeModel.Children.Add(CubeFaceYPos);
            CubeFaceYNeg = CreateFace(new Vector3D(0, -1, 0), darkGreen, "下 (Bottom)");
            ViewCubeModel.Children.Add(CubeFaceYNeg);
            CubeFaceZPos = CreateFace(new Vector3D(0, 0, 1), blue, "前 (Front)");
            ViewCubeModel.Children.Add(CubeFaceZPos);
            CubeFaceZNeg = CreateFace(new Vector3D(0, 0, -1), darkBlue, "後 (Back)");
            ViewCubeModel.Children.Add(CubeFaceZNeg);

            double off = 0.6;
            double sz = 0.35;
            CornerFRT = CreateCorner(new Point3D(off, off, off), sz, "斜め(前右上)");
            CornerFLT = CreateCorner(new Point3D(-off, off, off), sz, "斜め(前左上)");
            CornerBRT = CreateCorner(new Point3D(off, off, -off), sz, "斜め(後右上)");
            CornerBLT = CreateCorner(new Point3D(-off, off, -off), sz, "斜め(後左上)");

            CornerFRB = CreateCorner(new Point3D(off, -off, off), sz, "斜め(前右下)");
            CornerFLB = CreateCorner(new Point3D(-off, -off, off), sz, "斜め(前左下)");
            CornerBRB = CreateCorner(new Point3D(off, -off, -off), sz, "斜め(後右下)");
            CornerBLB = CreateCorner(new Point3D(-off, -off, -off), sz, "斜め(後左下)");

            ViewCubeModel.Children.Add(CornerFRT); ViewCubeModel.Children.Add(CornerFLT);
            ViewCubeModel.Children.Add(CornerBRT); ViewCubeModel.Children.Add(CornerBLT);
            ViewCubeModel.Children.Add(CornerFRB); ViewCubeModel.Children.Add(CornerFLB);
            ViewCubeModel.Children.Add(CornerBRB); ViewCubeModel.Children.Add(CornerBLB);
        }

        private GeometryModel3D CreateFace(Vector3D dir, Material mat, string name)
        {
            var mesh = new MeshGeometry3D();
            var center = dir * 0.85;
            double size = 0.6;

            Point3D c = new Point3D(center.X, center.Y, center.Z);
            AddCubeToMesh(mesh, c, size);

            var model = new GeometryModel3D(mesh, mat);
            model.SetValue(FrameworkElement.TagProperty, name);
            return model;
        }

        private GeometryModel3D CreateCorner(Point3D center, double size, string name)
        {
            var mesh = new MeshGeometry3D();
            AddSphereToMesh(mesh, center, size, 4, 4);
            var mat = new DiffuseMaterial(Brushes.Silver);
            var model = new GeometryModel3D(mesh, mat);
            model.SetValue(FrameworkElement.TagProperty, name);
            return model;
        }

        public void HandleGizmoMove(object? modelHit)
        {
            if (modelHit is GeometryModel3D gm && gm.GetValue(FrameworkElement.TagProperty) is string name)
            {
                HoveredDirectionName = name;
            }
            else
            {
                HoveredDirectionName = "";
            }
        }

        public void HandleViewCubeClick(object? modelHit)
        {
            if (modelHit == null) return;

            if (modelHit == CubeFaceXPos) AnimateView(Math.PI / 2, Math.PI / 2);
            else if (modelHit == CubeFaceXNeg) AnimateView(-Math.PI / 2, Math.PI / 2);
            else if (modelHit == CubeFaceYPos) AnimateView(0, 0.01);
            else if (modelHit == CubeFaceYNeg) AnimateView(0, Math.PI - 0.01);
            else if (modelHit == CubeFaceZPos) AnimateView(0, Math.PI / 2);
            else if (modelHit == CubeFaceZNeg) AnimateView(Math.PI, Math.PI / 2);

            else if (modelHit == CornerFRT) AnimateView(Math.PI / 4, Math.PI / 3);
            else if (modelHit == CornerFLT) AnimateView(-Math.PI / 4, Math.PI / 3);
            else if (modelHit == CornerBRT) AnimateView(3 * Math.PI / 4, Math.PI / 3);
            else if (modelHit == CornerBLT) AnimateView(-3 * Math.PI / 4, Math.PI / 3);
            else if (modelHit == CornerFRB) AnimateView(Math.PI / 4, 2 * Math.PI / 3);
            else if (modelHit == CornerFLB) AnimateView(-Math.PI / 4, 2 * Math.PI / 3);
            else if (modelHit == CornerBRB) AnimateView(3 * Math.PI / 4, 2 * Math.PI / 3);
            else if (modelHit == CornerBLB) AnimateView(-3 * Math.PI / 4, 2 * Math.PI / 3);
        }

        private void AnimateView(double targetTheta, double targetPhi)
        {
            if (_animationTimer != null) _animationTimer.Stop();

            _animStartTheta = _viewTheta;
            _animStartPhi = _viewPhi;

            while (targetTheta - _animStartTheta > Math.PI) _animStartTheta += 2 * Math.PI;
            while (targetTheta - _animStartTheta < -Math.PI) _animStartTheta -= 2 * Math.PI;

            _animTargetTheta = targetTheta;
            _animTargetPhi = targetPhi;
            _animProgress = 0;

            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += (s, e) =>
            {
                _animProgress += 0.08;
                if (_animProgress >= 1.0)
                {
                    _animProgress = 1.0;
                    _animationTimer.Stop();
                    _animationTimer = null;
                }

                double t = 1 - Math.Pow(1 - _animProgress, 3);

                _viewTheta = _animStartTheta + (_animTargetTheta - _animStartTheta) * t;
                _viewPhi = _animStartPhi + (_animTargetPhi - _animStartPhi) * t;

                UpdateViewportCamera();
            };
            _animationTimer.Start();
        }

        private void UpdateViewportCamera()
        {
            double y = _viewRadius * Math.Cos(_viewPhi);
            double hRadius = _viewRadius * Math.Sin(_viewPhi);
            double x = hRadius * Math.Sin(_viewTheta);
            double z = hRadius * Math.Cos(_viewTheta);

            var pos = new Point3D(x, y, z) + (Vector3D)_viewCenter;
            Camera.Position = pos;
            Camera.LookDirection = _viewCenter - pos;

            double gy = _gizmoRadius * Math.Cos(_viewPhi);
            double ghRadius = _gizmoRadius * Math.Sin(_viewPhi);
            double gx = ghRadius * Math.Sin(_viewTheta);
            double gz = ghRadius * Math.Cos(_viewTheta);

            GizmoCamera.Position = new Point3D(gx, gy, gz);
            GizmoCamera.LookDirection = new Point3D(0, 0, 0) - GizmoCamera.Position;

            UpdateD3DScene();
        }

        private void UpdateGrid()
        {
            GridGeometry = new MeshGeometry3D();
            if (_isGridVisible)
            {
                int size = 20;
                double step = _modelScale / 5.0;
                double thickness = _modelScale * 0.001;

                void AddLine(Point3D start, Point3D end)
                {
                    AddLineToMesh(GridGeometry, start, end, thickness);
                }

                for (int i = -size; i <= size; i++)
                {
                    if (i == 0) continue;
                    double pos = i * step;
                    double limit = size * step;
                    AddLine(new Point3D(pos, 0, -limit), new Point3D(pos, 0, limit));
                    AddLine(new Point3D(-limit, 0, pos), new Point3D(limit, 0, pos));
                }
            }
            OnPropertyChanged(nameof(GridGeometry));
        }

        private void UpdateSceneCameraVisual()
        {
            CameraVisualGeometry = new MeshGeometry3D();
            TargetVisualGeometry = new MeshGeometry3D();
            CameraHandleGeometry = new MeshGeometry3D();

            var camPos = new Point3D(_camX, _camY, _camZ);
            var targetPos = new Point3D(_targetX, _targetY, _targetZ);

            double handleScale = _modelScale * 0.05;
            double thickness = _modelScale * 0.003;

            var dir = targetPos - camPos;
            if (dir.LengthSquared < 0.0001) dir = new Vector3D(0, 0, -1);
            double dist = dir.Length;
            dir.Normalize();

            var lineStart = camPos;
            var lineEnd = camPos + (dir * _modelScale * 100.0);
            AddLineToMesh(CameraVisualGeometry, lineStart, lineEnd, thickness * 0.5);

            Vector3D forward = dir;
            Vector3D up = new Vector3D(0, 1, 0);
            Vector3D right = Vector3D.CrossProduct(forward, up);
            if (right.LengthSquared < 0.001) right = new Vector3D(1, 0, 0);
            right.Normalize();
            up = Vector3D.CrossProduct(right, forward);
            up.Normalize();

            double fovValue = _parameter.Fov.Values[0].Value;
            double radFov = Math.Max(1, Math.Min(179, fovValue)) * Math.PI / 180.0;

            double frustumLen = _modelScale * 0.5;
            double tanHalf = Math.Tan(radFov / 2.0);
            double hHalf = frustumLen * tanHalf;
            double wHalf = hHalf * _aspectRatio;

            Point3D cEnd = camPos + forward * frustumLen;
            Point3D tr = cEnd + (up * hHalf) + (right * wHalf);
            Point3D tl = cEnd + (up * hHalf) - (right * wHalf);
            Point3D br = cEnd - (up * hHalf) + (right * wHalf);
            Point3D bl = cEnd - (up * hHalf) - (right * wHalf);

            AddLineToMesh(CameraVisualGeometry, camPos, tr, thickness);
            AddLineToMesh(CameraVisualGeometry, camPos, tl, thickness);
            AddLineToMesh(CameraVisualGeometry, camPos, br, thickness);
            AddLineToMesh(CameraVisualGeometry, camPos, bl, thickness);
            AddLineToMesh(CameraVisualGeometry, tr, tl, thickness);
            AddLineToMesh(CameraVisualGeometry, tl, bl, thickness);
            AddLineToMesh(CameraVisualGeometry, bl, br, thickness);
            AddLineToMesh(CameraVisualGeometry, br, tr, thickness);

            Point3D upTip = cEnd + (up * hHalf * 1.3);
            AddLineToMesh(CameraVisualGeometry, tr, upTip, thickness);
            AddLineToMesh(CameraVisualGeometry, tl, upTip, thickness);

            var visualTargetPos = targetPos;

            AddSphereToMesh(TargetVisualGeometry, visualTargetPos, handleScale, 16, 16);

            AddSphereToMesh(CameraHandleGeometry, camPos, handleScale, 16, 16);

            OnPropertyChanged(nameof(CameraVisualGeometry));
            OnPropertyChanged(nameof(TargetVisualGeometry));
            OnPropertyChanged(nameof(CameraHandleGeometry));
        }

        private void AddCubeToMesh(MeshGeometry3D mesh, Point3D center, double size)
        {
            double s = size / 2.0;
            Point3D[] p = {
                new(center.X-s, center.Y-s, center.Z+s), new(center.X+s, center.Y-s, center.Z+s),
                new(center.X+s, center.Y+s, center.Z+s), new(center.X-s, center.Y+s, center.Z+s),
                new(center.X-s, center.Y-s, center.Z-s), new(center.X+s, center.Y-s, center.Z-s),
                new(center.X+s, center.Y+s, center.Z-s), new(center.X-s, center.Y+s, center.Z-s)
            };

            int idx = mesh.Positions.Count;
            foreach (var pt in p) mesh.Positions.Add(pt);

            int[] indices = {
                0,1,2, 0,2,3,
                4,6,5, 4,7,6,
                0,4,5, 0,5,1,
                1,5,6, 1,6,2,
                2,6,7, 2,7,3,
                3,7,4, 3,4,0
            };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private void AddLineToMesh(MeshGeometry3D mesh, Point3D start, Point3D end, double thickness)
        {
            var vec = end - start;
            var dir = vec;
            if (dir.LengthSquared < double.Epsilon) return;
            dir.Normalize();

            var perp1 = Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0));
            if (perp1.LengthSquared < 0.0001) perp1 = Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(dir, perp1);
            perp2.Normalize();

            perp1 *= thickness;
            perp2 *= thickness;

            var p1 = start - perp1 - perp2; var p2 = start + perp1 - perp2;
            var p3 = start + perp1 + perp2; var p4 = start - perp1 + perp2;
            var p5 = end - perp1 - perp2; var p6 = end + perp1 - perp2;
            var p7 = end + perp1 + perp2; var p8 = end - perp1 + perp2;

            int idx = mesh.Positions.Count;
            Point3D[] pts = { p1, p2, p3, p4, p5, p6, p7, p8 };
            foreach (var p in pts) mesh.Positions.Add(p);

            for (int i = 0; i < 8; i++) mesh.Normals.Add(new Vector3D(0, 1, 0));

            int[] indices = {
                0,1,2, 0,2,3,
                4,6,5, 4,7,6,
                0,4,5, 0,5,1,
                1,5,6, 1,6,2,
                2,6,7, 2,7,3,
                3,7,4, 3,4,0
            };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private void AddSphereToMesh(MeshGeometry3D mesh, Point3D center, double radius, int tDiv, int pDiv)
        {
            int baseIdx = mesh.Positions.Count;
            double dt = 2 * Math.PI / tDiv;
            double dp = Math.PI / pDiv;

            for (int pi = 0; pi <= pDiv; pi++)
            {
                double phi = pi * dp;
                for (int ti = 0; ti <= tDiv; ti++)
                {
                    double theta = ti * dt;
                    double x = radius * Math.Sin(phi) * Math.Cos(theta);
                    double y = radius * Math.Cos(phi);
                    double z = radius * Math.Sin(phi) * Math.Sin(theta);

                    var pt = new Point3D(center.X + x, center.Y + y, center.Z + z);
                    var n = new Vector3D(x, y, z);
                    n.Normalize();

                    mesh.Positions.Add(pt);
                    mesh.Normals.Add(n);
                }
            }

            for (int pi = 0; pi < pDiv; pi++)
            {
                for (int ti = 0; ti < tDiv; ti++)
                {
                    int x0 = ti;
                    int x1 = (ti + 1);
                    int y0 = pi * (tDiv + 1);
                    int y1 = (pi + 1) * (tDiv + 1);

                    mesh.TriangleIndices.Add(baseIdx + y0 + x0);
                    mesh.TriangleIndices.Add(baseIdx + y1 + x0);
                    mesh.TriangleIndices.Add(baseIdx + y0 + x1);

                    mesh.TriangleIndices.Add(baseIdx + y1 + x0);
                    mesh.TriangleIndices.Add(baseIdx + y1 + x1);
                    mesh.TriangleIndices.Add(baseIdx + y0 + x1);
                }
            }
        }

        private void SyncToParameter()
        {
            _parameter.CameraX.CopyFrom(new Animation(_camX, -100000, 100000));
            _parameter.CameraY.CopyFrom(new Animation(_camY, -100000, 100000));
            _parameter.CameraZ.CopyFrom(new Animation(_camZ, -100000, 100000));
            _parameter.TargetX.CopyFrom(new Animation(_targetX, -100000, 100000));
            _parameter.TargetY.CopyFrom(new Animation(_targetY, -100000, 100000));
            _parameter.TargetZ.CopyFrom(new Animation(_targetZ, -100000, 100000));
        }

        public void Zoom(int delta)
        {
            _viewRadius -= delta * (_modelScale * 0.005);
            if (_viewRadius < _modelScale * 0.01) _viewRadius = _modelScale * 0.01;
            UpdateViewportCamera();
        }

        public void StartPan(Point pos)
        {
            if (IsTargetFixed) return;
            _isPanningView = true;
            _lastMousePos = pos;
        }

        public void StartRotate(Point pos)
        {
            _isRotatingView = true;
            _lastMousePos = pos;
        }

        public void StartTargetDrag(Point pos)
        {
            if (IsTargetFixed) return;
            _isDraggingTarget = true;
            _lastMousePos = pos;
        }

        public void StartCameraDrag(Point pos)
        {
            _isDraggingCamera = true;
            _lastMousePos = pos;
        }

        public void EndDrag()
        {
            _isRotatingView = false;
            _isPanningView = false;
            _isDraggingTarget = false;
            _isDraggingCamera = false;
        }

        public void Move(Point pos)
        {
            if (_isRotatingView || _isPanningView)
            {
                var dx = pos.X - _lastMousePos.X;
                var dy = pos.Y - _lastMousePos.Y;
                _lastMousePos = pos;

                if (_isPanningView)
                {
                    var look = Camera.LookDirection; look.Normalize();
                    var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
                    var up = Vector3D.CrossProduct(right, look); up.Normalize();
                    double panSpeed = _viewRadius * 0.002;
                    _viewCenter += (-right * dx * panSpeed) + (up * dy * panSpeed);
                    UpdateViewportCamera();
                }
                else
                {
                    _viewTheta += dx * 0.01;
                    _viewPhi -= dy * 0.01;
                    if (_viewPhi < 0.01) _viewPhi = 0.01;
                    if (_viewPhi > Math.PI - 0.01) _viewPhi = Math.PI - 0.01;
                    UpdateViewportCamera();
                }
            }
            else if (_isDraggingTarget || _isDraggingCamera)
            {
                var dx = pos.X - _lastMousePos.X;
                var dy = pos.Y - _lastMousePos.Y;
                _lastMousePos = pos;

                var look = Camera.LookDirection; look.Normalize();
                var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
                var up = Vector3D.CrossProduct(right, look); up.Normalize();
                double moveSpeed = _viewRadius * 0.002;
                var move = (right * dx * moveSpeed) + (-up * dy * moveSpeed);

                if (_isDraggingTarget)
                {
                    TargetX += move.X; TargetY += move.Y; TargetZ += move.Z;
                }
                else
                {
                    CamX += move.X; CamY += move.Y; CamZ += move.Z;
                }
            }
        }

        public void Dispose()
        {
            _d3dResources?.Dispose();
            _rtv?.Dispose();
            _renderTarget?.Dispose();
            _dsv?.Dispose();
            _depthStencil?.Dispose();
            _stagingTexture?.Dispose();
            _modelResource?.Dispose();
            _device?.Dispose();
            _context?.Dispose();
        }
    }
}
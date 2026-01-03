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
using ObjLoader.Localization;

namespace ObjLoader.ViewModels
{
    public class CameraWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;

        private double _camX, _camY, _camZ;
        private double _targetX, _targetY, _targetZ;

        private double _viewCenterX, _viewCenterY, _viewCenterZ;

        private double _viewRadius = 15;
        private double _viewTheta = 45 * Math.PI / 180;
        private double _viewPhi = 45 * Math.PI / 180;

        private double _gizmoRadius = 6.0;

        private double _modelScale = 1.0;
        private double _modelHeight = 1.0;
        private double _aspectRatio = 16.0 / 9.0;
        private int _viewportWidth = 100;
        private int _viewportHeight = 100;

        private bool _isGridVisible = true;
        private bool _isInfiniteGrid = true;
        private bool _isWireframe = false;
        private bool _isPilotView = false;
        private bool _isSnapping = false;

        private Point _lastMousePos;
        private bool _isRotatingView;
        private bool _isPanningView;
        private bool _isDraggingTarget;
        private bool _isSpacePanning;
        private string _hoveredDirectionName = "";
        private bool _isTargetFixed = true;
        private Geometry3D? _hoveredGeometry;

        private DispatcherTimer? _animationTimer;
        private double _animTargetTheta, _animTargetPhi;
        private double _animStartTheta, _animStartPhi;
        private double _animProgress;

        private Stack<(double cx, double cy, double cz, double tx, double ty, double tz)> _undoStack = new();
        private Stack<(double cx, double cy, double cz, double tx, double ty, double tz)> _redoStack = new();

        public PerspectiveCamera Camera { get; }
        public PerspectiveCamera GizmoCamera { get; }

        public MeshGeometry3D CameraVisualGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D TargetVisualGeometry { get; private set; } = new MeshGeometry3D();

        public MeshGeometry3D GizmoXGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoYGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoZGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoXYGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoYZGeometry { get; private set; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoZXGeometry { get; private set; } = new MeshGeometry3D();

        public Model3DGroup ViewCubeModel { get; private set; } = new Model3DGroup();
        public GeometryModel3D? CubeFaceXPos, CubeFaceXNeg, CubeFaceYPos, CubeFaceYNeg, CubeFaceZPos, CubeFaceZNeg;
        public GeometryModel3D? CornerFRT, CornerFLT, CornerBRT, CornerBLT;
        public GeometryModel3D? CornerFRB, CornerFLB, CornerBRB, CornerBLB;

        public string HoveredDirectionName { get => _hoveredDirectionName; set => Set(ref _hoveredDirectionName, value); }

        public double CamX { get => _camX; set { Set(ref _camX, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double CamY { get => _camY; set { Set(ref _camY, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double CamZ { get => _camZ; set { Set(ref _camZ, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); } }
        public double TargetX { get => _targetX; set { Set(ref _targetX, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); UpdateViewportCamera(); } }
        public double TargetY { get => _targetY; set { Set(ref _targetY, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); UpdateViewportCamera(); } }
        public double TargetZ { get => _targetZ; set { Set(ref _targetZ, value); UpdateSceneCameraVisual(); SyncToParameter(); UpdateD3DScene(); UpdateViewportCamera(); } }

        public bool IsGridVisible { get => _isGridVisible; set { Set(ref _isGridVisible, value); UpdateD3DScene(); } }
        public bool IsInfiniteGrid { get => _isInfiniteGrid; set { Set(ref _isInfiniteGrid, value); UpdateD3DScene(); } }
        public bool IsWireframe { get => _isWireframe; set { Set(ref _isWireframe, value); UpdateD3DScene(); } }
        public bool IsPilotView { get => _isPilotView; set { Set(ref _isPilotView, value); UpdateViewportCamera(); UpdateSceneCameraVisual(); OnPropertyChanged(nameof(IsPilotFrameVisible)); } }
        public bool IsSnapping { get => _isSnapping; set => Set(ref _isSnapping, value); }
        public bool IsTargetFixed
        {
            get => _isTargetFixed;
            set
            {
                if (Set(ref _isTargetFixed, value))
                {
                    OnPropertyChanged(nameof(IsTargetFree));
                    UpdateSceneCameraVisual();
                    UpdateD3DScene();
                }
            }
        }
        public bool IsTargetFree => !_isTargetFixed;

        public bool IsPilotFrameVisible => IsPilotView;

        public ActionCommand ResetCommand { get; }
        public ActionCommand UndoCommand { get; }
        public ActionCommand RedoCommand { get; }
        public ActionCommand FocusCommand { get; }

        private WriteableBitmap? _sceneImage;
        public WriteableBitmap? SceneImage { get => _sceneImage; private set => Set(ref _sceneImage, value); }

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private ID3D11Texture2D? _renderTarget;
        private ID3D11RenderTargetView? _rtv;
        private ID3D11Texture2D? _depthStencil;
        private ID3D11DepthStencilView? _dsv;
        private ID3D11Texture2D? _stagingTexture;
        private ID3D11Texture2D? _resolveTexture;
        private D3DResources? _d3dResources;
        private GpuResourceCacheItem? _modelResource;
        private ID3D11Buffer? _gridVertexBuffer;

        private enum GizmoMode { None, X, Y, Z, XY, YZ, ZX, View }
        private GizmoMode _currentGizmoMode = GizmoMode.None;

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

            _viewCenterX = _targetX;
            _viewCenterY = _targetY;
            _viewCenterZ = _targetZ;

            double sw = _parameter.ScreenWidth.Values[0].Value;
            double sh = _parameter.ScreenHeight.Values[0].Value;
            if (sh > 0) _aspectRatio = sw / sh;

            ResetCommand = new ActionCommand(_ => true, _ => ResetSceneCamera());
            UndoCommand = new ActionCommand(_ => _undoStack.Count > 0, _ => PerformUndo());
            RedoCommand = new ActionCommand(_ => _redoStack.Count > 0, _ => PerformRedo());
            FocusCommand = new ActionCommand(_ => true, _ => PerformFocus());

            InitializeD3D();
            LoadModel();
            UpdateViewportCamera();
            UpdateSceneCameraVisual();
            CreateViewCube();
        }

        private void InitializeD3D()
        {
            var result = D3D11.D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, new[] { FeatureLevel.Level_11_0 }, out _device, out _context);
            if (result.Failure || _device == null) return;
            _d3dResources = new D3DResources(_device!);

            float[] gridVerts = {
                -1000, 0, 1000, -1000, 0, -1000, 1000, 0, 1000,
                1000, 0, 1000, -1000, 0, -1000, 1000, 0, -1000
            };
            var vDesc = new BufferDescription(gridVerts.Length * 4, BindFlags.VertexBuffer, ResourceUsage.Immutable);
            unsafe
            {
                fixed (float* p = gridVerts) _gridVertexBuffer = _device.CreateBuffer(vDesc, new SubresourceData(p));
            }
        }

        public void ResizeViewport(int width, int height)
        {
            if (width < 1 || height < 1) return;
            if (_device == null) return;

            _viewportWidth = width;
            _viewportHeight = height;

            _rtv?.Dispose();
            _renderTarget?.Dispose();
            _dsv?.Dispose();
            _depthStencil?.Dispose();
            _stagingTexture?.Dispose();
            _resolveTexture?.Dispose();

            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(4, 0),
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
                SampleDescription = new SampleDescription(4, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CPUAccessFlags = CpuAccessFlags.None
            };
            _depthStencil = _device.CreateTexture2D(depthDesc);
            _dsv = _device.CreateDepthStencilView(_depthStencil);

            var resolveDesc = texDesc;
            resolveDesc.SampleDescription = new SampleDescription(1, 0);
            _resolveTexture = _device.CreateTexture2D(resolveDesc);

            var stagingDesc = resolveDesc;
            stagingDesc.Usage = ResourceUsage.Staging;
            stagingDesc.BindFlags = BindFlags.None;
            stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
            _stagingTexture = _device.CreateTexture2D(stagingDesc);

            SceneImage = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
            UpdateD3DScene();
        }

        private void UpdateD3DScene()
        {
            if (_device == null || _context == null || _rtv == null || _d3dResources == null || SceneImage == null) return;

            _context.OMSetRenderTargets(_rtv, _dsv);
            _context.ClearRenderTargetView(_rtv, new Color4(0.13f, 0.13f, 0.13f, 1.0f));
            _context.ClearDepthStencilView(_dsv!, DepthStencilClearFlags.Depth, 1.0f, 0);

            _context.RSSetState(_isWireframe ? _d3dResources.WireframeRasterizerState : _d3dResources.RasterizerState);
            _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
            _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
            _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);

            Matrix4x4 view, proj;
            Vector3 camPos;

            double yOffset = _modelHeight / 2.0;

            if (_isPilotView)
            {
                camPos = new Vector3((float)_camX, (float)(_camY + yOffset), (float)_camZ);
                var target = new Vector3((float)_targetX, (float)(_targetY + yOffset), (float)_targetZ);
                var dir = target - camPos;
                if (dir.LengthSquared() < 0.0001f) dir = -Vector3.UnitZ;
                view = Matrix4x4.CreateLookAt(camPos, target, Vector3.UnitY);
            }
            else
            {
                double y = _viewRadius * Math.Cos(_viewPhi);
                double hRadius = _viewRadius * Math.Sin(_viewPhi);
                double x = hRadius * Math.Sin(_viewTheta);
                double z = hRadius * Math.Cos(_viewTheta);

                var targetPos = new Vector3((float)_viewCenterX, (float)(_viewCenterY + yOffset), (float)_viewCenterZ);
                camPos = new Vector3((float)x, (float)y, (float)z) + targetPos;
                view = Matrix4x4.CreateLookAt(camPos, targetPos, Vector3.UnitY);
            }

            float aspect = (float)_viewportWidth / _viewportHeight;

            double fovValue = _parameter.Fov.Values[0].Value;
            float hFovRad = (float)(Math.Max(1, Math.Min(179, fovValue)) * Math.PI / 180.0);

            if (!_isPilotView) hFovRad = (float)(45 * Math.PI / 180.0);

            float vFovRad = 2.0f * (float)Math.Atan(Math.Tan(hFovRad / 2.0f) / aspect);

            proj = Matrix4x4.CreatePerspectiveFieldOfView(vFovRad, aspect, 0.1f, 10000.0f);

            if (_modelResource != null)
            {
                _context.IASetInputLayout(_d3dResources.InputLayout);
                bool isInteracting = _isRotatingView || _isPanningView || _isDraggingTarget || _isSpacePanning;
                _context.IASetPrimitiveTopology(isInteracting ? PrimitiveTopology.PointList : PrimitiveTopology.TriangleList);

                int stride = Unsafe.SizeOf<ObjVertex>();
                _context.IASetVertexBuffers(0, 1, new[] { _modelResource.VertexBuffer }, new[] { stride }, new[] { 0 });
                _context.IASetIndexBuffer(_modelResource.IndexBuffer, Format.R32_UInt, 0);

                _context.VSSetShader(_d3dResources.VertexShader);
                _context.PSSetShader(_d3dResources.PixelShader);
                _context.PSSetSamplers(0, new[] { _d3dResources.SamplerState });

                float heightOffset = (float)(_modelHeight / 2.0);
                var normalize = Matrix4x4.CreateTranslation(-_modelResource.ModelCenter) * Matrix4x4.CreateScale(_modelResource.ModelScale);
                normalize *= Matrix4x4.CreateTranslation(0, heightOffset, 0);

                Matrix4x4 axisConversion = Matrix4x4.Identity;
                var settings = PluginSettings.Instance;
                switch (settings.CoordinateSystem)
                {
                    case CoordinateSystem.RightHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)); break;
                    case CoordinateSystem.LeftHandedYUp: axisConversion = Matrix4x4.CreateScale(1, 1, -1); break;
                    case CoordinateSystem.LeftHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1); break;
                }

                float scale = (float)(_parameter.Scale.Values[0].Value / 100.0);
                float rx = (float)(_parameter.RotationX.Values[0].Value * Math.PI / 180.0);
                float ry = (float)(_parameter.RotationY.Values[0].Value * Math.PI / 180.0);
                float rz = (float)(_parameter.RotationZ.Values[0].Value * Math.PI / 180.0);
                float tx = (float)_parameter.X.Values[0].Value;
                float ty = (float)_parameter.Y.Values[0].Value;
                float tz = (float)_parameter.Z.Values[0].Value;

                var placement = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationZ(rz) * Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateTranslation(tx, ty, tz);
                var world = normalize * axisConversion * placement;
                var wvp = world * view * proj;

                for (int i = 0; i < _modelResource.Parts.Length; i++)
                {
                    var part = _modelResource.Parts[i];
                    var texView = _modelResource.PartTextures[i];
                    _context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { texView != null ? texView! : _d3dResources.WhiteTextureView! });

                    ConstantBufferData cbData = new ConstantBufferData
                    {
                        WorldViewProj = Matrix4x4.Transpose(wvp),
                        World = Matrix4x4.Transpose(world),
                        LightPos = new Vector4(1, 1, 1, 0),
                        BaseColor = part.BaseColor,
                        AmbientColor = new Vector4(0.2f, 0.2f, 0.2f, 1),
                        LightColor = new Vector4(0.8f, 0.8f, 0.8f, 1),
                        CameraPos = new Vector4(camPos, 1),
                        LightEnabled = 1.0f,
                        DiffuseIntensity = 1.0f,
                        SpecularIntensity = 0.5f,
                        Shininess = 30.0f
                    };
                    UpdateConstantBuffer(ref cbData);

                    if (isInteracting)
                    {
                        _context.DrawIndexed(Math.Max(part.IndexCount / 16, 32), part.IndexOffset, 0);
                    }
                    else
                    {
                        _context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                    }
                }
            }

            if (_isGridVisible && _gridVertexBuffer != null)
            {
                _context.IASetInputLayout(_d3dResources.GridInputLayout);
                _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
                _context.IASetVertexBuffers(0, 1, new[] { _gridVertexBuffer }, new[] { 12 }, new[] { 0 });
                _context.VSSetShader(_d3dResources.GridVertexShader);
                _context.PSSetShader(_d3dResources.GridPixelShader);
                _context.OMSetBlendState(_d3dResources.GridBlendState, new Color4(0, 0, 0, 0), -1);

                Matrix4x4 gridWorld = Matrix4x4.Identity;
                if (!_isInfiniteGrid)
                {
                    float finiteScale = (float)(_modelScale * 50.0 / 1000.0);
                    if (finiteScale < 0.001f) finiteScale = 0.001f;
                    gridWorld = Matrix4x4.CreateScale(finiteScale);
                }

                ConstantBufferData gridCb = new ConstantBufferData { WorldViewProj = Matrix4x4.Transpose(gridWorld * view * proj), World = Matrix4x4.Transpose(gridWorld), CameraPos = new Vector4(camPos, 1) };
                UpdateConstantBuffer(ref gridCb);
                _context.Draw(6, 0);

                _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
            }

            _context.ResolveSubresource(_resolveTexture!, 0, _renderTarget!, 0, Format.B8G8R8A8_UNorm);
            _context.CopyResource(_stagingTexture, _resolveTexture);
            var map = _context.Map(_stagingTexture!, 0, MapMode.Read, D3D11.MapFlags.None);

            try
            {
                SceneImage.Lock();
                unsafe
                {
                    var srcPtr = (byte*)map.DataPointer;
                    var dstPtr = (byte*)SceneImage.BackBuffer;
                    for (int r = 0; r < _viewportHeight; r++)
                    {
                        Buffer.MemoryCopy(srcPtr + (r * map.RowPitch), dstPtr + (r * SceneImage.BackBufferStride), SceneImage.BackBufferStride, _viewportWidth * 4);
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

        private void UpdateConstantBuffer(ref ConstantBufferData data)
        {
            D3D11.MappedSubresource mapped;
            _context!.Map(_d3dResources!.ConstantBuffer, 0, MapMode.WriteDiscard, D3D11.MapFlags.None, out mapped);
            unsafe { Unsafe.Copy(mapped.DataPointer.ToPointer(), ref data); }
            _context.Unmap(_d3dResources.ConstantBuffer, 0);
            _context.VSSetConstantBuffers(0, new[] { _d3dResources.ConstantBuffer });
            _context.PSSetConstantBuffers(0, new[] { _d3dResources.ConstantBuffer });
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
                        fixed (byte* p = pixels) { using var t = _device.CreateTexture2D(tDesc, new[] { new SubresourceData(p, conv.PixelWidth * 4) }); partTextures[i] = _device.CreateShaderResourceView(t); }
                    }
                    catch { }
                }
            }
            _modelResource = new GpuResourceCacheItem(_device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale);

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (var v in model.Vertices)
            {
                double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }
            _modelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            _modelHeight = maxY - minY;
            if (_modelScale < 0.1) _modelScale = 1.0;
            _viewRadius = _modelScale * 2.5;
            UpdateViewportCamera();
            UpdateD3DScene();
        }

        private void ResetSceneCamera()
        {
            RecordUndo();
            CamX = 0; CamY = 0; CamZ = -_modelScale * 2.0;
            TargetX = 0; TargetY = 0; TargetZ = 0;

            _viewCenterX = 0; _viewCenterY = 0; _viewCenterZ = 0;

            _viewRadius = _modelScale * 3.0;
            _viewTheta = Math.PI / 4;
            _viewPhi = Math.PI / 4;
            AnimateView(Math.PI / 4, Math.PI / 4);
        }

        private void CreateViewCube()
        {
            ViewCubeModel = new Model3DGroup();
            var gray = new DiffuseMaterial(Brushes.Gray);
            var centerMesh = new MeshGeometry3D();
            AddCubeToMesh(centerMesh, new Point3D(0, 0, 0), 0.7);
            ViewCubeModel.Children.Add(new GeometryModel3D(centerMesh, gray));

            CubeFaceXPos = CreateFace(new Vector3D(1, 0, 0), new DiffuseMaterial(Brushes.Red), Texts.ViewRight);
            CubeFaceXNeg = CreateFace(new Vector3D(-1, 0, 0), new DiffuseMaterial(Brushes.DarkRed), Texts.ViewLeft);
            CubeFaceYPos = CreateFace(new Vector3D(0, 1, 0), new DiffuseMaterial(Brushes.Lime), Texts.ViewTop);
            CubeFaceYNeg = CreateFace(new Vector3D(0, -1, 0), new DiffuseMaterial(Brushes.Green), Texts.ViewBottom);
            CubeFaceZPos = CreateFace(new Vector3D(0, 0, 1), new DiffuseMaterial(Brushes.Blue), Texts.ViewFront);
            CubeFaceZNeg = CreateFace(new Vector3D(0, 0, -1), new DiffuseMaterial(Brushes.DarkBlue), Texts.ViewBack);
            ViewCubeModel.Children.Add(CubeFaceXPos); ViewCubeModel.Children.Add(CubeFaceXNeg);
            ViewCubeModel.Children.Add(CubeFaceYPos); ViewCubeModel.Children.Add(CubeFaceYNeg);
            ViewCubeModel.Children.Add(CubeFaceZPos); ViewCubeModel.Children.Add(CubeFaceZNeg);

            CornerFRT = CreateCorner(new Vector3D(1, 1, 1), Texts.CornerFRT);
            CornerFLT = CreateCorner(new Vector3D(-1, 1, 1), Texts.CornerFLT);
            CornerBRT = CreateCorner(new Vector3D(1, 1, -1), Texts.CornerBRT);
            CornerBLT = CreateCorner(new Vector3D(-1, 1, -1), Texts.CornerBLT);
            CornerFRB = CreateCorner(new Vector3D(1, -1, 1), Texts.CornerFRB);
            CornerFLB = CreateCorner(new Vector3D(-1, -1, 1), Texts.CornerFLB);
            CornerBRB = CreateCorner(new Vector3D(1, -1, -1), Texts.CornerBRB);
            CornerBLB = CreateCorner(new Vector3D(-1, -1, -1), Texts.CornerBLB);

            ViewCubeModel.Children.Add(CornerFRT); ViewCubeModel.Children.Add(CornerFLT);
            ViewCubeModel.Children.Add(CornerBRT); ViewCubeModel.Children.Add(CornerBLT);
            ViewCubeModel.Children.Add(CornerFRB); ViewCubeModel.Children.Add(CornerFLB);
            ViewCubeModel.Children.Add(CornerBRB); ViewCubeModel.Children.Add(CornerBLB);
        }

        private GeometryModel3D CreateFace(Vector3D dir, Material mat, string name)
        {
            var mesh = new MeshGeometry3D();
            var center = dir * 0.85;
            AddCubeToMesh(mesh, new Point3D(center.X, center.Y, center.Z), 0.6);
            var model = new GeometryModel3D(mesh, mat);
            model.SetValue(FrameworkElement.TagProperty, name);
            return model;
        }

        private GeometryModel3D CreateCorner(Vector3D dir, string name)
        {
            var mesh = new MeshGeometry3D();
            dir.Normalize();
            var center = dir * 0.85;
            AddCubeToMesh(mesh, new Point3D(center.X, center.Y, center.Z), 0.25);
            var mat = new DiffuseMaterial(Brushes.LightGray);
            var model = new GeometryModel3D(mesh, mat);
            model.SetValue(FrameworkElement.TagProperty, name);
            return model;
        }

        public void HandleGizmoMove(object? modelHit)
        {
            if (modelHit is GeometryModel3D gm && gm.GetValue(FrameworkElement.TagProperty) is string name) HoveredDirectionName = name;
            else HoveredDirectionName = "";

            CheckGizmoHit(modelHit);
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
            else if (modelHit == CornerFRT) AnimateView(Math.PI / 4, 0.955);
            else if (modelHit == CornerFLT) AnimateView(-Math.PI / 4, 0.955);
            else if (modelHit == CornerBRT) AnimateView(3 * Math.PI / 4, 0.955);
            else if (modelHit == CornerBLT) AnimateView(-3 * Math.PI / 4, 0.955);
            else if (modelHit == CornerFRB) AnimateView(Math.PI / 4, 2.186);
            else if (modelHit == CornerFLB) AnimateView(-Math.PI / 4, 2.186);
            else if (modelHit == CornerBRB) AnimateView(3 * Math.PI / 4, 2.186);
            else if (modelHit == CornerBLB) AnimateView(-3 * Math.PI / 4, 2.186);
        }

        private void AnimateView(double targetTheta, double targetPhi)
        {
            if (_animationTimer != null) _animationTimer.Stop();
            _animStartTheta = _viewTheta; _animStartPhi = _viewPhi;
            while (targetTheta - _animStartTheta > Math.PI) _animStartTheta += 2 * Math.PI;
            while (targetTheta - _animStartTheta < -Math.PI) _animStartTheta -= 2 * Math.PI;
            _animTargetTheta = targetTheta; _animTargetPhi = targetPhi; _animProgress = 0;
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += (s, e) =>
            {
                _animProgress += 0.08;
                if (_animProgress >= 1.0) { _animProgress = 1.0; _animationTimer.Stop(); _animationTimer = null; }
                double t = 1 - Math.Pow(1 - _animProgress, 3);
                _viewTheta = _animStartTheta + (_animTargetTheta - _animStartTheta) * t;
                _viewPhi = _animStartPhi + (_animTargetPhi - _animStartPhi) * t;
                UpdateViewportCamera();
            };
            _animationTimer.Start();
        }

        private void UpdateViewportCamera()
        {
            double yOffset = _modelHeight / 2.0;

            if (_isPilotView)
            {
                var camPos = new Point3D(_camX, _camY + yOffset, _camZ);
                var target = new Point3D(_targetX, _targetY + yOffset, _targetZ);
                Camera.Position = camPos;
                Camera.LookDirection = target - camPos;

                double fovValue = _parameter.Fov.Values[0].Value;
                if (Camera.FieldOfView != fovValue) Camera.FieldOfView = fovValue;
            }
            else
            {
                double y = _viewRadius * Math.Cos(_viewPhi);
                double hRadius = _viewRadius * Math.Sin(_viewPhi);
                double x = hRadius * Math.Sin(_viewTheta);
                double z = hRadius * Math.Cos(_viewTheta);

                var target = new Point3D(_viewCenterX, _viewCenterY, _viewCenterZ);
                var pos = new Point3D(x, y, z) + (Vector3D)target + new Vector3D(0, yOffset, 0);

                Camera.Position = pos;
                Camera.LookDirection = (target + new Vector3D(0, yOffset, 0)) - pos;

                if (Camera.FieldOfView != 45) Camera.FieldOfView = 45;
            }

            double gy = _gizmoRadius * Math.Cos(_viewPhi);
            double ghRadius = _gizmoRadius * Math.Sin(_viewPhi);
            double gx = ghRadius * Math.Sin(_viewTheta);
            double gz = ghRadius * Math.Cos(_viewTheta);
            GizmoCamera.Position = new Point3D(gx, gy, gz);
            GizmoCamera.LookDirection = new Point3D(0, 0, 0) - GizmoCamera.Position;
            UpdateD3DScene();
        }

        private void UpdateSceneCameraVisual()
        {
            CameraVisualGeometry = new MeshGeometry3D();
            TargetVisualGeometry = new MeshGeometry3D();
            GizmoXGeometry = new MeshGeometry3D(); GizmoYGeometry = new MeshGeometry3D(); GizmoZGeometry = new MeshGeometry3D();
            GizmoXYGeometry = new MeshGeometry3D(); GizmoYZGeometry = new MeshGeometry3D(); GizmoZXGeometry = new MeshGeometry3D();

            if (_isPilotView)
            {
                OnPropertyChanged(nameof(CameraVisualGeometry));
                OnPropertyChanged(nameof(TargetVisualGeometry));
                OnPropertyChanged(nameof(GizmoXGeometry)); OnPropertyChanged(nameof(GizmoYGeometry)); OnPropertyChanged(nameof(GizmoZGeometry));
                OnPropertyChanged(nameof(GizmoXYGeometry)); OnPropertyChanged(nameof(GizmoYZGeometry)); OnPropertyChanged(nameof(GizmoZXGeometry));
                return;
            }

            double yOffset = _modelHeight / 2.0;
            var camPos = new Point3D(_camX, _camY + yOffset, _camZ);
            var targetPos = new Point3D(_targetX, _targetY + yOffset, _targetZ);

            double gScale = _modelScale * 0.15;
            double gThick = gScale * 0.05;
            Point3D gPos = _isTargetFixed ? camPos : targetPos;

            bool isInteracting = _currentGizmoMode != GizmoMode.None || _isRotatingView || _isPanningView;
            int sphereDiv = isInteracting ? 4 : 16;
            int coneSegs = isInteracting ? 6 : 12;

            AddArrow(GizmoXGeometry, gPos, new Vector3D(1, 0, 0), gScale, gThick, coneSegs);
            AddArrow(GizmoYGeometry, gPos, new Vector3D(0, 1, 0), gScale, gThick, coneSegs);

            var zDir = _isTargetFixed ? new Vector3D(0, 0, -1) : new Vector3D(0, 0, 1);
            AddArrow(GizmoZGeometry, gPos, zDir, gScale, gThick, coneSegs);

            double pOff = gScale * 0.3;
            double pSz = gScale * 0.2;
            AddQuad(GizmoXYGeometry, gPos + new Vector3D(pOff, pOff, 0), new Vector3D(0, 0, 1), pSz);
            AddQuad(GizmoYZGeometry, gPos + new Vector3D(0, pOff, pOff), new Vector3D(1, 0, 0), pSz);
            AddQuad(GizmoZXGeometry, gPos + new Vector3D(pOff, 0, pOff), new Vector3D(0, 1, 0), pSz);

            var dir = targetPos - camPos;
            if (dir.LengthSquared < 0.0001) dir = new Vector3D(0, 0, -1);
            double dist = dir.Length;
            dir.Normalize();

            AddLineToMesh(CameraVisualGeometry, camPos, camPos + (dir * _modelScale * 100.0), _modelScale * 0.003 * 0.5);

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

            AddFrustumMesh(CameraVisualGeometry, camPos, tr, tl, bl, br);

            AddSphereToMesh(TargetVisualGeometry, targetPos, _modelScale * 0.05, sphereDiv, sphereDiv);

            OnPropertyChanged(nameof(CameraVisualGeometry));
            OnPropertyChanged(nameof(TargetVisualGeometry));
            OnPropertyChanged(nameof(GizmoXGeometry)); OnPropertyChanged(nameof(GizmoYGeometry)); OnPropertyChanged(nameof(GizmoZGeometry));
            OnPropertyChanged(nameof(GizmoXYGeometry)); OnPropertyChanged(nameof(GizmoYZGeometry)); OnPropertyChanged(nameof(GizmoZXGeometry));
        }

        private void AddArrow(MeshGeometry3D mesh, Point3D start, Vector3D dir, double len, double thick, int segs = 12)
        {
            AddLineToMesh(mesh, start, start + dir * len, thick);
            AddConeToMesh(mesh, start + dir * len, dir, thick * 2.5, thick * 5, segs);
        }

        private void AddConeToMesh(MeshGeometry3D mesh, Point3D tip, Vector3D dir, double radius, double height, int segs = 12)
        {
            dir.Normalize();
            var perp1 = Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0));
            if (perp1.LengthSquared < 0.001) perp1 = Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(dir, perp1);
            Point3D centerBase = tip - dir * height;
            int tipIdx = mesh.Positions.Count;
            mesh.Positions.Add(tip);
            int baseCenterIdx = mesh.Positions.Count;
            mesh.Positions.Add(centerBase);
            int baseStartIdx = mesh.Positions.Count;

            for (int i = 0; i < segs; i++)
            {
                double angle = i * 2 * Math.PI / segs;
                var pt = centerBase + perp1 * radius * Math.Cos(angle) + perp2 * radius * Math.Sin(angle);
                mesh.Positions.Add(pt);
            }

            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                mesh.TriangleIndices.Add(tipIdx);
                mesh.TriangleIndices.Add(baseStartIdx + i);
                mesh.TriangleIndices.Add(baseStartIdx + next);
                mesh.TriangleIndices.Add(baseCenterIdx);
                mesh.TriangleIndices.Add(baseStartIdx + next);
                mesh.TriangleIndices.Add(baseStartIdx + i);
            }
        }

        private void AddQuad(MeshGeometry3D mesh, Point3D center, Vector3D normal, double size)
        {
            normal.Normalize();
            var u = Vector3D.CrossProduct(normal, new Vector3D(0, 1, 0));
            if (u.LengthSquared < 0.001) u = Vector3D.CrossProduct(normal, new Vector3D(1, 0, 0));
            u.Normalize();
            var v = Vector3D.CrossProduct(normal, u);
            double s = size / 2;
            Point3D p0 = center - u * s - v * s;
            Point3D p1 = center + u * s - v * s;
            Point3D p2 = center + u * s + v * s;
            Point3D p3 = center - u * s + v * s;
            int idx = mesh.Positions.Count;
            mesh.Positions.Add(p0); mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 1); mesh.TriangleIndices.Add(idx + 2);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 2); mesh.TriangleIndices.Add(idx + 3);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 2); mesh.TriangleIndices.Add(idx + 1);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 3); mesh.TriangleIndices.Add(idx + 2);
        }

        private void AddFrustumMesh(MeshGeometry3D mesh, Point3D o, Point3D tr, Point3D tl, Point3D bl, Point3D br)
        {
            AddLineToMesh(mesh, o, tr, 0.01); AddLineToMesh(mesh, o, tl, 0.01);
            AddLineToMesh(mesh, o, br, 0.01); AddLineToMesh(mesh, o, bl, 0.01);
            AddLineToMesh(mesh, tr, tl, 0.01); AddLineToMesh(mesh, tl, bl, 0.01);
            AddLineToMesh(mesh, bl, br, 0.01); AddLineToMesh(mesh, br, tr, 0.01);

            int idx = mesh.Positions.Count;
            mesh.Positions.Add(o); mesh.Positions.Add(tr); mesh.Positions.Add(tl); mesh.Positions.Add(bl); mesh.Positions.Add(br);
            int[] indices = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 4, 3, 2, 4, 2, 1 };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private void AddCubeToMesh(MeshGeometry3D mesh, Point3D center, double size)
        {
            double s = size / 2.0;
            Point3D[] p = { new(center.X - s, center.Y - s, center.Z + s), new(center.X + s, center.Y - s, center.Z + s), new(center.X + s, center.Y + s, center.Z + s), new(center.X - s, center.Y + s, center.Z + s), new(center.X - s, center.Y - s, center.Z - s), new(center.X + s, center.Y - s, center.Z - s), new(center.X + s, center.Y + s, center.Z - s), new(center.X - s, center.Y + s, center.Z - s) };
            int idx = mesh.Positions.Count;
            foreach (var pt in p) mesh.Positions.Add(pt);
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private void AddLineToMesh(MeshGeometry3D mesh, Point3D start, Point3D end, double thickness)
        {
            var vec = end - start;
            if (vec.LengthSquared < double.Epsilon) return;
            vec.Normalize();
            var p1 = Vector3D.CrossProduct(vec, new Vector3D(0, 1, 0));
            if (p1.LengthSquared < 0.001) p1 = Vector3D.CrossProduct(vec, new Vector3D(1, 0, 0));
            p1.Normalize();
            var p2 = Vector3D.CrossProduct(vec, p1);
            p1 *= thickness; p2 *= thickness;
            int idx = mesh.Positions.Count;
            mesh.Positions.Add(start - p1 - p2); mesh.Positions.Add(start + p1 - p2);
            mesh.Positions.Add(start + p1 + p2); mesh.Positions.Add(start - p1 + p2);
            mesh.Positions.Add(end - p1 - p2); mesh.Positions.Add(end + p1 - p2);
            mesh.Positions.Add(end + p1 + p2); mesh.Positions.Add(end - p1 + p2);
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private void AddSphereToMesh(MeshGeometry3D mesh, Point3D center, double radius, int tDiv, int pDiv)
        {
            int baseIdx = mesh.Positions.Count;
            for (int pi = 0; pi <= pDiv; pi++)
            {
                double phi = pi * Math.PI / pDiv;
                for (int ti = 0; ti <= tDiv; ti++)
                {
                    double theta = ti * 2 * Math.PI / tDiv;
                    var pt = new Point3D(center.X + radius * Math.Sin(phi) * Math.Cos(theta), center.Y + radius * Math.Cos(phi), center.Z + radius * Math.Sin(phi) * Math.Sin(theta));
                    mesh.Positions.Add(pt);
                }
            }
            for (int pi = 0; pi < pDiv; pi++)
            {
                for (int ti = 0; ti < tDiv; ti++)
                {
                    int x0 = ti; int x1 = ti + 1; int y0 = pi * (tDiv + 1); int y1 = (pi + 1) * (tDiv + 1);
                    mesh.TriangleIndices.Add(baseIdx + y0 + x0); mesh.TriangleIndices.Add(baseIdx + y1 + x0); mesh.TriangleIndices.Add(baseIdx + y0 + x1);
                    mesh.TriangleIndices.Add(baseIdx + y1 + x0); mesh.TriangleIndices.Add(baseIdx + y1 + x1); mesh.TriangleIndices.Add(baseIdx + y0 + x1);
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

        public void RecordUndo()
        {
            _undoStack.Push((_camX, _camY, _camZ, _targetX, _targetY, _targetZ));
            _redoStack.Clear();
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        public void PerformUndo()
        {
            if (_undoStack.Count > 0)
            {
                _redoStack.Push((_camX, _camY, _camZ, _targetX, _targetY, _targetZ));
                var state = _undoStack.Pop();
                _camX = state.cx; _camY = state.cy; _camZ = state.cz;
                _targetX = state.tx; _targetY = state.ty; _targetZ = state.tz;
                OnPropertyChanged(nameof(CamX)); OnPropertyChanged(nameof(CamY)); OnPropertyChanged(nameof(CamZ));
                OnPropertyChanged(nameof(TargetX)); OnPropertyChanged(nameof(TargetY)); OnPropertyChanged(nameof(TargetZ));
                SyncToParameter(); UpdateSceneCameraVisual(); UpdateD3DScene(); UpdateViewportCamera();
                UndoCommand.RaiseCanExecuteChanged(); RedoCommand.RaiseCanExecuteChanged();
            }
        }

        public void PerformRedo()
        {
            if (_redoStack.Count > 0)
            {
                _undoStack.Push((_camX, _camY, _camZ, _targetX, _targetY, _targetZ));
                var state = _redoStack.Pop();
                _camX = state.cx; _camY = state.cy; _camZ = state.cz;
                _targetX = state.tx; _targetY = state.ty; _targetZ = state.tz;
                OnPropertyChanged(nameof(CamX)); OnPropertyChanged(nameof(CamY)); OnPropertyChanged(nameof(CamZ));
                OnPropertyChanged(nameof(TargetX)); OnPropertyChanged(nameof(TargetY)); OnPropertyChanged(nameof(TargetZ));
                SyncToParameter(); UpdateSceneCameraVisual(); UpdateD3DScene(); UpdateViewportCamera();
                UndoCommand.RaiseCanExecuteChanged(); RedoCommand.RaiseCanExecuteChanged();
            }
        }

        public void PerformFocus()
        {
            if (_isPilotView) return;

            _viewRadius = _modelScale * 2.0;
            _viewCenterX = _targetX;
            _viewCenterY = _targetY;
            _viewCenterZ = _targetZ;
            UpdateViewportCamera();
        }

        public void Zoom(int delta)
        {
            if (_isPilotView)
            {
                double speed = _modelScale * 0.1 * (delta > 0 ? 1 : -1);
                var dir = Camera.LookDirection; dir.Normalize();
                _camX += dir.X * speed; _camY += dir.Y * speed; _camZ += dir.Z * speed;
                OnPropertyChanged(nameof(CamX)); OnPropertyChanged(nameof(CamY)); OnPropertyChanged(nameof(CamZ));
                UpdateSceneCameraVisual(); UpdateD3DScene(); UpdateViewportCamera();
                SyncToParameter();
            }
            else
            {
                _viewRadius -= delta * (_modelScale * 0.005);
                if (_viewRadius < _modelScale * 0.01) _viewRadius = _modelScale * 0.01;
                UpdateViewportCamera();
            }
        }

        public void StartPan(Point pos)
        {
            IsTargetFixed = false;
            _isPanningView = true;
            _lastMousePos = pos;
        }

        public void StartRotate(Point pos)
        {
            _isRotatingView = true;
            _lastMousePos = pos;
        }

        public void CheckGizmoHit(object? modelHit)
        {
            _currentGizmoMode = GizmoMode.None;
            _hoveredGeometry = null;

            if (modelHit is GeometryModel3D gm)
            {
                _hoveredGeometry = gm.Geometry;

                if (gm.Geometry == GizmoXGeometry) _currentGizmoMode = GizmoMode.X;
                else if (gm.Geometry == GizmoYGeometry) _currentGizmoMode = GizmoMode.Y;
                else if (gm.Geometry == GizmoZGeometry) _currentGizmoMode = GizmoMode.Z;
                else if (gm.Geometry == GizmoXYGeometry) _currentGizmoMode = GizmoMode.XY;
                else if (gm.Geometry == GizmoYZGeometry) _currentGizmoMode = GizmoMode.YZ;
                else if (gm.Geometry == GizmoZXGeometry) _currentGizmoMode = GizmoMode.ZX;
                else if (gm.Geometry == TargetVisualGeometry || gm.Geometry == CameraVisualGeometry)
                {
                    _currentGizmoMode = GizmoMode.View;
                }
            }
        }

        public void StartGizmoDrag(Point pos)
        {
            RecordUndo();

            if (_currentGizmoMode == GizmoMode.View)
            {
                if (_hoveredGeometry == CameraVisualGeometry) IsTargetFixed = true;
                else if (_hoveredGeometry == TargetVisualGeometry) IsTargetFixed = false;
            }

            if (_currentGizmoMode != GizmoMode.None)
            {
                _isDraggingTarget = true;
                _lastMousePos = pos;
            }
            else
            {
                _isSpacePanning = true;
                _lastMousePos = pos;
            }
        }

        public void EndDrag()
        {
            _isRotatingView = false;
            _isPanningView = false;
            _isDraggingTarget = false;
            _isSpacePanning = false;
            _currentGizmoMode = GizmoMode.None;
            SyncToParameter();
            UpdateSceneCameraVisual();
            UpdateD3DScene();
        }

        public void ScrubValue(string axis, double delta)
        {
            RecordUndo();
            double val = delta * _modelScale * 0.01;
            if (axis == "X") { _camX += val; OnPropertyChanged(nameof(CamX)); }
            else if (axis == "Y") { _camY += val; OnPropertyChanged(nameof(CamY)); }
            else if (axis == "Z") { _camZ += val; OnPropertyChanged(nameof(CamZ)); }

            UpdateSceneCameraVisual();
            UpdateD3DScene();
        }

        public void Move(Point pos)
        {
            var dx = pos.X - _lastMousePos.X;
            var dy = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            if (_isDraggingTarget && _currentGizmoMode != GizmoMode.None)
            {
                double speed = _modelScale * 0.005;
                if (IsSnapping) speed = Math.Round(speed * 10) / 10.0;

                double mx = 0, my = 0, mz = 0;

                var camDir = Camera.LookDirection; camDir.Normalize();
                var camRight = Vector3D.CrossProduct(camDir, Camera.UpDirection); camRight.Normalize();
                var camUp = Vector3D.CrossProduct(camRight, camDir); camUp.Normalize();

                var moveVec = camRight * dx * speed + (-camUp) * dy * speed;

                switch (_currentGizmoMode)
                {
                    case GizmoMode.X: mx = moveVec.X; break;
                    case GizmoMode.Y: my = moveVec.Y; break;
                    case GizmoMode.Z: mz = moveVec.Z; break;
                    case GizmoMode.XY: mx = moveVec.X; my = moveVec.Y; break;
                    case GizmoMode.YZ: my = moveVec.Y; mz = moveVec.Z; break;
                    case GizmoMode.ZX: mx = moveVec.X; mz = moveVec.Z; break;
                    case GizmoMode.View: mx = moveVec.X; my = moveVec.Y; mz = moveVec.Z; break;
                }

                if (IsSnapping)
                {
                    mx = Math.Round(mx / 0.5) * 0.5; my = Math.Round(my / 0.5) * 0.5; mz = Math.Round(mz / 0.5) * 0.5;
                }

                if (_isTargetFixed)
                {
                    _camX += mx; _camY += my; _camZ += mz;
                    OnPropertyChanged(nameof(CamX)); OnPropertyChanged(nameof(CamY)); OnPropertyChanged(nameof(CamZ));
                }
                else
                {
                    _targetX += mx; _targetY += my; _targetZ += mz;
                    OnPropertyChanged(nameof(TargetX)); OnPropertyChanged(nameof(TargetY)); OnPropertyChanged(nameof(TargetZ));
                }
                UpdateSceneCameraVisual();
                UpdateD3DScene();
            }
            else if (_isSpacePanning)
            {
                var look = Camera.LookDirection; look.Normalize();
                var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
                var up = Vector3D.CrossProduct(right, look); up.Normalize();
                double panSpeed = _viewRadius * 0.002;
                var move = (-right * dx * panSpeed) + (up * dy * panSpeed);
                _viewCenterX += move.X; _viewCenterY += move.Y; _viewCenterZ += move.Z;
                UpdateViewportCamera();
            }
            else if (_isRotatingView || _isPanningView)
            {
                if (_isPanningView)
                {
                    if (!_isTargetFixed)
                    {
                        var look = Camera.LookDirection; look.Normalize();
                        var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
                        var up = Vector3D.CrossProduct(right, look); up.Normalize();
                        double panSpeed = _viewRadius * 0.002;
                        var move = (-right * dx * panSpeed) + (up * dy * panSpeed);
                        _targetX += move.X; _targetY += move.Y; _targetZ += move.Z;
                        OnPropertyChanged(nameof(TargetX)); OnPropertyChanged(nameof(TargetY)); OnPropertyChanged(nameof(TargetZ));
                        UpdateSceneCameraVisual();
                        UpdateD3DScene();
                    }
                }
                else
                {
                    _viewTheta += dx * 0.01;
                    _viewPhi -= dy * 0.01;
                    if (_viewPhi < 0.01) _viewPhi = 0.01;
                    if (_viewPhi > Math.PI - 0.01) _viewPhi = Math.PI - 0.01;
                    if (IsSnapping)
                    {
                        _viewTheta = Math.Round(_viewTheta / (Math.PI / 12)) * (Math.PI / 12);
                    }
                    UpdateViewportCamera();
                }
            }
        }

        public void MovePilot(double fwd, double right, double up)
        {
            if (!_isPilotView) return;
            double speed = _modelScale * 0.05;
            var look = Camera.LookDirection; look.Normalize();
            var r = Vector3D.CrossProduct(look, Camera.UpDirection); r.Normalize();
            var u = Vector3D.CrossProduct(r, look); u.Normalize();

            var move = look * fwd * speed + r * right * speed + u * up * speed;
            _camX += move.X; _camY += move.Y; _camZ += move.Z;
            _targetX += move.X; _targetY += move.Y; _targetZ += move.Z;
            OnPropertyChanged(nameof(CamX)); OnPropertyChanged(nameof(CamY)); OnPropertyChanged(nameof(CamZ));
            OnPropertyChanged(nameof(TargetX)); OnPropertyChanged(nameof(TargetY)); OnPropertyChanged(nameof(TargetZ));
            UpdateViewportCamera();
            UpdateSceneCameraVisual();
        }

        public void Dispose()
        {
            _animationTimer?.Stop();
            _animationTimer = null;
            _d3dResources?.Dispose();
            _d3dResources = null;
            _rtv?.Dispose();
            _rtv = null;
            _renderTarget?.Dispose();
            _renderTarget = null;
            _dsv?.Dispose();
            _dsv = null;
            _depthStencil?.Dispose();
            _depthStencil = null;
            _stagingTexture?.Dispose();
            _stagingTexture = null;
            _resolveTexture?.Dispose();
            _resolveTexture = null;
            _modelResource?.Dispose();
            _modelResource = null;
            _gridVertexBuffer?.Dispose();
            _gridVertexBuffer = null;

            if (_context != null)
            {
                _context.ClearState();
                _context.Flush();
                _context.Dispose();
                _context = null;
            }
            _device?.Dispose();
            _device = null;
        }
    }
}
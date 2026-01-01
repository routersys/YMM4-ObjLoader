using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using ObjLoader.Core;
using ObjLoader.Cache;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Settings;
using D2D = Vortice.Direct2D1;
using D3D11 = Vortice.Direct3D11;

namespace ObjLoader.Rendering
{
    public class ObjLoaderSource : IShapeSource
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly ObjLoaderParameter _parameter;
        private readonly DisposeCollector _disposer = new DisposeCollector();
        private readonly ObjModelLoader _loader;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;

        private D2D.ID2D1CommandList? _commandList;

        private double _lastX = double.NaN;
        private double _lastY = double.NaN;
        private double _lastZ = double.NaN;
        private double _lastScale = double.NaN;
        private double _lastRx = double.NaN;
        private double _lastRy = double.NaN;
        private double _lastRz = double.NaN;
        private double _lastFov = double.NaN;
        private ProjectionType _lastProjection = (ProjectionType)(-1);
        private bool _lastLightEnabled = false;
        private double _lastLightX = double.NaN;
        private double _lastLightY = double.NaN;
        private double _lastLightZ = double.NaN;
        private string _lastFilePath = string.Empty;
        private Color _lastBaseColor = Colors.Transparent;
        private CoordinateSystem _lastCoordinateSystem = (CoordinateSystem)(-1);
        private RenderCullMode _lastCullMode = (RenderCullMode)(-1);
        private Color _lastAmbientColor = Colors.Transparent;
        private Color _lastLightColor = Colors.Transparent;
        private double _lastDiffuseIntensity = double.NaN;
        private double _lastSpecularIntensity = double.NaN;
        private double _lastShininess = double.NaN;

        static ObjLoaderSource()
        {
            var app = Application.Current;
            if (app != null)
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    if (app.MainWindow != null)
                    {
                        app.MainWindow.Closing += (s, e) => GpuResourceCache.Clear();
                    }
                    app.Exit += (s, e) => GpuResourceCache.Clear();
                    app.Dispatcher.ShutdownStarted += (s, e) => GpuResourceCache.Clear();
                });
            }
        }

        public D2D.ID2D1Image Output => _commandList ?? throw new InvalidOperationException("Update must be called before accessing Output.");

        public ObjLoaderSource(IGraphicsDevicesAndContext devices, ObjLoaderParameter parameter)
        {
            _devices = devices;
            _parameter = parameter;
            _loader = new ObjModelLoader();
            _resources = new D3DResources(devices.D3D.Device);
            _renderTargets = new RenderTargetManager();
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            int sw = (int)_parameter.ScreenWidth.GetValue(frame, length, fps);
            int sh = (int)_parameter.ScreenHeight.GetValue(frame, length, fps);
            sw = Math.Max(1, sw);
            sh = Math.Max(1, sh);

            bool resized = _renderTargets.EnsureSize(_devices, sw, sh);

            var x = _parameter.X.GetValue(frame, length, fps);
            var y = _parameter.Y.GetValue(frame, length, fps);
            var z = _parameter.Z.GetValue(frame, length, fps);
            var scale = _parameter.Scale.GetValue(frame, length, fps);
            var rx = _parameter.RotationX.GetValue(frame, length, fps);
            var ry = _parameter.RotationY.GetValue(frame, length, fps);
            var rz = _parameter.RotationZ.GetValue(frame, length, fps);
            var fov = _parameter.Fov.GetValue(frame, length, fps);
            var lightX = _parameter.LightX.GetValue(frame, length, fps);
            var lightY = _parameter.LightY.GetValue(frame, length, fps);
            var lightZ = _parameter.LightZ.GetValue(frame, length, fps);
            var baseColor = _parameter.BaseColor;
            var filePath = _parameter.FilePath?.Trim('"') ?? string.Empty;
            var worldIdParam = (int)_parameter.WorldId.GetValue(frame, length, fps);

            var settings = PluginSettings.Instance;
            var coordSystem = settings.CoordinateSystem;
            var cullMode = settings.CullMode;

            var ambientColor = settings.GetAmbientColor(worldIdParam);
            var lightColor = settings.GetLightColor(worldIdParam);
            var diffuseIntensity = settings.GetDiffuseIntensity(worldIdParam);
            var specularIntensity = settings.GetSpecularIntensity(worldIdParam);
            var shininess = settings.GetShininess(worldIdParam);

            _resources.UpdateRasterizerState(cullMode);

            GpuResourceCacheItem? resource = null;

            if (GpuResourceCache.TryGetValue(filePath, out var cached))
            {
                if (cached != null && cached.Device == _devices.D3D.Device)
                {
                    resource = cached;
                }
            }

            if (resource == null)
            {
                var model = _loader.Load(filePath);
                if (model.Vertices.Length > 0)
                {
                    resource = CreateGpuResource(model, filePath);
                    model = null;
                    GC.Collect(2, GCCollectionMode.Optimized);
                }
            }

            if (resource == null)
            {
                EnsureEmptyCommandList();
                return;
            }

            if (!resized &&
                _commandList != null &&
                string.Equals(_lastFilePath, filePath, StringComparison.Ordinal) &&
                Math.Abs(_lastX - x) < 1e-5 &&
                Math.Abs(_lastY - y) < 1e-5 &&
                Math.Abs(_lastZ - z) < 1e-5 &&
                Math.Abs(_lastScale - scale) < 1e-5 &&
                Math.Abs(_lastRx - rx) < 1e-5 &&
                Math.Abs(_lastRy - ry) < 1e-5 &&
                Math.Abs(_lastRz - rz) < 1e-5 &&
                Math.Abs(_lastFov - fov) < 1e-5 &&
                _lastProjection == _parameter.Projection &&
                _lastLightEnabled == _parameter.IsLightEnabled &&
                Math.Abs(_lastLightX - lightX) < 1e-5 &&
                Math.Abs(_lastLightY - lightY) < 1e-5 &&
                Math.Abs(_lastLightZ - lightZ) < 1e-5 &&
                _lastBaseColor == baseColor &&
                _lastCoordinateSystem == coordSystem &&
                _lastCullMode == cullMode &&
                _lastAmbientColor == ambientColor &&
                _lastLightColor == lightColor &&
                Math.Abs(_lastDiffuseIntensity - diffuseIntensity) < 1e-5 &&
                Math.Abs(_lastSpecularIntensity - specularIntensity) < 1e-5 &&
                Math.Abs(_lastShininess - shininess) < 1e-5)
            {
                return;
            }

            RenderToTexture(resource, sw, sh, x, y, z, scale, rx, ry, rz, fov, lightX, lightY, lightZ, baseColor, coordSystem, ambientColor, lightColor, diffuseIntensity, specularIntensity, shininess);
            CreateCommandList();

            _lastX = x;
            _lastY = y;
            _lastZ = z;
            _lastScale = scale;
            _lastRx = rx;
            _lastRy = ry;
            _lastRz = rz;
            _lastFov = fov;
            _lastProjection = _parameter.Projection;
            _lastLightEnabled = _parameter.IsLightEnabled;
            _lastLightX = lightX;
            _lastLightY = lightY;
            _lastLightZ = lightZ;
            _lastFilePath = filePath;
            _lastBaseColor = baseColor;
            _lastCoordinateSystem = coordSystem;
            _lastCullMode = cullMode;
            _lastAmbientColor = ambientColor;
            _lastLightColor = lightColor;
            _lastDiffuseIntensity = diffuseIntensity;
            _lastSpecularIntensity = specularIntensity;
            _lastShininess = shininess;
        }

        private unsafe GpuResourceCacheItem CreateGpuResource(ObjModel model, string filePath)
        {
            var device = _devices.D3D.Device;

            var vDesc = new BufferDescription(
                model.Vertices.Length * Unsafe.SizeOf<ObjVertex>(),
                BindFlags.VertexBuffer,
                ResourceUsage.Immutable,
                CpuAccessFlags.None);

            ID3D11Buffer vb;
            fixed (ObjVertex* pVerts = model.Vertices)
            {
                var vData = new SubresourceData(pVerts);
                vb = device.CreateBuffer(vDesc, vData);
            }

            var iDesc = new BufferDescription(
                model.Indices.Length * sizeof(int),
                BindFlags.IndexBuffer,
                ResourceUsage.Immutable,
                CpuAccessFlags.None);

            ID3D11Buffer ib;
            fixed (int* pIndices = model.Indices)
            {
                var iData = new SubresourceData(pIndices);
                ib = device.CreateBuffer(iDesc, iData);
            }

            var parts = model.Parts.ToArray();
            var partTextures = new ID3D11ShaderResourceView?[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                string tPath = parts[i].TexturePath;
                if (!string.IsNullOrEmpty(tPath) && File.Exists(tPath))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(tPath);
                        using var ms = new MemoryStream(bytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        var format = PixelFormats.Bgra32;
                        var convertedBitmap = new FormatConvertedBitmap(bitmap, format, null, 0);

                        int width = convertedBitmap.PixelWidth;
                        int height = convertedBitmap.PixelHeight;
                        int stride = width * 4;
                        byte[] pixels = new byte[stride * height];
                        convertedBitmap.CopyPixels(pixels, stride, 0);

                        var texDesc = new Texture2DDescription
                        {
                            Width = width,
                            Height = height,
                            MipLevels = 1,
                            ArraySize = 1,
                            Format = Format.B8G8R8A8_UNorm,
                            SampleDescription = new SampleDescription(1, 0),
                            Usage = ResourceUsage.Immutable,
                            BindFlags = BindFlags.ShaderResource
                        };

                        fixed (byte* p = pixels)
                        {
                            var data = new SubresourceData(p, stride);
                            using var tex = device.CreateTexture2D(texDesc, new[] { data });
                            partTextures[i] = device.CreateShaderResourceView(tex);
                        }
                    }
                    catch { }
                }
            }

            var item = new GpuResourceCacheItem(device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale);
            GpuResourceCache.AddOrUpdate(filePath, item);
            return item;
        }

        private void RenderToTexture(GpuResourceCacheItem resource, int width, int height, double x, double y, double z, double scale, double rx, double ry, double rz, double fov, double lightX, double lightY, double lightZ, Color baseColor, CoordinateSystem coordSystem, Color ambientColor, Color lightColor, double diffuseIntensity, double specularIntensity, double shininess)
        {
            if (_resources.ConstantBuffer == null || _renderTargets.RenderTargetView == null) return;

            var context = _devices.D3D.Device.ImmediateContext;

            context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);
            context.ClearRenderTargetView(_renderTargets.RenderTargetView, new Vortice.Mathematics.Color4(0, 0, 0, 0));
            context.ClearDepthStencilView(_renderTargets.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

            context.RSSetState(_resources.RasterizerState);
            context.OMSetDepthStencilState(_resources.DepthStencilState);
            context.OMSetBlendState(_resources.BlendState, new Vortice.Mathematics.Color4(0, 0, 0, 0), -1);

            context.RSSetViewport(0, 0, width, height);

            context.IASetInputLayout(_resources.InputLayout);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            int stride = Unsafe.SizeOf<ObjVertex>();
            int offset = 0;
            context.IASetVertexBuffers(0, 1, new[] { resource.VertexBuffer }, new[] { stride }, new[] { offset });
            context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(_resources.PixelShader);

            context.PSSetSamplers(0, new[] { _resources.SamplerState });

            var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);

            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (coordSystem)
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

            var rotation = Matrix4x4.CreateRotationX((float)(rx * Math.PI / 180.0)) *
                           Matrix4x4.CreateRotationY((float)(ry * Math.PI / 180.0)) *
                           Matrix4x4.CreateRotationZ((float)(rz * Math.PI / 180.0));
            var userScale = Matrix4x4.CreateScale((float)(scale / 100.0));
            var userTranslation = Matrix4x4.CreateTranslation((float)x, (float)y, (float)z);

            var world = normalize * axisConversion * rotation * userScale * userTranslation;

            Matrix4x4 view, proj;
            float aspect = (float)width / height;
            Vector3 cameraPosition;

            if (_parameter.Projection == ProjectionType.Parallel)
            {
                cameraPosition = new Vector3(0, 0, -2.0f);
                view = Matrix4x4.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.UnitY);
                proj = Matrix4x4.CreateOrthographic(2.0f * aspect, 2.0f, 0.1f, 100.0f);
            }
            else
            {
                cameraPosition = new Vector3(0, 0, -2.5f);
                view = Matrix4x4.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.UnitY);
                float radFov = (float)(Math.Max(1, Math.Min(179, fov)) * Math.PI / 180.0);
                proj = Matrix4x4.CreatePerspectiveFieldOfView(radFov, aspect, 0.1f, 100.0f);
            }

            var wvp = world * view * proj;
            var lightPos = new Vector4((float)lightX, (float)lightY, (float)lightZ, 1.0f);
            var amb = new Vector4(ambientColor.ScR, ambientColor.ScG, ambientColor.ScB, ambientColor.ScA);
            var lCol = new Vector4(lightColor.ScR, lightColor.ScG, lightColor.ScB, lightColor.ScA);
            var camPos = new Vector4(cameraPosition, 1.0f);

            for (int i = 0; i < resource.Parts.Length; i++)
            {
                var part = resource.Parts[i];
                var texView = resource.PartTextures[i];
                bool hasTexture = texView != null;

                context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { hasTexture ? texView! : _resources.WhiteTextureView! });

                var uiColorVec = hasTexture ? Vector4.One : new Vector4(baseColor.ScR, baseColor.ScG, baseColor.ScB, baseColor.ScA);
                var partColor = part.BaseColor * uiColorVec;

                ConstantBufferData cbData = new ConstantBufferData
                {
                    WorldViewProj = Matrix4x4.Transpose(wvp),
                    World = Matrix4x4.Transpose(world),
                    LightPos = lightPos,
                    BaseColor = partColor,
                    AmbientColor = amb,
                    LightColor = lCol,
                    CameraPos = camPos,
                    LightEnabled = _parameter.IsLightEnabled ? 1.0f : 0.0f,
                    DiffuseIntensity = (float)diffuseIntensity,
                    SpecularIntensity = (float)specularIntensity,
                    Shininess = (float)shininess
                };

                D3D11.MappedSubresource mapped;
                context.Map(_resources.ConstantBuffer, 0, D3D11.MapMode.WriteDiscard, D3D11.MapFlags.None, out mapped);
                unsafe
                {
                    Unsafe.Copy(mapped.DataPointer.ToPointer(), ref cbData);
                }
                context.Unmap(_resources.ConstantBuffer, 0);

                context.VSSetConstantBuffers(0, new[] { _resources.ConstantBuffer });
                context.PSSetConstantBuffers(0, new[] { _resources.ConstantBuffer });

                context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
            }

            context.OMSetRenderTargets((ID3D11RenderTargetView?)null!, (ID3D11DepthStencilView?)null);
            context.Flush();
        }

        private void CreateCommandList()
        {
            _disposer.RemoveAndDispose(ref _commandList);
            _commandList = _devices.DeviceContext.CreateCommandList();
            _disposer.Collect(_commandList);

            var dc = _devices.DeviceContext;

            dc.Target = _commandList;
            dc.BeginDraw();
            dc.Clear(null);

            if (_renderTargets.SharedBitmap != null)
            {
                dc.DrawImage(_renderTargets.SharedBitmap, new Vector2(-_renderTargets.SharedBitmap.Size.Width / 2.0f, -_renderTargets.SharedBitmap.Size.Height / 2.0f));
            }

            dc.EndDraw();
            dc.Target = null;
            _commandList.Close();
        }

        private void EnsureEmptyCommandList()
        {
            if (_commandList == null)
            {
                _commandList = _devices.DeviceContext.CreateCommandList();
                _disposer.Collect(_commandList);
                var dc = _devices.DeviceContext;
                dc.Target = _commandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.EndDraw();
                dc.Target = null;
                _commandList.Close();
            }
        }

        public void Dispose()
        {
            _resources.Dispose();
            _renderTargets.Dispose();
            _disposer.DisposeAndClear();
        }
    }
}
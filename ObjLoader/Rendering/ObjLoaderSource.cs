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
        private ID3D11VertexShader? _customVertexShader;
        private ID3D11PixelShader? _customPixelShader;
        private ID3D11InputLayout? _customInputLayout;

        private double _lastSettingsVersion = double.NaN;
        private double _lastCamX = double.NaN;
        private double _lastCamY = double.NaN;
        private double _lastCamZ = double.NaN;
        private double _lastTargetX = double.NaN;
        private double _lastTargetY = double.NaN;
        private double _lastTargetZ = double.NaN;

        private Dictionary<string, LayerState> _layerStates = new Dictionary<string, LayerState>();

        private struct LayerState
        {
            public double X, Y, Z, Scale, Rx, Ry, Rz, Cx, Cy, Cz, Fov, LightX, LightY, LightZ, Diffuse, Specular, Shininess;
            public bool IsLightEnabled;
            public LightType LightType;
            public string FilePath, ShaderFilePath;
            public Color BaseColor, Ambient, Light;
            public ProjectionType Projection;
            public CoordinateSystem CoordSystem;
            public RenderCullMode CullMode;
            public int WorldId;
            public bool IsVisible;
            public HashSet<int>? VisibleParts;
            public string ParentGuid;
        }

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
            _parameter.Duration = (double)desc.ItemDuration.Frame / desc.FPS;

            if (!_parameter.IsSwitchingLayer)
            {
                _parameter.SyncActiveLayer();
            }

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            int sw = (int)_parameter.ScreenWidth.GetValue(frame, length, fps);
            int sh = (int)_parameter.ScreenHeight.GetValue(frame, length, fps);
            sw = Math.Max(1, sw);
            sh = Math.Max(1, sh);

            bool resized = _renderTargets.EnsureSize(_devices, sw, sh);

            double camX, camY, camZ, targetX, targetY, targetZ;
            if (_parameter.Keyframes.Count > 0)
            {
                double time = (double)frame / fps;
                var state = _parameter.GetCameraState(time);
                camX = state.cx; camY = state.cy; camZ = state.cz;
                targetX = state.tx; targetY = state.ty; targetZ = state.tz;
            }
            else
            {
                camX = _parameter.CameraX.GetValue(frame, length, fps);
                camY = _parameter.CameraY.GetValue(frame, length, fps);
                camZ = _parameter.CameraZ.GetValue(frame, length, fps);
                targetX = _parameter.TargetX.GetValue(frame, length, fps);
                targetY = _parameter.TargetY.GetValue(frame, length, fps);
                targetZ = _parameter.TargetZ.GetValue(frame, length, fps);
            }

            var settingsVersion = _parameter.SettingsVersion.GetValue(frame, length, fps);
            var settings = PluginSettings.Instance;

            _resources.UpdateRasterizerState(settings.CullMode);

            bool needsRedraw = resized || _commandList == null;

            if (Math.Abs(_lastCamX - camX) > 1e-5 || Math.Abs(_lastCamY - camY) > 1e-5 || Math.Abs(_lastCamZ - camZ) > 1e-5 ||
                Math.Abs(_lastTargetX - targetX) > 1e-5 || Math.Abs(_lastTargetY - targetY) > 1e-5 || Math.Abs(_lastTargetZ - targetZ) > 1e-5 ||
                Math.Abs(_lastSettingsVersion - settingsVersion) > 1e-5)
            {
                needsRedraw = true;
            }

            var preCalcStates = new List<(string Guid, LayerState State, LayerData Data)>();
            var worldMasterLights = new Dictionary<int, LayerState>();
            var activeGuid = _parameter.ActiveLayerGuid;

            foreach (var layer in _parameter.Layers)
            {
                double x = layer.X.GetValue(frame, length, fps);
                double y = layer.Y.GetValue(frame, length, fps);
                double z = layer.Z.GetValue(frame, length, fps);
                double scale = layer.Scale.GetValue(frame, length, fps);
                double rx = layer.RotationX.GetValue(frame, length, fps);
                double ry = layer.RotationY.GetValue(frame, length, fps);
                double rz = layer.RotationZ.GetValue(frame, length, fps);
                double cx = layer.RotationCenterX;
                double cy = layer.RotationCenterY;
                double cz = layer.RotationCenterZ;
                double fov = layer.Fov.GetValue(frame, length, fps);
                double lx = layer.LightX.GetValue(frame, length, fps);
                double ly = layer.LightY.GetValue(frame, length, fps);
                double lz = layer.LightZ.GetValue(frame, length, fps);
                int worldId = (int)layer.WorldId.GetValue(frame, length, fps);

                var state = new LayerState
                {
                    X = x,
                    Y = y,
                    Z = z,
                    Scale = scale,
                    Rx = rx,
                    Ry = ry,
                    Rz = rz,
                    Cx = cx,
                    Cy = cy,
                    Cz = cz,
                    Fov = fov,
                    LightX = lx,
                    LightY = ly,
                    LightZ = lz,
                    IsLightEnabled = layer.IsLightEnabled,
                    LightType = layer.LightType,
                    FilePath = layer.FilePath?.Trim('"') ?? string.Empty,
                    ShaderFilePath = _parameter.ShaderFilePath?.Trim('"') ?? string.Empty,
                    BaseColor = layer.BaseColor,
                    WorldId = worldId,
                    Projection = layer.Projection,
                    CoordSystem = settings.CoordinateSystem,
                    CullMode = settings.CullMode,
                    Ambient = settings.GetAmbientColor(worldId),
                    Light = settings.GetLightColor(worldId),
                    Diffuse = settings.GetDiffuseIntensity(worldId),
                    Specular = settings.GetSpecularIntensity(worldId),
                    Shininess = settings.GetShininess(worldId),
                    IsVisible = layer.IsVisible,
                    VisibleParts = layer.VisibleParts != null ? new HashSet<int>(layer.VisibleParts) : null,
                    ParentGuid = layer.ParentGuid
                };

                preCalcStates.Add((layer.Guid, state, layer));

                if (!worldMasterLights.ContainsKey(worldId))
                {
                    worldMasterLights[worldId] = state;
                }
                else if (layer.Guid == activeGuid)
                {
                    worldMasterLights[worldId] = state;
                }
            }

            var currentLayerStates = new Dictionary<string, LayerState>();

            foreach (var item in preCalcStates)
            {
                var state = item.State;
                if (worldMasterLights.TryGetValue(state.WorldId, out var master))
                {
                    state.LightX = master.LightX;
                    state.LightY = master.LightY;
                    state.LightZ = master.LightZ;
                    state.IsLightEnabled = master.IsLightEnabled;
                    state.LightType = master.LightType;
                }

                currentLayerStates[item.Guid] = state;

                if (!needsRedraw)
                {
                    if (!_layerStates.TryGetValue(item.Guid, out var oldState) || !AreStatesEqual(ref oldState, ref state))
                    {
                        needsRedraw = true;
                    }
                }
            }

            if (!needsRedraw && _layerStates.Count != currentLayerStates.Count)
            {
                needsRedraw = true;
            }

            if (!needsRedraw)
            {
                return;
            }

            var layersToRender = new List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)>();

            foreach (var item in preCalcStates)
            {
                if (!currentLayerStates.TryGetValue(item.Guid, out var state)) continue;

                bool effectiveVisibility = state.IsVisible;
                var parentGuid = state.ParentGuid;
                int depth = 0;
                while (effectiveVisibility && !string.IsNullOrEmpty(parentGuid) && currentLayerStates.TryGetValue(parentGuid, out var parentState))
                {
                    if (!parentState.IsVisible)
                    {
                        effectiveVisibility = false;
                        break;
                    }
                    parentGuid = parentState.ParentGuid;
                    depth++;
                    if (depth > 100) break;
                }

                if (effectiveVisibility && !string.IsNullOrEmpty(state.FilePath))
                {
                    GpuResourceCacheItem? resource = null;
                    if (GpuResourceCache.TryGetValue(state.FilePath, out var cached))
                    {
                        if (cached != null && cached.Device == _devices.D3D.Device)
                        {
                            resource = cached;
                        }
                    }

                    if (resource == null)
                    {
                        var model = _loader.Load(state.FilePath);
                        if (model.Vertices.Length > 0)
                        {
                            resource = CreateGpuResource(model, state.FilePath);
                            model = null;
                            GC.Collect(2, GCCollectionMode.Optimized);
                        }
                    }

                    if (resource != null)
                    {
                        layersToRender.Add((item.Data, resource, state));
                    }
                }
            }

            _layerStates = currentLayerStates;

            RenderToTexture(layersToRender, sw, sh, camX, camY, camZ, targetX, targetY, targetZ);
            CreateCommandList();

            _lastCamX = camX;
            _lastCamY = camY;
            _lastCamZ = camZ;
            _lastTargetX = targetX;
            _lastTargetY = targetY;
            _lastTargetZ = targetZ;
            _lastSettingsVersion = settingsVersion;
        }

        private bool AreStatesEqual(ref LayerState a, ref LayerState b)
        {
            return Math.Abs(a.X - b.X) < 1e-5 && Math.Abs(a.Y - b.Y) < 1e-5 && Math.Abs(a.Z - b.Z) < 1e-5 &&
                   Math.Abs(a.Scale - b.Scale) < 1e-5 && Math.Abs(a.Rx - b.Rx) < 1e-5 && Math.Abs(a.Ry - b.Ry) < 1e-5 && Math.Abs(a.Rz - b.Rz) < 1e-5 &&
                   Math.Abs(a.Cx - b.Cx) < 1e-5 && Math.Abs(a.Cy - b.Cy) < 1e-5 && Math.Abs(a.Cz - b.Cz) < 1e-5 &&
                   Math.Abs(a.Fov - b.Fov) < 1e-5 && Math.Abs(a.LightX - b.LightX) < 1e-5 && Math.Abs(a.LightY - b.LightY) < 1e-5 && Math.Abs(a.LightZ - b.LightZ) < 1e-5 &&
                   a.IsLightEnabled == b.IsLightEnabled && a.LightType == b.LightType && string.Equals(a.FilePath, b.FilePath, StringComparison.Ordinal) &&
                   string.Equals(a.ShaderFilePath, b.ShaderFilePath, StringComparison.Ordinal) && a.BaseColor == b.BaseColor &&
                   a.Projection == b.Projection && a.CoordSystem == b.CoordSystem && a.CullMode == b.CullMode &&
                   a.Ambient == b.Ambient && a.Light == b.Light && Math.Abs(a.Diffuse - b.Diffuse) < 1e-5 &&
                   Math.Abs(a.Specular - b.Specular) < 1e-5 && Math.Abs(a.Shininess - b.Shininess) < 1e-5 && a.WorldId == b.WorldId &&
                   AreSetsEqual(a.VisibleParts, b.VisibleParts) && string.Equals(a.ParentGuid, b.ParentGuid, StringComparison.Ordinal) &&
                   a.IsVisible == b.IsVisible;
        }

        private bool AreSetsEqual(HashSet<int>? a, HashSet<int>? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return a.SetEquals(b);
        }

        private void UpdateCustomShader(string path)
        {
            if (_customVertexShader != null) { _customVertexShader.Dispose(); _customVertexShader = null; }
            if (_customPixelShader != null) { _customPixelShader.Dispose(); _customPixelShader = null; }
            if (_customInputLayout != null) { _customInputLayout.Dispose(); _customInputLayout = null; }

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var source = _parameter.GetAdaptedShaderSource();
                if (string.IsNullOrEmpty(source)) return;

                var vsResult = ShaderStore.Compile(source, "VS", "vs_5_0");
                if (vsResult.Blob != null)
                {
                    using (vsResult.Blob)
                    {
                        _customVertexShader = _devices.D3D.Device.CreateVertexShader(vsResult.Blob.AsBytes());
                        var inputElements = new[] {
                            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
                            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
                        };
                        _customInputLayout = _devices.D3D.Device.CreateInputLayout(inputElements, vsResult.Blob.AsBytes());
                    }
                }

                var psResult = ShaderStore.Compile(source, "PS", "ps_5_0");
                if (psResult.Blob != null)
                {
                    using (psResult.Blob)
                    {
                        _customPixelShader = _devices.D3D.Device.CreatePixelShader(psResult.Blob.AsBytes());
                    }
                }
            }
            catch { }
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

        private Matrix4x4 GetLayerTransform(LayerState state)
        {
            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (state.CoordSystem)
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

            var rotation = Matrix4x4.CreateRotationX((float)(state.Rx * Math.PI / 180.0)) *
                           Matrix4x4.CreateRotationY((float)(state.Ry * Math.PI / 180.0)) *
                           Matrix4x4.CreateRotationZ((float)(state.Rz * Math.PI / 180.0));
            var scale = Matrix4x4.CreateScale((float)(state.Scale / 100.0));
            var translation = Matrix4x4.CreateTranslation((float)state.X, (float)state.Y, (float)state.Z);

            var center = new Vector3((float)state.Cx, (float)state.Cy, (float)state.Cz);
            var pivotOffset = Matrix4x4.CreateTranslation(-center);

            return pivotOffset * axisConversion * rotation * scale * translation;
        }

        private void RenderToTexture(List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> layers, int width, int height, double camX, double camY, double camZ, double targetX, double targetY, double targetZ)
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
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            string lastShaderPath = string.Empty;

            foreach (var item in layers)
            {
                var state = item.State;
                var resource = item.Resource;
                var settings = PluginSettings.Instance;

                if (state.ShaderFilePath != lastShaderPath)
                {
                    UpdateCustomShader(state.ShaderFilePath);
                    lastShaderPath = state.ShaderFilePath;
                }

                var vs = _customVertexShader ?? _resources.VertexShader;
                var ps = _customPixelShader ?? _resources.PixelShader;
                var layout = _customVertexShader != null ? _customInputLayout : _resources.InputLayout;

                if (vs == null || ps == null || layout == null) continue;

                context.IASetInputLayout(layout);
                context.VSSetShader(vs);
                context.PSSetShader(ps);
                context.PSSetSamplers(0, new[] { _resources.SamplerState });

                Matrix4x4 hierarchyMatrix = GetLayerTransform(state);
                var currentGuid = state.ParentGuid;
                int depth = 0;
                while (!string.IsNullOrEmpty(currentGuid) && _layerStates.TryGetValue(currentGuid, out var parentState))
                {
                    hierarchyMatrix *= GetLayerTransform(parentState);
                    currentGuid = parentState.ParentGuid;
                    depth++;
                    if (depth > 100) break;
                }

                var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);

                var world = normalize * hierarchyMatrix;

                Matrix4x4 view, proj;
                float aspect = (float)width / height;
                Vector3 cameraPosition = new Vector3((float)camX, (float)camY, (float)camZ);
                var target = new Vector3((float)targetX, (float)targetY, (float)targetZ);

                if (state.Projection == ProjectionType.Parallel)
                {
                    if (cameraPosition == target) cameraPosition.Z -= 2.0f;
                    view = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                    float orthoSize = 2.0f;
                    proj = Matrix4x4.CreateOrthographic(orthoSize * aspect, orthoSize, 0.1f, 1000.0f);
                }
                else
                {
                    if (cameraPosition == target) cameraPosition.Z -= 2.5f;
                    view = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                    float radFov = (float)(Math.Max(1, Math.Min(179, state.Fov)) * Math.PI / 180.0);
                    proj = Matrix4x4.CreatePerspectiveFieldOfView(radFov, aspect, 0.1f, 1000.0f);
                }

                var wvp = world * view * proj;
                var lightPos = new Vector4((float)state.LightX, (float)state.LightY, (float)state.LightZ, 1.0f);
                var amb = new Vector4(state.Ambient.ScR, state.Ambient.ScG, state.Ambient.ScB, state.Ambient.ScA);
                var lCol = new Vector4(state.Light.ScR, state.Light.ScG, state.Light.ScB, state.Light.ScA);
                var camPos = new Vector4(cameraPosition, 1.0f);
                System.Numerics.Vector4 ToVec4(System.Windows.Media.Color c) => new System.Numerics.Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);

                int stride = Unsafe.SizeOf<ObjVertex>();
                context.IASetVertexBuffers(0, 1, new[] { resource.VertexBuffer }, new[] { stride }, new[] { 0 });
                context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                int wId = state.WorldId;

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;

                    var part = resource.Parts[i];
                    var texView = resource.PartTextures[i];
                    bool hasTexture = texView != null;

                    context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { hasTexture ? texView! : _resources.WhiteTextureView! });

                    var uiColorVec = hasTexture ? Vector4.One : new Vector4(state.BaseColor.ScR, state.BaseColor.ScG, state.BaseColor.ScB, state.BaseColor.ScA);
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
                        LightEnabled = state.IsLightEnabled ? 1.0f : 0.0f,
                        DiffuseIntensity = (float)state.Diffuse,
                        SpecularIntensity = (float)state.Specular,
                        Shininess = (float)state.Shininess,

                        ToonParams = new System.Numerics.Vector4(settings.GetToonEnabled(wId) ? 1 : 0, settings.GetToonSteps(wId), (float)settings.GetToonSmoothness(wId), 0),
                        RimParams = new System.Numerics.Vector4(settings.GetRimEnabled(wId) ? 1 : 0, (float)settings.GetRimIntensity(wId), (float)settings.GetRimPower(wId), 0),
                        RimColor = ToVec4(settings.GetRimColor(wId)),
                        OutlineParams = new System.Numerics.Vector4(settings.GetOutlineEnabled(wId) ? 1 : 0, (float)settings.GetOutlineWidth(wId), (float)settings.GetOutlinePower(wId), 0),
                        OutlineColor = ToVec4(settings.GetOutlineColor(wId)),
                        FogParams = new System.Numerics.Vector4(settings.GetFogEnabled(wId) ? 1 : 0, (float)settings.GetFogStart(wId), (float)settings.GetFogEnd(wId), (float)settings.GetFogDensity(wId)),
                        FogColor = ToVec4(settings.GetFogColor(wId)),
                        ColorCorrParams = new System.Numerics.Vector4((float)settings.GetSaturation(wId), (float)settings.GetContrast(wId), (float)settings.GetGamma(wId), (float)settings.GetBrightnessPost(wId)),
                        VignetteParams = new System.Numerics.Vector4(settings.GetVignetteEnabled(wId) ? 1 : 0, (float)settings.GetVignetteIntensity(wId), (float)settings.GetVignetteRadius(wId), (float)settings.GetVignetteSoftness(wId)),
                        VignetteColor = ToVec4(settings.GetVignetteColor(wId)),
                        ScanlineParams = new System.Numerics.Vector4(settings.GetScanlineEnabled(wId) ? 1 : 0, (float)settings.GetScanlineIntensity(wId), (float)settings.GetScanlineFrequency(wId), 0),
                        ChromAbParams = new System.Numerics.Vector4(settings.GetChromAbEnabled(wId) ? 1 : 0, (float)settings.GetChromAbIntensity(wId), 0, 0),
                        MonoParams = new System.Numerics.Vector4(settings.GetMonochromeEnabled(wId) ? 1 : 0, (float)settings.GetMonochromeMix(wId), 0, 0),
                        MonoColor = ToVec4(settings.GetMonochromeColor(wId)),
                        PosterizeParams = new System.Numerics.Vector4(settings.GetPosterizeEnabled(wId) ? 1 : 0, settings.GetPosterizeLevels(wId), 0, 0),
                        LightTypeParams = new System.Numerics.Vector4((float)state.LightType, 0, 0, 0)
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
            if (_customVertexShader != null) { _customVertexShader.Dispose(); _customVertexShader = null; }
            if (_customPixelShader != null) { _customPixelShader.Dispose(); _customPixelShader = null; }
            if (_customInputLayout != null) { _customInputLayout.Dispose(); _customInputLayout = null; }

            _resources.Dispose();
            _renderTargets.Dispose();
            _disposer.DisposeAndClear();
        }
    }
}
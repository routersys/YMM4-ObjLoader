using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Settings;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.Services
{
    internal class SceneService : IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;
        private readonly RenderService _renderService;
        private readonly Dictionary<string, (GpuResourceCacheItem Resource, double Height)> _modelResources = new();

        public double ModelScale { get; private set; } = 1.0;
        public double ModelHeight { get; private set; } = 1.0;

        public SceneService(ObjLoaderParameter parameter, RenderService renderService)
        {
            _parameter = parameter;
            _renderService = renderService;
            _loader = new ObjModelLoader();
        }

        public unsafe void LoadModel()
        {
            foreach (var entry in _modelResources) entry.Value.Resource.Dispose();
            _modelResources.Clear();

            var paths = new HashSet<string>();
            foreach (var layer in _parameter.Layers)
            {
                if (!string.IsNullOrWhiteSpace(layer.FilePath))
                    paths.Add(layer.FilePath.Trim('"'));
            }
            if (!string.IsNullOrWhiteSpace(_parameter.FilePath))
                paths.Add(_parameter.FilePath.Trim('"'));

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool hasModel = false;

            foreach (var path in paths)
            {
                if (_modelResources.ContainsKey(path)) continue;
                if (!File.Exists(path)) continue;

                var model = _loader.Load(path);
                if (model.Vertices.Length == 0) continue;

                var vDesc = new BufferDescription(model.Vertices.Length * Unsafe.SizeOf<ObjVertex>(), BindFlags.VertexBuffer, ResourceUsage.Immutable);
                ID3D11Buffer vb;
                fixed (ObjVertex* p = model.Vertices) vb = _renderService.Device!.CreateBuffer(vDesc, new SubresourceData(p));

                var iDesc = new BufferDescription(model.Indices.Length * sizeof(int), BindFlags.IndexBuffer, ResourceUsage.Immutable);
                ID3D11Buffer ib;
                fixed (int* p = model.Indices) ib = _renderService.Device.CreateBuffer(iDesc, new SubresourceData(p));

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
                            fixed (byte* p = pixels) { using var t = _renderService.Device.CreateTexture2D(tDesc, new[] { new SubresourceData(p, conv.PixelWidth * 4) }); partTextures[i] = _renderService.Device.CreateShaderResourceView(t); }
                        }
                        catch { }
                    }
                }
                var resource = new GpuResourceCacheItem(_renderService.Device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale);

                double localMinY = double.MaxValue, localMaxY = double.MinValue;
                foreach (var v in model.Vertices)
                {
                    double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                    if (y < localMinY) localMinY = y; if (y > localMaxY) localMaxY = y;
                    double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                    if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                }

                double height = localMaxY - localMinY;
                _modelResources[path] = (resource, height);
                hasModel = true;
            }

            if (hasModel)
            {
                ModelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
                ModelHeight = maxY - minY;
                if (ModelScale < 0.1) ModelScale = 1.0;
            }
        }

        public void Render(PerspectiveCamera camera, double currentTime, int width, int height, bool isPilotView, Color themeColor, bool isWireframe, bool isGrid, bool isInfinite, bool isInteracting)
        {
            if (_renderService.SceneImage == null) return;
            var camDir = camera.LookDirection; camDir.Normalize();
            var camUp = camera.UpDirection; camUp.Normalize();
            var camPos = camera.Position;
            var target = camPos + camDir;
            var view = Matrix4x4.CreateLookAt(
                new Vector3((float)camPos.X, (float)camPos.Y, (float)camPos.Z),
                new Vector3((float)target.X, (float)target.Y, (float)target.Z),
                new Vector3((float)camUp.X, (float)camUp.Y, (float)camUp.Z));

            double fovValue = _parameter.Fov.Values[0].Value;
            if (fovValue < 0.1) fovValue = 0.1;
            if (isPilotView && camera.FieldOfView != fovValue) camera.FieldOfView = fovValue;
            else if (!isPilotView && camera.FieldOfView != 45) camera.FieldOfView = 45;

            float hFovRad = (float)(camera.FieldOfView * Math.PI / 180.0);
            float aspect = (float)width / height;
            float vFovRad = 2.0f * (float)Math.Atan(Math.Tan(hFovRad / 2.0f) / aspect);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(vFovRad, aspect, 0.1f, 10000.0f);

            int fps = _parameter.CurrentFPS > 0 ? _parameter.CurrentFPS : 60;
            double currentFrame = currentTime * fps;
            int len = (int)(_parameter.Duration * fps);

            var layers = new List<LayerRenderData>();
            int activeIndex = _parameter.SelectedLayerIndex;

            var settings = PluginSettings.Instance;
            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (settings.CoordinateSystem)
            {
                case CoordinateSystem.RightHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)); break;
                case CoordinateSystem.LeftHandedYUp: axisConversion = Matrix4x4.CreateScale(1, 1, -1); break;
                case CoordinateSystem.LeftHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1); break;
            }

            var localPlacements = new Dictionary<string, (Matrix4x4 Local, string? ParentId, LayerData Layer, GpuResourceCacheItem Resource, double Height)>();

            for (int i = 0; i < _parameter.Layers.Count; i++)
            {
                var layer = _parameter.Layers[i];
                if (!layer.IsVisible) continue;

                string filePath = layer.FilePath?.Trim('"') ?? string.Empty;
                if (string.IsNullOrEmpty(filePath) || !_modelResources.ContainsKey(filePath)) continue;

                var (resource, h) = _modelResources[filePath];
                bool isActive = (i == activeIndex);

                double x, y, z, scale, rx, ry, rz;

                if (isActive)
                {
                    x = _parameter.X.GetValue((long)currentFrame, len, fps);
                    y = _parameter.Y.GetValue((long)currentFrame, len, fps);
                    z = _parameter.Z.GetValue((long)currentFrame, len, fps);
                    scale = _parameter.Scale.GetValue((long)currentFrame, len, fps);
                    rx = _parameter.RotationX.GetValue((long)currentFrame, len, fps);
                    ry = _parameter.RotationY.GetValue((long)currentFrame, len, fps);
                    rz = _parameter.RotationZ.GetValue((long)currentFrame, len, fps);
                }
                else
                {
                    x = layer.X.GetValue((long)currentFrame, len, fps);
                    y = layer.Y.GetValue((long)currentFrame, len, fps);
                    z = layer.Z.GetValue((long)currentFrame, len, fps);
                    scale = layer.Scale.GetValue((long)currentFrame, len, fps);
                    rx = layer.RotationX.GetValue((long)currentFrame, len, fps);
                    ry = layer.RotationY.GetValue((long)currentFrame, len, fps);
                    rz = layer.RotationZ.GetValue((long)currentFrame, len, fps);
                }

                float fScale = (float)(scale / 100.0);
                float fRx = (float)(rx * Math.PI / 180.0);
                float fRy = (float)(ry * Math.PI / 180.0);
                float fRz = (float)(rz * Math.PI / 180.0);
                float fTx = (float)x;
                float fTy = (float)y;
                float fTz = (float)z;

                var placement = Matrix4x4.CreateScale(fScale) * Matrix4x4.CreateRotationZ(fRz) * Matrix4x4.CreateRotationX(fRx) * Matrix4x4.CreateRotationY(fRy) * Matrix4x4.CreateTranslation(fTx, fTy, fTz);

                localPlacements[layer.Guid] = (placement, layer.ParentGuid, layer, resource, h);
            }

            var globalPlacements = new Dictionary<string, Matrix4x4>();

            Matrix4x4 GetGlobalPlacement(string guid)
            {
                if (globalPlacements.TryGetValue(guid, out var cached)) return cached;
                if (!localPlacements.TryGetValue(guid, out var info)) return Matrix4x4.Identity;

                var parentMat = Matrix4x4.Identity;
                if (!string.IsNullOrEmpty(info.ParentId) && localPlacements.ContainsKey(info.ParentId))
                {
                    parentMat = GetGlobalPlacement(info.ParentId);
                }

                var global = info.Local * parentMat;
                globalPlacements[guid] = global;
                return global;
            }

            foreach (var kvp in localPlacements)
            {
                var guid = kvp.Key;
                var info = kvp.Value;
                var layer = info.Layer;
                var resource = info.Resource;

                var globalPlacement = GetGlobalPlacement(guid);

                var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);
                normalize *= Matrix4x4.CreateTranslation(0, (float)(info.Height / 2.0), 0);

                var finalWorld = normalize * axisConversion * globalPlacement;

                bool isActive = (layer == _parameter.Layers[activeIndex]);
                bool lightEnabled = isActive ? _parameter.IsLightEnabled : layer.IsLightEnabled;
                Color baseColor = isActive ? _parameter.BaseColor : layer.BaseColor;
                int worldId = (int)(isActive ? _parameter.WorldId.GetValue((long)currentFrame, len, fps) : layer.WorldId.GetValue((long)currentFrame, len, fps));

                layers.Add(new LayerRenderData
                {
                    Resource = resource,
                    WorldMatrixOverride = finalWorld,
                    X = 0,
                    Y = 0,
                    Z = 0,
                    Scale = 100,
                    Rx = 0,
                    Ry = 0,
                    Rz = 0,
                    BaseColor = baseColor,
                    LightEnabled = lightEnabled,
                    WorldId = worldId,
                    HeightOffset = 0,
                    VisibleParts = layer.VisibleParts
                });
            }

            _renderService.Render(
                layers,
                view,
                proj,
                new Vector3((float)camPos.X, (float)camPos.Y, (float)camPos.Z),
                themeColor,
                isWireframe,
                isGrid,
                isInfinite,
                ModelScale,
                isInteracting);
        }

        public void Dispose()
        {
            foreach (var entry in _modelResources) entry.Value.Resource.Dispose();
            _modelResources.Clear();
        }
    }
}
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

namespace ObjLoader.Services.Rendering
{
    internal class SceneService : IDisposable
    {
        private const int MaxHierarchyDepth = 100;

        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;
        private readonly RenderService _renderService;
        private readonly Dictionary<string, (GpuResourceCacheItem Resource, Vector3 Size, Vector3 Min, Vector3 Max)> _modelResources = new();

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
            var validPaths = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(_parameter.FilePath))
                validPaths.Add(_parameter.FilePath.Trim('"'));

            foreach (var layer in _parameter.Layers)
            {
                if (!string.IsNullOrWhiteSpace(layer.FilePath))
                    validPaths.Add(layer.FilePath.Trim('"'));
            }

            var keysToRemove = new List<string>();
            foreach (var key in _modelResources.Keys)
            {
                if (!validPaths.Contains(key))
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                _modelResources[key].Resource.Dispose();
                _modelResources.Remove(key);
            }

            foreach (var path in validPaths)
            {
                if (_modelResources.ContainsKey(path)) continue;
                if (!File.Exists(path)) continue;

                var model = _loader.Load(path);
                if (model.Vertices.Length == 0) continue;

                ID3D11Buffer? vb = null;
                ID3D11Buffer? ib = null;
                ID3D11ShaderResourceView?[]? partTextures = null;
                bool success = false;

                try
                {
                    var vDesc = new BufferDescription(model.Vertices.Length * Unsafe.SizeOf<ObjVertex>(), BindFlags.VertexBuffer, ResourceUsage.Immutable);
                    fixed (ObjVertex* p = model.Vertices) vb = _renderService.Device!.CreateBuffer(vDesc, new SubresourceData(p));

                    var iDesc = new BufferDescription(model.Indices.Length * sizeof(int), BindFlags.IndexBuffer, ResourceUsage.Immutable);
                    fixed (int* p = model.Indices) ib = _renderService.Device.CreateBuffer(iDesc, new SubresourceData(p));

                    var parts = model.Parts.ToArray();
                    partTextures = new ID3D11ShaderResourceView?[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (!File.Exists(parts[i].TexturePath)) continue;

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
                            int width = conv.PixelWidth;
                            int height = conv.PixelHeight;
                            int stride = width * 4;
                            var pixels = new byte[stride * height];
                            conv.CopyPixels(pixels, stride, 0);
                            var tDesc = new Texture2DDescription { Width = width, Height = height, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource };
                            fixed (byte* p = pixels)
                            {
                                using var t = _renderService.Device.CreateTexture2D(tDesc, new[] { new SubresourceData(p, stride) });
                                partTextures[i] = _renderService.Device.CreateShaderResourceView(t);
                            }
                        }
                        catch
                        {
                        }
                    }

                    var resource = new GpuResourceCacheItem(_renderService.Device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale);

                    double localMinX = double.MaxValue, localMaxX = double.MinValue;
                    double localMinY = double.MaxValue, localMaxY = double.MinValue;
                    double localMinZ = double.MaxValue, localMaxZ = double.MinValue;

                    foreach (var v in model.Vertices)
                    {
                        double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                        if (x < localMinX) localMinX = x; if (x > localMaxX) localMaxX = x;

                        double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                        if (y < localMinY) localMinY = y; if (y > localMaxY) localMaxY = y;

                        double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                        if (z < localMinZ) localMinZ = z; if (z > localMaxZ) localMaxZ = z;
                    }

                    Vector3 size = new Vector3((float)(localMaxX - localMinX), (float)(localMaxY - localMinY), (float)(localMaxZ - localMinZ));
                    Vector3 min = new Vector3((float)localMinX, (float)localMinY, (float)localMinZ);
                    Vector3 max = new Vector3((float)localMaxX, (float)localMaxY, (float)localMaxZ);

                    _modelResources[path] = (resource, size, min, max);
                    success = true;
                }
                finally
                {
                    if (!success)
                    {
                        if (partTextures != null)
                        {
                            foreach (var srv in partTextures)
                            {
                                SafeDispose(srv);
                            }
                        }
                        SafeDispose(ib);
                        SafeDispose(vb);
                    }
                }
            }

            if (_modelResources.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (var entry in _modelResources.Values)
                {
                    if (entry.Min.X < minX) minX = entry.Min.X;
                    if (entry.Min.Y < minY) minY = entry.Min.Y;
                    if (entry.Min.Z < minZ) minZ = entry.Min.Z;

                    if (entry.Max.X > maxX) maxX = entry.Max.X;
                    if (entry.Max.Y > maxY) maxY = entry.Max.Y;
                    if (entry.Max.Z > maxZ) maxZ = entry.Max.Z;
                }

                ModelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
                ModelHeight = maxY - minY;
                if (ModelScale < 0.1) ModelScale = 1.0;
            }
        }

        public void Render(PerspectiveCamera camera, double currentTime, int width, int height, bool isPilotView, Color themeColor, bool isWireframe, bool isGrid, bool isInfinite, bool isInteracting, bool enableShadow = true)
        {
            LoadModel();

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
            var layerList = _parameter.Layers;
            int activeIndex = _parameter.SelectedLayerIndex;

            bool isIndexValid = activeIndex >= 0 && activeIndex < layerList.Count;
            LayerData? activeLayer = isIndexValid ? layerList[activeIndex] : null;

            var settings = PluginSettings.Instance;
            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (settings.CoordinateSystem)
            {
                case CoordinateSystem.RightHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)); break;
                case CoordinateSystem.LeftHandedYUp: axisConversion = Matrix4x4.CreateScale(1, 1, -1); break;
                case CoordinateSystem.LeftHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1); break;
            }

            var localPlacements = new Dictionary<string, (Matrix4x4 Local, string? ParentId, LayerData Layer, GpuResourceCacheItem Resource)>();
            double? globalLiftY = null;

            for (int i = 0; i < layerList.Count; i++)
            {
                var layer = layerList[i];
                if (!layer.IsVisible) continue;

                string filePath = layer.FilePath?.Trim('"') ?? string.Empty;
                if (string.IsNullOrEmpty(filePath) || !_modelResources.ContainsKey(filePath)) continue;

                var (resource, size, _, _) = _modelResources[filePath];
                bool isActive = (activeLayer != null && layer == activeLayer);

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

                if (globalLiftY == null)
                {
                    double h = 0;
                    if (settings.CoordinateSystem == CoordinateSystem.RightHandedZUp || settings.CoordinateSystem == CoordinateSystem.LeftHandedZUp)
                        h = size.Z;
                    else
                        h = size.Y;

                    globalLiftY = (h * scale / 100.0) / 2.0;
                    ModelHeight = h * scale / 100.0;
                    ModelScale *= scale / 100.0;
                }

                float fScale = (float)(scale / 100.0);
                float fRx = (float)(rx * Math.PI / 180.0);
                float fRy = (float)(ry * Math.PI / 180.0);
                float fRz = (float)(rz * Math.PI / 180.0);
                float fTx = (float)x;
                float fTy = (float)y;
                float fTz = (float)z;

                var placement = Matrix4x4.CreateScale(fScale) * Matrix4x4.CreateRotationZ(fRz) * Matrix4x4.CreateRotationX(fRx) * Matrix4x4.CreateRotationY(fRy) * Matrix4x4.CreateTranslation(fTx, fTy, fTz);

                localPlacements[layer.Guid] = (placement, layer.ParentGuid, layer, resource);
            }

            var globalPlacements = new Dictionary<string, Matrix4x4>();

            Matrix4x4 GetGlobalPlacement(string guid, int depth = 0)
            {
                if (globalPlacements.TryGetValue(guid, out var cached)) return cached;
                if (!localPlacements.TryGetValue(guid, out var info)) return Matrix4x4.Identity;
                if (depth > MaxHierarchyDepth) return Matrix4x4.Identity;

                var parentMat = Matrix4x4.Identity;
                if (!string.IsNullOrEmpty(info.ParentId) && localPlacements.ContainsKey(info.ParentId))
                {
                    parentMat = GetGlobalPlacement(info.ParentId, depth + 1);
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

                var finalWorld = normalize * axisConversion * globalPlacement * Matrix4x4.CreateTranslation(0, (float)(globalLiftY ?? 0), 0);

                bool isActive = (activeLayer != null && layer == activeLayer);
                bool lightEnabled = isActive ? _parameter.IsLightEnabled : layer.IsLightEnabled;
                Color baseColor = isActive ? _parameter.BaseColor : layer.BaseColor;

                double wIdVal = isActive ? _parameter.WorldId.GetValue((long)currentFrame, len, fps) : layer.WorldId.GetValue((long)currentFrame, len, fps);
                int worldId = (int)wIdVal;

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
                isInteracting,
                enableShadow);
        }

        public void Dispose()
        {
            foreach (var entry in _modelResources) entry.Value.Resource.Dispose();
            _modelResources.Clear();
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }
    }
}
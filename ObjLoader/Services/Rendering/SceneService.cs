using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Services.Textures;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using ObjLoader.Utilities.Logging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Vortice.Direct3D11;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.Services.Rendering
{
    internal class SceneService : IDisposable
    {
        private const int MaxHierarchyDepth = 100;
        private const string CacheKeyPrefix = "scene:";

        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;
        private readonly RenderService _renderService;
        private readonly ITextureService _textureService;
        private readonly Dictionary<string, (Vector3 Size, Vector3 Min, Vector3 Max)> _boundingData = new();
        private readonly HashSet<string> _managedCacheKeys = new();
        private ISkinningManager? _skinningManager;
        private readonly Dictionary<string, (ObjVertex[] Vertices, Core.Mmd.VertexBoneWeight[] BoneWeights)> _modelSkinningData = new();
        private readonly Dictionary<string, List<Core.Mmd.PmxBone>> _modelBones = new();
        private readonly Dictionary<string, string> _registeredSkinningGuids = new();
        private readonly Dictionary<string, (BoneAnimator Animator, string VmdPath)> _animators = new();

        private readonly Dictionary<string, (Matrix4x4 Local, string? ParentId, LayerData Layer, GpuResourceCacheItem Resource)> _localPlacementsBuffer = new();
        private readonly Dictionary<string, Matrix4x4> _globalPlacementsBuffer = new();
        private readonly List<LayerRenderData> _layerRenderDataBuffer = new();
        private readonly HashSet<string> _validLayerGuidsBuffer = new();
        private readonly List<string> _animatorsToRemoveBuffer = new();

        private bool _isLoadingModel;
        private bool _isModelLoaded;
        private Task? _loadModelTask;
        private readonly object _loadLock = new object();
        public Action? OnModelLoaded { get; set; }

        public double ModelScale { get; private set; } = 1.0;
        public double ModelHeight { get; private set; } = 1.0;

        public SceneService(ObjLoaderParameter parameter, RenderService renderService)
        {
            _parameter = parameter;
            _renderService = renderService;
            _loader = new ObjModelLoader();
            _textureService = new TextureService();
        }

        private static string ToCacheKey(string path) => $"{CacheKeyPrefix}{path}";

        public void LoadModel()
        {
            lock (_loadLock)
            {
                if (_isLoadingModel) return;
                _isLoadingModel = true;
            }

            try
            {
                LoadModelInternal();
            }
            finally
            {
                lock (_loadLock)
                {
                    _isLoadingModel = false;
                }
            }
        }

        public async Task LoadModelAsync()
        {
            lock (_loadLock)
            {
                if (_isLoadingModel || _isModelLoaded) return;
                _isLoadingModel = true;
            }

            try
            {
                await Task.Run(() => LoadModelInternal()).ConfigureAwait(false);
                _isModelLoaded = true;
            }
            catch (Exception ex)
            {
                Logger<SceneService>.Instance.Error("Failed to load model asynchronously", ex);
            }
            finally
            {
                lock (_loadLock)
                {
                    _isLoadingModel = false;
                }
                OnModelLoaded?.Invoke();
            }
        }

        public void ComputeModelScale()
        {
            var validPaths = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(_parameter.FilePath))
                validPaths.Add(_parameter.FilePath.Trim('"'));
            foreach (var layer in _parameter.Layers)
            {
                if (!string.IsNullOrWhiteSpace(layer.FilePath))
                    validPaths.Add(layer.FilePath);
            }

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool hasData = false;

            foreach (var path in validPaths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    var model = _loader.Load(path);
                    if (model.Vertices.Length == 0) continue;

                    foreach (var v in model.Vertices)
                    {
                        double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                        double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                        double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
                    }
                    hasData = true;
                }
                catch (IOException ex)
                {
                    Logger<SceneService>.Instance.Warning($"IO error loading model for scale computation: {path}", ex);
                }
                catch (InvalidDataException ex)
                {
                    Logger<SceneService>.Instance.Warning($"Invalid model data for scale computation: {path}", ex);
                }
            }

            if (hasData)
            {
                ModelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
                ModelHeight = maxY - minY;
                if (ModelScale < 0.1) ModelScale = 1.0;
            }
        }

        private unsafe void LoadModelInternal()
        {
            var validPaths = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(_parameter.FilePath))
                validPaths.Add(_parameter.FilePath.Trim('"'));

            foreach (var layer in _parameter.Layers)
            {
                if (!string.IsNullOrWhiteSpace(layer.FilePath))
                    validPaths.Add(layer.FilePath);
            }

            var keysToRemove = new List<string>();
            foreach (var key in _boundingData.Keys)
            {
                if (!validPaths.Contains(key))
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                string cacheKey = ToCacheKey(key);
                GpuResourceCache.Instance.Remove(cacheKey);
                _managedCacheKeys.Remove(cacheKey);
                _boundingData.Remove(key);
                _modelSkinningData.Remove(key);
                _modelBones.Remove(key);
            }

            var modelSettings = ModelSettings.Instance;

            foreach (var path in validPaths)
            {
                if (_boundingData.ContainsKey(path))
                {
                    string cacheKey = ToCacheKey(path);
                    if (GpuResourceCache.Instance.TryGetValue(cacheKey, out var existing) && existing.Device == _renderService.Device)
                    {
                        continue;
                    }
                }

                if (!File.Exists(path)) continue;

                var model = _loader.Load(path);
                if (model.Vertices.Length == 0) continue;

                ID3D11Buffer? vb = null;
                ID3D11Buffer? ib = null;
                ID3D11ShaderResourceView?[]? partTextures = null;
                bool success = false;
                long gpuBytes = 0;

                try
                {
                    var vDesc = new BufferDescription(model.Vertices.Length * Unsafe.SizeOf<ObjVertex>(), BindFlags.VertexBuffer, ResourceUsage.Immutable);
                    fixed (ObjVertex* p = model.Vertices) vb = _renderService.Device!.CreateBuffer(vDesc, new SubresourceData(p));
                    gpuBytes += model.Vertices.Length * Unsafe.SizeOf<ObjVertex>();

                    var iDesc = new BufferDescription(model.Indices.Length * sizeof(int), BindFlags.IndexBuffer, ResourceUsage.Immutable);
                    fixed (int* p = model.Indices) ib = _renderService.Device.CreateBuffer(iDesc, new SubresourceData(p));
                    gpuBytes += model.Indices.Length * sizeof(int);

                    var parts = model.Parts.ToArray();
                    partTextures = new ID3D11ShaderResourceView?[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (!File.Exists(parts[i].TexturePath)) continue;

                        try
                        {
                            var (srv, texGpuBytes) = _textureService.CreateShaderResourceView(parts[i].TexturePath, _renderService.Device);
                            partTextures[i] = srv;
                            gpuBytes += texGpuBytes;
                        }
                        catch (IOException ex)
                        {
                            Logger<SceneService>.Instance.Warning($"IO error loading texture {parts[i].TexturePath}", ex);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            Logger<SceneService>.Instance.Warning($"Access denied for texture {parts[i].TexturePath}", ex);
                        }
                        catch (InvalidDataException ex)
                        {
                            Logger<SceneService>.Instance.Warning($"Corrupt texture {parts[i].TexturePath}", ex);
                        }
                    }

                    if (!modelSettings.IsGpuMemoryPerModelAllowed(gpuBytes))
                    {
                        long gpuMB = gpuBytes / (1024L * 1024L);
                        string message = string.Format(
                            Texts.GpuMemoryExceeded,
                            Path.GetFileName(path),
                            gpuMB,
                            modelSettings.MaxGpuMemoryPerModelMB);
                        UserNotification.ShowWarning(message, Texts.ResourceLimitTitle);
                        continue;
                    }

                    var resource = new GpuResourceCacheItem(_renderService.Device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale, gpuBytes);

                    string cacheKey = ToCacheKey(path);
                    GpuResourceCache.Instance.AddOrUpdate(cacheKey, resource);
                    _managedCacheKeys.Add(cacheKey);

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

                    _boundingData[path] = (size, min, max);
                    success = true;
                }
                finally
                {
                    if (!success)
                    {
                        if (partTextures != null)
                        {
                            for (int i = 0; i < partTextures.Length; i++)
                            {
                                SafeDispose(partTextures[i]);
                                partTextures[i] = null;
                            }
                        }
                        SafeDispose(ib);
                        SafeDispose(vb);
                    }
                }
            }

            if (_boundingData.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (var entry in _boundingData.Values)
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

        private GpuResourceCacheItem? GetCachedResource(string path)
        {
            string cacheKey = ToCacheKey(path);
            if (GpuResourceCache.Instance.TryGetValue(cacheKey, out var cached) && cached.Device == _renderService.Device)
            {
                return cached;
            }
            return null;
        }

        public void Render(PerspectiveCamera camera, double currentTime, int width, int height, bool isPilotView, Color themeColor, bool isWireframe, bool isGrid, bool isInfinite, bool isInteracting, bool enableShadow = true)
        {
            if (!_isModelLoaded)
            {
                lock (_loadLock)
                {
                    if (_loadModelTask == null || _loadModelTask.IsCompleted)
                    {
                        _loadModelTask = LoadModelAsync();
                    }
                }
            }

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

            var settings = PluginSettings.Instance;
            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (settings.CoordinateSystem)
            {
                case CoordinateSystem.RightHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)); break;
                case CoordinateSystem.LeftHandedYUp: axisConversion = Matrix4x4.CreateScale(1, 1, -1); break;
                case CoordinateSystem.LeftHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1); break;
            }

            var layerList = _parameter.Layers;
            int activeIndex = _parameter.SelectedLayerIndex;
            bool isIndexValid = activeIndex >= 0 && activeIndex < layerList.Count;
            LayerData? activeLayer = isIndexValid ? layerList[activeIndex] : null;

            double globalLiftY;
            BuildLocalPlacements(currentFrame, len, fps, settings, activeLayer, out globalLiftY);
            ResolveHierarchy();
            ConvertToRenderData(axisConversion, globalLiftY, currentFrame, len, fps, activeLayer);

            _renderService.Render(
                _layerRenderDataBuffer,
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

        private void BuildLocalPlacements(double currentFrame, int len, int fps, PluginSettings settings, LayerData? activeLayer, out double globalLiftY)
        {
            _localPlacementsBuffer.Clear();
            globalLiftY = 0;
            bool liftComputed = false;

            var layerList = _parameter.Layers;

            for (int i = 0; i < layerList.Count; i++)
            {
                var layer = layerList[i];
                if (!layer.IsVisible) continue;

                string filePath = layer.FilePath ?? string.Empty;
                if (string.IsNullOrEmpty(filePath)) continue;

                var resource = GetCachedResource(filePath);
                if (resource == null) continue;

                if (!_boundingData.TryGetValue(filePath, out var bounds)) continue;

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

                if (!liftComputed)
                {
                    double h = 0;
                    if (settings.CoordinateSystem == CoordinateSystem.RightHandedZUp || settings.CoordinateSystem == CoordinateSystem.LeftHandedZUp)
                        h = bounds.Size.Z;
                    else
                        h = bounds.Size.Y;

                    globalLiftY = (h * scale / 100.0) / 2.0;
                    liftComputed = true;
                }

                float fScale = (float)(scale / 100.0);
                float fRx = (float)(rx * Math.PI / 180.0);
                float fRy = (float)(ry * Math.PI / 180.0);
                float fRz = (float)(rz * Math.PI / 180.0);
                float fTx = (float)x;
                float fTy = (float)y;
                float fTz = (float)z;

                var placement = Matrix4x4.CreateScale(fScale) * Matrix4x4.CreateRotationZ(fRz) * Matrix4x4.CreateRotationX(fRx) * Matrix4x4.CreateRotationY(fRy) * Matrix4x4.CreateTranslation(fTx, fTy, fTz);

                _localPlacementsBuffer[layer.Guid] = (placement, layer.ParentGuid, layer, resource);
            }
        }

        private void ResolveHierarchy()
        {
            _globalPlacementsBuffer.Clear();

            foreach (var guid in _localPlacementsBuffer.Keys)
            {
                GetGlobalPlacement(guid, 0);
            }
        }

        private Matrix4x4 GetGlobalPlacement(string guid, int depth)
        {
            if (_globalPlacementsBuffer.TryGetValue(guid, out var cached)) return cached;
            if (!_localPlacementsBuffer.TryGetValue(guid, out var info)) return Matrix4x4.Identity;
            if (depth > MaxHierarchyDepth) return Matrix4x4.Identity;

            var parentMat = Matrix4x4.Identity;
            if (!string.IsNullOrEmpty(info.ParentId) && _localPlacementsBuffer.ContainsKey(info.ParentId))
            {
                parentMat = GetGlobalPlacement(info.ParentId, depth + 1);
            }

            var global = info.Local * parentMat;
            _globalPlacementsBuffer[guid] = global;
            return global;
        }

        private void ConvertToRenderData(
            Matrix4x4 axisConversion,
            double globalLiftY,
            double currentFrame, int len, int fps,
            LayerData? activeLayer)
        {
            _layerRenderDataBuffer.Clear();
            _validLayerGuidsBuffer.Clear();

            if (_skinningManager == null && _renderService.Device != null)
            {
                _skinningManager = new SkinningManager(_renderService.Device);
            }

            foreach (var kvp in _localPlacementsBuffer)
            {
                var guid = kvp.Key;
                _validLayerGuidsBuffer.Add(guid);
                var info = kvp.Value;
                var layer = info.Layer;
                var resource = info.Resource;
                ID3D11Buffer? overrideVB = null;

                if (_skinningManager != null && !string.IsNullOrEmpty(layer.FilePath))
                {
                    if (!_modelSkinningData.ContainsKey(layer.FilePath))
                    {
                        if (Path.GetExtension(layer.FilePath).Equals(".pmx", StringComparison.OrdinalIgnoreCase) && File.Exists(layer.FilePath))
                        {
                            try
                            {
                                var pmxModel = new PmxParser().Parse(layer.FilePath);
                                if (pmxModel.BoneWeights != null && pmxModel.Bones.Count > 0)
                                {
                                    _modelSkinningData[layer.FilePath] = (pmxModel.Vertices.ToArray(), pmxModel.BoneWeights);
                                    _modelBones[layer.FilePath] = pmxModel.Bones;
                                }
                                else
                                {
                                    _modelSkinningData[layer.FilePath] = (Array.Empty<ObjVertex>(), Array.Empty<Core.Mmd.VertexBoneWeight>());
                                    _modelBones[layer.FilePath] = new List<Core.Mmd.PmxBone>();
                                }
                            }
                            catch (IOException)
                            {
                                _modelSkinningData[layer.FilePath] = (Array.Empty<ObjVertex>(), Array.Empty<Core.Mmd.VertexBoneWeight>());
                                _modelBones[layer.FilePath] = new List<Core.Mmd.PmxBone>();
                            }
                            catch (InvalidDataException)
                            {
                                _modelSkinningData[layer.FilePath] = (Array.Empty<ObjVertex>(), Array.Empty<Core.Mmd.VertexBoneWeight>());
                                _modelBones[layer.FilePath] = new List<Core.Mmd.PmxBone>();
                            }
                        }
                        else
                        {
                            _modelSkinningData[layer.FilePath] = (Array.Empty<ObjVertex>(), Array.Empty<Core.Mmd.VertexBoneWeight>());
                            _modelBones[layer.FilePath] = new List<Core.Mmd.PmxBone>();
                        }
                    }

                    if (_modelSkinningData.TryGetValue(layer.FilePath, out var skinData) && skinData.Vertices.Length > 0)
                    {
                        bool needsRegister = true;
                        if (_registeredSkinningGuids.TryGetValue(guid, out var currentPath) && currentPath == layer.FilePath)
                        {
                            needsRegister = false;
                        }

                        if (needsRegister)
                        {
                            _skinningManager.RegisterSkinningState(guid, layer.FilePath, skinData.Vertices, skinData.BoneWeights);
                            _registeredSkinningGuids[guid] = layer.FilePath;
                        }

                        BoneAnimator? animator = null;
                        if (!string.IsNullOrEmpty(layer.VmdFilePath) && File.Exists(layer.VmdFilePath))
                        {
                            if (!_animators.TryGetValue(guid, out var cache) || cache.VmdPath != layer.VmdFilePath)
                            {
                                try
                                {
                                    var vmdData = VmdParser.Parse(layer.VmdFilePath);
                                    if (vmdData.BoneFrames.Count > 0 && _modelBones.TryGetValue(layer.FilePath, out var bones) && bones.Count > 0)
                                    {
                                        animator = new BoneAnimator(bones, vmdData.BoneFrames);
                                        _animators[guid] = (animator, layer.VmdFilePath);
                                    }
                                }
                                catch (IOException ex)
                                {
                                    Logger<SceneService>.Instance.Warning($"IO error loading VMD: {layer.VmdFilePath}", ex);
                                }
                                catch (InvalidDataException ex)
                                {
                                    Logger<SceneService>.Instance.Warning($"Invalid VMD data: {layer.VmdFilePath}", ex);
                                }
                            }
                            else
                            {
                                animator = cache.Animator;
                            }
                        }

                        if (animator != null)
                        {
                            double motionTime = (currentFrame / fps) - layer.VmdTimeOffset;
                            if (motionTime < 0) motionTime = 0;
                            _skinningManager.ProcessSkinning(guid, layer.FilePath, animator, motionTime);
                            overrideVB = _skinningManager.GetOverrideVertexBuffer(guid);
                        }
                        else
                        {
                            _skinningManager.ProcessSkinning(guid, layer.FilePath, null, 0);
                        }
                    }
                }

                if (!_globalPlacementsBuffer.TryGetValue(guid, out var globalPlacement))
                    globalPlacement = info.Local;

                var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);

                var finalWorld = normalize * axisConversion * globalPlacement * Matrix4x4.CreateTranslation(0, (float)globalLiftY, 0);

                bool isActive = (activeLayer != null && layer == activeLayer);
                bool lightEnabled = isActive ? _parameter.IsLightEnabled : layer.IsLightEnabled;
                Color baseColor = isActive ? _parameter.BaseColor : layer.BaseColor;

                double wIdVal = isActive ? _parameter.WorldId.GetValue((long)currentFrame, len, fps) : layer.WorldId.GetValue((long)currentFrame, len, fps);
                int worldId = (int)wIdVal;

                _layerRenderDataBuffer.Add(new LayerRenderData
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
                    VisibleParts = layer.VisibleParts,
                    Data = layer,
                    OverrideVB = overrideVB
                });
            }

            _animatorsToRemoveBuffer.Clear();
            foreach (var key in _animators.Keys)
            {
                if (!_validLayerGuidsBuffer.Contains(key))
                {
                    _animatorsToRemoveBuffer.Add(key);
                }
            }

            foreach (var key in _animatorsToRemoveBuffer)
            {
                _animators.Remove(key);
                _registeredSkinningGuids.Remove(key);
                _skinningManager?.RemoveSkinningState(key);
            }
        }

        public void Dispose()
        {
            foreach (var cacheKey in _managedCacheKeys)
            {
                GpuResourceCache.Instance.Remove(cacheKey);
            }
            _managedCacheKeys.Clear();
            _boundingData.Clear();

            if (_textureService is IDisposable disposableTextureService)
            {
                disposableTextureService.Dispose();
            }
            _skinningManager?.Dispose();
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Logger<SceneService>.Instance.Error($"Dispose failed", ex);
            }
        }
    }
}
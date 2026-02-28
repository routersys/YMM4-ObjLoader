using ObjLoader.Api;
using ObjLoader.Api.Core;
using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Enums;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Renderers;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering.Core
{
    public sealed class ObjLoaderSource : IShapeSource
    {
        public static readonly object SharedRenderLock = new();

        private const int MaxHierarchyDepth = 100;
        private const string CacheKeyPrefix = "export:";

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly ObjLoaderParameter _parameter;
        private readonly DisposeCollector _disposer = new();
        private readonly ObjModelLoader _loader;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;
        private readonly CustomShaderManager _shaderManager;
        private readonly ShadowRenderer _shadowRenderer;
        private readonly SceneRenderer _sceneRenderer;
        private readonly ITextureService _textureService;
        private readonly IDynamicTextureManager _dynamicTextureManager;
        private readonly ISkinningManager _skinningManager;
        private readonly ISceneDrawManager _sceneDrawManager;
        private readonly IFrameStateCache _frameStateCache;

        private D2D.ID2D1CommandList? _commandList;

        private double _lastSettingsVersion = double.NaN;
        private double _lastCamX = double.NaN;
        private double _lastCamY = double.NaN;
        private double _lastCamZ = double.NaN;
        private double _lastTargetX = double.NaN;
        private double _lastTargetY = double.NaN;
        private double _lastTargetZ = double.NaN;

        private int _lastActiveWorldId = -1;
        private int _lastShadowResolution = -1;
        private bool _lastShadowEnabled;

        private Dictionary<string, LayerState> _layerStates = new Dictionary<string, LayerState>();

        private ObjLoaderSceneApi? _sceneApi;
        private Guid _registrationToken;

        private bool _isDisposed;
        private TimelineItemSourceDescription _lastDesc = default!;
        private bool _hasDesc;

        private static readonly ID3D11ShaderResourceView[] _emptySrvArray4 = new ID3D11ShaderResourceView[4];
        private static readonly ID3D11Buffer[] _emptyBufferArray1 = new ID3D11Buffer[1];

        private readonly List<(string Guid, LayerState State, LayerData Data)> _preCalcStates = [];
        private readonly Dictionary<int, LayerState> _worldMasterLights = [];
        private readonly List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> _layersToRender = [];
        private readonly Dictionary<string, LayerState> _newLayerStatesTemp = [];
        private readonly Matrix4x4[] _lightViewProjs = new Matrix4x4[D3DResources.CascadeCount];
        private readonly float[] _cascadeSplits = new float[4];
        private readonly float[] _splitDistances = new float[4];
        private readonly HashSet<string> _usedTexturePaths = [];
        private readonly Vector3[] _frustumCorners = new Vector3[8];
        private double _currentTime;
        private bool _hasBoneAnimation;

        private static readonly Vector3[] _ndcCorners =
        [
            new(-1, -1, 0), new(1, -1, 0), new(-1, 1, 0), new(1, 1, 0),
            new(-1, -1, 1), new(1, -1, 1), new(-1, 1, 1), new(1, 1, 1)
        ];

        static ObjLoaderSource()
        {
            var app = Application.Current;
            if (app != null)
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    if (app.MainWindow != null)
                    {
                        app.MainWindow.Closing += (s, e) =>
                        {
                            GpuResourceCache.Instance.Clear();
                            D3DResourcesPool.ClearAll();
                        };
                    }
                    app.Exit += (s, e) =>
                    {
                        GpuResourceCache.Instance.Clear();
                        D3DResourcesPool.ClearAll();
                    };
                    app.Dispatcher.ShutdownStarted += (s, e) =>
                    {
                        GpuResourceCache.Instance.Clear();
                        D3DResourcesPool.ClearAll();
                    };
                });
            }
        }

        public D2D.ID2D1Image Output
        {
            get
            {
                if (_commandList == null)
                {
                    CreateEmptyCommandList();
                }
                return _commandList!;
            }
        }

        public ObjLoaderSource(IGraphicsDevicesAndContext devices, ObjLoaderParameter parameter)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            _loader = new ObjModelLoader();
            _textureService = new TextureService();
            _dynamicTextureManager = new DynamicTextureManager(_textureService);
            _skinningManager = new SkinningManager(_devices.D3D.Device);
            _sceneDrawManager = new SceneDrawManager(_devices);
            _frameStateCache = new FrameStateCache();

            _resources = D3DResourcesPool.Acquire(devices.D3D.Device);

            _renderTargets = new RenderTargetManager();
            _shaderManager = new CustomShaderManager(devices);
            _shadowRenderer = new ShadowRenderer(devices, _resources);
            _sceneRenderer = new SceneRenderer(devices, _resources, _renderTargets, _shaderManager, _sceneDrawManager);

            _sceneApi = new ObjLoaderSceneApi(
                _parameter,
                _parameter.GetLayerManager(),
                () =>
                {
                    var ds = _renderTargets.DepthStencilTexture;
                    return (ds, (int)_parameter.ScreenWidth.GetValue(0, 1, 1), (int)_parameter.ScreenHeight.GetValue(0, 1, 1));
                },
                () => _renderTargets.DepthCopySRV,
                () =>
                {
                    return (Matrix4x4.Identity, Matrix4x4.Identity, (int)_parameter.ScreenWidth.GetValue(0, 1, 1), (int)_parameter.ScreenHeight.GetValue(0, 1, 1));
                },
                ForceRender);

            _registrationToken = SceneContext.Register(_parameter.InstanceId, _sceneApi);
        }

        private static string GetCacheKey(string filePath) => $"{CacheKeyPrefix}{filePath}";

        private bool IsDeviceLost()
        {
            var device = _devices.D3D.Device;
            if (device == null) return true;
            return device.DeviceRemovedReason.Failure;
        }

        private bool ValidateDeviceState()
        {
            if (!IsDeviceLost()) return true;
            GpuResourceCache.Instance.CleanupInvalidResources();
            CreateEmptyCommandList();
            return false;
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            if (_isDisposed) return;

            _lastDesc = desc;
            _hasDesc = true;

            lock (SharedRenderLock)
            {
                if (_isDisposed) return;
                if (!ValidateDeviceState()) return;

                try
                {
                    UpdateInternal(desc);
                }
                catch (SharpGen.Runtime.SharpGenException ex) when (
                    ex.HResult == unchecked((int)0x887A0005) ||
                    ex.HResult == unchecked((int)0x887A0006) ||
                    ex.HResult == unchecked((int)0x887A0007))
                {
                    GpuResourceCache.Instance.CleanupInvalidResources();
                    CreateEmptyCommandList();
                }
                catch (Exception)
                {
                    CreateEmptyCommandList();
                }
            }
        }

        public D2D.ID2D1Image? ForceRender()
        {
            if (_isDisposed || !_hasDesc) return _commandList;

            lock (SharedRenderLock)
            {
                if (!ValidateDeviceState()) return _commandList;

                try
                {
                    UpdateInternal(_lastDesc);
                }
                catch (SharpGen.Runtime.SharpGenException ex) when (
                    ex.HResult == unchecked((int)0x887A0005) ||
                    ex.HResult == unchecked((int)0x887A0006) ||
                    ex.HResult == unchecked((int)0x887A0007))
                {
                    GpuResourceCache.Instance.CleanupInvalidResources();
                    CreateEmptyCommandList();
                }
                catch (Exception)
                {
                    CreateEmptyCommandList();
                }
            }
            return _commandList;
        }

        private void UpdateInternal(TimelineItemSourceDescription desc)
        {
            if (_sceneApi != null)
            {
                _sceneDrawManager.UpdateFromApi(_sceneApi.DrawInternal);
            }

            if (desc != null && desc.FPS > 0)
            {
                _parameter.Duration = (double)desc.ItemDuration.Frame / desc.FPS;
            }
            else
            {
                _parameter.Duration = 0.0;
            }

            if (!_parameter.IsSwitchingLayer)
            {
                _parameter.SyncActiveLayer();
            }

            var frame = desc != null ? desc.ItemPosition.Frame : 0L;
            var length = desc != null ? desc.ItemDuration.Frame : 1L;
            var fps = desc != null && desc.FPS > 0 ? desc.FPS : 60;
            _currentTime = (double)frame / fps;

            int sw = Math.Max(1, (int)_parameter.ScreenWidth.GetValue(frame, length, fps));
            int sh = Math.Max(1, (int)_parameter.ScreenHeight.GetValue(frame, length, fps));
            bool resized = _renderTargets.EnsureSize(_devices, sw, sh);

            var (camX, camY, camZ, targetX, targetY, targetZ) = CalculateCameraTransforms(frame, length, fps);

            var settingsVersion = _parameter.SettingsVersion.GetValue(frame, length, fps);
            var settings = PluginSettings.Instance;

            UpdateResourcesIfNeeded(settings);

            bool settingsChanged = Math.Abs(_lastSettingsVersion - settingsVersion) > RenderingConstants.StateComparisonEpsilon;
            bool cameraChanged = Math.Abs(_lastCamX - camX) > RenderingConstants.StateComparisonEpsilon || Math.Abs(_lastCamY - camY) > RenderingConstants.StateComparisonEpsilon || Math.Abs(_lastCamZ - camZ) > RenderingConstants.StateComparisonEpsilon ||
                                 Math.Abs(_lastTargetX - targetX) > RenderingConstants.StateComparisonEpsilon || Math.Abs(_lastTargetY - targetY) > RenderingConstants.StateComparisonEpsilon || Math.Abs(_lastTargetZ - targetZ) > RenderingConstants.StateComparisonEpsilon;
            bool shadowSettingsChanged = _lastShadowResolution != settings.ShadowResolution || _lastShadowEnabled != settings.ShadowMappingEnabled;

            var (activeWorldId, layersChanged) = BuildLayerStates(frame, length, fps, settings);

            var stateToRender = _frameStateCache.GetOrCreateState();
            stateToRender.Update(frame, camX, camY, camZ, targetX, targetY, targetZ, activeWorldId, _newLayerStatesTemp);
            _frameStateCache.SaveState(frame, stateToRender);

            bool activeWorldIdChanged = _lastActiveWorldId != stateToRender.ActiveWorldId;
            bool needsShadowRedraw = layersChanged || settingsChanged || shadowSettingsChanged || activeWorldIdChanged || cameraChanged;
            bool needsSceneRedraw = needsShadowRedraw || cameraChanged || resized || _commandList == null || _hasBoneAnimation || _sceneDrawManager.IsDirty || _sceneDrawManager.GetExternalObjects().Count > 0 || _sceneDrawManager.GetBillboards().Count > 0;

            if (!needsSceneRedraw)
            {
                return;
            }

            ProcessVisibilityHierarchy(stateToRender.LayerStates);

            _layerStates = stateToRender.LayerStates;

            int renderWorldId = stateToRender.ActiveWorldId;
            bool shadowValid = RenderShadowsIfApplicable(
                settings, ref renderWorldId, sw, sh,
                stateToRender.CamX, stateToRender.CamY, stateToRender.CamZ,
                stateToRender.TargetX, stateToRender.TargetY, stateToRender.TargetZ,
                needsShadowRedraw);

            bool needsEnvMapRedraw = layersChanged || activeWorldIdChanged || settingsChanged || _sceneDrawManager.IsDirty || _sceneDrawManager.GetExternalObjects().Count > 0 || _sceneDrawManager.GetBillboards().Count > 0;
            RenderMainScene(sw, sh, stateToRender.CamX, stateToRender.CamY, stateToRender.CamZ,
                stateToRender.TargetX, stateToRender.TargetY, stateToRender.TargetZ,
                shadowValid, renderWorldId, needsEnvMapRedraw);



            FinalizeCommandList(camX, camY, camZ, targetX, targetY, targetZ,
                settingsVersion, activeWorldId, settings);
            _sceneDrawManager.ClearDirtyFlag();
        }

        private (double camX, double camY, double camZ, double targetX, double targetY, double targetZ) CalculateCameraTransforms(long frame, long length, int fps)
        {
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
            return (camX, camY, camZ, targetX, targetY, targetZ);
        }

        private void UpdateResourcesIfNeeded(PluginSettings settings)
        {
            _resources.UpdateRasterizerState(settings.CullMode);
            _resources.EnsureShadowMapSize(settings.ShadowResolution, true);
            _resources.EnsureEnvironmentMap();
        }

        private void PrepareDynamicTextures(IEnumerable<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> layers)
        {
            var device = _devices.D3D.Device;
            if (device == null) return;

            _usedTexturePaths.Clear();

            foreach (var layer in layers)
            {
                if (layer.State.PartMaterials != null)
                {
                    foreach (var pm in layer.State.PartMaterials.Values)
                    {
                        if (!string.IsNullOrEmpty(pm.TexturePath))
                        {
                            _usedTexturePaths.Add(pm.TexturePath!);
                        }
                    }
                }
            }
            _dynamicTextureManager.Prepare(_usedTexturePaths, device);
        }

        private (int activeWorldId, bool layersChanged) BuildLayerStates(long frame, long length, int fps, PluginSettings settings)
        {
            _preCalcStates.Clear();
            _worldMasterLights.Clear();
            _hasBoneAnimation = false;

            var activeGuid = _parameter.ActiveLayerGuid;
            int activeWorldId = 0;
            var previousStates = _layerStates;

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

                var visibleParts = layer.VisibleParts;
                previousStates.TryGetValue(layer.Guid, out var oldState);

                HashSet<int>? copiedVisibleParts = null;
                if (visibleParts != null)
                {
                    if (oldState.VisibleParts != null && oldState.VisibleParts.SetEquals(visibleParts))
                    {
                        copiedVisibleParts = oldState.VisibleParts;
                    }
                    else
                    {
                        copiedVisibleParts = new HashSet<int>(visibleParts);
                    }
                }

                var partMaterials = CreatePartMaterials(layer, oldState.PartMaterials);

                string filePath = layer.FilePath ?? string.Empty;
                string cacheKey = filePath.Length == 0 ? string.Empty : (string.Equals(oldState.FilePath, filePath, StringComparison.Ordinal) ? oldState.CacheKey : GetCacheKey(filePath));

                var layerState = new LayerState
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
                    FilePath = filePath,
                    CacheKey = cacheKey,
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
                    VisibleParts = copiedVisibleParts,
                    ParentGuid = layer.ParentGuid,
                    PartMaterials = partMaterials
                };

                _preCalcStates.Add((layer.Guid, layerState, layer));

                if (layer.BoneAnimatorInstance != null)
                {
                    _hasBoneAnimation = true;
                }

                if (!_worldMasterLights.ContainsKey(worldId))
                {
                    _worldMasterLights[worldId] = layerState;
                }
                else if (layer.Guid == activeGuid)
                {
                    _worldMasterLights[worldId] = layerState;
                    activeWorldId = worldId;
                }
            }

            _newLayerStatesTemp.Clear();
            bool layersChanged = false;

            var preCalcSpan = CollectionsMarshal.AsSpan(_preCalcStates);
            foreach (var item in preCalcSpan)
            {
                var layerState = item.State;
                if (_worldMasterLights.TryGetValue(layerState.WorldId, out var master))
                {
                    layerState.LightX = master.LightX;
                    layerState.LightY = master.LightY;
                    layerState.LightZ = master.LightZ;
                    layerState.IsLightEnabled = master.IsLightEnabled;
                    layerState.LightType = master.LightType;
                }

                _newLayerStatesTemp[item.Guid] = layerState;

                if (!previousStates.TryGetValue(item.Guid, out var oldState) || !AreStatesEqual(in oldState, in layerState))
                {
                    layersChanged = true;
                }
            }

            if (!layersChanged && previousStates.Count != _newLayerStatesTemp.Count)
            {
                layersChanged = true;
            }

            return (activeWorldId, layersChanged);
        }

        private Dictionary<int, PartMaterialState>? CreatePartMaterials(LayerData layer, Dictionary<int, PartMaterialState>? oldPartMaterials)
        {
            var newMaterials = layer.PartMaterials;
            if (newMaterials == null || newMaterials.Count == 0) return null;

            if (oldPartMaterials != null && oldPartMaterials.Count == newMaterials.Count)
            {
                bool isSame = true;
                foreach (var kvp in newMaterials)
                {
                    if (!oldPartMaterials.TryGetValue(kvp.Key, out var oldState))
                    {
                        isSame = false;
                        break;
                    }
                    var newState = kvp.Value;
                    if (Math.Abs(newState.Roughness - oldState.Roughness) > RenderingConstants.StateComparisonEpsilon ||
                        Math.Abs(newState.Metallic - oldState.Metallic) > RenderingConstants.StateComparisonEpsilon ||
                        newState.BaseColor != oldState.BaseColor ||
                        !string.Equals(newState.TexturePath, oldState.TexturePath, StringComparison.Ordinal))
                    {
                        isSame = false;
                        break;
                    }
                }
                if (isSame) return oldPartMaterials;
            }

            var dict = new Dictionary<int, PartMaterialState>(newMaterials.Count);
            foreach (var kvp in newMaterials)
            {
                dict[kvp.Key] = new PartMaterialState
                {
                    Roughness = kvp.Value.Roughness,
                    Metallic = kvp.Value.Metallic,
                    BaseColor = kvp.Value.BaseColor,
                    TexturePath = kvp.Value.TexturePath
                };
            }
            return dict;
        }

        private void ProcessVisibilityHierarchy(Dictionary<string, LayerState> newLayerStates)
        {
            _layersToRender.Clear();

            var preCalcSpan = CollectionsMarshal.AsSpan(_preCalcStates);
            foreach (var item in preCalcSpan)
            {
                if (!newLayerStates.TryGetValue(item.Guid, out var layerState)) continue;

                bool effectiveVisibility = layerState.IsVisible;
                var parentGuid = layerState.ParentGuid;
                int depth = 0;
                while (effectiveVisibility && !string.IsNullOrEmpty(parentGuid) && newLayerStates.TryGetValue(parentGuid, out var parentState))
                {
                    if (!parentState.IsVisible)
                    {
                        effectiveVisibility = false;
                        break;
                    }
                    parentGuid = parentState.ParentGuid;
                    depth++;
                    if (depth > MaxHierarchyDepth) break;
                }

                if (effectiveVisibility && !string.IsNullOrEmpty(layerState.FilePath))
                {
                    GpuResourceCacheItem? resource = null;
                    if (GpuResourceCache.Instance.TryGetValue(layerState.CacheKey, out var cached))
                    {
                        if (cached != null && cached.Device == _devices.D3D.Device)
                        {
                            resource = cached;
                        }
                    }

                    if (resource == null)
                    {
                        var model = _loader.Load(layerState.FilePath);
                        if (model.Vertices.Length > 0)
                        {
                            resource = CreateGpuResource(model, layerState.FilePath);

                            if (model.BoneWeights != null && model.Bones.Count > 0)
                            {
                                _skinningManager.RegisterSkinningState(item.Guid, layerState.FilePath, model.Vertices.ToArray(), model.BoneWeights);
                            }
                        }
                    }

                    if (resource != null)
                    {
                        if (item.Data.BoneAnimatorInstance == null && !string.IsNullOrEmpty(item.Data.VmdFilePath) && File.Exists(item.Data.VmdFilePath))
                        {
                            try
                            {
                                var vmdData = VmdParser.Parse(item.Data.VmdFilePath);
                                item.Data.VmdMotionData = vmdData;
                                if (vmdData.BoneFrames.Count > 0 && Path.GetExtension(layerState.FilePath).Equals(".pmx", StringComparison.OrdinalIgnoreCase))
                                {
                                    var pmxModel = new PmxParser().Parse(layerState.FilePath);
                                    if (pmxModel.Vertices != null && pmxModel.BoneWeights != null)
                                    {
                                        _skinningManager.RegisterSkinningState(item.Guid, layerState.FilePath, [.. pmxModel.Vertices], pmxModel.BoneWeights);
                                    }
                                    if (pmxModel.Bones.Count > 0)
                                    {
                                        item.Data.BoneAnimatorInstance = new BoneAnimator(pmxModel.Bones, vmdData.BoneFrames, pmxModel.RigidBodies, pmxModel.Joints);
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }

                        double motionTime = _currentTime - item.Data.VmdTimeOffset;
                        if (motionTime < 0) motionTime = 0;

                        _skinningManager.ProcessSkinning(item.Guid, layerState.FilePath, item.Data.BoneAnimatorInstance, motionTime);

                        ID3D11Buffer? overrideVB = _skinningManager.GetOverrideVertexBuffer(item.Guid);

                        _layersToRender.Add((item.Data, resource, layerState, overrideVB));
                    }
                }
            }
            PrepareDynamicTextures(_layersToRender);
        }

        private bool RenderShadowsIfApplicable(
            PluginSettings settings, ref int activeWorldId, int sw, int sh,
            double camX, double camY, double camZ,
            double targetX, double targetY, double targetZ,
            bool needsShadowRedraw)
        {
            LayerState shadowLightState = default;
            if (_worldMasterLights.TryGetValue(activeWorldId, out var al))
            {
                shadowLightState = al;
            }
            else if (_worldMasterLights.Count > 0)
            {
                shadowLightState = _worldMasterLights.Values.First();
                activeWorldId = shadowLightState.WorldId;
            }

            Array.Clear(_lightViewProjs, 0, _lightViewProjs.Length);
            Array.Clear(_cascadeSplits, 0, _cascadeSplits.Length);

            if (!settings.ShadowMappingEnabled || !shadowLightState.IsLightEnabled ||
                (shadowLightState.LightType != LightType.Sun && shadowLightState.LightType != LightType.Spot))
            {
                return false;
            }

            Vector3 lPos = new Vector3((float)shadowLightState.LightX, (float)shadowLightState.LightY, (float)shadowLightState.LightZ);
            Vector3 lightDir;

            if (shadowLightState.LightType == LightType.Sun)
            {
                lightDir = Vector3.Normalize(lPos == Vector3.Zero ? Vector3.UnitY : lPos);
                ComputeCascadeShadowMatrices(settings, sw, sh, camX, camY, camZ, targetX, targetY, targetZ, shadowLightState.Fov, lightDir);
            }
            else
            {
                lightDir = Vector3.Normalize(lPos == Vector3.Zero ? Vector3.UnitY : -lPos);
                Vector3 dir = -Vector3.Normalize(lPos);
                var lightView = Matrix4x4.CreateLookAt(lPos, lPos + dir, Vector3.UnitY);
                var lightProj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, 1.0f, RenderingConstants.SpotLightFarPlaneExport);
                for (int i = 0; i < D3DResources.CascadeCount; i++)
                {
                    _lightViewProjs[i] = lightView * lightProj;
                    _cascadeSplits[i] = RenderingConstants.CascadeSplitInfinity;
                }
            }

            if (needsShadowRedraw)
            {
                _shadowRenderer.Render(_layersToRender, _lightViewProjs, activeWorldId, _layerStates);
            }

            return true;
        }

        private void ComputeCascadeShadowMatrices(
            PluginSettings settings, int sw, int sh,
            double camX, double camY, double camZ,
            double targetX, double targetY, double targetZ,
            double fovDeg, Vector3 lightDir)
        {
            var cameraPos = new Vector3((float)camX, (float)camY, (float)camZ);
            var cameraTarget = new Vector3((float)targetX, (float)targetY, (float)targetZ);
            var viewMatrix = Matrix4x4.CreateLookAt(cameraPos, cameraTarget, Vector3.UnitY);

            float fov = (float)(Math.Max(1, Math.Min(RenderingConstants.DefaultFovLimit, fovDeg)) * Math.PI / 180.0);
            float aspect = (float)sw / sh;
            float nearPlane = RenderingConstants.ShadowNearPlane;
            float farPlane = RenderingConstants.DefaultFarPlane;

            _splitDistances[0] = nearPlane;
            _splitDistances[1] = nearPlane + (farPlane - nearPlane) * 0.05f;
            _splitDistances[2] = nearPlane + (farPlane - nearPlane) * 0.2f;
            _splitDistances[3] = farPlane;

            _cascadeSplits[0] = _splitDistances[1];
            _cascadeSplits[1] = _splitDistances[2];
            _cascadeSplits[2] = _splitDistances[3];

            for (int i = 0; i < D3DResources.CascadeCount; i++)
            {
                float sn = _splitDistances[i];
                float sf = _splitDistances[i + 1];
                var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, sn, sf);
                var invViewProj = Matrix4x4.Invert(viewMatrix * projMatrix, out var inv) ? inv : Matrix4x4.Identity;

                for (int j = 0; j < 8; j++)
                {
                    _frustumCorners[j] = Vector3.Transform(_ndcCorners[j], invViewProj);
                }

                Vector3 center = Vector3.Zero;
                for (int j = 0; j < 8; j++) center += _frustumCorners[j];
                center /= 8.0f;

                var lightView = Matrix4x4.CreateLookAt(center + lightDir * RenderingConstants.SunLightDistance, center, Vector3.UnitY);

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;

                for (int j = 0; j < 8; j++)
                {
                    var tr = Vector3.Transform(_frustumCorners[j], lightView);
                    minX = Math.Min(minX, tr.X); maxX = Math.Max(maxX, tr.X);
                    minY = Math.Min(minY, tr.Y); maxY = Math.Max(maxY, tr.Y);
                    minZ = Math.Min(minZ, tr.Z); maxZ = Math.Max(maxZ, tr.Z);
                }

                float worldUnitsPerTexel = (maxX - minX) / settings.ShadowResolution;
                minX = MathF.Floor(minX / worldUnitsPerTexel) * worldUnitsPerTexel;
                maxX = MathF.Floor(maxX / worldUnitsPerTexel) * worldUnitsPerTexel;
                minY = MathF.Floor(minY / worldUnitsPerTexel) * worldUnitsPerTexel;
                maxY = MathF.Floor(maxY / worldUnitsPerTexel) * worldUnitsPerTexel;

                var lightProj = Matrix4x4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, -maxZ - RenderingConstants.ShadowOrthoMargin, -minZ + RenderingConstants.ShadowOrthoMargin);
                _lightViewProjs[i] = lightView * lightProj;
            }
        }

        private void RenderMainScene(
            int sw, int sh,
            double camX, double camY, double camZ,
            double targetX, double targetY, double targetZ,
            bool shadowValid, int activeWorldId, bool needsEnvMapRedraw)
        {
            _sceneRenderer.Render(_layersToRender, _layerStates, _parameter, sw, sh,
                camX, camY, camZ, targetX, targetY, targetZ,
                _lightViewProjs, _cascadeSplits, shadowValid, activeWorldId, needsEnvMapRedraw, _dynamicTextureManager.Textures);
        }

        private void FinalizeCommandList(
            double camX, double camY, double camZ,
            double targetX, double targetY, double targetZ,
            double settingsVersion, int activeWorldId, PluginSettings settings)
        {
            ClearResourceBindings();
            CreateCommandList();

            _lastCamX = camX;
            _lastCamY = camY;
            _lastCamZ = camZ;
            _lastTargetX = targetX;
            _lastTargetY = targetY;
            _lastTargetZ = targetZ;
            _lastSettingsVersion = settingsVersion;
            _lastActiveWorldId = activeWorldId;
            _lastShadowResolution = settings.ShadowResolution;
            _lastShadowEnabled = settings.ShadowMappingEnabled;
        }



        private void ClearResourceBindings()
        {
            try
            {
                var context = _devices.D3D.Device.ImmediateContext;
                context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
                context.PSSetShaderResources(0, 4, _emptySrvArray4);
                context.VSSetShaderResources(0, 4, _emptySrvArray4);
                context.VSSetConstantBuffers(0, 1, _emptyBufferArray1);
                context.PSSetConstantBuffers(0, 1, _emptyBufferArray1);
                context.Flush();
            }
            catch (Exception)
            {
            }
        }

        private void ClearDynamicTextureCache()
        {
            _dynamicTextureManager.Clear();
        }

        private void CreateEmptyCommandList()
        {
            try
            {
                _disposer.RemoveAndDispose(ref _commandList);
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
            catch (Exception)
            {
            }
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

        private static bool AreStatesEqual(in LayerState a, in LayerState b)
        {
            return Math.Abs(a.X - b.X) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Y - b.Y) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Z - b.Z) < RenderingConstants.StateComparisonEpsilon &&
                   Math.Abs(a.Scale - b.Scale) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Rx - b.Rx) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Ry - b.Ry) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Rz - b.Rz) < RenderingConstants.StateComparisonEpsilon &&
                   Math.Abs(a.Cx - b.Cx) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Cy - b.Cy) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Cz - b.Cz) < RenderingConstants.StateComparisonEpsilon &&
                   Math.Abs(a.Fov - b.Fov) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.LightX - b.LightX) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.LightY - b.LightY) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.LightZ - b.LightZ) < RenderingConstants.StateComparisonEpsilon &&
                   a.IsLightEnabled == b.IsLightEnabled && a.LightType == b.LightType && string.Equals(a.FilePath, b.FilePath, StringComparison.Ordinal) &&
                   string.Equals(a.ShaderFilePath, b.ShaderFilePath, StringComparison.Ordinal) && a.BaseColor == b.BaseColor &&
                   a.Projection == b.Projection && a.CoordSystem == b.CoordSystem && a.CullMode == b.CullMode &&
                   a.Ambient == b.Ambient && a.Light == b.Light && Math.Abs(a.Diffuse - b.Diffuse) < RenderingConstants.StateComparisonEpsilon &&
                   Math.Abs(a.Specular - b.Specular) < RenderingConstants.StateComparisonEpsilon && Math.Abs(a.Shininess - b.Shininess) < RenderingConstants.StateComparisonEpsilon && a.WorldId == b.WorldId &&
                   AreSetsEqual(a.VisibleParts, b.VisibleParts) && string.Equals(a.ParentGuid, b.ParentGuid, StringComparison.Ordinal) &&
                   a.IsVisible == b.IsVisible && ArePartMaterialsEqual(a.PartMaterials, b.PartMaterials);
        }

        private static bool ArePartMaterialsEqual(Dictionary<int, PartMaterialState>? a, Dictionary<int, PartMaterialState>? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;

            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var valB)) return false;
                var valA = kvp.Value;
                if (Math.Abs(valA.Roughness - valB.Roughness) > RenderingConstants.StateComparisonEpsilon ||
                    Math.Abs(valA.Metallic - valB.Metallic) > RenderingConstants.StateComparisonEpsilon ||
                    valA.BaseColor != valB.BaseColor ||
                    !string.Equals(valA.TexturePath, valB.TexturePath, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreSetsEqual(HashSet<int>? a, HashSet<int>? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return a.SetEquals(b);
        }

        private unsafe GpuResourceCacheItem? CreateGpuResource(ObjModel model, string filePath)
        {
            var device = _devices.D3D.Device;
            ID3D11Buffer? vb = null;
            ID3D11Buffer? ib = null;
            ID3D11ShaderResourceView?[]? partTextures = null;
            bool success = false;
            long gpuBytes = 0;

            try
            {
                int vertexBufferSize = model.Vertices.Length * Unsafe.SizeOf<ObjVertex>();
                var vDesc = new BufferDescription(
                    vertexBufferSize,
                    BindFlags.VertexBuffer,
                    ResourceUsage.Immutable,
                    CpuAccessFlags.None);

                fixed (ObjVertex* pVerts = model.Vertices)
                {
                    var vData = new SubresourceData(pVerts);
                    vb = device.CreateBuffer(vDesc, vData);
                }
                gpuBytes += vertexBufferSize;

                int indexBufferSize = model.Indices.Length * sizeof(int);
                var iDesc = new BufferDescription(
                    indexBufferSize,
                    BindFlags.IndexBuffer,
                    ResourceUsage.Immutable,
                    CpuAccessFlags.None);

                fixed (int* pIndices = model.Indices)
                {
                    var iData = new SubresourceData(pIndices);
                    ib = device.CreateBuffer(iDesc, iData);
                }
                gpuBytes += indexBufferSize;

                var parts = model.Parts.ToArray();
                partTextures = new ID3D11ShaderResourceView?[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    string tPath = parts[i].TexturePath;
                    if (string.IsNullOrEmpty(tPath) || !File.Exists(tPath)) continue;

                    try
                    {
                        var (srv, texGpuBytes) = _textureService.CreateShaderResourceView(tPath, device);
                        partTextures[i] = srv;
                        gpuBytes += texGpuBytes;
                    }
                    catch (Exception)
                    {
                    }
                }

                var modelSettings = ModelSettings.Instance;
                if (!modelSettings.IsGpuMemoryPerModelAllowed(gpuBytes))
                {
                    long gpuMB = gpuBytes / (1024L * 1024L);
                    string message = string.Format(
                        Texts.GpuMemoryExceeded,
                        Path.GetFileName(filePath),
                        gpuMB,
                        modelSettings.MaxGpuMemoryPerModelMB);
                    UserNotification.ShowWarning(message, Texts.ResourceLimitTitle);
                    return null;
                }

                var item = new GpuResourceCacheItem(device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale, gpuBytes);
                string cacheKey = GetCacheKey(filePath);
                GpuResourceCache.Instance.AddOrUpdate(cacheKey, item);
                success = true;
                return item;
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

        public void Dispose()
        {
            lock (SharedRenderLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;
            }

            _sceneDrawManager.Dispose();
            _shaderManager.Dispose();
            _renderTargets.Dispose();
            _skinningManager.Dispose();
            _frameStateCache?.Dispose();

            if (_textureService is IDisposable disposableTextureService)
            {
                disposableTextureService.Dispose();
            }

            _preCalcStates.Clear();
            _worldMasterLights.Clear();
            _layersToRender.Clear();
            _newLayerStatesTemp.Clear();

            _disposer.DisposeAndClear();
            ClearDynamicTextureCache();

            D3DResourcesPool.Release(_devices.D3D.Device);

            SceneContext.Unregister(_parameter.InstanceId, _registrationToken);
            _sceneApi?.Dispose();
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
}

using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Renderers;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering.Core
{
    public sealed class ObjLoaderSource : IShapeSource
    {
        public static readonly object SharedRenderLock = new object();

        private const int MaxHierarchyDepth = 100;

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly ObjLoaderParameter _parameter;
        private readonly DisposeCollector _disposer = new DisposeCollector();
        private readonly ObjModelLoader _loader;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;
        private readonly CustomShaderManager _shaderManager;
        private readonly ShadowRenderer _shadowRenderer;
        private readonly SceneRenderer _sceneRenderer;
        private readonly ITextureService _textureService;

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

        private ImmutableDictionary<string, LayerState> _layerStates = ImmutableDictionary<string, LayerState>.Empty;

        private bool _isDisposed;

        private static readonly ID3D11ShaderResourceView[] _emptySrvArray4 = new ID3D11ShaderResourceView[4];
        private static readonly ID3D11Buffer[] _emptyBufferArray1 = new ID3D11Buffer[1];

        private readonly List<(string Guid, LayerState State, LayerData Data)> _preCalcStates = new();
        private readonly Dictionary<int, LayerState> _worldMasterLights = new();
        private readonly List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> _layersToRender = new();
        private readonly Dictionary<string, LayerState> _newLayerStatesTemp = new();
        private readonly Dictionary<string, LayerState> _mutableStatesForShadow = new();
        private readonly Dictionary<string, LayerState> _mutableStatesForScene = new();
        private readonly Matrix4x4[] _lightViewProjs = new Matrix4x4[D3DResources.CascadeCount];
        private readonly float[] _cascadeSplits = new float[4];
        private readonly Vector3[] _frustumCorners = new Vector3[8];

        private static readonly Vector3[] _ndcCorners =
        {
            new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0),
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, 1)
        };

        static ObjLoaderSource()
        {
            var app = Application.Current;
            if (app != null)
            {
                app.Dispatcher.InvokeAsync(() =>
                {
                    if (app.MainWindow != null)
                    {
                        app.MainWindow.Closing += (s, e) => GpuResourceCache.Instance.Clear();
                    }
                    app.Exit += (s, e) => GpuResourceCache.Instance.Clear();
                    app.Dispatcher.ShutdownStarted += (s, e) => GpuResourceCache.Instance.Clear();
                });
            }
        }

        public D2D.ID2D1Image Output => _commandList ?? throw new InvalidOperationException("Update must be called before accessing Output.");

        public ObjLoaderSource(IGraphicsDevicesAndContext devices, ObjLoaderParameter parameter)
        {
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            _loader = new ObjModelLoader();
            _textureService = new TextureService();

            _resources = new D3DResources(devices.D3D.Device);
            _disposer.Collect(_resources);

            _renderTargets = new RenderTargetManager();
            _shaderManager = new CustomShaderManager(devices);
            _shadowRenderer = new ShadowRenderer(devices, _resources);
            _sceneRenderer = new SceneRenderer(devices, _resources, _renderTargets, _shaderManager);
        }

        private bool IsDeviceLost()
        {
            try
            {
                var device = _devices.D3D.Device;
                if (device == null) return true;
                var reason = device.DeviceRemovedReason;
                return reason.Failure;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Failed to check device status: {ex.Message}");
                return true;
            }
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
                    System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Device lost: 0x{ex.HResult:X8}");
                    GpuResourceCache.Instance.CleanupInvalidResources();
                    CreateEmptyCommandList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Render error: {ex.Message}");
                    CreateEmptyCommandList();
                }
            }
        }

        private void UpdateInternal(TimelineItemSourceDescription desc)
        {
            _parameter.Duration = (double)desc.ItemDuration.Frame / desc.FPS;

            if (!_parameter.IsSwitchingLayer)
            {
                _parameter.SyncActiveLayer();
            }

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            int sw = Math.Max(1, (int)_parameter.ScreenWidth.GetValue(frame, length, fps));
            int sh = Math.Max(1, (int)_parameter.ScreenHeight.GetValue(frame, length, fps));
            bool resized = _renderTargets.EnsureSize(_devices, sw, sh);

            var (camX, camY, camZ, targetX, targetY, targetZ) = CalculateCameraTransforms(frame, length, fps);

            var settingsVersion = _parameter.SettingsVersion.GetValue(frame, length, fps);
            var settings = PluginSettings.Instance;

            UpdateResourcesIfNeeded(settings);

            bool settingsChanged = Math.Abs(_lastSettingsVersion - settingsVersion) > 1e-5;
            bool cameraChanged = Math.Abs(_lastCamX - camX) > 1e-5 || Math.Abs(_lastCamY - camY) > 1e-5 || Math.Abs(_lastCamZ - camZ) > 1e-5 ||
                                 Math.Abs(_lastTargetX - targetX) > 1e-5 || Math.Abs(_lastTargetY - targetY) > 1e-5 || Math.Abs(_lastTargetZ - targetZ) > 1e-5;
            bool shadowSettingsChanged = _lastShadowResolution != settings.ShadowResolution || _lastShadowEnabled != settings.ShadowMappingEnabled;

            var (activeWorldId, newLayerStates, layersChanged) = BuildLayerStates(frame, length, fps, settings);

            bool activeWorldIdChanged = _lastActiveWorldId != activeWorldId;
            bool needsShadowRedraw = layersChanged || settingsChanged || shadowSettingsChanged || activeWorldIdChanged || cameraChanged;
            bool needsSceneRedraw = needsShadowRedraw || cameraChanged || resized || _commandList == null;

            if (!needsSceneRedraw)
            {
                return;
            }

            ProcessVisibilityHierarchy(newLayerStates);

            Interlocked.Exchange(ref _layerStates, newLayerStates);

            bool shadowValid = RenderShadowsIfApplicable(
                settings, ref activeWorldId, sw, sh,
                camX, camY, camZ, targetX, targetY, targetZ, needsShadowRedraw);

            bool needsEnvMapRedraw = layersChanged || activeWorldIdChanged || settingsChanged;
            RenderMainScene(sw, sh, camX, camY, camZ, targetX, targetY, targetZ,
                shadowValid, activeWorldId, needsEnvMapRedraw);

            FinalizeCommandList(camX, camY, camZ, targetX, targetY, targetZ,
                settingsVersion, activeWorldId, settings);
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
        }

        private (int activeWorldId, ImmutableDictionary<string, LayerState> newLayerStates, bool layersChanged) BuildLayerStates(long frame, long length, int fps, PluginSettings settings)
        {
            _preCalcStates.Clear();
            _worldMasterLights.Clear();

            var activeGuid = _parameter.ActiveLayerGuid;
            int activeWorldId = 0;

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
                    FilePath = layer.FilePath ?? string.Empty,
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
                    VisibleParts = layer.VisibleParts,
                    ParentGuid = layer.ParentGuid
                };

                _preCalcStates.Add((layer.Guid, layerState, layer));

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
            var previousStates = Volatile.Read(ref _layerStates);

            foreach (var item in _preCalcStates)
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

            var newLayerStates = _newLayerStatesTemp.ToImmutableDictionary();
            return (activeWorldId, newLayerStates, layersChanged);
        }

        private void ProcessVisibilityHierarchy(ImmutableDictionary<string, LayerState> newLayerStates)
        {
            _layersToRender.Clear();

            foreach (var item in _preCalcStates)
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
                    if (GpuResourceCache.Instance.TryGetValue(layerState.FilePath, out var cached))
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
                            GC.Collect(2, GCCollectionMode.Optimized);
                        }
                    }

                    if (resource != null)
                    {
                        _layersToRender.Add((item.Data, resource, layerState));
                    }
                }
            }
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
                PopulateDictionary(_mutableStatesForShadow, Volatile.Read(ref _layerStates));
                _shadowRenderer.Render(_layersToRender, _lightViewProjs, activeWorldId, _mutableStatesForShadow);
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

            float[] splitDistances = { nearPlane, nearPlane + (farPlane - nearPlane) * 0.05f, nearPlane + (farPlane - nearPlane) * 0.2f, farPlane };
            _cascadeSplits[0] = splitDistances[1];
            _cascadeSplits[1] = splitDistances[2];
            _cascadeSplits[2] = splitDistances[3];

            for (int i = 0; i < D3DResources.CascadeCount; i++)
            {
                float sn = splitDistances[i];
                float sf = splitDistances[i + 1];
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
            PopulateDictionary(_mutableStatesForScene, Volatile.Read(ref _layerStates));
            _sceneRenderer.Render(_layersToRender, _mutableStatesForScene, _parameter, sw, sh,
                camX, camY, camZ, targetX, targetY, targetZ,
                _lightViewProjs, _cascadeSplits, shadowValid, activeWorldId, needsEnvMapRedraw);
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

        private static void PopulateDictionary(Dictionary<string, LayerState> target, ImmutableDictionary<string, LayerState> source)
        {
            target.Clear();
            foreach (var kvp in source)
            {
                target[kvp.Key] = kvp.Value;
            }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Failed to clear resource bindings: {ex.Message}");
            }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Failed to create empty command list: {ex.Message}");
            }
        }

        private static bool AreStatesEqual(in LayerState a, in LayerState b)
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
                        var bitmapSource = _textureService.Load(tPath);
                        if (bitmapSource.CanFreeze && !bitmapSource.IsFrozen) bitmapSource.Freeze();
                        var convertedBitmap = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

                        int width = convertedBitmap.PixelWidth;
                        int height = convertedBitmap.PixelHeight;
                        int stride = width * 4;
                        int requiredSize = stride * height;
                        byte[] pixels = ArrayPool<byte>.Shared.Rent(requiredSize);
                        try
                        {
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

                            gpuBytes += (long)width * height * 4;
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(pixels);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Failed to load texture {tPath}: {ex.Message}");
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
                GpuResourceCache.Instance.AddOrUpdate(filePath, item);
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

        public void Dispose()
        {
            lock (SharedRenderLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                _shaderManager.Dispose();
                _renderTargets.Dispose();

                if (_textureService is IDisposable disposableTextureService)
                {
                    disposableTextureService.Dispose();
                }

                _preCalcStates.Clear();
                _worldMasterLights.Clear();
                _layersToRender.Clear();
                _newLayerStatesTemp.Clear();
                _mutableStatesForShadow.Clear();
                _mutableStatesForScene.Clear();

                _disposer.DisposeAndClear();
            }
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
                System.Diagnostics.Debug.WriteLine($"ObjLoaderSource: Dispose failed: {ex.Message}");
            }
        }
    }
}
using ObjLoader.Api;
using ObjLoader.Api.Core;
using ObjLoader.Cache.Gpu;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core.Resolvers;
using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Rendering.Core.States;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Models;
using ObjLoader.Rendering.Renderers;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Services.Rendering;
using ObjLoader.Services.Textures;
using ObjLoader.Settings;
using System.Numerics;
using System.Windows;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering.Core;

public sealed class ObjLoaderSource : IShapeSource2
{
    public static readonly object SharedRenderLock = new();

    public IEnumerable<VideoController> Controllers { get; private set; } = [];

    private readonly IGraphicsDevicesAndContext _devices;
    private readonly ObjLoaderParameter _parameter;
    private readonly DisposeCollector _disposer = new();
    private readonly D3DResources _resources;
    private readonly RenderTargetManager _renderTargets;
    private readonly CustomShaderCache _shaderCache;
    private readonly ShaderCompiler _shaderCompiler;
    private readonly ShadowRenderer _shadowRenderer;
    private readonly SceneRenderer _sceneRenderer;
    private readonly ITextureService _textureService;
    private readonly IDynamicTextureManager _dynamicTextureManager;
    private readonly ISkinningManager _skinningManager;
    private readonly ISceneDrawManager _sceneDrawManager;
    private readonly IFrameStateCache _frameStateCache;

    private readonly LayerStateBuilder _layerStateBuilder = new();
    private readonly ShadowCameraCalculator _shadowCameraCalc = new();
    private readonly VisibilityAndSkinningResolver _visibilityResolver;
    private readonly GpuResourceFactory _resourceFactory;

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
    private int _lastRenderWidth;
    private int _lastRenderHeight;
    private bool _lastShadowValid;
    private int _lastRenderWorldId;

    private Dictionary<string, LayerState> _layerStates = new();

    private ObjLoaderSceneApi? _sceneApi;
    private Guid _registrationToken;

    private bool _isDisposed;
    private TimelineItemSourceDescription _lastDesc = default!;
    private bool _hasDesc;

    private static readonly ID3D11ShaderResourceView[] _emptySrvArray4 = new ID3D11ShaderResourceView[4];
    private static readonly ID3D11Buffer[] _emptyBufferArray1 = new ID3D11Buffer[1];
    private static readonly ID3D11RenderTargetView[] _emptyRtvArray = [];
    private static readonly Color4 _clearBlend = new Color4(0, 0, 0, 0);

    private double _currentTime;

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
        _textureService = new TextureService();
        _dynamicTextureManager = new DynamicTextureManager(_textureService);
        _skinningManager = new SkinningManager(_devices.D3D.Device);
        _sceneDrawManager = new SceneDrawManager(_devices);
        _frameStateCache = new FrameStateCache();
        _resourceFactory = new GpuResourceFactory(() => _devices.D3D.Device, _textureService, "export:");
        _visibilityResolver = new VisibilityAndSkinningResolver(_devices, new ObjModelLoader(), _skinningManager, _dynamicTextureManager, _resourceFactory);

        if (Services.Shared.SharedResourceRegistry.SharedDevices == null)
        {
            Services.Shared.SharedResourceRegistry.SetSharedDevices(_devices);
        }
        Services.Shared.SharedResourceRegistry.RegisterDrawManager(_parameter.InstanceId, _sceneDrawManager);

        _resources = D3DResourcesPool.Acquire(devices.D3D.Device);

        _renderTargets = new RenderTargetManager();
        _shaderCompiler = new ShaderCompiler(devices);
        _shaderCache = new CustomShaderCache(new ShaderService(), _shaderCompiler);
        _shadowRenderer = new ShadowRenderer(devices, _resources);
        _sceneRenderer = new SceneRenderer(devices, _resources, _renderTargets, _shaderCache, _sceneDrawManager);

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
            ForceRender,
            devices.D3D?.Device?.NativePointer ?? IntPtr.Zero);

        _registrationToken = SceneContext.Register(_parameter.InstanceId, _sceneApi);
    }

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

        if (desc != null && desc.FPS > 0)
        {
            _parameter.Duration = (double)desc.ItemDuration.Frame / desc.FPS;
        }
        else
        {
            _parameter.Duration = 0.0;
        }

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
                RefreshSceneContent();
            }
            catch (SharpGen.Runtime.SharpGenException ex) when (
                ex.HResult == unchecked((int)0x887A0005) ||
                ex.HResult == unchecked((int)0x887A0006) ||
                ex.HResult == unchecked((int)0x887A0007))
            {
                GpuResourceCache.Instance.CleanupInvalidResources();
            }
            catch { }
        }
        return _commandList;
    }

    private void RefreshSceneContent()
    {
        if (_isDisposed || _lastRenderWidth <= 0 || _lastRenderHeight <= 0 || double.IsNaN(_lastCamX)) return;

        if (_sceneApi != null)
            _sceneDrawManager.UpdateFromApi(_sceneApi.DrawInternal);

        bool hasBillboards = _sceneDrawManager.GetBillboards().Count > 0;

        RenderMainScene(_lastRenderWidth, _lastRenderHeight,
            _lastCamX, _lastCamY, _lastCamZ,
            _lastTargetX, _lastTargetY, _lastTargetZ,
            _lastShadowValid, _lastRenderWorldId, hasBillboards);

        ClearResourceBindings();
        _sceneDrawManager.ClearDirtyFlag();
    }

    private void UpdateInternal(TimelineItemSourceDescription? desc)
    {
        if (_isDisposed) return;

        if (_sceneApi != null)
        {
            _sceneDrawManager.UpdateFromApi(_sceneApi.DrawInternal);
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

        var (activeWorldId, layersChanged) = _layerStateBuilder.Build(_parameter, frame, length, fps, settings, _layerStates);

        var stateToRender = _frameStateCache.GetOrCreateState();
        stateToRender.Update(frame, camX, camY, camZ, targetX, targetY, targetZ, activeWorldId, _layerStateBuilder.NewLayerStatesTemp);
        _frameStateCache.SaveState(frame, stateToRender);

        bool activeWorldIdChanged = _lastActiveWorldId != stateToRender.ActiveWorldId;
        bool drawManagerDirty = _sceneDrawManager.IsDirty;
        bool hasExternalOrBillboards = _sceneDrawManager.GetExternalObjects().Count > 0 || _sceneDrawManager.GetBillboards().Count > 0;
        bool needsShadowRedraw = layersChanged || settingsChanged || shadowSettingsChanged || activeWorldIdChanged || cameraChanged;
        bool needsSceneRedraw = needsShadowRedraw || cameraChanged || resized || _commandList == null || _layerStateBuilder.HasBoneAnimation || drawManagerDirty || hasExternalOrBillboards;

        if (!needsSceneRedraw)
        {
            return;
        }

        _visibilityResolver.Process(stateToRender.LayerStates, _layerStateBuilder.PreCalcStates, _currentTime);

        _layerStates = stateToRender.LayerStates;

        int renderWorldId = stateToRender.ActiveWorldId;
        bool shadowValid = RenderShadowsIfApplicable(
            settings, ref renderWorldId, sw, sh,
            stateToRender.CamX, stateToRender.CamY, stateToRender.CamZ,
            stateToRender.TargetX, stateToRender.TargetY, stateToRender.TargetZ,
            needsShadowRedraw);

        bool needsEnvMapRedraw = layersChanged || activeWorldIdChanged || settingsChanged || drawManagerDirty || hasExternalOrBillboards;
        RenderMainScene(sw, sh, stateToRender.CamX, stateToRender.CamY, stateToRender.CamZ,
            stateToRender.TargetX, stateToRender.TargetY, stateToRender.TargetZ,
            shadowValid, renderWorldId, needsEnvMapRedraw);

        _lastRenderWidth = sw;
        _lastRenderHeight = sh;
        _lastShadowValid = shadowValid;
        _lastRenderWorldId = renderWorldId;

        FinalizeCommandList(camX, camY, camZ, targetX, targetY, targetZ,
            settingsVersion, activeWorldId, settings);
        _sceneDrawManager.ClearDirtyFlag();

        UpdateControllers(stateToRender, sw, sh);
    }

    private void UpdateControllers(FrameState stateToRender, int sw, int sh)
    {
        Controllers = ObjLoaderController.CreateControllers(_parameter, stateToRender, _layerStates, sw, sh);
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

    private bool RenderShadowsIfApplicable(
        PluginSettings settings, ref int activeWorldId, int sw, int sh,
        double camX, double camY, double camZ,
        double targetX, double targetY, double targetZ,
        bool needsShadowRedraw)
    {
        LayerState shadowLightState = default;
        if (_layerStateBuilder.WorldMasterLights.TryGetValue(activeWorldId, out var al))
        {
            shadowLightState = al;
        }
        else if (_layerStateBuilder.WorldMasterLights.Count > 0)
        {
            foreach (var entry in _layerStateBuilder.WorldMasterLights.Values)
            {
                shadowLightState = entry;
                break;
            }
            activeWorldId = shadowLightState.WorldId;
        }

        Array.Clear(_shadowCameraCalc.LightViewProjs, 0, _shadowCameraCalc.LightViewProjs.Length);
        Array.Clear(_shadowCameraCalc.CascadeSplits, 0, _shadowCameraCalc.CascadeSplits.Length);

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
            _shadowCameraCalc.Compute(settings, sw, sh, camX, camY, camZ, targetX, targetY, targetZ, shadowLightState.Fov, lightDir);
        }
        else
        {
            lightDir = Vector3.Normalize(lPos == Vector3.Zero ? Vector3.UnitY : -lPos);
            Vector3 dir = -Vector3.Normalize(lPos);
            var lightView = Matrix4x4.CreateLookAt(lPos, lPos + dir, Vector3.UnitY);
            var lightProj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, 1.0f, RenderingConstants.SpotLightFarPlaneExport);
            for (int i = 0; i < D3DResources.CascadeCount; i++)
            {
                _shadowCameraCalc.LightViewProjs[i] = lightView * lightProj;
                _shadowCameraCalc.CascadeSplits[i] = RenderingConstants.CascadeSplitInfinity;
            }
        }

        if (needsShadowRedraw)
        {
            _shadowRenderer.Render(_visibilityResolver.LayersToRender, _shadowCameraCalc.LightViewProjs, activeWorldId, _layerStates);
        }

        return true;
    }

    private void RenderMainScene(
        int sw, int sh,
        double camX, double camY, double camZ,
        double targetX, double targetY, double targetZ,
        bool shadowValid, int activeWorldId, bool needsEnvMapRedraw)
    {
        _sceneRenderer.Render(_visibilityResolver.LayersToRender, _layerStates, _parameter, sw, sh,
            camX, camY, camZ, targetX, targetY, targetZ,
            _shadowCameraCalc.LightViewProjs, _shadowCameraCalc.CascadeSplits, shadowValid, activeWorldId, needsEnvMapRedraw, _dynamicTextureManager.Textures);
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
            context.OMSetRenderTargets(0, _emptyRtvArray, null);
            context.OMSetBlendState(null, _clearBlend, -1);
            context.OMSetDepthStencilState(null, 0);
            context.PSSetShaderResources(0, 4, _emptySrvArray4);
            context.VSSetShaderResources(0, 4, _emptySrvArray4);
            context.VSSetConstantBuffers(0, 1, _emptyBufferArray1);
            context.PSSetConstantBuffers(0, 1, _emptyBufferArray1);
            context.RSSetState(null);
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

    public void Dispose()
    {
        lock (SharedRenderLock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }

        Services.Shared.SharedResourceRegistry.UnregisterDrawManager(_parameter.InstanceId);
        if (!Services.Shared.SharedResourceRegistry.HasDrawManagers)
        {
            Services.Shared.SharedResourceRegistry.SetSharedDevices(null);
        }

        foreach (var layer in _visibilityResolver.LayersToRender)
        {
            GpuResourceCache.Instance.ScheduleRelease(layer.State.CacheKey);
        }

        var delay = ModelSettings.Instance.D3DResourceReleaseDelay;
        var texPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in _visibilityResolver.LayersToRender)
        {
            if (layer.Resource.Parts != null)
            {
                foreach (var part in layer.Resource.Parts)
                {
                    if (!string.IsNullOrEmpty(part.TexturePath))
                        texPaths.Add(part.TexturePath);
                }
            }
        }
        if (texPaths.Count > 0)
        {
            TextureService.ScheduleEvictPaths(texPaths, delay);
        }

        _sceneDrawManager.Dispose();
        _shaderCache.Dispose();
        _renderTargets.Dispose();
        _skinningManager.Dispose();
        _sceneRenderer?.Dispose();
        _shadowRenderer?.Dispose();
        _frameStateCache?.Dispose();

        if (_textureService is IDisposable disposableTextureService)
        {
            disposableTextureService.Dispose();
        }

        _layerStateBuilder.PreCalcStates.Clear();
        _layerStateBuilder.WorldMasterLights.Clear();
        _visibilityResolver.LayersToRender.Clear();
        _layerStateBuilder.NewLayerStatesTemp.Clear();

        _disposer.DisposeAndClear();
        ClearDynamicTextureCache();

        D3DResourcesPool.Release(_devices.D3D.Device);

        SceneContext.Unregister(_parameter.InstanceId, _registrationToken);
        _sceneApi?.Dispose();
    }
}
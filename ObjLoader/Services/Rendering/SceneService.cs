using ObjLoader.Core.Timeline;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core;
using ObjLoader.Services.Rendering.Scene;
using ObjLoader.Settings;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.Services.Rendering;

internal sealed class SceneService : IDisposable
{
    private readonly ObjLoaderParameter _parameter;
    private readonly RenderService _renderService;
    private readonly ModelLoaderService _loaderService;
    private readonly SceneHierarchyResolver _hierarchyResolver;
    private readonly SceneRenderDataConverter _dataConverter;

    public Action? OnModelLoaded
    {
        get => _loaderService.OnModelLoaded;
        set => _loaderService.OnModelLoaded = value;
    }

    public double ModelScale => _loaderService.ModelScale;
    public double ModelHeight => _loaderService.ModelHeight;

    public SceneService(ObjLoaderParameter parameter, RenderService renderService)
    {
        _parameter = parameter;
        _renderService = renderService;
        _loaderService = new ModelLoaderService(parameter, renderService);
        _hierarchyResolver = new SceneHierarchyResolver();
        _dataConverter = new SceneRenderDataConverter(renderService);
    }

    public void LoadModel() => _loaderService.LoadModel();

    public Task LoadModelAsync() => _loaderService.LoadModelAsync();

    public void ComputeModelScale() => _loaderService.ComputeModelScale();

    public void Render(PerspectiveCamera camera, double currentTime, int width, int height, bool isPilotView, Color themeColor, bool isWireframe, bool isGrid, bool isInfinite, bool isInteracting, bool enableShadow = true)
    {
        _loaderService.EnsureModelLoadedAsyncIfNeeded();

        if (_renderService.SceneImage == null) return;

        var camDir = camera.LookDirection; camDir.Normalize();
        var camUp = camera.UpDirection; camUp.Normalize();
        var camPos = camera.Position;
        var target = camPos + camDir;

        float camPosX = (float)camPos.X;
        float camPosY = (float)camPos.Y;
        float camPosZ = (float)camPos.Z;

        var view = Matrix4x4.CreateLookAt(
            new Vector3(camPosX, camPosY, camPosZ),
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
        Matrix4x4 axisConversion = RenderingConstants.GetAxisConversionMatrix(settings.CoordinateSystem);

        var layerList = _parameter.Layers;
        int activeIndex = _parameter.SelectedLayerIndex;
        bool isIndexValid = activeIndex >= 0 && activeIndex < layerList.Count;
        LayerData? activeLayer = isIndexValid ? layerList[activeIndex] : null;

        double globalLiftY = _hierarchyResolver.BuildLocalPlacements(_parameter, _loaderService, currentFrame, len, fps, settings, activeLayer);
        _hierarchyResolver.ResolveHierarchy();

        _dataConverter.ConvertToRenderData(
            _parameter,
            _hierarchyResolver.LocalPlacements,
            _hierarchyResolver.GlobalPlacements,
            axisConversion,
            globalLiftY,
            currentFrame,
            len,
            fps,
            activeLayer);

        _renderService.Render(
            _dataConverter.RenderDataList,
            view,
            proj,
            new Vector3(camPosX, camPosY, camPosZ),
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
        _loaderService.Dispose();
        _dataConverter.Dispose();
    }
}
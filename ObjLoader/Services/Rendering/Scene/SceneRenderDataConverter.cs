using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Mathematics;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.Utilities.Logging;
using System.IO;
using Vortice.Direct3D11;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Services.Rendering.Scene;

internal sealed class SceneRenderDataConverter : IDisposable
{
    private readonly RenderService _renderService;
    private ID3D11Device? _device => _renderService.Device;
    private ISkinningManager? _skinningManager;

    private readonly Dictionary<string, (ObjVertex[] Vertices, Core.Mmd.VertexBoneWeight[] BoneWeights)> _modelSkinningData = new();
    private readonly Dictionary<string, List<Core.Mmd.PmxBone>> _modelBones = new();
    private readonly Dictionary<string, string> _registeredSkinningGuids = new();
    private readonly Dictionary<string, (BoneAnimator? Animator, string VmdPath, string ModelPath, VmdData VmdData)> _animators = new();

    private readonly List<LayerRenderData> _layerRenderDataBuffer = new();
    private readonly HashSet<string> _validLayerGuidsBuffer = new();
    private readonly List<string> _guidsToRemoveBuffer = new();
    private readonly HashSet<string> _usedPathsBuffer = new();
    private readonly List<string> _pathsToRemoveBuffer = new();

    public List<LayerRenderData> RenderDataList => _layerRenderDataBuffer;

    public SceneRenderDataConverter(RenderService renderService)
    {
        _renderService = renderService;
    }

    public void ConvertToRenderData(
        ObjLoaderParameter parameter,
        Dictionary<string, HierarchyNode> localPlacements,
        Dictionary<string, Matrix4x4> globalPlacements,
        Matrix4x4 axisConversion,
        double globalLiftY,
        double currentFrame,
        int len,
        int fps,
        LayerData? activeLayer)
    {
        _layerRenderDataBuffer.Clear();
        _validLayerGuidsBuffer.Clear();

        if (_skinningManager == null && _device != null)
        {
            _skinningManager = new SkinningManager(_device);
        }

        foreach (var kvp in localPlacements)
        {
            var guid = kvp.Key;
            _validLayerGuidsBuffer.Add(guid);

            var info = kvp.Value;
            var layer = info.Layer;
            var resource = info.Resource;

            var overrideVB = ProcessLayerSkinning(guid, layer, currentFrame, fps);

            if (!globalPlacements.TryGetValue(guid, out var globalPlacement))
            {
                globalPlacement = info.Local;
            }

            var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter)
                          * Matrix4x4.CreateScale(resource.ModelScale);

            var finalWorld = normalize
                           * axisConversion
                           * globalPlacement
                           * Matrix4x4.CreateTranslation(0, (float)globalLiftY, 0);

            bool isActive = activeLayer != null && layer == activeLayer;

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
                BaseColor = isActive ? parameter.BaseColor : layer.BaseColor,
                LightEnabled = isActive ? parameter.IsLightEnabled : layer.IsLightEnabled,
                WorldId = (int)(isActive
                    ? parameter.WorldId.GetValue((long)currentFrame, len, fps)
                    : layer.WorldId.GetValue((long)currentFrame, len, fps)),
                HeightOffset = 0,
                VisibleParts = layer.VisibleParts,
                Data = layer,
                OverrideVB = overrideVB,
                WorldBoundingBox = CullingBox.Transform(resource.LocalBoundingBox, finalWorld),
                IsAnimated = overrideVB != null
            });
        }
        CleanupStaleSkinningGuids();
        CleanupUnusedModelData();
    }

    private ID3D11Buffer? ProcessLayerSkinning(string guid, LayerData layer, double currentFrame, int fps)
    {
        if (_skinningManager == null || string.IsNullOrEmpty(layer.FilePath))
        {
            return null;
        }

        EnsureModelSkinningData(layer.FilePath);

        if (!_modelSkinningData.TryGetValue(layer.FilePath, out var skinData) || skinData.Vertices.Length == 0)
        {
            return null;
        }

        EnsureSkinningRegistration(guid, layer.FilePath, skinData);

        var animator = ResolveAnimator(guid, layer);

        double motionTime = animator != null
            ? Math.Max(0, (currentFrame / fps) - layer.VmdTimeOffset)
            : 0;

        _skinningManager.ProcessSkinning(guid, layer.FilePath, animator, motionTime);
        return _skinningManager.GetOverrideVertexBuffer(guid);
    }

    private void EnsureModelSkinningData(string filePath)
    {
        if (_modelSkinningData.ContainsKey(filePath))
        {
            return;
        }

        if (!filePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
        {
            SetEmptyModelData(filePath);
            return;
        }

        try
        {
            var pmxModel = new PmxParser().Parse(filePath);
            if (pmxModel.BoneWeights != null && pmxModel.Bones.Count > 0)
            {
                _modelSkinningData[filePath] = (pmxModel.Vertices, pmxModel.BoneWeights);
                _modelBones[filePath] = pmxModel.Bones;
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
        }
        SetEmptyModelData(filePath);
    }

    private void SetEmptyModelData(string filePath)
    {
        _modelSkinningData[filePath] = (Array.Empty<ObjVertex>(), Array.Empty<Core.Mmd.VertexBoneWeight>());
        _modelBones[filePath] = new List<Core.Mmd.PmxBone>();
    }

    private void EnsureSkinningRegistration(
        string guid,
        string filePath,
        (ObjVertex[] Vertices, Core.Mmd.VertexBoneWeight[] BoneWeights) skinData)
    {
        if (_registeredSkinningGuids.TryGetValue(guid, out var currentPath) && currentPath == filePath)
        {
            return;
        }
        _skinningManager!.RegisterSkinningState(guid, filePath, skinData.Vertices, skinData.BoneWeights);
        _registeredSkinningGuids[guid] = filePath;
    }

    private BoneAnimator? ResolveAnimator(string guid, LayerData layer)
    {
        if (string.IsNullOrEmpty(layer.VmdFilePath) || !File.Exists(layer.VmdFilePath))
        {
            return null;
        }

        if (_animators.TryGetValue(guid, out var cache))
        {
            if (cache.VmdPath == layer.VmdFilePath && cache.ModelPath == layer.FilePath)
            {
                return cache.Animator;
            }
            return RebuildAnimator(guid, layer,
                cache.VmdPath == layer.VmdFilePath ? cache.VmdData : null);
        }
        return RebuildAnimator(guid, layer, null);
    }

    private BoneAnimator? RebuildAnimator(string guid, LayerData layer, VmdData? cachedVmd)
    {
        try
        {
            var vmdData = cachedVmd ?? VmdParser.Parse(layer.VmdFilePath);
            BoneAnimator? animator = null;

            if (vmdData.BoneFrames.Count > 0
                && _modelBones.TryGetValue(layer.FilePath, out var bones)
                && bones.Count > 0)
            {
                animator = new BoneAnimator(bones, vmdData.BoneFrames);
            }
            _animators[guid] = (animator, layer.VmdFilePath, layer.FilePath, vmdData);
            return animator;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            Logger<SceneRenderDataConverter>.Instance.Warning($"Error loading VMD: {layer.VmdFilePath}", ex);
            return null;
        }
    }

    private void CleanupStaleSkinningGuids()
    {
        _guidsToRemoveBuffer.Clear();

        foreach (var key in _registeredSkinningGuids.Keys)
        {
            if (!_validLayerGuidsBuffer.Contains(key))
            {
                _guidsToRemoveBuffer.Add(key);
            }
        }

        foreach (var key in _guidsToRemoveBuffer)
        {
            _animators.Remove(key);
            _registeredSkinningGuids.Remove(key);
            _skinningManager?.RemoveSkinningState(key);
        }
    }

    private void CleanupUnusedModelData()
    {
        _usedPathsBuffer.Clear();
        foreach (var path in _registeredSkinningGuids.Values)
        {
            _usedPathsBuffer.Add(path);
        }

        _pathsToRemoveBuffer.Clear();
        foreach (var key in _modelSkinningData.Keys)
        {
            if (!_usedPathsBuffer.Contains(key))
            {
                _pathsToRemoveBuffer.Add(key);
            }
        }

        foreach (var path in _pathsToRemoveBuffer)
        {
            _modelSkinningData.Remove(path);
            _modelBones.Remove(path);
        }
    }

    public void Dispose()
    {
        _skinningManager?.Dispose();
    }
}
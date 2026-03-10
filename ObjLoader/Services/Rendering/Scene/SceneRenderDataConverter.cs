using System.IO;
using System.Windows.Media;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.Parsers;
using ObjLoader.Utilities.Logging;
using Vortice.Direct3D11;
using Matrix4x4 = System.Numerics.Matrix4x4;
using ObjLoader.Rendering.Mathematics;

namespace ObjLoader.Services.Rendering.Scene;

internal sealed class SceneRenderDataConverter : IDisposable
{
    private readonly RenderService _renderService;
    private ID3D11Device? _device => _renderService.Device;
    private ISkinningManager? _skinningManager;

    private readonly Dictionary<string, (ObjVertex[] Vertices, Core.Mmd.VertexBoneWeight[] BoneWeights)> _modelSkinningData = new();
    private readonly Dictionary<string, List<Core.Mmd.PmxBone>> _modelBones = new();
    private readonly Dictionary<string, string> _registeredSkinningGuids = new();
    private readonly Dictionary<string, (BoneAnimator Animator, string VmdPath)> _animators = new();

    private readonly List<LayerRenderData> _layerRenderDataBuffer = new();
    private readonly HashSet<string> _validLayerGuidsBuffer = new();
    private readonly List<string> _animatorsToRemoveBuffer = new();

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
                        catch (Exception ex) when (ex is IOException || ex is InvalidDataException)
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
                            catch (Exception ex) when (ex is IOException || ex is InvalidDataException)
                            {
                                Logger<SceneRenderDataConverter>.Instance.Warning($"Error loading VMD: {layer.VmdFilePath}", ex);
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

            if (!globalPlacements.TryGetValue(guid, out var globalPlacement))
                globalPlacement = info.Local;

            var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);

            var finalWorld = normalize * axisConversion * globalPlacement * Matrix4x4.CreateTranslation(0, (float)globalLiftY, 0);

            var worldBox = CullingBox.Transform(resource.LocalBoundingBox, finalWorld);

            bool isAnimated = overrideVB != null;

            bool isActive = (activeLayer != null && layer == activeLayer);
            bool lightEnabled = isActive ? parameter.IsLightEnabled : layer.IsLightEnabled;
            Color baseColor = isActive ? parameter.BaseColor : layer.BaseColor;

            double wIdVal = isActive ? parameter.WorldId.GetValue((long)currentFrame, len, fps) : layer.WorldId.GetValue((long)currentFrame, len, fps);
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
                OverrideVB = overrideVB,
                WorldBoundingBox = worldBox,
                IsAnimated = isAnimated
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
        _skinningManager?.Dispose();
    }
}
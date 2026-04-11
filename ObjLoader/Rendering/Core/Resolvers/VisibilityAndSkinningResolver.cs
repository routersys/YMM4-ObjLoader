using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Parsers;
using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Rendering.Core.States;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Services.Mmd.Animation;
using ObjLoader.Services.Mmd.Parsers;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Rendering.Core.Resolvers;

internal sealed class VisibilityAndSkinningResolver(
    IGraphicsDevicesAndContext devices,
    ObjModelLoader loader,
    ISkinningManager skinningManager,
    IDynamicTextureManager dynamicTextureManager,
    GpuResourceFactory resourceFactory)
{
    private const int MaxHierarchyDepth = 100;

    public List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> LayersToRender { get; } = new(8);
    private readonly HashSet<string> _activeSkinningGuids = [];
    private readonly HashSet<string> _usedTexturePaths = [];

    public void Process(
        Dictionary<string, LayerState> newLayerStates,
        List<(string Guid, LayerState State, LayerData Data)> preCalcStates,
        double currentTime)
    {
        LayersToRender.Clear();

        var preCalcSpan = CollectionsMarshal.AsSpan(preCalcStates);
        foreach (var item in preCalcSpan)
        {
            if (!newLayerStates.TryGetValue(item.Guid, out var layerState))
            {
                continue;
            }

            if (!ResolveEffectiveVisibility(layerState, newLayerStates))
            {
                continue;
            }

            if (string.IsNullOrEmpty(layerState.FilePath))
            {
                continue;
            }

            var resource = ResolveGpuResource(layerState, out var loadedModel);
            if (resource == null)
            {
                continue;
            }

            EnsureSkinningReady(item, layerState, ref loadedModel);

            double motionTime = Math.Max(0, currentTime - item.Data.VmdTimeOffset);
            ID3D11Buffer? overrideVB = null;

            if (item.Data.BoneAnimatorInstance != null)
            {
                skinningManager.ProcessSkinning(item.Guid, layerState.FilePath, item.Data.BoneAnimatorInstance, motionTime);
                overrideVB = skinningManager.GetOverrideVertexBuffer(item.Guid);
            }
            else
            {
                skinningManager.RemoveSkinningState(item.Guid);
            }

            if (layerState.FilePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
            {
                _activeSkinningGuids.Add(item.Guid);
            }
            LayersToRender.Add((item.Data, resource, layerState, overrideVB));
        }
        skinningManager.CleanupStaleStates(_activeSkinningGuids);
        _activeSkinningGuids.Clear();
        PrepareDynamicTextures();
    }

    private static bool ResolveEffectiveVisibility(LayerState layerState, Dictionary<string, LayerState> allStates)
    {
        if (!layerState.IsVisible)
        {
            return false;
        }

        var parentGuid = layerState.ParentGuid;
        int depth = 0;

        while (!string.IsNullOrEmpty(parentGuid) && allStates.TryGetValue(parentGuid, out var parentState))
        {
            if (!parentState.IsVisible)
            {
                return false;
            }

            parentGuid = parentState.ParentGuid;
            if (++depth > MaxHierarchyDepth)
            {
                break;
            }
        }
        return true;
    }

    private GpuResourceCacheItem? ResolveGpuResource(LayerState layerState, out ObjModel? loadedModel)
    {
        loadedModel = null;

        if (GpuResourceCache.Instance.TryGetValue(layerState.CacheKey, out var cached)
            && cached != null
            && cached.Device == devices.D3D.Device)
        {
            return cached;
        }

        loadedModel = loader.Load(layerState.FilePath);
        return loadedModel.Vertices.Length > 0
            ? resourceFactory.Create(loadedModel, layerState.FilePath)
            : null;
    }

    private void EnsureSkinningReady(
        (string Guid, LayerState State, LayerData Data) item,
        LayerState layerState,
        ref ObjModel? loadedModel)
    {
        if (string.IsNullOrEmpty(item.Data.VmdFilePath) || !File.Exists(item.Data.VmdFilePath))
        {
            return;
        }

        if (!layerState.FilePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool needsRebuild = DetermineSkinningRebuildNeeded(item.Data, layerState.FilePath);

        if (needsRebuild && item.Data.VmdMotionData != null)
        {
            RebuildSkinningState(item, layerState, ref loadedModel);
        }
        else if (item.Data.BoneAnimatorInstance != null)
        {
            EnsureSkinningRegistered(item.Guid, layerState, ref loadedModel);
        }
    }

    private static bool DetermineSkinningRebuildNeeded(LayerData data, string filePath)
    {
        if (data.VmdMotionData == null)
        {
            try
            {
                data.VmdMotionData = VmdParser.Parse(data.VmdFilePath);
            }
            catch
            {
                return false;
            }
            return true;
        }

        if (data.BoneAnimatorInstance == null)
        {
            return true;
        }

        if (!string.Equals(data.AppliedModelFilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            data.BoneAnimatorInstance = null;
            data.AppliedModelFilePath = string.Empty;
            return true;
        }
        return false;
    }

    private void RebuildSkinningState(
        (string Guid, LayerState State, LayerData Data) item,
        LayerState layerState,
        ref ObjModel? loadedModel)
    {
        try
        {
            loadedModel ??= loader.Load(layerState.FilePath);
            loadedModel.ExtensionLoadTask?.Wait();

            if (loadedModel.BoneWeights == null)
            {
                return;
            }

            skinningManager.RegisterSkinningState(
                item.Guid, layerState.FilePath, loadedModel.Vertices, loadedModel.BoneWeights);

            if (loadedModel.Bones.Count > 0)
            {
                item.Data.BoneAnimatorInstance = new BoneAnimator(
                    loadedModel.Bones,
                    item.Data.VmdMotionData!.BoneFrames,
                    loadedModel.RigidBodies,
                    loadedModel.Joints);
                item.Data.AppliedModelFilePath = layerState.FilePath;
            }
        }
        catch
        {
        }
    }

    private void EnsureSkinningRegistered(
        string guid,
        LayerState layerState,
        ref ObjModel? loadedModel)
    {
        try
        {
            loadedModel ??= loader.Load(layerState.FilePath);
            loadedModel.ExtensionLoadTask?.Wait();

            if (loadedModel.BoneWeights == null)
            {
                return;
            }

            skinningManager.RegisterSkinningState(
                guid, layerState.FilePath, loadedModel.Vertices, loadedModel.BoneWeights);
        }
        catch
        {
        }
    }

    private void PrepareDynamicTextures()
    {
        var device = devices.D3D.Device;
        if (device == null)
        {
            return;
        }

        _usedTexturePaths.Clear();

        foreach (var layer in LayersToRender)
        {
            if (layer.State.PartMaterials == null)
            {
                continue;
            }

            foreach (var pm in layer.State.PartMaterials.Values)
            {
                if (!string.IsNullOrEmpty(pm.TexturePath))
                {
                    _usedTexturePaths.Add(pm.TexturePath!);
                }
            }
        }
        dynamicTextureManager.Prepare(_usedTexturePaths, device);
    }
}
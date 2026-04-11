using ObjLoader.Core.Timeline;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core.States;
using ObjLoader.Settings;
using System.Runtime.InteropServices;

namespace ObjLoader.Rendering.Core.Resolvers;

internal sealed class LayerStateBuilder
{
    private const string CacheKeyPrefix = "export:";

    public List<(string Guid, LayerState State, LayerData Data)> PreCalcStates { get; } = new(8);
    public Dictionary<int, LayerState> WorldMasterLights { get; } = new(8);
    public Dictionary<string, LayerState> NewLayerStatesTemp { get; } = new(8);
    public bool HasBoneAnimation { get; private set; }

    private string _cachedRawShaderPath = string.Empty;
    private string _cachedTrimmedShaderPath = string.Empty;

    private static string GetCacheKey(string filePath) => string.Concat(CacheKeyPrefix, filePath);

    public (int ActiveWorldId, bool LayersChanged) Build(
        ObjLoaderParameter parameter,
        long frame,
        long length,
        int fps,
        PluginSettings settings,
        Dictionary<string, LayerState> previousStates)
    {
        PreCalcStates.Clear();
        WorldMasterLights.Clear();
        HasBoneAnimation = false;

        var activeGuid = parameter.ActiveLayerGuid;
        int activeWorldId = 0;

        foreach (var layer in parameter.Layers)
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
                    copiedVisibleParts = [.. visibleParts];
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
                ShaderFilePath = GetTrimmedShaderPath(parameter.ShaderFilePath),
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

            PreCalcStates.Add((layer.Guid, layerState, layer));

            if (layer.BoneAnimatorInstance != null)
            {
                HasBoneAnimation = true;
            }

            if (layer.Guid == activeGuid)
            {
                activeWorldId = worldId;
            }

            if (!WorldMasterLights.ContainsKey(worldId) || layer.Guid == activeGuid)
            {
                WorldMasterLights[worldId] = layerState;
            }
        }

        NewLayerStatesTemp.Clear();
        bool layersChanged = false;

        var preCalcSpan = CollectionsMarshal.AsSpan(PreCalcStates);
        foreach (var item in preCalcSpan)
        {
            var layerState = item.State;
            if (WorldMasterLights.TryGetValue(layerState.WorldId, out var master))
            {
                layerState = layerState with
                {
                    LightX = master.LightX,
                    LightY = master.LightY,
                    LightZ = master.LightZ,
                    IsLightEnabled = master.IsLightEnabled,
                    LightType = master.LightType
                };
            }

            NewLayerStatesTemp[item.Guid] = layerState;

            if (!previousStates.TryGetValue(item.Guid, out var oldState) || !AreStatesEqual(in oldState, in layerState))
            {
                layersChanged = true;
            }
        }

        if (!layersChanged && previousStates.Count != NewLayerStatesTemp.Count)
        {
            layersChanged = true;
        }

        return (activeWorldId, layersChanged);
    }

    private string GetTrimmedShaderPath(string? raw)
    {
        if (raw == null) return string.Empty;
        if (string.Equals(raw, _cachedRawShaderPath, StringComparison.Ordinal))
            return _cachedTrimmedShaderPath;
        _cachedRawShaderPath = raw;
        _cachedTrimmedShaderPath = raw.Trim('"');
        return _cachedTrimmedShaderPath;
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
}
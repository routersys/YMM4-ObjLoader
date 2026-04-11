using ObjLoader.Core.Timeline;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core;
using ObjLoader.Settings;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Services.Rendering.Scene;

internal sealed class SceneHierarchyResolver
{
    private const int MaxHierarchyDepth = 100;

    private readonly Dictionary<string, HierarchyNode> _localPlacementsBuffer = new();
    private readonly Dictionary<string, Matrix4x4> _globalPlacementsBuffer = new();

    public Dictionary<string, HierarchyNode> LocalPlacements => _localPlacementsBuffer;
    public Dictionary<string, Matrix4x4> GlobalPlacements => _globalPlacementsBuffer;

    public double BuildLocalPlacements(
        ObjLoaderParameter parameter,
        ModelLoaderService loaderService,
        double currentFrame,
        int len,
        int fps,
        PluginSettings settings,
        LayerData? activeLayer)
    {
        _localPlacementsBuffer.Clear();
        double globalLiftY = 0;
        bool liftComputed = false;

        var layerList = parameter.Layers;

        for (int i = 0; i < layerList.Count; i++)
        {
            var layer = layerList[i];
            if (!layer.IsVisible) continue;

            string filePath = layer.FilePath ?? string.Empty;
            if (string.IsNullOrEmpty(filePath)) continue;

            var resource = loaderService.GetCachedResource(filePath);
            if (resource == null) continue;

            if (!loaderService.BoundingData.TryGetValue(filePath, out var bounds)) continue;

            bool isActive = (activeLayer != null && layer == activeLayer);

            double x, y, z, scale, rx, ry, rz;

            if (isActive)
            {
                x = parameter.X.GetValue((long)currentFrame, len, fps);
                y = parameter.Y.GetValue((long)currentFrame, len, fps);
                z = parameter.Z.GetValue((long)currentFrame, len, fps);
                scale = parameter.Scale.GetValue((long)currentFrame, len, fps);
                rx = parameter.RotationX.GetValue((long)currentFrame, len, fps);
                ry = parameter.RotationY.GetValue((long)currentFrame, len, fps);
                rz = parameter.RotationZ.GetValue((long)currentFrame, len, fps);
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
                if (RenderingConstants.IsZUp(settings.CoordinateSystem))
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

            _localPlacementsBuffer[layer.Guid] = new HierarchyNode(placement, layer.ParentGuid, layer, resource);
        }

        return globalLiftY;
    }

    public void ResolveHierarchy()
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
}
using ObjLoader.Core.Enums;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core.States;
using ObjLoader.Rendering.Models;
using ObjLoader.Rendering.Utilities;
using System.Numerics;
using YukkuriMovieMaker.Player.Video;

namespace ObjLoader.Rendering.Core;

internal static class ObjLoaderController
{
    public static IEnumerable<VideoController> CreateControllers(
        ObjLoaderParameter parameter,
        FrameState stateToRender,
        Dictionary<string, LayerState> layerStates,
        int screenWidth,
        int screenHeight)
    {
        if (parameter.Layers == null || parameter.Layers.Count == 0 || parameter.SelectedLayerIndex < 0 || parameter.SelectedLayerIndex >= parameter.Layers.Count)
        {
            return [];
        }

        var activeLayer = parameter.Layers[parameter.SelectedLayerIndex];
        if (!layerStates.TryGetValue(activeLayer.Guid, out var activeLayerState))
        {
            return [];
        }

        float aspect = (float)screenWidth / screenHeight;
        Vector3 cameraPosition = new Vector3((float)stateToRender.CamX, (float)stateToRender.CamY, (float)stateToRender.CamZ);
        var target = new Vector3((float)stateToRender.TargetX, (float)stateToRender.TargetY, (float)stateToRender.TargetZ);
        Matrix4x4 mainView, mainProj;

        float fov = 45.0f;
        ProjectionType projectionType = ProjectionType.Perspective;
        foreach (var l in layerStates.Values)
        {
            if (l.WorldId == stateToRender.ActiveWorldId)
            {
                fov = (float)l.Fov;
                projectionType = l.Projection;
                break;
            }
        }

        if (projectionType == ProjectionType.Parallel)
        {
            if (cameraPosition == target) cameraPosition.Z -= RenderingConstants.CameraFallbackOffsetParallel;
            mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
            mainProj = Matrix4x4.CreateOrthographic(RenderingConstants.DefaultOrthoSize * aspect, RenderingConstants.DefaultOrthoSize, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
        }
        else
        {
            if (cameraPosition == target) cameraPosition.Z -= RenderingConstants.CameraFallbackOffsetPerspective;
            mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
            float radFov = (float)(Math.Max(1, Math.Min(RenderingConstants.DefaultFovLimit, fov)) * Math.PI / 180.0);
            mainProj = Matrix4x4.CreatePerspectiveFieldOfView(radFov, aspect, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
        }
        var viewProj = mainView * mainProj;

        Matrix4x4 parentMatrix = Matrix4x4.Identity;
        var currentGuid = activeLayerState.ParentGuid;
        int depth = 0;
        while (!string.IsNullOrEmpty(currentGuid) && layerStates.TryGetValue(currentGuid, out var parentState))
        {
            parentMatrix *= RenderUtils.GetLayerTransform(parentState);
            currentGuid = parentState.ParentGuid;
            depth++;
            if (depth > 100) break;
        }

        var localMatrix = RenderUtils.GetLayerTransform(activeLayerState);
        var worldMatrix = localMatrix * parentMatrix;

        var worldPos = worldMatrix.Translation;

        var v4 = Vector4.Transform(new Vector4(worldPos, 1.0f), viewProj);
        if (v4.W <= 0) return [];

        var ndc = new Vector3(v4.X / v4.W, v4.Y / v4.W, v4.Z / v4.W);

        float screenX = ndc.X * (screenWidth / 2f);
        float screenY = -ndc.Y * (screenHeight / 2f);

        var controlPoint = new ControllerPoint(new Vector3(screenX, screenY, 0), e =>
        {
            if (!Matrix4x4.Invert(viewProj, out var invViewProj)) return;
            if (!Matrix4x4.Invert(parentMatrix, out var invParent)) return;

            float zTarget = v4.Z / v4.W;

            float startNdcX = screenX / (screenWidth / 2f);
            float startNdcY = -screenY / (screenHeight / 2f);
            var startV4 = Vector4.Transform(new Vector4(startNdcX, startNdcY, zTarget, 1.0f), invViewProj);
            var startWorldPos = new Vector3(startV4.X / startV4.W, startV4.Y / startV4.W, startV4.Z / startV4.W);

            float newNdcX = (screenX + (float)e.Delta.X) / (screenWidth / 2f);
            float newNdcY = -(screenY + (float)e.Delta.Y) / (screenHeight / 2f);
            var newV4 = Vector4.Transform(new Vector4(newNdcX, newNdcY, zTarget, 1.0f), invViewProj);
            var newWorldPos = new Vector3(newV4.X / newV4.W, newV4.Y / newV4.W, newV4.Z / newV4.W);

            var deltaWorld = newWorldPos - startWorldPos;
            var deltaLocal = Vector3.TransformNormal(deltaWorld, invParent);

            var scaleFactor = activeLayerState.Scale > 0 ? 100.0 / activeLayerState.Scale : 1.0;
            parameter.X.AddToEachValues(deltaLocal.X * scaleFactor);
            parameter.Y.AddToEachValues(deltaLocal.Y * scaleFactor);
            parameter.Z.AddToEachValues(deltaLocal.Z * scaleFactor);
        });

        return [new VideoController([controlPoint]) { Connection = VideoControllerPointConnection.None }];
    }
}
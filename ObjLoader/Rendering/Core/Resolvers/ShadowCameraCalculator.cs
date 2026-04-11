using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Settings;
using System.Numerics;

namespace ObjLoader.Rendering.Core.Resolvers;

internal sealed class ShadowCameraCalculator
{
    private readonly float[] _splitDistances = new float[4];
    private static readonly Vector3[] NdcCorners =
    [
        new(-1, -1, 0), new(1, -1, 0), new(-1, 1, 0), new(1, 1, 0),
        new(-1, -1, 1), new(1, -1, 1), new(-1, 1, 1), new(1, 1, 1)
    ];

    public Matrix4x4[] LightViewProjs { get; } = new Matrix4x4[D3DResources.CascadeCount];
    public float[] CascadeSplits { get; } = new float[4];

    public void Compute(
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

        CascadeSplits[0] = _splitDistances[1];
        CascadeSplits[1] = _splitDistances[2];
        CascadeSplits[2] = _splitDistances[3];

        var frustumCorners = new Vector3[8];

        int limit = D3DResources.CascadeCount;
        for (int i = 0; i < limit; i++)
        {
            float sn = _splitDistances[i];
            float sf = _splitDistances[i + 1];
            var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, sn, sf);
            var invViewProj = Matrix4x4.Invert(viewMatrix * projMatrix, out var inv) ? inv : Matrix4x4.Identity;

            for (int j = 0; j < 8; j++)
            {
                frustumCorners[j] = Vector3.Transform(NdcCorners[j], invViewProj);
            }

            Vector3 center = Vector3.Zero;
            for (int j = 0; j < 8; j++) center += frustumCorners[j];
            center /= 8.0f;

            var lightView = Matrix4x4.CreateLookAt(center + lightDir * RenderingConstants.SunLightDistance, center, Vector3.UnitY);

            float minX = float.MaxValue, maxX = -float.MaxValue;
            float minY = float.MaxValue, maxY = -float.MaxValue;
            float minZ = float.MaxValue, maxZ = -float.MaxValue;

            for (int j = 0; j < 8; j++)
            {
                var tr = Vector3.Transform(frustumCorners[j], lightView);
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
            LightViewProjs[i] = lightView * lightProj;
        }
    }
}
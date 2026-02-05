using System.Numerics;
using Vortice.Mathematics;

namespace ObjLoader.Rendering.Core
{
    public static class RenderingConstants
    {
        public const float DefaultNearPlane = 0.1f;
        public const float DefaultFarPlane = 1000.0f;
        public const float ShadowNearPlane = 0.1f;
        public const float SunLightDistance = 500.0f;
        public const float SpotLightFarPlaneExport = 5000.0f;
        public const float SpotLightFarPlanePreview = 2000.0f;
        public const float ShadowOrthoMargin = 1000.0f;
        public const float CascadeSplitInfinity = 10000.0f;
        public const int EnvironmentMapSize = 512;
        public const int EnvironmentMapFaceCount = 6;
        public const int GridSize = 1000;
        public const float GridScaleBase = 50.0f;
        public const float DefaultFovLimit = 179.0f;

        public const int SlotStandardTexture = 0;
        public const int SlotShadowMap = 1;
        public const int SlotEnvironmentMap = 2;

        public const int SlotStandardSampler = 0;
        public const int SlotShadowSampler = 1;

        public static readonly Color4 ClearColorDark = new Color4(0.0f, 0.0f, 0.0f, 1.0f);
        public static readonly Vector4 GridColorDark = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
        public static readonly Vector4 AxisColorDark = new Vector4(0.8f, 0.8f, 0.8f, 1.0f);

        public static readonly Color4 ClearColorMedium = new Color4(0.13f, 0.13f, 0.13f, 1.0f);
        public static readonly Vector4 GridColorMedium = new Vector4(0.65f, 0.65f, 0.65f, 1.0f);
        public static readonly Vector4 AxisColorMedium = new Vector4(0.9f, 0.9f, 0.9f, 1.0f);

        public static readonly Color4 ClearColorLight = new Color4(0.9f, 0.9f, 0.9f, 1.0f);
        public static readonly Vector4 GridColorLight = new Vector4(0.4f, 0.4f, 0.4f, 1.0f);
        public static readonly Vector4 AxisColorLight = new Vector4(0.1f, 0.1f, 0.1f, 1.0f);
    }
}
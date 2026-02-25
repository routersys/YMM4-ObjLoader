using System.Numerics;
using System.Windows.Media;
using ObjLoader.Settings;

namespace ObjLoader.Rendering.Core
{
    internal static class ConstantBufferFactory
    {
        private static Vector4 ToVec4(Color c) =>
            new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);

        public static CBPerMaterial CreatePerMaterial(
            int worldId,
            Vector4 baseColor,
            bool lightEnabled,
            float diffuseIntensity,
            float shininess,
            float roughness,
            float metallic)
        {
            var settings = PluginSettings.Instance;

            return new CBPerMaterial
            {
                BaseColor = baseColor,
                LightEnabled = lightEnabled ? 1.0f : 0.0f,
                DiffuseIntensity = diffuseIntensity,
                SpecularIntensity = (float)settings.GetSpecularIntensity(worldId),
                Shininess = shininess,
                ToonParams = new Vector4(settings.GetToonEnabled(worldId) ? 1 : 0, settings.GetToonSteps(worldId), (float)settings.GetToonSmoothness(worldId), 0),
                RimParams = new Vector4(settings.GetRimEnabled(worldId) ? 1 : 0, (float)settings.GetRimIntensity(worldId), (float)settings.GetRimPower(worldId), 0),
                RimColor = ToVec4(settings.GetRimColor(worldId)),
                OutlineParams = new Vector4(settings.GetOutlineEnabled(worldId) ? 1 : 0, (float)settings.GetOutlineWidth(worldId), (float)settings.GetOutlinePower(worldId), 0),
                OutlineColor = ToVec4(settings.GetOutlineColor(worldId)),
                FogParams = new Vector4(settings.GetFogEnabled(worldId) ? 1 : 0, (float)settings.GetFogStart(worldId), (float)settings.GetFogEnd(worldId), (float)settings.GetFogDensity(worldId)),
                FogColor = ToVec4(settings.GetFogColor(worldId)),
                ColorCorrParams = new Vector4((float)settings.GetSaturation(worldId), (float)settings.GetContrast(worldId), (float)settings.GetGamma(worldId), (float)settings.GetBrightnessPost(worldId)),
                VignetteParams = new Vector4(settings.GetVignetteEnabled(worldId) ? 1 : 0, (float)settings.GetVignetteIntensity(worldId), (float)settings.GetVignetteRadius(worldId), (float)settings.GetVignetteSoftness(worldId)),
                VignetteColor = ToVec4(settings.GetVignetteColor(worldId)),
                ScanlineParams = new Vector4(settings.GetScanlineEnabled(worldId) ? 1 : 0, (float)settings.GetScanlineIntensity(worldId), (float)settings.GetScanlineFrequency(worldId), settings.GetScanlinePost(worldId) ? 1 : 0),
                ChromAbParams = new Vector4(settings.GetChromAbEnabled(worldId) ? 1 : 0, (float)settings.GetChromAbIntensity(worldId), 0, 0),
                MonoParams = new Vector4(settings.GetMonochromeEnabled(worldId) ? 1 : 0, (float)settings.GetMonochromeMix(worldId), 0, 0),
                MonoColor = ToVec4(settings.GetMonochromeColor(worldId)),
                PosterizeParams = new Vector4(settings.GetPosterizeEnabled(worldId) ? 1 : 0, settings.GetPosterizeLevels(worldId), 0, 0),
                PbrParams = new Vector4(metallic, roughness, 1.0f, 0),
                IblParams = new Vector4((float)settings.GetIBLIntensity(worldId), 6.0f, 0, 0),
                SsrParams = new Vector4(settings.GetSSREnabled(worldId) ? 1 : 0, (float)settings.GetSSRStep(worldId), (float)settings.GetSSRMaxDist(worldId), (float)settings.GetSSRMaxSteps(worldId)),
                SsrParams2 = new Vector4((float)settings.GetSSRMaxSteps(worldId), (float)settings.GetSSRThickness(worldId), 0, 0)
            };
        }

        public static CBPerFrame CreatePerFrameForScene(
            Matrix4x4 viewProj,
            Vector4 cameraPos,
            Vector4 lightPos,
            Vector4 ambientColor,
            Vector4 lightColor,
            Matrix4x4[] lightViewProjs,
            float[] cascadeSplits,
            int lightType,
            bool shadowValid,
            int activeWorldId,
            int stateWorldId,
            bool bindEnvironment)
        {
            var settings = PluginSettings.Instance;
            Matrix4x4.Invert(viewProj, out var inverseViewProj);

            return new CBPerFrame
            {
                ViewProj = Matrix4x4.Transpose(viewProj),
                InverseViewProj = Matrix4x4.Transpose(inverseViewProj),
                CameraPos = cameraPos,
                LightPos = lightPos,
                AmbientColor = ambientColor,
                LightColor = lightColor,
                GridColor = Vector4.Zero,
                GridAxisColor = Vector4.Zero,
                LightViewProj0 = Matrix4x4.Transpose(lightViewProjs[0]),
                LightViewProj1 = Matrix4x4.Transpose(lightViewProjs.Length > 1 ? lightViewProjs[1] : Matrix4x4.Identity),
                LightViewProj2 = Matrix4x4.Transpose(lightViewProjs.Length > 2 ? lightViewProjs[2] : Matrix4x4.Identity),
                LightTypeParams = new Vector4(lightType, 0, 0, 0),
                ShadowParams = new Vector4(
                    (shadowValid && stateWorldId == activeWorldId) ? 1.0f : 0.0f,
                    (float)settings.ShadowBias,
                    (float)settings.ShadowStrength,
                    (float)settings.ShadowResolution),
                CascadeSplits = new Vector4(
                    cascadeSplits[0],
                    cascadeSplits[1],
                    cascadeSplits[2],
                    cascadeSplits.Length > 3 ? cascadeSplits[3] : float.MaxValue),
                EnvironmentParam = bindEnvironment ? new Vector4(1, 0, 0, 0) : Vector4.Zero,
                PcssParams = new Vector4(
                    (float)settings.GetPcssLightSize(stateWorldId),
                    RenderingConstants.PcssDefaultSearchFactor,
                    (float)settings.GetPcssQuality(stateWorldId),
                    (float)settings.GetPcssQuality(stateWorldId))
            };
        }

        public static CBPerFrame CreatePerFrameForPreview(
            Matrix4x4 viewProj,
            Vector3 camPos,
            Vector3 finalLightPos,
            int worldId,
            Vector4 gridColor,
            Vector4 axisColor,
            Matrix4x4 lightViewProj,
            int lightType,
            bool enableShadow)
        {
            var settings = PluginSettings.Instance;

            return new CBPerFrame
            {
                ViewProj = Matrix4x4.Transpose(viewProj),
                InverseViewProj = Matrix4x4.Transpose(Matrix4x4.Identity),
                LightPos = new Vector4(finalLightPos, 1.0f),
                AmbientColor = ToVec4(settings.GetAmbientColor(worldId)),
                LightColor = ToVec4(settings.GetLightColor(worldId)),
                CameraPos = new Vector4(camPos, 1),
                GridColor = gridColor,
                GridAxisColor = axisColor,
                LightViewProj0 = Matrix4x4.Transpose(lightViewProj),
                LightViewProj1 = Matrix4x4.Identity,
                LightViewProj2 = Matrix4x4.Identity,
                LightTypeParams = new Vector4(lightType, 0, 0, 0),
                ShadowParams = new Vector4(
                    (enableShadow && settings.ShadowMappingEnabled) ? 1 : 0,
                    (float)settings.ShadowBias,
                    (float)settings.ShadowStrength,
                    (float)settings.ShadowResolution),
                CascadeSplits = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue),
                EnvironmentParam = new Vector4(1, 0, 0, 0),
                PcssParams = new Vector4(
                    (float)settings.GetPcssLightSize(worldId),
                    RenderingConstants.PcssDefaultSearchFactor,
                    (float)settings.GetPcssQuality(worldId),
                    (float)settings.GetPcssQuality(worldId))
            };
        }
    }
}
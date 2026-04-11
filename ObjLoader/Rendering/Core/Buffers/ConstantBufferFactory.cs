using ObjLoader.Settings;
using System.Numerics;
using System.Windows.Media;

namespace ObjLoader.Rendering.Core.Buffers
{
    internal static class ConstantBufferFactory
    {
        private static Vector4 ToVec4(Color c) =>
            new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);

        public static void CreatePerMaterial(
            int worldId,
            Vector4 baseColor,
            bool lightEnabled,
            float diffuseIntensity,
            float shininess,
            float roughness,
            float metallic,
            out CBPerMaterial core,
            out CBSceneEffects scene,
            out CBPostEffects post)
        {
            var s = PluginSettings.Instance;

            core = new CBPerMaterial
            {
                BaseColor = baseColor,
                LightEnabled = lightEnabled ? 1.0f : 0.0f,
                DiffuseIntensity = diffuseIntensity,
                SpecularIntensity = (float)s.GetSpecularIntensity(worldId),
                Shininess = shininess,
                ToonParams = new Vector4(s.GetToonEnabled(worldId) ? 1 : 0, s.GetToonSteps(worldId), (float)s.GetToonSmoothness(worldId), 0),
                PbrParams = new Vector4(metallic, roughness, 1.0f, 0)
            };

            scene = new CBSceneEffects
            {
                RimParams = new Vector4(s.GetRimEnabled(worldId) ? 1 : 0, (float)s.GetRimIntensity(worldId), (float)s.GetRimPower(worldId), 0),
                RimColor = ToVec4(s.GetRimColor(worldId)),
                OutlineParams = new Vector4(s.GetOutlineEnabled(worldId) ? 1 : 0, (float)s.GetOutlineWidth(worldId), (float)s.GetOutlinePower(worldId), 0),
                OutlineColor = ToVec4(s.GetOutlineColor(worldId)),
                FogParams = new Vector4(s.GetFogEnabled(worldId) ? 1 : 0, (float)s.GetFogStart(worldId), (float)s.GetFogEnd(worldId), (float)s.GetFogDensity(worldId)),
                FogColor = ToVec4(s.GetFogColor(worldId)),
                IblParams = new Vector4((float)s.GetIBLIntensity(worldId), 6.0f, 0, 0),
                SsrParams = new Vector4(s.GetSSREnabled(worldId) ? 1 : 0, (float)s.GetSSRStep(worldId), (float)s.GetSSRMaxDist(worldId), (float)s.GetSSRMaxSteps(worldId)),
                SsrParams2 = new Vector4((float)s.GetSSRMaxSteps(worldId), (float)s.GetSSRThickness(worldId), 0, 0)
            };

            post = new CBPostEffects
            {
                ColorCorrParams = new Vector4((float)s.GetSaturation(worldId), (float)s.GetContrast(worldId), (float)s.GetGamma(worldId), (float)s.GetBrightnessPost(worldId)),
                VignetteParams = new Vector4(s.GetVignetteEnabled(worldId) ? 1 : 0, (float)s.GetVignetteIntensity(worldId), (float)s.GetVignetteRadius(worldId), (float)s.GetVignetteSoftness(worldId)),
                VignetteColor = ToVec4(s.GetVignetteColor(worldId)),
                ScanlineParams = new Vector4(s.GetScanlineEnabled(worldId) ? 1 : 0, (float)s.GetScanlineIntensity(worldId), (float)s.GetScanlineFrequency(worldId), s.GetScanlinePost(worldId) ? 1 : 0),
                ChromAbParams = new Vector4(s.GetChromAbEnabled(worldId) ? 1 : 0, (float)s.GetChromAbIntensity(worldId), 0, 0),
                MonoParams = new Vector4(s.GetMonochromeEnabled(worldId) ? 1 : 0, (float)s.GetMonochromeMix(worldId), 0, 0),
                MonoColor = ToVec4(s.GetMonochromeColor(worldId)),
                PosterizeParams = new Vector4(s.GetPosterizeEnabled(worldId) ? 1 : 0, s.GetPosterizeLevels(worldId), 0, 0)
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
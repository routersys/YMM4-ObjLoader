using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin;

namespace ObjLoader.Settings
{
    public partial class PluginSettings : SettingsBase<PluginSettings>
    {
        public override string Name => Texts.PluginName;
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => false;
        public override object? SettingView => null;
        public static PluginSettings Instance => Default;

        private const int MaxWorlds = 20;
        private bool _isSpecularFixApplied = false;

        public List<WorldParameter> WorldParameters { get; set; } = new();

        public WorldParameter CurrentWorld
        {
            get
            {
                EnsureWorlds();
                return WorldParameters[Math.Clamp(_worldId, 0, MaxWorlds - 1)];
            }
        }

        private void EnsureWorlds()
        {
            if (WorldParameters == null) WorldParameters = new List<WorldParameter>();

            if (WorldParameters.Count < MaxWorlds)
            {
                for (int i = WorldParameters.Count; i < MaxWorlds; i++)
                {
                    WorldParameters.Add(new WorldParameter());
                }
            }
            else if (WorldParameters.Count > MaxWorlds)
            {
                WorldParameters.RemoveRange(MaxWorlds, WorldParameters.Count - MaxWorlds);
            }
        }

        public override void Initialize()
        {
            EnsureWorlds();
        }

        public PluginSettingsMemento CreateMemento()
        {
            EnsureWorlds();
            return new PluginSettingsMemento
            {
                CoordinateSystem = _coordinateSystem,
                CullMode = _cullMode,
                RenderQuality = _renderQuality,
                ShadowMappingEnabled = _shadowMappingEnabled,
                CascadedShadowsEnabled = _cascadedShadowsEnabled,
                ShadowResolution = _shadowResolution,
                ShadowBias = _shadowBias,
                ShadowStrength = _shadowStrength,
                SunLightShadowRange = _sunLightShadowRange,
                AssimpObj = _assimpObj,
                AssimpGlb = _assimpGlb,
                AssimpPly = _assimpPly,
                AssimpStl = _assimpStl,
                Assimp3mf = _assimp3mf,
                AssimpPmx = _assimpPmx,
                IsSpecularFixApplied = _isSpecularFixApplied,
                WorldId = _worldId,
                WorldParameters = WorldParameters.Select(w => (WorldParameter)w.Clone()).ToList()
            };
        }

        public void RestoreMemento(PluginSettingsMemento m)
        {
            _coordinateSystem = m.CoordinateSystem;
            _cullMode = m.CullMode;
            _renderQuality = m.RenderQuality;
            _shadowMappingEnabled = m.ShadowMappingEnabled;
            _cascadedShadowsEnabled = m.CascadedShadowsEnabled;
            _shadowResolution = m.ShadowResolution > 0 ? m.ShadowResolution : 2048;
            _shadowBias = m.ShadowBias;
            _shadowStrength = m.ShadowStrength;
            _sunLightShadowRange = m.SunLightShadowRange > 0 ? m.SunLightShadowRange : 100.0;

            _assimpObj = m.AssimpObj;
            _assimpGlb = m.AssimpGlb;
            _assimpPly = m.AssimpPly;
            _assimpStl = m.AssimpStl;
            _assimp3mf = m.Assimp3mf;
            _assimpPmx = m.AssimpPmx;
            _isSpecularFixApplied = m.IsSpecularFixApplied;
            _worldId = m.WorldId;

            if (m.WorldParameters != null && m.WorldParameters.Count > 0)
            {
                WorldParameters = m.WorldParameters.Select(w => (WorldParameter)w.Clone()).ToList();
            }
            else
            {
                WorldParameters = new List<WorldParameter>();
                for (int i = 0; i < MaxWorlds; i++)
                {
                    var w = new WorldParameter();

                    if (m.AmbientColors?.Count > i) w.Lighting.AmbientColor = m.AmbientColors[i];
                    if (m.LightColors?.Count > i) w.Lighting.LightColor = m.LightColors[i];
                    if (m.DiffuseIntensities?.Count > i) w.Lighting.DiffuseIntensity = m.DiffuseIntensities[i];
                    if (m.SpecularIntensities?.Count > i) w.Lighting.SpecularIntensity = m.SpecularIntensities[i];
                    if (m.Shininesses?.Count > i) w.Lighting.Shininess = m.Shininesses[i];

                    if (m.ToonEnabled?.Count > i) w.Toon.Enabled = m.ToonEnabled[i];
                    if (m.ToonSteps?.Count > i) w.Toon.Steps = m.ToonSteps[i];
                    if (m.ToonSmoothness?.Count > i) w.Toon.Smoothness = m.ToonSmoothness[i];

                    if (m.RimEnabled?.Count > i) w.Rim.Enabled = m.RimEnabled[i];
                    if (m.RimColor?.Count > i) w.Rim.Color = m.RimColor[i];
                    if (m.RimIntensity?.Count > i) w.Rim.Intensity = m.RimIntensity[i];
                    if (m.RimPower?.Count > i) w.Rim.Power = m.RimPower[i];

                    if (m.OutlineEnabled?.Count > i) w.Outline.Enabled = m.OutlineEnabled[i];
                    if (m.OutlineColor?.Count > i) w.Outline.Color = m.OutlineColor[i];
                    if (m.OutlineWidth?.Count > i) w.Outline.Width = m.OutlineWidth[i];
                    if (m.OutlinePower?.Count > i) w.Outline.Power = m.OutlinePower[i];

                    if (m.FogEnabled?.Count > i) w.Fog.Enabled = m.FogEnabled[i];
                    if (m.FogColor?.Count > i) w.Fog.Color = m.FogColor[i];
                    if (m.FogStart?.Count > i) w.Fog.Start = m.FogStart[i];
                    if (m.FogEnd?.Count > i) w.Fog.End = m.FogEnd[i];
                    if (m.FogDensity?.Count > i) w.Fog.Density = m.FogDensity[i];

                    if (m.Saturation?.Count > i) w.PostEffect.Saturation = m.Saturation[i];
                    if (m.Contrast?.Count > i) w.PostEffect.Contrast = m.Contrast[i];
                    if (m.Gamma?.Count > i) w.PostEffect.Gamma = m.Gamma[i];
                    if (m.BrightnessPost?.Count > i) w.PostEffect.BrightnessPost = m.BrightnessPost[i];

                    if (m.VignetteEnabled?.Count > i) w.Vignette.Enabled = m.VignetteEnabled[i];
                    if (m.VignetteColor?.Count > i) w.Vignette.Color = m.VignetteColor[i];
                    if (m.VignetteIntensity?.Count > i) w.Vignette.Intensity = m.VignetteIntensity[i];
                    if (m.VignetteRadius?.Count > i) w.Vignette.Radius = m.VignetteRadius[i];
                    if (m.VignetteSoftness?.Count > i) w.Vignette.Softness = m.VignetteSoftness[i];

                    if (m.ScanlineEnabled?.Count > i) w.Scanline.Enabled = m.ScanlineEnabled[i];
                    if (m.ScanlineIntensity?.Count > i) w.Scanline.Intensity = m.ScanlineIntensity[i];
                    if (m.ScanlineFrequency?.Count > i) w.Scanline.Frequency = m.ScanlineFrequency[i];
                    if (m.ScanlinePost?.Count > i) w.Scanline.ApplyAfterTonemap = m.ScanlinePost[i];

                    if (m.ChromAbEnabled?.Count > i) w.Artistic.ChromAbEnabled = m.ChromAbEnabled[i];
                    if (m.ChromAbIntensity?.Count > i) w.Artistic.ChromAbIntensity = m.ChromAbIntensity[i];
                    if (m.MonochromeEnabled?.Count > i) w.Artistic.MonochromeEnabled = m.MonochromeEnabled[i];
                    if (m.MonochromeColor?.Count > i) w.Artistic.MonochromeColor = m.MonochromeColor[i];
                    if (m.MonochromeMix?.Count > i) w.Artistic.MonochromeMix = m.MonochromeMix[i];
                    if (m.PosterizeEnabled?.Count > i) w.Artistic.PosterizeEnabled = m.PosterizeEnabled[i];
                    if (m.PosterizeLevels?.Count > i) w.Artistic.PosterizeLevels = m.PosterizeLevels[i];

                    WorldParameters.Add(w);
                }
            }
            EnsureWorlds();

            if (!_isSpecularFixApplied)
            {
                foreach (var w in WorldParameters)
                {
                    w.Lighting.SpecularIntensity = 1.0;
                }
                _isSpecularFixApplied = true;
            }

            OnPropertyChanged(string.Empty);
        }

        public WorldParameter GetWorld(int id)
        {
            EnsureWorlds();
            return WorldParameters[Math.Clamp(id, 0, MaxWorlds - 1)];
        }

        public Color GetAmbientColor(int id) => GetWorld(id).Lighting.AmbientColor;
        public Color GetLightColor(int id) => GetWorld(id).Lighting.LightColor;
        public double GetDiffuseIntensity(int id) => GetWorld(id).Lighting.DiffuseIntensity;
        public double GetSpecularIntensity(int id) => GetWorld(id).Lighting.SpecularIntensity;
        public double GetShininess(int id) => GetWorld(id).Lighting.Shininess;
        public bool GetShadowEnabled(int id) => GetWorld(id).Lighting.ShadowEnabled;

        public bool GetToonEnabled(int id) => GetWorld(id).Toon.Enabled;
        public int GetToonSteps(int id) => GetWorld(id).Toon.Steps;
        public double GetToonSmoothness(int id) => GetWorld(id).Toon.Smoothness;

        public bool GetRimEnabled(int id) => GetWorld(id).Rim.Enabled;
        public Color GetRimColor(int id) => GetWorld(id).Rim.Color;
        public double GetRimIntensity(int id) => GetWorld(id).Rim.Intensity;
        public double GetRimPower(int id) => GetWorld(id).Rim.Power;

        public bool GetOutlineEnabled(int id) => GetWorld(id).Outline.Enabled;
        public Color GetOutlineColor(int id) => GetWorld(id).Outline.Color;
        public double GetOutlineWidth(int id) => GetWorld(id).Outline.Width;
        public double GetOutlinePower(int id) => GetWorld(id).Outline.Power;

        public bool GetFogEnabled(int id) => GetWorld(id).Fog.Enabled;
        public Color GetFogColor(int id) => GetWorld(id).Fog.Color;
        public double GetFogStart(int id) => GetWorld(id).Fog.Start;
        public double GetFogEnd(int id) => GetWorld(id).Fog.End;
        public double GetFogDensity(int id) => GetWorld(id).Fog.Density;

        public double GetSaturation(int id) => GetWorld(id).PostEffect.Saturation;
        public double GetContrast(int id) => GetWorld(id).PostEffect.Contrast;
        public double GetGamma(int id) => GetWorld(id).PostEffect.Gamma;
        public double GetBrightnessPost(int id) => GetWorld(id).PostEffect.BrightnessPost;

        public bool GetVignetteEnabled(int id) => GetWorld(id).Vignette.Enabled;
        public Color GetVignetteColor(int id) => GetWorld(id).Vignette.Color;
        public double GetVignetteIntensity(int id) => GetWorld(id).Vignette.Intensity;
        public double GetVignetteRadius(int id) => GetWorld(id).Vignette.Radius;
        public double GetVignetteSoftness(int id) => GetWorld(id).Vignette.Softness;

        public bool GetChromAbEnabled(int id) => GetWorld(id).Artistic.ChromAbEnabled;
        public double GetChromAbIntensity(int id) => GetWorld(id).Artistic.ChromAbIntensity;

        public bool GetScanlineEnabled(int id) => GetWorld(id).Scanline.Enabled;
        public double GetScanlineIntensity(int id) => GetWorld(id).Scanline.Intensity;
        public double GetScanlineFrequency(int id) => GetWorld(id).Scanline.Frequency;
        public bool GetScanlinePost(int id) => GetWorld(id).Scanline.ApplyAfterTonemap;

        public bool GetMonochromeEnabled(int id) => GetWorld(id).Artistic.MonochromeEnabled;
        public Color GetMonochromeColor(int id) => GetWorld(id).Artistic.MonochromeColor;
        public double GetMonochromeMix(int id) => GetWorld(id).Artistic.MonochromeMix;

        public bool GetPosterizeEnabled(int id) => GetWorld(id).Artistic.PosterizeEnabled;
        public int GetPosterizeLevels(int id) => GetWorld(id).Artistic.PosterizeLevels;

        public double GetMetallic(int id) => GetWorld(id).PBR.Metallic;
        public double GetRoughness(int id) => GetWorld(id).PBR.Roughness;
        public double GetIBLIntensity(int id) => GetWorld(id).PBR.IBLIntensity;

        public bool GetSSREnabled(int id) => GetWorld(id).SSR.Enabled;
        public double GetSSRStep(int id) => GetWorld(id).SSR.Step;
        public double GetSSRMaxDist(int id) => GetWorld(id).SSR.MaxDist;
        public double GetSSRThickness(int id) => GetWorld(id).SSR.Thickness;

        public double GetPcssLightSize(int id) => GetWorld(id).PCSS.LightSize;
        public int GetPcssQuality(int id) => GetWorld(id).PCSS.Quality;

        [SettingButton(nameof(Texts.ResetDefaults), Placement = SettingButtonPlacement.BottomLeft, Order = 0, ResourceType = typeof(Texts))]
        public void ResetDefaults()
        {
            CoordinateSystem = CoordinateSystem.RightHandedYUp;
            CullMode = RenderCullMode.None;
            RenderQuality = RenderQuality.Standard;
            ShadowMappingEnabled = true;
            CascadedShadowsEnabled = false;
            ShadowResolution = 2048;
            ShadowBias = 0.001;
            ShadowStrength = 0.5;
            SunLightShadowRange = 100.0;
            AssimpObj = false;
            AssimpGlb = false;
            AssimpPly = false;
            AssimpStl = false;
            Assimp3mf = false;
            AssimpPmx = false;

            WorldParameters = new List<WorldParameter>();
            EnsureWorlds();

            OnPropertyChanged(string.Empty);
            NotifyWorldPropertiesChanged();
        }

        private void NotifyWorldPropertiesChanged()
        {
            OnPropertyChanged(string.Empty);
        }

        [SettingButton(nameof(Texts.OK), Placement = SettingButtonPlacement.BottomRight, Type = SettingButtonType.OK, Order = 100, ResourceType = typeof(Texts))]
        public void OK() { }

        [SettingButton(nameof(Texts.Cancel), Placement = SettingButtonPlacement.BottomRight, Type = SettingButtonType.Cancel, Order = 101, ResourceType = typeof(Texts))]
        public void Cancel() { }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            if (propertyName != null)
            {
                OnPropertyChanged(propertyName);
            }
            return true;
        }
    }
}
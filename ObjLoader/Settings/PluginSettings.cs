using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin;

namespace ObjLoader.Settings
{
    public class PluginSettings : SettingsBase<PluginSettings>
    {
        public override string Name => Texts.PluginName;
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => false;
        public override object? SettingView => null;
        public static PluginSettings Instance => Default;

        private const int MaxWorlds = 20;

        private CoordinateSystem _coordinateSystem = CoordinateSystem.RightHandedYUp;
        private RenderCullMode _cullMode = RenderCullMode.None;
        private RenderQuality _renderQuality = RenderQuality.Standard;

        private bool _assimpObj = false;
        private bool _assimpGlb = false;
        private bool _assimpPly = false;
        private bool _assimpStl = false;
        private bool _assimp3mf = false;
        private bool _assimpPmx = false;

        private int _worldId = 0;

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
                AssimpObj = _assimpObj,
                AssimpGlb = _assimpGlb,
                AssimpPly = _assimpPly,
                AssimpStl = _assimpStl,
                Assimp3mf = _assimp3mf,
                AssimpPmx = _assimpPmx,
                WorldId = _worldId,
                WorldParameters = WorldParameters.Select(w => (WorldParameter)w.Clone()).ToList()
            };
        }

        public void RestoreMemento(PluginSettingsMemento m)
        {
            _coordinateSystem = m.CoordinateSystem;
            _cullMode = m.CullMode;
            _renderQuality = m.RenderQuality;
            _assimpObj = m.AssimpObj;
            _assimpGlb = m.AssimpGlb;
            _assimpPly = m.AssimpPly;
            _assimpStl = m.AssimpStl;
            _assimp3mf = m.Assimp3mf;
            _assimpPmx = m.AssimpPmx;
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

        public bool GetMonochromeEnabled(int id) => GetWorld(id).Artistic.MonochromeEnabled;
        public Color GetMonochromeColor(int id) => GetWorld(id).Artistic.MonochromeColor;
        public double GetMonochromeMix(int id) => GetWorld(id).Artistic.MonochromeMix;

        public bool GetPosterizeEnabled(int id) => GetWorld(id).Artistic.PosterizeEnabled;
        public int GetPosterizeLevels(int id) => GetWorld(id).Artistic.PosterizeLevels;

        [SettingGroup("Global", nameof(Texts.Group_Global), Order = 0, Icon = "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,12.5A0.5,0.5 0 0,1 11.5,12A0.5,0.5 0 0,1 12,11.5A0.5,0.5 0 0,1 12.5,12A0.5,0.5 0 0,1 12,12.5M12,7.2C9.9,7.2 8.2,8.9 8.2,11C8.2,14 12,17.5 12,17.5C12,17.5 15.8,14 15.8,11C15.8,8.9 14.1,7.2 12,7.2Z", ResourceType = typeof(Texts))]
        [EnumSetting("Global", nameof(Texts.CoordinateSystem), Description = nameof(Texts.CoordinateSystem_Desc), ResourceType = typeof(Texts))]
        public CoordinateSystem CoordinateSystem
        {
            get => _coordinateSystem;
            set => SetProperty(ref _coordinateSystem, value);
        }

        [EnumSetting("Global", nameof(Texts.CullMode), Description = nameof(Texts.CullMode_Desc), ResourceType = typeof(Texts))]
        public RenderCullMode CullMode
        {
            get => _cullMode;
            set => SetProperty(ref _cullMode, value);
        }

        [EnumSetting("Global", nameof(Texts.RenderQuality), Description = nameof(Texts.RenderQuality_Desc), ResourceType = typeof(Texts))]
        public RenderQuality RenderQuality
        {
            get => _renderQuality;
            set => SetProperty(ref _renderQuality, value);
        }

        [SettingGroup("Lighting", nameof(Texts.Group_Lighting), Order = 1, Icon = "M12,2A7,7 0 0,0 5,9C5,11.38 6.19,13.47 8,14.74V17A1,1 0 0,0 9,18H15A1,1 0 0,0 16,17V14.74C17.81,13.47 19,11.38 19,9A7,7 0 0,0 12,2M9,21A1,1 0 0,0 10,22H14A1,1 0 0,0 15,21V20H9V21Z", ResourceType = typeof(Texts))]
        [IntSpinnerSetting("Lighting", nameof(Texts.WorldId), 0, 19, IsGroupHeader = true, Description = nameof(Texts.WorldId_Desc), ResourceType = typeof(Texts))]
        public int WorldId
        {
            get => _worldId;
            set
            {
                if (SetProperty(ref _worldId, value))
                {
                    NotifyWorldPropertiesChanged();
                }
            }
        }

        [ColorSetting("Lighting", nameof(Texts.AmbientColor), Description = nameof(Texts.AmbientColor_Desc), ResourceType = typeof(Texts))]
        public Color AmbientColor
        {
            get => CurrentWorld.Lighting.AmbientColor;
            set { if (CurrentWorld.Lighting.AmbientColor != value) { CurrentWorld.Lighting.AmbientColor = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Lighting", nameof(Texts.DiffuseIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.DiffuseIntensity_Desc), ResourceType = typeof(Texts))]
        public double DiffuseIntensity
        {
            get => CurrentWorld.Lighting.DiffuseIntensity;
            set { if (CurrentWorld.Lighting.DiffuseIntensity != value) { CurrentWorld.Lighting.DiffuseIntensity = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Lighting", nameof(Texts.SpecularIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.SpecularIntensity_Desc), ResourceType = typeof(Texts))]
        public double SpecularIntensity
        {
            get => CurrentWorld.Lighting.SpecularIntensity;
            set { if (CurrentWorld.Lighting.SpecularIntensity != value) { CurrentWorld.Lighting.SpecularIntensity = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Lighting", nameof(Texts.Shininess), 1, 100, Tick = 1, Description = nameof(Texts.Shininess_Desc), ResourceType = typeof(Texts))]
        public double Shininess
        {
            get => CurrentWorld.Lighting.Shininess;
            set { if (CurrentWorld.Lighting.Shininess != value) { CurrentWorld.Lighting.Shininess = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Environment", nameof(Texts.Group_Environment), Order = 2, ParentId = "Lighting", Icon = "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,5.29C7.24,5.84 7.09,6.44 7.09,7.09C7.09,7.74 7.24,8.34 7.5,8.89L3.34,7.18V7M3.34,17L7.5,18.71C7.24,18.16 7.09,17.56 7.09,16.91C7.09,16.26 7.24,15.66 7.5,15.11L3.34,16.82V17M20.66,17L16.5,15.29C16.76,15.84 16.91,16.44 16.91,17.09C16.91,17.74 16.76,18.34 16.5,18.89L20.66,17.18V17M20.66,7L16.5,8.71C16.76,8.16 16.91,7.56 16.91,6.91C16.91,6.26 16.76,5.66 16.5,5.11L20.66,6.82V7M12,22L9.61,18.58C10.35,18.85 11.16,19 12,19C12.84,19 13.65,18.85 14.39,18.58L12,22Z", ResourceType = typeof(Texts))]
        [ColorSetting("Environment", nameof(Texts.LightColor), Description = nameof(Texts.LightColor_Desc), ResourceType = typeof(Texts))]
        public Color LightColor
        {
            get => CurrentWorld.Lighting.LightColor;
            set { if (CurrentWorld.Lighting.LightColor != value) { CurrentWorld.Lighting.LightColor = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Toon", nameof(Texts.Group_Toon), Order = 3, ParentId = "Lighting", Icon = "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M11,7H13V9H15V11H13V13H11V11H9V9H11V7Z", ResourceType = typeof(Texts))]
        [BoolSetting("Toon", nameof(Texts.ToonEnabled), Description = nameof(Texts.ToonEnabled_Desc), ResourceType = typeof(Texts))]
        public bool ToonEnabled
        {
            get => CurrentWorld.Toon.Enabled;
            set { if (CurrentWorld.Toon.Enabled != value) { CurrentWorld.Toon.Enabled = value; OnPropertyChanged(); } }
        }

        [IntSpinnerSetting("Toon", nameof(Texts.ToonSteps), 1, 10, EnableBy = nameof(ToonEnabled), Description = nameof(Texts.ToonSteps_Desc), ResourceType = typeof(Texts))]
        public int ToonSteps
        {
            get => CurrentWorld.Toon.Steps;
            set { if (CurrentWorld.Toon.Steps != value) { CurrentWorld.Toon.Steps = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Toon", nameof(Texts.ToonSmoothness), 0, 1, Tick = 0.01, EnableBy = nameof(ToonEnabled), Description = nameof(Texts.ToonSmoothness_Desc), ResourceType = typeof(Texts))]
        public double ToonSmoothness
        {
            get => CurrentWorld.Toon.Smoothness;
            set { if (CurrentWorld.Toon.Smoothness != value) { CurrentWorld.Toon.Smoothness = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Rim", nameof(Texts.Group_Rim), Order = 4, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4Z", ResourceType = typeof(Texts))]
        [BoolSetting("Rim", nameof(Texts.RimEnabled), Description = nameof(Texts.RimEnabled_Desc), ResourceType = typeof(Texts))]
        public bool RimEnabled
        {
            get => CurrentWorld.Rim.Enabled;
            set { if (CurrentWorld.Rim.Enabled != value) { CurrentWorld.Rim.Enabled = value; OnPropertyChanged(); } }
        }

        [ColorSetting("Rim", nameof(Texts.RimColor), EnableBy = nameof(RimEnabled), Description = nameof(Texts.RimColor_Desc), ResourceType = typeof(Texts))]
        public Color RimColor
        {
            get => CurrentWorld.Rim.Color;
            set { if (CurrentWorld.Rim.Color != value) { CurrentWorld.Rim.Color = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Rim", nameof(Texts.RimIntensity), 0, 10, Tick = 0.1, EnableBy = nameof(RimEnabled), Description = nameof(Texts.RimIntensity_Desc), ResourceType = typeof(Texts))]
        public double RimIntensity
        {
            get => CurrentWorld.Rim.Intensity;
            set { if (CurrentWorld.Rim.Intensity != value) { CurrentWorld.Rim.Intensity = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Rim", nameof(Texts.RimPower), 0.1, 10, Tick = 0.1, EnableBy = nameof(RimEnabled), Description = nameof(Texts.RimPower_Desc), ResourceType = typeof(Texts))]
        public double RimPower
        {
            get => CurrentWorld.Rim.Power;
            set { if (CurrentWorld.Rim.Power != value) { CurrentWorld.Rim.Power = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Outline", nameof(Texts.Group_Outline), Order = 5, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M12,6A6,6 0 0,1 18,12A6,6 0 0,1 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6Z", ResourceType = typeof(Texts))]
        [BoolSetting("Outline", nameof(Texts.OutlineEnabled), Description = nameof(Texts.OutlineEnabled_Desc), ResourceType = typeof(Texts))]
        public bool OutlineEnabled
        {
            get => CurrentWorld.Outline.Enabled;
            set { if (CurrentWorld.Outline.Enabled != value) { CurrentWorld.Outline.Enabled = value; OnPropertyChanged(); } }
        }

        [ColorSetting("Outline", nameof(Texts.OutlineColor), EnableBy = nameof(OutlineEnabled), Description = nameof(Texts.OutlineColor_Desc), ResourceType = typeof(Texts))]
        public Color OutlineColor
        {
            get => CurrentWorld.Outline.Color;
            set { if (CurrentWorld.Outline.Color != value) { CurrentWorld.Outline.Color = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Outline", nameof(Texts.OutlineWidth), 0, 20, Tick = 0.1, EnableBy = nameof(OutlineEnabled), Description = nameof(Texts.OutlineWidth_Desc), ResourceType = typeof(Texts))]
        public double OutlineWidth
        {
            get => CurrentWorld.Outline.Width;
            set { if (CurrentWorld.Outline.Width != value) { CurrentWorld.Outline.Width = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Outline", nameof(Texts.OutlinePower), 0.1, 10, Tick = 0.1, EnableBy = nameof(OutlineEnabled), Description = nameof(Texts.OutlinePower_Desc), ResourceType = typeof(Texts))]
        public double OutlinePower
        {
            get => CurrentWorld.Outline.Power;
            set { if (CurrentWorld.Outline.Power != value) { CurrentWorld.Outline.Power = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Fog", nameof(Texts.Group_Fog), Order = 6, ParentId = "Lighting", Icon = "M3,4H21V8H3V4M3,10H21V14H3V10M3,16H21V20H3V16Z", ResourceType = typeof(Texts))]
        [BoolSetting("Fog", nameof(Texts.FogEnabled), Description = nameof(Texts.FogEnabled_Desc), ResourceType = typeof(Texts))]
        public bool FogEnabled
        {
            get => CurrentWorld.Fog.Enabled;
            set { if (CurrentWorld.Fog.Enabled != value) { CurrentWorld.Fog.Enabled = value; OnPropertyChanged(); } }
        }

        [ColorSetting("Fog", nameof(Texts.FogColor), EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogColor_Desc), ResourceType = typeof(Texts))]
        public Color FogColor
        {
            get => CurrentWorld.Fog.Color;
            set { if (CurrentWorld.Fog.Color != value) { CurrentWorld.Fog.Color = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Fog", nameof(Texts.FogStart), 0, 1000, Tick = 1, EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogStart_Desc), ResourceType = typeof(Texts))]
        public double FogStart
        {
            get => CurrentWorld.Fog.Start;
            set { if (CurrentWorld.Fog.Start != value) { CurrentWorld.Fog.Start = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Fog", nameof(Texts.FogEnd), 0, 5000, Tick = 10, EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogEnd_Desc), ResourceType = typeof(Texts))]
        public double FogEnd
        {
            get => CurrentWorld.Fog.End;
            set { if (CurrentWorld.Fog.End != value) { CurrentWorld.Fog.End = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Fog", nameof(Texts.FogDensity), 0, 5, Tick = 0.01, EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogDensity_Desc), ResourceType = typeof(Texts))]
        public double FogDensity
        {
            get => CurrentWorld.Fog.Density;
            set { if (CurrentWorld.Fog.Density != value) { CurrentWorld.Fog.Density = value; OnPropertyChanged(); } }
        }

        [SettingGroup("PostEffect", nameof(Texts.Group_PostEffect), Order = 7, Icon = "M2,2V22H22V2H2M20,20H4V4H20V20M8,6H16V14H8V6M10,8V12H14V8H10Z", ResourceType = typeof(Texts))]
        [RangeSetting("PostEffect", nameof(Texts.Saturation), 0, 3, Tick = 0.1, Description = nameof(Texts.Saturation_Desc), ResourceType = typeof(Texts))]
        public double Saturation
        {
            get => CurrentWorld.PostEffect.Saturation;
            set { if (CurrentWorld.PostEffect.Saturation != value) { CurrentWorld.PostEffect.Saturation = value; OnPropertyChanged(); } }
        }

        [RangeSetting("PostEffect", nameof(Texts.Contrast), 0, 3, Tick = 0.1, Description = nameof(Texts.Contrast_Desc), ResourceType = typeof(Texts))]
        public double Contrast
        {
            get => CurrentWorld.PostEffect.Contrast;
            set { if (CurrentWorld.PostEffect.Contrast != value) { CurrentWorld.PostEffect.Contrast = value; OnPropertyChanged(); } }
        }

        [RangeSetting("PostEffect", nameof(Texts.Gamma), 0.1, 5, Tick = 0.1, Description = nameof(Texts.Gamma_Desc), ResourceType = typeof(Texts))]
        public double Gamma
        {
            get => CurrentWorld.PostEffect.Gamma;
            set { if (CurrentWorld.PostEffect.Gamma != value) { CurrentWorld.PostEffect.Gamma = value; OnPropertyChanged(); } }
        }

        [RangeSetting("PostEffect", nameof(Texts.BrightnessPost), -1, 1, Tick = 0.01, Description = nameof(Texts.BrightnessPost_Desc), ResourceType = typeof(Texts))]
        public double BrightnessPost
        {
            get => CurrentWorld.PostEffect.BrightnessPost;
            set { if (CurrentWorld.PostEffect.BrightnessPost != value) { CurrentWorld.PostEffect.BrightnessPost = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Vignette", nameof(Texts.Group_Vignette), Order = 8, ParentId = "PostEffect", Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z", ResourceType = typeof(Texts))]
        [BoolSetting("Vignette", nameof(Texts.VignetteEnabled), Description = nameof(Texts.VignetteEnabled_Desc), ResourceType = typeof(Texts))]
        public bool VignetteEnabled
        {
            get => CurrentWorld.Vignette.Enabled;
            set { if (CurrentWorld.Vignette.Enabled != value) { CurrentWorld.Vignette.Enabled = value; OnPropertyChanged(); } }
        }

        [ColorSetting("Vignette", nameof(Texts.VignetteColor), EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteColor_Desc), ResourceType = typeof(Texts))]
        public Color VignetteColor
        {
            get => CurrentWorld.Vignette.Color;
            set { if (CurrentWorld.Vignette.Color != value) { CurrentWorld.Vignette.Color = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Vignette", nameof(Texts.VignetteIntensity), 0, 2, Tick = 0.05, EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteIntensity_Desc), ResourceType = typeof(Texts))]
        public double VignetteIntensity
        {
            get => CurrentWorld.Vignette.Intensity;
            set { if (CurrentWorld.Vignette.Intensity != value) { CurrentWorld.Vignette.Intensity = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Vignette", nameof(Texts.VignetteRadius), 0, 2, Tick = 0.05, EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteRadius_Desc), ResourceType = typeof(Texts))]
        public double VignetteRadius
        {
            get => CurrentWorld.Vignette.Radius;
            set { if (CurrentWorld.Vignette.Radius != value) { CurrentWorld.Vignette.Radius = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Vignette", nameof(Texts.VignetteSoftness), 0.01, 1, Tick = 0.01, EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteSoftness_Desc), ResourceType = typeof(Texts))]
        public double VignetteSoftness
        {
            get => CurrentWorld.Vignette.Softness;
            set { if (CurrentWorld.Vignette.Softness != value) { CurrentWorld.Vignette.Softness = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Scanline", nameof(Texts.Group_Scanline), Order = 9, ParentId = "PostEffect", Icon = "M3,3H21V5H3V3M3,7H21V9H3V7M3,11H21V13H3V11M3,15H21V17H3V15M3,19H21V21H3V19Z", ResourceType = typeof(Texts))]
        [BoolSetting("Scanline", nameof(Texts.ScanlineEnabled), Description = nameof(Texts.ScanlineEnabled_Desc), ResourceType = typeof(Texts))]
        public bool ScanlineEnabled
        {
            get => CurrentWorld.Scanline.Enabled;
            set { if (CurrentWorld.Scanline.Enabled != value) { CurrentWorld.Scanline.Enabled = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Scanline", nameof(Texts.ScanlineIntensity), 0, 1, Tick = 0.01, EnableBy = nameof(ScanlineEnabled), Description = nameof(Texts.ScanlineIntensity_Desc), ResourceType = typeof(Texts))]
        public double ScanlineIntensity
        {
            get => CurrentWorld.Scanline.Intensity;
            set { if (CurrentWorld.Scanline.Intensity != value) { CurrentWorld.Scanline.Intensity = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Scanline", nameof(Texts.ScanlineFrequency), 1, 500, Tick = 1, EnableBy = nameof(ScanlineEnabled), Description = nameof(Texts.ScanlineFrequency_Desc), ResourceType = typeof(Texts))]
        public double ScanlineFrequency
        {
            get => CurrentWorld.Scanline.Frequency;
            set { if (CurrentWorld.Scanline.Frequency != value) { CurrentWorld.Scanline.Frequency = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Artistic", nameof(Texts.Group_Artistic), Order = 10, ParentId = "PostEffect", Icon = "M12,3C16.97,3 21,7.03 21,12C21,16.97 16.97,21 12,21C7.03,21 3,16.97 3,12C3,7.03 7.03,3 12,3M12,5C8.13,5 5,8.13 5,12C5,15.87 8.13,19 12,19C15.87,19 19,15.87 19,12C19,8.13 15.87,5 12,5Z", ResourceType = typeof(Texts))]
        [BoolSetting("Artistic", nameof(Texts.ChromAbEnabled), Description = nameof(Texts.ChromAbEnabled_Desc), ResourceType = typeof(Texts))]
        public bool ChromAbEnabled
        {
            get => CurrentWorld.Artistic.ChromAbEnabled;
            set { if (CurrentWorld.Artistic.ChromAbEnabled != value) { CurrentWorld.Artistic.ChromAbEnabled = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Artistic", nameof(Texts.ChromAbIntensity), 0, 0.1, Tick = 0.001, EnableBy = nameof(ChromAbEnabled), Description = nameof(Texts.ChromAbIntensity_Desc), ResourceType = typeof(Texts))]
        public double ChromAbIntensity
        {
            get => CurrentWorld.Artistic.ChromAbIntensity;
            set { if (CurrentWorld.Artistic.ChromAbIntensity != value) { CurrentWorld.Artistic.ChromAbIntensity = value; OnPropertyChanged(); } }
        }

        [BoolSetting("Artistic", nameof(Texts.MonochromeEnabled), Description = nameof(Texts.MonochromeEnabled_Desc), ResourceType = typeof(Texts))]
        public bool MonochromeEnabled
        {
            get => CurrentWorld.Artistic.MonochromeEnabled;
            set { if (CurrentWorld.Artistic.MonochromeEnabled != value) { CurrentWorld.Artistic.MonochromeEnabled = value; OnPropertyChanged(); } }
        }

        [ColorSetting("Artistic", nameof(Texts.MonochromeColor), EnableBy = nameof(MonochromeEnabled), Description = nameof(Texts.MonochromeColor_Desc), ResourceType = typeof(Texts))]
        public Color MonochromeColor
        {
            get => CurrentWorld.Artistic.MonochromeColor;
            set { if (CurrentWorld.Artistic.MonochromeColor != value) { CurrentWorld.Artistic.MonochromeColor = value; OnPropertyChanged(); } }
        }

        [RangeSetting("Artistic", nameof(Texts.MonochromeMix), 0, 1, Tick = 0.01, EnableBy = nameof(MonochromeEnabled), Description = nameof(Texts.MonochromeMix_Desc), ResourceType = typeof(Texts))]
        public double MonochromeMix
        {
            get => CurrentWorld.Artistic.MonochromeMix;
            set { if (CurrentWorld.Artistic.MonochromeMix != value) { CurrentWorld.Artistic.MonochromeMix = value; OnPropertyChanged(); } }
        }

        [BoolSetting("Artistic", nameof(Texts.PosterizeEnabled), Description = nameof(Texts.PosterizeEnabled_Desc), ResourceType = typeof(Texts))]
        public bool PosterizeEnabled
        {
            get => CurrentWorld.Artistic.PosterizeEnabled;
            set { if (CurrentWorld.Artistic.PosterizeEnabled != value) { CurrentWorld.Artistic.PosterizeEnabled = value; OnPropertyChanged(); } }
        }

        [IntSpinnerSetting("Artistic", nameof(Texts.PosterizeLevels), 2, 255, EnableBy = nameof(PosterizeEnabled), Description = nameof(Texts.PosterizeLevels_Desc), ResourceType = typeof(Texts))]
        public int PosterizeLevels
        {
            get => CurrentWorld.Artistic.PosterizeLevels;
            set { if (CurrentWorld.Artistic.PosterizeLevels != value) { CurrentWorld.Artistic.PosterizeLevels = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Assimp", nameof(Texts.Group_Assimp), Order = 11, Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z", ResourceType = typeof(Texts))]
        [BoolSetting("Assimp", nameof(Texts.Assimp_Obj), Description = nameof(Texts.Assimp_Obj_Desc), ResourceType = typeof(Texts))]
        public bool AssimpObj { get => _assimpObj; set => SetProperty(ref _assimpObj, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Glb), Description = nameof(Texts.Assimp_Glb_Desc), ResourceType = typeof(Texts))]
        public bool AssimpGlb { get => _assimpGlb; set => SetProperty(ref _assimpGlb, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Ply), Description = nameof(Texts.Assimp_Ply_Desc), ResourceType = typeof(Texts))]
        public bool AssimpPly { get => _assimpPly; set => SetProperty(ref _assimpPly, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Stl), Description = nameof(Texts.Assimp_Stl_Desc), ResourceType = typeof(Texts))]
        public bool AssimpStl { get => _assimpStl; set => SetProperty(ref _assimpStl, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_3mf), Description = nameof(Texts.Assimp_3mf_Desc), ResourceType = typeof(Texts))]
        public bool Assimp3mf { get => _assimp3mf; set => SetProperty(ref _assimp3mf, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Pmx), Description = nameof(Texts.Assimp_Pmx_Desc), ResourceType = typeof(Texts))]
        public bool AssimpPmx { get => _assimpPmx; set => SetProperty(ref _assimpPmx, value); }

        [SettingButton(nameof(Texts.ResetDefaults), Placement = SettingButtonPlacement.BottomLeft, Order = 0, ResourceType = typeof(Texts))]
        public void ResetDefaults()
        {
            CoordinateSystem = CoordinateSystem.RightHandedYUp;
            CullMode = RenderCullMode.None;
            RenderQuality = RenderQuality.Standard;
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
            OnPropertyChanged(nameof(AmbientColor));
            OnPropertyChanged(nameof(DiffuseIntensity));
            OnPropertyChanged(nameof(SpecularIntensity));
            OnPropertyChanged(nameof(Shininess));
            OnPropertyChanged(nameof(LightColor));
            OnPropertyChanged(nameof(ToonEnabled));
            OnPropertyChanged(nameof(ToonSteps));
            OnPropertyChanged(nameof(ToonSmoothness));
            OnPropertyChanged(nameof(RimEnabled));
            OnPropertyChanged(nameof(RimColor));
            OnPropertyChanged(nameof(RimIntensity));
            OnPropertyChanged(nameof(RimPower));
            OnPropertyChanged(nameof(OutlineEnabled));
            OnPropertyChanged(nameof(OutlineColor));
            OnPropertyChanged(nameof(OutlineWidth));
            OnPropertyChanged(nameof(OutlinePower));
            OnPropertyChanged(nameof(FogEnabled));
            OnPropertyChanged(nameof(FogColor));
            OnPropertyChanged(nameof(FogStart));
            OnPropertyChanged(nameof(FogEnd));
            OnPropertyChanged(nameof(FogDensity));
            OnPropertyChanged(nameof(Saturation));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(Gamma));
            OnPropertyChanged(nameof(BrightnessPost));
            OnPropertyChanged(nameof(VignetteEnabled));
            OnPropertyChanged(nameof(VignetteColor));
            OnPropertyChanged(nameof(VignetteIntensity));
            OnPropertyChanged(nameof(VignetteRadius));
            OnPropertyChanged(nameof(VignetteSoftness));
            OnPropertyChanged(nameof(ScanlineEnabled));
            OnPropertyChanged(nameof(ScanlineIntensity));
            OnPropertyChanged(nameof(ScanlineFrequency));
            OnPropertyChanged(nameof(ChromAbEnabled));
            OnPropertyChanged(nameof(ChromAbIntensity));
            OnPropertyChanged(nameof(MonochromeEnabled));
            OnPropertyChanged(nameof(MonochromeColor));
            OnPropertyChanged(nameof(MonochromeMix));
            OnPropertyChanged(nameof(PosterizeEnabled));
            OnPropertyChanged(nameof(PosterizeLevels));
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

    public class WorldParameter : ICloneable
    {
        public LightingSettings Lighting { get; set; } = new();
        public ToonSettings Toon { get; set; } = new();
        public RimSettings Rim { get; set; } = new();
        public OutlineSettings Outline { get; set; } = new();
        public FogSettings Fog { get; set; } = new();
        public PostEffectSettings PostEffect { get; set; } = new();
        public VignetteSettings Vignette { get; set; } = new();
        public ScanlineSettings Scanline { get; set; } = new();
        public ArtisticSettings Artistic { get; set; } = new();

        public object Clone()
        {
            var clone = (WorldParameter)MemberwiseClone();
            clone.Lighting = (LightingSettings)Lighting.Clone();
            clone.Toon = (ToonSettings)Toon.Clone();
            clone.Rim = (RimSettings)Rim.Clone();
            clone.Outline = (OutlineSettings)Outline.Clone();
            clone.Fog = (FogSettings)Fog.Clone();
            clone.PostEffect = (PostEffectSettings)PostEffect.Clone();
            clone.Vignette = (VignetteSettings)Vignette.Clone();
            clone.Scanline = (ScanlineSettings)Scanline.Clone();
            clone.Artistic = (ArtisticSettings)Artistic.Clone();
            return clone;
        }
    }

    public class LightingSettings : ICloneable
    {
        public Color AmbientColor { get; set; } = Color.FromRgb(50, 50, 50);
        public Color LightColor { get; set; } = Colors.White;
        public double DiffuseIntensity { get; set; } = 1.0;
        public double SpecularIntensity { get; set; } = 0.5;
        public double Shininess { get; set; } = 20.0;
        public object Clone() => MemberwiseClone();
    }

    public class ToonSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public int Steps { get; set; } = 4;
        public double Smoothness { get; set; } = 0.05;
        public object Clone() => MemberwiseClone();
    }

    public class RimSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public Color Color { get; set; } = Colors.White;
        public double Intensity { get; set; } = 1.0;
        public double Power { get; set; } = 3.0;
        public object Clone() => MemberwiseClone();
    }

    public class OutlineSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public Color Color { get; set; } = Colors.Black;
        public double Width { get; set; } = 1.0;
        public double Power { get; set; } = 2.0;
        public object Clone() => MemberwiseClone();
    }

    public class FogSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public Color Color { get; set; } = Colors.Gray;
        public double Start { get; set; } = 10.0;
        public double End { get; set; } = 100.0;
        public double Density { get; set; } = 1.0;
        public object Clone() => MemberwiseClone();
    }

    public class PostEffectSettings : ICloneable
    {
        public double Saturation { get; set; } = 1.0;
        public double Contrast { get; set; } = 1.0;
        public double Gamma { get; set; } = 1.0;
        public double BrightnessPost { get; set; } = 0.0;
        public object Clone() => MemberwiseClone();
    }

    public class VignetteSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public Color Color { get; set; } = Colors.Black;
        public double Intensity { get; set; } = 0.5;
        public double Radius { get; set; } = 0.8;
        public double Softness { get; set; } = 0.3;
        public object Clone() => MemberwiseClone();
    }

    public class ScanlineSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public double Intensity { get; set; } = 0.2;
        public double Frequency { get; set; } = 100.0;
        public object Clone() => MemberwiseClone();
    }

    public class ArtisticSettings : ICloneable
    {
        public bool ChromAbEnabled { get; set; } = false;
        public double ChromAbIntensity { get; set; } = 0.005;
        public bool MonochromeEnabled { get; set; } = false;
        public Color MonochromeColor { get; set; } = Colors.White;
        public double MonochromeMix { get; set; } = 1.0;
        public bool PosterizeEnabled { get; set; } = false;
        public int PosterizeLevels { get; set; } = 8;
        public object Clone() => MemberwiseClone();
    }
}
using ObjLoader.Localization;
using ObjLoader.Infrastructure;
using System.Windows.Media;

namespace ObjLoader.Settings
{
    public partial class PluginSettings
    {
        private int _worldId = 0;

        [SettingGroup("Lighting", nameof(Texts.Group_Lighting), Order = 2, Icon = "M12,2A7,7 0 0,0 5,9C5,11.38 6.19,13.47 8,14.74V17A1,1 0 0,0 9,18H15A1,1 0 0,0 16,17V14.74C17.81,13.47 19,11.38 19,9A7,7 0 0,0 12,2M9,21A1,1 0 0,0 10,22H14A1,1 0 0,0 15,21V20H9V21Z", ResourceType = typeof(Texts))]
        [IntSpinnerSetting("Lighting", nameof(Texts.WorldId), 0, 19, IsGroupHeader = true, Description = nameof(Texts.WorldId_Desc), ResourceType = typeof(Texts))]
        public int WorldId
        {
            get => _worldId;
            set
            {
                if (SetProperty(ref _worldId, value))
                {
                    OnPropertyChanged(nameof(PostEffectWorldId));
                    NotifyWorldPropertiesChanged();
                }
            }
        }

        [BoolSetting("Lighting", nameof(Texts.ShadowMode), Description = nameof(Texts.ShadowMode_Desc), ResourceType = typeof(Texts))]
        public bool ShadowEnabled
        {
            get => CurrentWorld.Lighting.ShadowEnabled;
            set { if (CurrentWorld.Lighting.ShadowEnabled != value) { CurrentWorld.Lighting.ShadowEnabled = value; OnPropertyChanged(); } }
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

        [SettingGroup("PBR", nameof(Texts.Group_PBR), Order = 3, ParentId = "Lighting", Icon = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20Z", ResourceType = typeof(Texts))]
        [RangeSetting("PBR", nameof(Texts.Metallic), 0, 1, Tick = 0.01, Description = nameof(Texts.Metallic_Desc), ResourceType = typeof(Texts))]
        public double Metallic
        {
            get => CurrentWorld.PBR.Metallic;
            set { if (CurrentWorld.PBR.Metallic != value) { CurrentWorld.PBR.Metallic = value; OnPropertyChanged(); } }
        }

        [RangeSetting("PBR", nameof(Texts.Roughness), 0, 1, Tick = 0.01, Description = nameof(Texts.Roughness_Desc), ResourceType = typeof(Texts))]
        public double Roughness
        {
            get => CurrentWorld.PBR.Roughness;
            set { if (CurrentWorld.PBR.Roughness != value) { CurrentWorld.PBR.Roughness = value; OnPropertyChanged(); } }
        }

        [RangeSetting("PBR", nameof(Texts.IBLIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.IBLIntensity_Desc), ResourceType = typeof(Texts))]
        public double IBLIntensity
        {
            get => CurrentWorld.PBR.IBLIntensity;
            set { if (CurrentWorld.PBR.IBLIntensity != value) { CurrentWorld.PBR.IBLIntensity = value; OnPropertyChanged(); } }
        }

        [SettingGroup("SSR", nameof(Texts.Group_SSR), Order = 4, ParentId = "Lighting", Icon = "M2,2H22V22H2V2M4,4V20H20V4H4M11,7H13V9H15V11H13V13H11V11H9V9H11V7Z", ResourceType = typeof(Texts))]
        [BoolSetting("SSR", nameof(Texts.SSREnabled), Description = nameof(Texts.SSREnabled_Desc), ResourceType = typeof(Texts))]
        public bool SSREnabled
        {
            get => CurrentWorld.SSR.Enabled;
            set { if (CurrentWorld.SSR.Enabled != value) { CurrentWorld.SSR.Enabled = value; OnPropertyChanged(); } }
        }

        [RangeSetting("SSR", nameof(Texts.SSRStep), 0.1, 5.0, Tick = 0.1, EnableBy = nameof(SSREnabled), Description = nameof(Texts.SSRStep_Desc), ResourceType = typeof(Texts))]
        public double SSRStep
        {
            get => CurrentWorld.SSR.Step;
            set { if (CurrentWorld.SSR.Step != value) { CurrentWorld.SSR.Step = value; OnPropertyChanged(); } }
        }

        [RangeSetting("SSR", nameof(Texts.SSRMaxDist), 1.0, 50.0, Tick = 1.0, EnableBy = nameof(SSREnabled), Description = nameof(Texts.SSRMaxDist_Desc), ResourceType = typeof(Texts))]
        public double SSRMaxDist
        {
            get => CurrentWorld.SSR.MaxDist;
            set { if (CurrentWorld.SSR.MaxDist != value) { CurrentWorld.SSR.MaxDist = value; OnPropertyChanged(); } }
        }

        [SettingGroup("PCSS", nameof(Texts.Group_PCSS), Order = 5, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z", ResourceType = typeof(Texts))]
        [RangeSetting("PCSS", nameof(Texts.PcssLightSize), 0.0, 5.0, Tick = 0.01, Description = nameof(Texts.PcssLightSize_Desc), ResourceType = typeof(Texts))]
        public double PcssLightSize
        {
            get => CurrentWorld.PCSS.LightSize;
            set { if (CurrentWorld.PCSS.LightSize != value) { CurrentWorld.PCSS.LightSize = value; OnPropertyChanged(); } }
        }

        [IntSpinnerSetting("PCSS", nameof(Texts.PcssQuality), 4, 64, Description = nameof(Texts.PcssQuality_Desc), ResourceType = typeof(Texts))]
        public int PcssQuality
        {
            get => CurrentWorld.PCSS.Quality;
            set { if (CurrentWorld.PCSS.Quality != value) { CurrentWorld.PCSS.Quality = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Environment", nameof(Texts.Group_Environment), Order = 6, ParentId = "Lighting", Icon = "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,5.29C7.24,5.84 7.09,6.44 7.09,7.09C7.09,7.74 7.24,8.34 7.5,8.89L3.34,7.18V7M3.34,17L7.5,18.71C7.24,18.16 7.09,17.56 7.09,16.91C7.09,16.26 7.24,15.66 7.5,15.11L3.34,16.82V17M20.66,17L16.5,15.29C16.76,15.84 16.91,16.44 16.91,17.09C16.91,17.74 16.76,18.34 16.5,18.89L20.66,17.18V17M20.66,7L16.5,8.71C16.76,8.16 16.91,7.56 16.91,6.91C16.91,6.26 16.76,5.66 16.5,5.11L20.66,6.82V7M12,22L9.61,18.58C10.35,18.85 11.16,19 12,19C12.84,19 13.65,18.85 14.39,18.58L12,22Z", ResourceType = typeof(Texts))]
        [ColorSetting("Environment", nameof(Texts.LightColor), Description = nameof(Texts.LightColor_Desc), ResourceType = typeof(Texts))]
        public Color LightColor
        {
            get => CurrentWorld.Lighting.LightColor;
            set { if (CurrentWorld.Lighting.LightColor != value) { CurrentWorld.Lighting.LightColor = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Toon", nameof(Texts.Group_Toon), Order = 7, ParentId = "Lighting", Icon = "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M11,7H13V9H15V11H13V13H11V11H9V9H11V7Z", ResourceType = typeof(Texts))]
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

        [SettingGroup("Rim", nameof(Texts.Group_Rim), Order = 8, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4Z", ResourceType = typeof(Texts))]
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

        [SettingGroup("Outline", nameof(Texts.Group_Outline), Order = 9, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M12,6A6,6 0 0,1 18,12A6,6 0 0,1 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6Z", ResourceType = typeof(Texts))]
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

        [SettingGroup("Fog", nameof(Texts.Group_Fog), Order = 10, ParentId = "Lighting", Icon = "M3,4H21V8H3V4M3,10H21V14H3V10M3,16H21V20H3V16Z", ResourceType = typeof(Texts))]
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
    }
}
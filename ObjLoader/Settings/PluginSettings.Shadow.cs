using ObjLoader.Infrastructure;
using ObjLoader.Localization;

namespace ObjLoader.Settings
{
    public partial class PluginSettings
    {
        private bool _shadowMappingEnabled = true;
        private int _shadowResolution = 2048;
        private double _shadowBias = 0.001;
        private double _shadowStrength = 0.5;
        private double _sunLightShadowRange = 100.0;
        private bool _cascadedShadowsEnabled = false;

        [SettingGroup("Shadow", nameof(Texts.Group_Shadow), Order = 1, Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M15,14L10.5,18.5L9,17L13.5,12.5L15,14Z", ResourceType = typeof(Texts))]
        [BoolSetting("Shadow", nameof(Texts.Shadow_Enabled), Description = nameof(Texts.Shadow_Enabled_Desc), ResourceType = typeof(Texts))]
        public bool ShadowMappingEnabled
        {
            get => _shadowMappingEnabled;
            set => SetProperty(ref _shadowMappingEnabled, value);
        }

        [BoolSetting("Shadow", nameof(Texts.CascadedShadows), Description = nameof(Texts.CascadedShadows_Desc), ResourceType = typeof(Texts))]
        public bool CascadedShadowsEnabled
        {
            get => _cascadedShadowsEnabled;
            set => SetProperty(ref _cascadedShadowsEnabled, value);
        }

        [RangeSetting("Shadow", nameof(Texts.Shadow_Resolution), 512, 8192, Tick = 128, EnableBy = nameof(ShadowMappingEnabled), Description = nameof(Texts.Shadow_Resolution_Desc), ResourceType = typeof(Texts))]
        public int ShadowResolution
        {
            get => _shadowResolution;
            set => SetProperty(ref _shadowResolution, value);
        }

        [RangeSetting("Shadow", nameof(Texts.Shadow_Bias), 0.0, 0.1, Tick = 0.0001, EnableBy = nameof(ShadowMappingEnabled), Description = nameof(Texts.Shadow_Bias_Desc), ResourceType = typeof(Texts))]
        public double ShadowBias
        {
            get => _shadowBias;
            set => SetProperty(ref _shadowBias, value);
        }

        [RangeSetting("Shadow", nameof(Texts.Shadow_Strength), 0.0, 1.0, Tick = 0.01, EnableBy = nameof(ShadowMappingEnabled), Description = nameof(Texts.Shadow_Strength_Desc), ResourceType = typeof(Texts))]
        public double ShadowStrength
        {
            get => _shadowStrength;
            set => SetProperty(ref _shadowStrength, value);
        }

        [RangeSetting("Shadow", nameof(Texts.SunLight_ShadowRange), 1.0, 10000.0, Tick = 10.0, EnableBy = nameof(ShadowMappingEnabled), Description = nameof(Texts.SunLight_ShadowRange_Desc), ResourceType = typeof(Texts))]
        public double SunLightShadowRange
        {
            get => _sunLightShadowRange;
            set => SetProperty(ref _sunLightShadowRange, value);
        }
    }
}
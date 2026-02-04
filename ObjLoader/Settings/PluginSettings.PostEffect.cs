using ObjLoader.Localization;
using ObjLoader.Infrastructure;
using System.Windows.Media;

namespace ObjLoader.Settings
{
    public partial class PluginSettings
    {
        [SettingGroup("PostEffect", nameof(Texts.Group_PostEffect), Order = 11, Icon = "M2,2V22H22V2H2M20,20H4V4H20V20M8,6H16V14H8V6M10,8V12H14V8H10Z", ResourceType = typeof(Texts))]
        [IntSpinnerSetting("PostEffect", nameof(Texts.WorldId), 0, 19, IsGroupHeader = true, Description = nameof(Texts.WorldId_Desc), ResourceType = typeof(Texts))]
        public int PostEffectWorldId
        {
            get => WorldId;
            set => WorldId = value;
        }

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

        [SettingGroup("Vignette", nameof(Texts.Group_Vignette), Order = 12, ParentId = "PostEffect", Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z", ResourceType = typeof(Texts))]
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

        [SettingGroup("Scanline", nameof(Texts.Group_Scanline), Order = 13, ParentId = "PostEffect", Icon = "M3,3H21V5H3V3M3,7H21V9H3V7M3,11H21V13H3V11M3,15H21V17H3V15M3,19H21V21H3V19Z", ResourceType = typeof(Texts))]
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

        [BoolSetting("Scanline", nameof(Texts.ScanlinePost), EnableBy = nameof(ScanlineEnabled), Description = nameof(Texts.ScanlinePost_Desc), ResourceType = typeof(Texts))]
        public bool ScanlinePost
        {
            get => CurrentWorld.Scanline.ApplyAfterTonemap;
            set { if (CurrentWorld.Scanline.ApplyAfterTonemap != value) { CurrentWorld.Scanline.ApplyAfterTonemap = value; OnPropertyChanged(); } }
        }

        [SettingGroup("Artistic", nameof(Texts.Group_Artistic), Order = 14, ParentId = "PostEffect", Icon = "M12,3C16.97,3 21,7.03 21,12C21,16.97 16.97,21 12,21C7.03,21 3,16.97 3,12C3,7.03 7.03,3 12,3M12,5C8.13,5 5,8.13 5,12C5,15.87 8.13,19 12,19C15.87,19 19,15.87 19,12C19,8.13 15.87,5 12,5Z", ResourceType = typeof(Texts))]
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
    }
}
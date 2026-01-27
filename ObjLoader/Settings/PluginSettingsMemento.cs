using System.Windows.Media;

namespace ObjLoader.Settings
{
    public class PluginSettingsMemento
    {
        public CoordinateSystem CoordinateSystem { get; set; }
        public RenderCullMode CullMode { get; set; }
        public RenderQuality RenderQuality { get; set; }
        public bool ShadowMappingEnabled { get; set; }
        public bool CascadedShadowsEnabled { get; set; }
        public int ShadowResolution { get; set; }
        public double ShadowBias { get; set; }
        public double ShadowStrength { get; set; }
        public double SunLightShadowRange { get; set; }

        public bool AssimpObj { get; set; }
        public bool AssimpGlb { get; set; }
        public bool AssimpPly { get; set; }
        public bool AssimpStl { get; set; }
        public bool Assimp3mf { get; set; }
        public bool AssimpPmx { get; set; }

        public int WorldId { get; set; }
        public List<WorldParameter>? WorldParameters { get; set; }

        public List<Color>? AmbientColors { get; set; }
        public List<Color>? LightColors { get; set; }
        public List<double>? DiffuseIntensities { get; set; }
        public List<double>? SpecularIntensities { get; set; }
        public List<double>? Shininesses { get; set; }

        public List<bool>? ToonEnabled { get; set; }
        public List<int>? ToonSteps { get; set; }
        public List<double>? ToonSmoothness { get; set; }

        public List<bool>? RimEnabled { get; set; }
        public List<Color>? RimColor { get; set; }
        public List<double>? RimIntensity { get; set; }
        public List<double>? RimPower { get; set; }

        public List<bool>? OutlineEnabled { get; set; }
        public List<Color>? OutlineColor { get; set; }
        public List<double>? OutlineWidth { get; set; }
        public List<double>? OutlinePower { get; set; }

        public List<bool>? FogEnabled { get; set; }
        public List<Color>? FogColor { get; set; }
        public List<double>? FogStart { get; set; }
        public List<double>? FogEnd { get; set; }
        public List<double>? FogDensity { get; set; }

        public List<double>? Saturation { get; set; }
        public List<double>? Contrast { get; set; }
        public List<double>? Gamma { get; set; }
        public List<double>? BrightnessPost { get; set; }

        public List<bool>? VignetteEnabled { get; set; }
        public List<Color>? VignetteColor { get; set; }
        public List<double>? VignetteIntensity { get; set; }
        public List<double>? VignetteRadius { get; set; }
        public List<double>? VignetteSoftness { get; set; }

        public List<bool>? ScanlineEnabled { get; set; }
        public List<double>? ScanlineIntensity { get; set; }
        public List<double>? ScanlineFrequency { get; set; }

        public List<bool>? ChromAbEnabled { get; set; }
        public List<double>? ChromAbIntensity { get; set; }
        public List<bool>? MonochromeEnabled { get; set; }
        public List<Color>? MonochromeColor { get; set; }
        public List<double>? MonochromeMix { get; set; }
        public List<bool>? PosterizeEnabled { get; set; }
        public List<int>? PosterizeLevels { get; set; }
    }
}
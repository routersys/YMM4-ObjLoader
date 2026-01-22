using System.Windows.Media;

namespace ObjLoader.Settings
{
    public class PluginSettingsMemento
    {
        public CoordinateSystem CoordinateSystem;
        public RenderCullMode CullMode;
        public RenderQuality RenderQuality;
        public bool AssimpObj;
        public bool AssimpGlb;
        public bool AssimpPly;
        public bool AssimpStl;
        public bool Assimp3mf;
        public bool AssimpPmx;
        public int WorldId;

        public List<WorldParameter> WorldParameters = new();

        public List<Color>? AmbientColors;
        public List<Color>? LightColors;
        public List<double>? DiffuseIntensities;
        public List<double>? SpecularIntensities;
        public List<double>? Shininesses;
        public List<bool>? ToonEnabled;
        public List<int>? ToonSteps;
        public List<double>? ToonSmoothness;
        public List<bool>? OutlineEnabled;
        public List<Color>? OutlineColor;
        public List<double>? OutlineWidth;
        public List<double>? OutlinePower;
        public List<bool>? RimEnabled;
        public List<Color>? RimColor;
        public List<double>? RimIntensity;
        public List<double>? RimPower;
        public List<bool>? FogEnabled;
        public List<Color>? FogColor;
        public List<double>? FogStart;
        public List<double>? FogEnd;
        public List<double>? FogDensity;
        public List<double>? Saturation;
        public List<double>? Contrast;
        public List<double>? Gamma;
        public List<double>? BrightnessPost;
        public List<bool>? VignetteEnabled;
        public List<Color>? VignetteColor;
        public List<double>? VignetteIntensity;
        public List<double>? VignetteRadius;
        public List<double>? VignetteSoftness;
        public List<bool>? ChromAbEnabled;
        public List<double>? ChromAbIntensity;
        public List<bool>? ScanlineEnabled;
        public List<double>? ScanlineIntensity;
        public List<double>? ScanlineFrequency;
        public List<bool>? MonochromeEnabled;
        public List<Color>? MonochromeColor;
        public List<double>? MonochromeMix;
        public List<bool>? PosterizeEnabled;
        public List<int>? PosterizeLevels;
    }
}
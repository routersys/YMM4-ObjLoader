using System.Windows.Media;

namespace ObjLoader.Settings
{
    public class PluginSettingsMemento
    {
        public CoordinateSystem CoordinateSystem;
        public RenderCullMode CullMode;
        public bool AssimpObj;
        public bool AssimpGlb;
        public bool AssimpPly;
        public bool AssimpStl;
        public bool Assimp3mf;
        public bool AssimpPmx;
        public int WorldId;
        public List<Color> AmbientColors = new();
        public List<Color> LightColors = new();
        public List<double> DiffuseIntensities = new();
        public List<double> SpecularIntensities = new();
        public List<double> Shininesses = new();
        public List<bool> ToonEnabled = new();
        public List<int> ToonSteps = new();
        public List<double> ToonSmoothness = new();
        public List<bool> OutlineEnabled = new();
        public List<Color> OutlineColor = new();
        public List<double> OutlineWidth = new();
        public List<double> OutlinePower = new();
        public List<bool> RimEnabled = new();
        public List<Color> RimColor = new();
        public List<double> RimIntensity = new();
        public List<double> RimPower = new();
        public List<bool> FogEnabled = new();
        public List<Color> FogColor = new();
        public List<double> FogStart = new();
        public List<double> FogEnd = new();
        public List<double> FogDensity = new();
        public List<double> Saturation = new();
        public List<double> Contrast = new();
        public List<double> Gamma = new();
        public List<double> BrightnessPost = new();
        public List<bool> VignetteEnabled = new();
        public List<Color> VignetteColor = new();
        public List<double> VignetteIntensity = new();
        public List<double> VignetteRadius = new();
        public List<double> VignetteSoftness = new();
        public List<bool> ChromAbEnabled = new();
        public List<double> ChromAbIntensity = new();
        public List<bool> ScanlineEnabled = new();
        public List<double> ScanlineIntensity = new();
        public List<double> ScanlineFrequency = new();
        public List<bool> MonochromeEnabled = new();
        public List<Color> MonochromeColor = new();
        public List<double> MonochromeMix = new();
        public List<bool> PosterizeEnabled = new();
        public List<int> PosterizeLevels = new();
    }
}
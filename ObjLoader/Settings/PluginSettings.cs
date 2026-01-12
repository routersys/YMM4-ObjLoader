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

        private const int MaxWorlds = 10;

        private CoordinateSystem _coordinateSystem = CoordinateSystem.RightHandedYUp;
        private RenderCullMode _cullMode = RenderCullMode.None;

        private bool _assimpObj = false;
        private bool _assimpGlb = false;
        private bool _assimpPly = false;
        private bool _assimpStl = false;
        private bool _assimp3mf = false;
        private bool _assimpPmx = false;

        private int _worldId = 0;

        private List<Color> _ambientColors = Enumerable.Repeat(Color.FromRgb(50, 50, 50), MaxWorlds).ToList();
        private List<Color> _lightColors = Enumerable.Repeat(Colors.White, MaxWorlds).ToList();
        private List<double> _diffuseIntensities = Enumerable.Repeat(1.0, MaxWorlds).ToList();
        private List<double> _specularIntensities = Enumerable.Repeat(0.5, MaxWorlds).ToList();
        private List<double> _shininesses = Enumerable.Repeat(20.0, MaxWorlds).ToList();

        private List<bool> _toonEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<int> _toonSteps = Enumerable.Repeat(4, MaxWorlds).ToList();
        private List<double> _toonSmoothness = Enumerable.Repeat(0.05, MaxWorlds).ToList();

        private List<bool> _outlineEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<Color> _outlineColor = Enumerable.Repeat(Colors.Black, MaxWorlds).ToList();
        private List<double> _outlineWidth = Enumerable.Repeat(1.0, MaxWorlds).ToList();
        private List<double> _outlinePower = Enumerable.Repeat(2.0, MaxWorlds).ToList();

        private List<bool> _rimEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<Color> _rimColor = Enumerable.Repeat(Colors.White, MaxWorlds).ToList();
        private List<double> _rimIntensity = Enumerable.Repeat(1.0, MaxWorlds).ToList();
        private List<double> _rimPower = Enumerable.Repeat(3.0, MaxWorlds).ToList();

        private List<bool> _fogEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<Color> _fogColor = Enumerable.Repeat(Colors.Gray, MaxWorlds).ToList();
        private List<double> _fogStart = Enumerable.Repeat(10.0, MaxWorlds).ToList();
        private List<double> _fogEnd = Enumerable.Repeat(100.0, MaxWorlds).ToList();
        private List<double> _fogDensity = Enumerable.Repeat(1.0, MaxWorlds).ToList();

        private List<double> _saturation = Enumerable.Repeat(1.0, MaxWorlds).ToList();
        private List<double> _contrast = Enumerable.Repeat(1.0, MaxWorlds).ToList();
        private List<double> _gamma = Enumerable.Repeat(1.0, MaxWorlds).ToList();
        private List<double> _brightnessPost = Enumerable.Repeat(0.0, MaxWorlds).ToList();

        private List<bool> _vignetteEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<Color> _vignetteColor = Enumerable.Repeat(Colors.Black, MaxWorlds).ToList();
        private List<double> _vignetteIntensity = Enumerable.Repeat(0.5, MaxWorlds).ToList();
        private List<double> _vignetteRadius = Enumerable.Repeat(0.8, MaxWorlds).ToList();
        private List<double> _vignetteSoftness = Enumerable.Repeat(0.3, MaxWorlds).ToList();

        private List<bool> _chromAbEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<double> _chromAbIntensity = Enumerable.Repeat(0.005, MaxWorlds).ToList();

        private List<bool> _scanlineEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<double> _scanlineIntensity = Enumerable.Repeat(0.2, MaxWorlds).ToList();
        private List<double> _scanlineFrequency = Enumerable.Repeat(100.0, MaxWorlds).ToList();

        private List<bool> _monochromeEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<Color> _monochromeColor = Enumerable.Repeat(Colors.White, MaxWorlds).ToList();
        private List<double> _monochromeMix = Enumerable.Repeat(1.0, MaxWorlds).ToList();

        private List<bool> _posterizeEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
        private List<int> _posterizeLevels = Enumerable.Repeat(8, MaxWorlds).ToList();

        public List<string> AmbientColorsHex
        {
            get => _ambientColors.Select(c => c.ToString()).ToList();
            set => _ambientColors = ConvertHexListToColors(value, Color.FromRgb(50, 50, 50));
        }
        public List<string> LightColorsHex
        {
            get => _lightColors.Select(c => c.ToString()).ToList();
            set => _lightColors = ConvertHexListToColors(value, Colors.White);
        }
        public List<double> DiffuseIntensities { get => _diffuseIntensities; set { _diffuseIntensities = value; EnsureCount(ref _diffuseIntensities, 1.0); } }
        public List<double> SpecularIntensities { get => _specularIntensities; set { _specularIntensities = value; EnsureCount(ref _specularIntensities, 0.5); } }
        public List<double> Shininesses { get => _shininesses; set { _shininesses = value; EnsureCount(ref _shininesses, 20.0); } }

        public List<bool> ToonEnabledList { get => _toonEnabled; set { _toonEnabled = value; EnsureCount(ref _toonEnabled, false); } }
        public List<int> ToonStepsList { get => _toonSteps; set { _toonSteps = value; EnsureCount(ref _toonSteps, 4); } }
        public List<double> ToonSmoothnessList { get => _toonSmoothness; set { _toonSmoothness = value; EnsureCount(ref _toonSmoothness, 0.05); } }

        public List<bool> OutlineEnabledList { get => _outlineEnabled; set { _outlineEnabled = value; EnsureCount(ref _outlineEnabled, false); } }
        public List<string> OutlineColorHex
        {
            get => _outlineColor.Select(c => c.ToString()).ToList();
            set => _outlineColor = ConvertHexListToColors(value, Colors.Black);
        }
        public List<double> OutlineWidthList { get => _outlineWidth; set { _outlineWidth = value; EnsureCount(ref _outlineWidth, 1.0); } }
        public List<double> OutlinePowerList { get => _outlinePower; set { _outlinePower = value; EnsureCount(ref _outlinePower, 2.0); } }

        public List<bool> RimEnabledList { get => _rimEnabled; set { _rimEnabled = value; EnsureCount(ref _rimEnabled, false); } }
        public List<string> RimColorHex
        {
            get => _rimColor.Select(c => c.ToString()).ToList();
            set => _rimColor = ConvertHexListToColors(value, Colors.White);
        }
        public List<double> RimIntensityList { get => _rimIntensity; set { _rimIntensity = value; EnsureCount(ref _rimIntensity, 1.0); } }
        public List<double> RimPowerList { get => _rimPower; set { _rimPower = value; EnsureCount(ref _rimPower, 3.0); } }

        public List<bool> FogEnabledList { get => _fogEnabled; set { _fogEnabled = value; EnsureCount(ref _fogEnabled, false); } }
        public List<string> FogColorHex
        {
            get => _fogColor.Select(c => c.ToString()).ToList();
            set => _fogColor = ConvertHexListToColors(value, Colors.Gray);
        }
        public List<double> FogStartList { get => _fogStart; set { _fogStart = value; EnsureCount(ref _fogStart, 10.0); } }
        public List<double> FogEndList { get => _fogEnd; set { _fogEnd = value; EnsureCount(ref _fogEnd, 100.0); } }
        public List<double> FogDensityList { get => _fogDensity; set { _fogDensity = value; EnsureCount(ref _fogDensity, 1.0); } }

        public List<double> SaturationList { get => _saturation; set { _saturation = value; EnsureCount(ref _saturation, 1.0); } }
        public List<double> ContrastList { get => _contrast; set { _contrast = value; EnsureCount(ref _contrast, 1.0); } }
        public List<double> GammaList { get => _gamma; set { _gamma = value; EnsureCount(ref _gamma, 1.0); } }
        public List<double> BrightnessPostList { get => _brightnessPost; set { _brightnessPost = value; EnsureCount(ref _brightnessPost, 0.0); } }

        public List<bool> VignetteEnabledList { get => _vignetteEnabled; set { _vignetteEnabled = value; EnsureCount(ref _vignetteEnabled, false); } }
        public List<string> VignetteColorHex
        {
            get => _vignetteColor.Select(c => c.ToString()).ToList();
            set => _vignetteColor = ConvertHexListToColors(value, Colors.Black);
        }
        public List<double> VignetteIntensityList { get => _vignetteIntensity; set { _vignetteIntensity = value; EnsureCount(ref _vignetteIntensity, 0.5); } }
        public List<double> VignetteRadiusList { get => _vignetteRadius; set { _vignetteRadius = value; EnsureCount(ref _vignetteRadius, 0.8); } }
        public List<double> VignetteSoftnessList { get => _vignetteSoftness; set { _vignetteSoftness = value; EnsureCount(ref _vignetteSoftness, 0.3); } }

        public List<bool> ChromAbEnabledList { get => _chromAbEnabled; set { _chromAbEnabled = value; EnsureCount(ref _chromAbEnabled, false); } }
        public List<double> ChromAbIntensityList { get => _chromAbIntensity; set { _chromAbIntensity = value; EnsureCount(ref _chromAbIntensity, 0.005); } }

        public List<bool> ScanlineEnabledList { get => _scanlineEnabled; set { _scanlineEnabled = value; EnsureCount(ref _scanlineEnabled, false); } }
        public List<double> ScanlineIntensityList { get => _scanlineIntensity; set { _scanlineIntensity = value; EnsureCount(ref _scanlineIntensity, 0.2); } }
        public List<double> ScanlineFrequencyList { get => _scanlineFrequency; set { _scanlineFrequency = value; EnsureCount(ref _scanlineFrequency, 100.0); } }

        public List<bool> MonochromeEnabledList { get => _monochromeEnabled; set { _monochromeEnabled = value; EnsureCount(ref _monochromeEnabled, false); } }
        public List<string> MonochromeColorHex
        {
            get => _monochromeColor.Select(c => c.ToString()).ToList();
            set => _monochromeColor = ConvertHexListToColors(value, Colors.White);
        }
        public List<double> MonochromeMixList { get => _monochromeMix; set { _monochromeMix = value; EnsureCount(ref _monochromeMix, 1.0); } }

        public List<bool> PosterizeEnabledList { get => _posterizeEnabled; set { _posterizeEnabled = value; EnsureCount(ref _posterizeEnabled, false); } }
        public List<int> PosterizeLevelsList { get => _posterizeLevels; set { _posterizeLevels = value; EnsureCount(ref _posterizeLevels, 8); } }

        private List<Color> ConvertHexListToColors(List<string>? hexList, Color defaultValue)
        {
            var list = new List<Color>();
            if (hexList != null)
            {
                foreach (var s in hexList)
                {
                    try
                    {
                        var c = ColorConverter.ConvertFromString(s);
                        if (c is Color color) list.Add(color);
                        else list.Add(defaultValue);
                    }
                    catch
                    {
                        list.Add(defaultValue);
                    }
                }
            }
            EnsureCount(ref list, defaultValue);
            return list;
        }

        public PluginSettingsMemento CreateMemento()
        {
            return new PluginSettingsMemento
            {
                CoordinateSystem = _coordinateSystem,
                CullMode = _cullMode,
                AssimpObj = _assimpObj,
                AssimpGlb = _assimpGlb,
                AssimpPly = _assimpPly,
                AssimpStl = _assimpStl,
                Assimp3mf = _assimp3mf,
                AssimpPmx = _assimpPmx,
                WorldId = _worldId,
                AmbientColors = new List<Color>(_ambientColors),
                LightColors = new List<Color>(_lightColors),
                DiffuseIntensities = new List<double>(_diffuseIntensities),
                SpecularIntensities = new List<double>(_specularIntensities),
                Shininesses = new List<double>(_shininesses),
                ToonEnabled = new List<bool>(_toonEnabled),
                ToonSteps = new List<int>(_toonSteps),
                ToonSmoothness = new List<double>(_toonSmoothness),
                OutlineEnabled = new List<bool>(_outlineEnabled),
                OutlineColor = new List<Color>(_outlineColor),
                OutlineWidth = new List<double>(_outlineWidth),
                OutlinePower = new List<double>(_outlinePower),
                RimEnabled = new List<bool>(_rimEnabled),
                RimColor = new List<Color>(_rimColor),
                RimIntensity = new List<double>(_rimIntensity),
                RimPower = new List<double>(_rimPower),
                FogEnabled = new List<bool>(_fogEnabled),
                FogColor = new List<Color>(_fogColor),
                FogStart = new List<double>(_fogStart),
                FogEnd = new List<double>(_fogEnd),
                FogDensity = new List<double>(_fogDensity),
                Saturation = new List<double>(_saturation),
                Contrast = new List<double>(_contrast),
                Gamma = new List<double>(_gamma),
                BrightnessPost = new List<double>(_brightnessPost),
                VignetteEnabled = new List<bool>(_vignetteEnabled),
                VignetteColor = new List<Color>(_vignetteColor),
                VignetteIntensity = new List<double>(_vignetteIntensity),
                VignetteRadius = new List<double>(_vignetteRadius),
                VignetteSoftness = new List<double>(_vignetteSoftness),
                ChromAbEnabled = new List<bool>(_chromAbEnabled),
                ChromAbIntensity = new List<double>(_chromAbIntensity),
                ScanlineEnabled = new List<bool>(_scanlineEnabled),
                ScanlineIntensity = new List<double>(_scanlineIntensity),
                ScanlineFrequency = new List<double>(_scanlineFrequency),
                MonochromeEnabled = new List<bool>(_monochromeEnabled),
                MonochromeColor = new List<Color>(_monochromeColor),
                MonochromeMix = new List<double>(_monochromeMix),
                PosterizeEnabled = new List<bool>(_posterizeEnabled),
                PosterizeLevels = new List<int>(_posterizeLevels)
            };
        }

        public void RestoreMemento(PluginSettingsMemento m)
        {
            _coordinateSystem = m.CoordinateSystem;
            _cullMode = m.CullMode;
            _assimpObj = m.AssimpObj;
            _assimpGlb = m.AssimpGlb;
            _assimpPly = m.AssimpPly;
            _assimpStl = m.AssimpStl;
            _assimp3mf = m.Assimp3mf;
            _assimpPmx = m.AssimpPmx;
            _worldId = m.WorldId;

            _ambientColors = RestoreList(m.AmbientColors, Color.FromRgb(50, 50, 50));
            _lightColors = RestoreList(m.LightColors, Colors.White);
            _diffuseIntensities = RestoreList(m.DiffuseIntensities, 1.0);
            _specularIntensities = RestoreList(m.SpecularIntensities, 0.5);
            _shininesses = RestoreList(m.Shininesses, 20.0);
            _toonEnabled = RestoreList(m.ToonEnabled, false);
            _toonSteps = RestoreList(m.ToonSteps, 4);
            _toonSmoothness = RestoreList(m.ToonSmoothness, 0.05);
            _outlineEnabled = RestoreList(m.OutlineEnabled, false);
            _outlineColor = RestoreList(m.OutlineColor, Colors.Black);
            _outlineWidth = RestoreList(m.OutlineWidth, 1.0);
            _outlinePower = RestoreList(m.OutlinePower, 2.0);
            _rimEnabled = RestoreList(m.RimEnabled, false);
            _rimColor = RestoreList(m.RimColor, Colors.White);
            _rimIntensity = RestoreList(m.RimIntensity, 1.0);
            _rimPower = RestoreList(m.RimPower, 3.0);
            _fogEnabled = RestoreList(m.FogEnabled, false);
            _fogColor = RestoreList(m.FogColor, Colors.Gray);
            _fogStart = RestoreList(m.FogStart, 10.0);
            _fogEnd = RestoreList(m.FogEnd, 100.0);
            _fogDensity = RestoreList(m.FogDensity, 1.0);
            _saturation = RestoreList(m.Saturation, 1.0);
            _contrast = RestoreList(m.Contrast, 1.0);
            _gamma = RestoreList(m.Gamma, 1.0);
            _brightnessPost = RestoreList(m.BrightnessPost, 0.0);
            _vignetteEnabled = RestoreList(m.VignetteEnabled, false);
            _vignetteColor = RestoreList(m.VignetteColor, Colors.Black);
            _vignetteIntensity = RestoreList(m.VignetteIntensity, 0.5);
            _vignetteRadius = RestoreList(m.VignetteRadius, 0.8);
            _vignetteSoftness = RestoreList(m.VignetteSoftness, 0.3);
            _chromAbEnabled = RestoreList(m.ChromAbEnabled, false);
            _chromAbIntensity = RestoreList(m.ChromAbIntensity, 0.005);
            _scanlineEnabled = RestoreList(m.ScanlineEnabled, false);
            _scanlineIntensity = RestoreList(m.ScanlineIntensity, 0.2);
            _scanlineFrequency = RestoreList(m.ScanlineFrequency, 100.0);
            _monochromeEnabled = RestoreList(m.MonochromeEnabled, false);
            _monochromeColor = RestoreList(m.MonochromeColor, Colors.White);
            _monochromeMix = RestoreList(m.MonochromeMix, 1.0);
            _posterizeEnabled = RestoreList(m.PosterizeEnabled, false);
            _posterizeLevels = RestoreList(m.PosterizeLevels, 8);

            OnPropertyChanged(string.Empty);
        }

        private List<T> RestoreList<T>(List<T>? source, T defaultValue)
        {
            var list = source != null ? new List<T>(source) : new List<T>();
            EnsureCount(ref list, defaultValue);
            return list;
        }

        public override void Initialize()
        {
            EnsureCount(ref _ambientColors, Color.FromRgb(50, 50, 50));
            EnsureCount(ref _lightColors, Colors.White);
            EnsureCount(ref _diffuseIntensities, 1.0);
            EnsureCount(ref _specularIntensities, 0.5);
            EnsureCount(ref _shininesses, 20.0);
            EnsureCount(ref _toonEnabled, false);
            EnsureCount(ref _toonSteps, 4);
            EnsureCount(ref _toonSmoothness, 0.05);
            EnsureCount(ref _outlineEnabled, false);
            EnsureCount(ref _outlineColor, Colors.Black);
            EnsureCount(ref _outlineWidth, 1.0);
            EnsureCount(ref _outlinePower, 2.0);
            EnsureCount(ref _rimEnabled, false);
            EnsureCount(ref _rimColor, Colors.White);
            EnsureCount(ref _rimIntensity, 1.0);
            EnsureCount(ref _rimPower, 3.0);
            EnsureCount(ref _fogEnabled, false);
            EnsureCount(ref _fogColor, Colors.Gray);
            EnsureCount(ref _fogStart, 10.0);
            EnsureCount(ref _fogEnd, 100.0);
            EnsureCount(ref _fogDensity, 1.0);
            EnsureCount(ref _saturation, 1.0);
            EnsureCount(ref _contrast, 1.0);
            EnsureCount(ref _gamma, 1.0);
            EnsureCount(ref _brightnessPost, 0.0);
            EnsureCount(ref _vignetteEnabled, false);
            EnsureCount(ref _vignetteColor, Colors.Black);
            EnsureCount(ref _vignetteIntensity, 0.5);
            EnsureCount(ref _vignetteRadius, 0.8);
            EnsureCount(ref _vignetteSoftness, 0.3);
            EnsureCount(ref _chromAbEnabled, false);
            EnsureCount(ref _chromAbIntensity, 0.005);
            EnsureCount(ref _scanlineEnabled, false);
            EnsureCount(ref _scanlineIntensity, 0.2);
            EnsureCount(ref _scanlineFrequency, 100.0);
            EnsureCount(ref _monochromeEnabled, false);
            EnsureCount(ref _monochromeColor, Colors.White);
            EnsureCount(ref _monochromeMix, 1.0);
            EnsureCount(ref _posterizeEnabled, false);
            EnsureCount(ref _posterizeLevels, 8);
        }

        private void EnsureCount<T>(ref List<T> list, T defaultValue)
        {
            if (list == null) list = new List<T>();
            if (list.Count < MaxWorlds)
            {
                list.AddRange(Enumerable.Repeat(defaultValue, MaxWorlds - list.Count));
            }
        }

        public Color GetAmbientColor(int id) => _ambientColors[Math.Clamp(id, 0, MaxWorlds - 1)];
        public Color GetLightColor(int id) => _lightColors[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetDiffuseIntensity(int id) => _diffuseIntensities[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetSpecularIntensity(int id) => _specularIntensities[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetShininess(int id) => _shininesses[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetToonEnabled(int id) => _toonEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public int GetToonSteps(int id) => _toonSteps[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetToonSmoothness(int id) => _toonSmoothness[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetRimEnabled(int id) => _rimEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public Color GetRimColor(int id) => _rimColor[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetRimIntensity(int id) => _rimIntensity[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetRimPower(int id) => _rimPower[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetOutlineEnabled(int id) => _outlineEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public Color GetOutlineColor(int id) => _outlineColor[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetOutlineWidth(int id) => _outlineWidth[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetOutlinePower(int id) => _outlinePower[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetFogEnabled(int id) => _fogEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public Color GetFogColor(int id) => _fogColor[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetFogStart(int id) => _fogStart[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetFogEnd(int id) => _fogEnd[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetFogDensity(int id) => _fogDensity[Math.Clamp(id, 0, MaxWorlds - 1)];

        public double GetSaturation(int id) => _saturation[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetContrast(int id) => _contrast[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetGamma(int id) => _gamma[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetBrightnessPost(int id) => _brightnessPost[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetVignetteEnabled(int id) => _vignetteEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public Color GetVignetteColor(int id) => _vignetteColor[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetVignetteIntensity(int id) => _vignetteIntensity[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetVignetteRadius(int id) => _vignetteRadius[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetVignetteSoftness(int id) => _vignetteSoftness[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetChromAbEnabled(int id) => _chromAbEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetChromAbIntensity(int id) => _chromAbIntensity[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetScanlineEnabled(int id) => _scanlineEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetScanlineIntensity(int id) => _scanlineIntensity[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetScanlineFrequency(int id) => _scanlineFrequency[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetMonochromeEnabled(int id) => _monochromeEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public Color GetMonochromeColor(int id) => _monochromeColor[Math.Clamp(id, 0, MaxWorlds - 1)];
        public double GetMonochromeMix(int id) => _monochromeMix[Math.Clamp(id, 0, MaxWorlds - 1)];

        public bool GetPosterizeEnabled(int id) => _posterizeEnabled[Math.Clamp(id, 0, MaxWorlds - 1)];
        public int GetPosterizeLevels(int id) => _posterizeLevels[Math.Clamp(id, 0, MaxWorlds - 1)];


        [SettingGroup("Global", nameof(Texts.Group_Global), Order = 0, Icon = "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,12.5A0.5,0.5 0 0,1 11.5,12A0.5,0.5 0 0,1 12,11.5A0.5,0.5 0 0,1 12.5,12A0.5,0.5 0 0,1 12,12.5M12,7.2C9.9,7.2 8.2,8.9 8.2,11C8.2,14 12,17.5 12,17.5C12,17.5 15.8,14 15.8,11C15.8,8.9 14.1,7.2 12,7.2Z", ResourceType = typeof(Texts))]
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

        [SettingGroup("Lighting", nameof(Texts.Group_Lighting), Order = 1, Icon = "M12,2A7,7 0 0,0 5,9C5,11.38 6.19,13.47 8,14.74V17A1,1 0 0,0 9,18H15A1,1 0 0,0 16,17V14.74C17.81,13.47 19,11.38 19,9A7,7 0 0,0 12,2M9,21A1,1 0 0,0 10,22H14A1,1 0 0,0 15,21V20H9V21Z", ResourceType = typeof(Texts))]
        [IntSpinnerSetting("Lighting", nameof(Texts.WorldId), 0, 9, IsGroupHeader = true, Description = nameof(Texts.WorldId_Desc), ResourceType = typeof(Texts))]
        public int WorldId
        {
            get => _worldId;
            set
            {
                if (SetProperty(ref _worldId, value))
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
            }
        }

        [ColorSetting("Lighting", nameof(Texts.AmbientColor), Description = nameof(Texts.AmbientColor_Desc), ResourceType = typeof(Texts))]
        public Color AmbientColor { get => _ambientColors[_worldId]; set { if (_ambientColors[_worldId] != value) { _ambientColors[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Lighting", nameof(Texts.DiffuseIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.DiffuseIntensity_Desc), ResourceType = typeof(Texts))]
        public double DiffuseIntensity { get => _diffuseIntensities[_worldId]; set { if (_diffuseIntensities[_worldId] != value) { _diffuseIntensities[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Lighting", nameof(Texts.SpecularIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.SpecularIntensity_Desc), ResourceType = typeof(Texts))]
        public double SpecularIntensity { get => _specularIntensities[_worldId]; set { if (_specularIntensities[_worldId] != value) { _specularIntensities[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Lighting", nameof(Texts.Shininess), 1, 100, Tick = 1, Description = nameof(Texts.Shininess_Desc), ResourceType = typeof(Texts))]
        public double Shininess { get => _shininesses[_worldId]; set { if (_shininesses[_worldId] != value) { _shininesses[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Environment", nameof(Texts.Group_Environment), Order = 2, ParentId = "Lighting", Icon = "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,5.29C7.24,5.84 7.09,6.44 7.09,7.09C7.09,7.74 7.24,8.34 7.5,8.89L3.34,7.18V7M3.34,17L7.5,18.71C7.24,18.16 7.09,17.56 7.09,16.91C7.09,16.26 7.24,15.66 7.5,15.11L3.34,16.82V17M20.66,17L16.5,15.29C16.76,15.84 16.91,16.44 16.91,17.09C16.91,17.74 16.76,18.34 16.5,18.89L20.66,17.18V17M20.66,7L16.5,8.71C16.76,8.16 16.91,7.56 16.91,6.91C16.91,6.26 16.76,5.66 16.5,5.11L20.66,6.82V7M12,22L9.61,18.58C10.35,18.85 11.16,19 12,19C12.84,19 13.65,18.85 14.39,18.58L12,22Z", ResourceType = typeof(Texts))]
        [ColorSetting("Environment", nameof(Texts.LightColor), Description = nameof(Texts.LightColor_Desc), ResourceType = typeof(Texts))]
        public Color LightColor { get => _lightColors[_worldId]; set { if (_lightColors[_worldId] != value) { _lightColors[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Toon", nameof(Texts.Group_Toon), Order = 3, ParentId = "Lighting", Icon = "M19,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3M19,19H5V5H19V19M11,7H13V9H15V11H13V13H11V11H9V9H11V7Z", ResourceType = typeof(Texts))]
        [BoolSetting("Toon", nameof(Texts.ToonEnabled), Description = nameof(Texts.ToonEnabled_Desc), ResourceType = typeof(Texts))]
        public bool ToonEnabled { get => _toonEnabled[_worldId]; set { if (_toonEnabled[_worldId] != value) { _toonEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [IntSpinnerSetting("Toon", nameof(Texts.ToonSteps), 1, 10, EnableBy = nameof(ToonEnabled), Description = nameof(Texts.ToonSteps_Desc), ResourceType = typeof(Texts))]
        public int ToonSteps { get => _toonSteps[_worldId]; set { if (_toonSteps[_worldId] != value) { _toonSteps[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Toon", nameof(Texts.ToonSmoothness), 0, 1, Tick = 0.01, EnableBy = nameof(ToonEnabled), Description = nameof(Texts.ToonSmoothness_Desc), ResourceType = typeof(Texts))]
        public double ToonSmoothness { get => _toonSmoothness[_worldId]; set { if (_toonSmoothness[_worldId] != value) { _toonSmoothness[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Rim", nameof(Texts.Group_Rim), Order = 4, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4Z", ResourceType = typeof(Texts))]
        [BoolSetting("Rim", nameof(Texts.RimEnabled), Description = nameof(Texts.RimEnabled_Desc), ResourceType = typeof(Texts))]
        public bool RimEnabled { get => _rimEnabled[_worldId]; set { if (_rimEnabled[_worldId] != value) { _rimEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [ColorSetting("Rim", nameof(Texts.RimColor), EnableBy = nameof(RimEnabled), Description = nameof(Texts.RimColor_Desc), ResourceType = typeof(Texts))]
        public Color RimColor { get => _rimColor[_worldId]; set { if (_rimColor[_worldId] != value) { _rimColor[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Rim", nameof(Texts.RimIntensity), 0, 10, Tick = 0.1, EnableBy = nameof(RimEnabled), Description = nameof(Texts.RimIntensity_Desc), ResourceType = typeof(Texts))]
        public double RimIntensity { get => _rimIntensity[_worldId]; set { if (_rimIntensity[_worldId] != value) { _rimIntensity[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Rim", nameof(Texts.RimPower), 0.1, 10, Tick = 0.1, EnableBy = nameof(RimEnabled), Description = nameof(Texts.RimPower_Desc), ResourceType = typeof(Texts))]
        public double RimPower { get => _rimPower[_worldId]; set { if (_rimPower[_worldId] != value) { _rimPower[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Outline", nameof(Texts.Group_Outline), Order = 5, ParentId = "Lighting", Icon = "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,4A8,8 0 0,0 4,12A8,8 0 0,0 12,20A8,8 0 0,0 20,12A8,8 0 0,0 12,4M12,6A6,6 0 0,1 18,12A6,6 0 0,1 12,18A6,6 0 0,1 6,12A6,6 0 0,1 12,6Z", ResourceType = typeof(Texts))]
        [BoolSetting("Outline", nameof(Texts.OutlineEnabled), Description = nameof(Texts.OutlineEnabled_Desc), ResourceType = typeof(Texts))]
        public bool OutlineEnabled { get => _outlineEnabled[_worldId]; set { if (_outlineEnabled[_worldId] != value) { _outlineEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [ColorSetting("Outline", nameof(Texts.OutlineColor), EnableBy = nameof(OutlineEnabled), Description = nameof(Texts.OutlineColor_Desc), ResourceType = typeof(Texts))]
        public Color OutlineColor { get => _outlineColor[_worldId]; set { if (_outlineColor[_worldId] != value) { _outlineColor[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Outline", nameof(Texts.OutlineWidth), 0, 20, Tick = 0.1, EnableBy = nameof(OutlineEnabled), Description = nameof(Texts.OutlineWidth_Desc), ResourceType = typeof(Texts))]
        public double OutlineWidth { get => _outlineWidth[_worldId]; set { if (_outlineWidth[_worldId] != value) { _outlineWidth[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Outline", nameof(Texts.OutlinePower), 0.1, 10, Tick = 0.1, EnableBy = nameof(OutlineEnabled), Description = nameof(Texts.OutlinePower_Desc), ResourceType = typeof(Texts))]
        public double OutlinePower { get => _outlinePower[_worldId]; set { if (_outlinePower[_worldId] != value) { _outlinePower[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Fog", nameof(Texts.Group_Fog), Order = 6, ParentId = "Lighting", Icon = "M3,4H21V8H3V4M3,10H21V14H3V10M3,16H21V20H3V16Z", ResourceType = typeof(Texts))]
        [BoolSetting("Fog", nameof(Texts.FogEnabled), Description = nameof(Texts.FogEnabled_Desc), ResourceType = typeof(Texts))]
        public bool FogEnabled { get => _fogEnabled[_worldId]; set { if (_fogEnabled[_worldId] != value) { _fogEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [ColorSetting("Fog", nameof(Texts.FogColor), EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogColor_Desc), ResourceType = typeof(Texts))]
        public Color FogColor { get => _fogColor[_worldId]; set { if (_fogColor[_worldId] != value) { _fogColor[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Fog", nameof(Texts.FogStart), 0, 1000, Tick = 1, EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogStart_Desc), ResourceType = typeof(Texts))]
        public double FogStart { get => _fogStart[_worldId]; set { if (_fogStart[_worldId] != value) { _fogStart[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Fog", nameof(Texts.FogEnd), 0, 5000, Tick = 10, EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogEnd_Desc), ResourceType = typeof(Texts))]
        public double FogEnd { get => _fogEnd[_worldId]; set { if (_fogEnd[_worldId] != value) { _fogEnd[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Fog", nameof(Texts.FogDensity), 0, 5, Tick = 0.01, EnableBy = nameof(FogEnabled), Description = nameof(Texts.FogDensity_Desc), ResourceType = typeof(Texts))]
        public double FogDensity { get => _fogDensity[_worldId]; set { if (_fogDensity[_worldId] != value) { _fogDensity[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("PostEffect", nameof(Texts.Group_PostEffect), Order = 7, Icon = "M2,2V22H22V2H2M20,20H4V4H20V20M8,6H16V14H8V6M10,8V12H14V8H10Z", ResourceType = typeof(Texts))]
        [RangeSetting("PostEffect", nameof(Texts.Saturation), 0, 3, Tick = 0.1, Description = nameof(Texts.Saturation_Desc), ResourceType = typeof(Texts))]
        public double Saturation { get => _saturation[_worldId]; set { if (_saturation[_worldId] != value) { _saturation[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("PostEffect", nameof(Texts.Contrast), 0, 3, Tick = 0.1, Description = nameof(Texts.Contrast_Desc), ResourceType = typeof(Texts))]
        public double Contrast { get => _contrast[_worldId]; set { if (_contrast[_worldId] != value) { _contrast[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("PostEffect", nameof(Texts.Gamma), 0.1, 5, Tick = 0.1, Description = nameof(Texts.Gamma_Desc), ResourceType = typeof(Texts))]
        public double Gamma { get => _gamma[_worldId]; set { if (_gamma[_worldId] != value) { _gamma[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("PostEffect", nameof(Texts.BrightnessPost), -1, 1, Tick = 0.01, Description = nameof(Texts.BrightnessPost_Desc), ResourceType = typeof(Texts))]
        public double BrightnessPost { get => _brightnessPost[_worldId]; set { if (_brightnessPost[_worldId] != value) { _brightnessPost[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Vignette", nameof(Texts.Group_Vignette), Order = 8, ParentId = "PostEffect", Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z", ResourceType = typeof(Texts))]
        [BoolSetting("Vignette", nameof(Texts.VignetteEnabled), Description = nameof(Texts.VignetteEnabled_Desc), ResourceType = typeof(Texts))]
        public bool VignetteEnabled { get => _vignetteEnabled[_worldId]; set { if (_vignetteEnabled[_worldId] != value) { _vignetteEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [ColorSetting("Vignette", nameof(Texts.VignetteColor), EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteColor_Desc), ResourceType = typeof(Texts))]
        public Color VignetteColor { get => _vignetteColor[_worldId]; set { if (_vignetteColor[_worldId] != value) { _vignetteColor[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Vignette", nameof(Texts.VignetteIntensity), 0, 2, Tick = 0.05, EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteIntensity_Desc), ResourceType = typeof(Texts))]
        public double VignetteIntensity { get => _vignetteIntensity[_worldId]; set { if (_vignetteIntensity[_worldId] != value) { _vignetteIntensity[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Vignette", nameof(Texts.VignetteRadius), 0, 2, Tick = 0.05, EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteRadius_Desc), ResourceType = typeof(Texts))]
        public double VignetteRadius { get => _vignetteRadius[_worldId]; set { if (_vignetteRadius[_worldId] != value) { _vignetteRadius[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Vignette", nameof(Texts.VignetteSoftness), 0.01, 1, Tick = 0.01, EnableBy = nameof(VignetteEnabled), Description = nameof(Texts.VignetteSoftness_Desc), ResourceType = typeof(Texts))]
        public double VignetteSoftness { get => _vignetteSoftness[_worldId]; set { if (_vignetteSoftness[_worldId] != value) { _vignetteSoftness[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Scanline", nameof(Texts.Group_Scanline), Order = 9, ParentId = "PostEffect", Icon = "M3,3H21V5H3V3M3,7H21V9H3V7M3,11H21V13H3V11M3,15H21V17H3V15M3,19H21V21H3V19Z", ResourceType = typeof(Texts))]
        [BoolSetting("Scanline", nameof(Texts.ScanlineEnabled), Description = nameof(Texts.ScanlineEnabled_Desc), ResourceType = typeof(Texts))]
        public bool ScanlineEnabled { get => _scanlineEnabled[_worldId]; set { if (_scanlineEnabled[_worldId] != value) { _scanlineEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Scanline", nameof(Texts.ScanlineIntensity), 0, 1, Tick = 0.01, EnableBy = nameof(ScanlineEnabled), Description = nameof(Texts.ScanlineIntensity_Desc), ResourceType = typeof(Texts))]
        public double ScanlineIntensity { get => _scanlineIntensity[_worldId]; set { if (_scanlineIntensity[_worldId] != value) { _scanlineIntensity[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Scanline", nameof(Texts.ScanlineFrequency), 1, 500, Tick = 1, EnableBy = nameof(ScanlineEnabled), Description = nameof(Texts.ScanlineFrequency_Desc), ResourceType = typeof(Texts))]
        public double ScanlineFrequency { get => _scanlineFrequency[_worldId]; set { if (_scanlineFrequency[_worldId] != value) { _scanlineFrequency[_worldId] = value; OnPropertyChanged(); } } }

        [SettingGroup("Artistic", nameof(Texts.Group_Artistic), Order = 10, ParentId = "PostEffect", Icon = "M12,3C16.97,3 21,7.03 21,12C21,16.97 16.97,21 12,21C7.03,21 3,16.97 3,12C3,7.03 7.03,3 12,3M12,5C8.13,5 5,8.13 5,12C5,15.87 8.13,19 12,19C15.87,19 19,15.87 19,12C19,8.13 15.87,5 12,5Z", ResourceType = typeof(Texts))]
        [BoolSetting("Artistic", nameof(Texts.ChromAbEnabled), Description = nameof(Texts.ChromAbEnabled_Desc), ResourceType = typeof(Texts))]
        public bool ChromAbEnabled { get => _chromAbEnabled[_worldId]; set { if (_chromAbEnabled[_worldId] != value) { _chromAbEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Artistic", nameof(Texts.ChromAbIntensity), 0, 0.1, Tick = 0.001, EnableBy = nameof(ChromAbEnabled), Description = nameof(Texts.ChromAbIntensity_Desc), ResourceType = typeof(Texts))]
        public double ChromAbIntensity { get => _chromAbIntensity[_worldId]; set { if (_chromAbIntensity[_worldId] != value) { _chromAbIntensity[_worldId] = value; OnPropertyChanged(); } } }

        [BoolSetting("Artistic", nameof(Texts.MonochromeEnabled), Description = nameof(Texts.MonochromeEnabled_Desc), ResourceType = typeof(Texts))]
        public bool MonochromeEnabled { get => _monochromeEnabled[_worldId]; set { if (_monochromeEnabled[_worldId] != value) { _monochromeEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [ColorSetting("Artistic", nameof(Texts.MonochromeColor), EnableBy = nameof(MonochromeEnabled), Description = nameof(Texts.MonochromeColor_Desc), ResourceType = typeof(Texts))]
        public Color MonochromeColor { get => _monochromeColor[_worldId]; set { if (_monochromeColor[_worldId] != value) { _monochromeColor[_worldId] = value; OnPropertyChanged(); } } }

        [RangeSetting("Artistic", nameof(Texts.MonochromeMix), 0, 1, Tick = 0.01, EnableBy = nameof(MonochromeEnabled), Description = nameof(Texts.MonochromeMix_Desc), ResourceType = typeof(Texts))]
        public double MonochromeMix { get => _monochromeMix[_worldId]; set { if (_monochromeMix[_worldId] != value) { _monochromeMix[_worldId] = value; OnPropertyChanged(); } } }

        [BoolSetting("Artistic", nameof(Texts.PosterizeEnabled), Description = nameof(Texts.PosterizeEnabled_Desc), ResourceType = typeof(Texts))]
        public bool PosterizeEnabled { get => _posterizeEnabled[_worldId]; set { if (_posterizeEnabled[_worldId] != value) { _posterizeEnabled[_worldId] = value; OnPropertyChanged(); } } }

        [IntSpinnerSetting("Artistic", nameof(Texts.PosterizeLevels), 2, 255, EnableBy = nameof(PosterizeEnabled), Description = nameof(Texts.PosterizeLevels_Desc), ResourceType = typeof(Texts))]
        public int PosterizeLevels { get => _posterizeLevels[_worldId]; set { if (_posterizeLevels[_worldId] != value) { _posterizeLevels[_worldId] = value; OnPropertyChanged(); } } }

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
            AssimpObj = false;
            AssimpGlb = false;
            AssimpPly = false;
            AssimpStl = false;
            Assimp3mf = false;
            AssimpPmx = false;
            _ambientColors = Enumerable.Repeat(Color.FromRgb(50, 50, 50), MaxWorlds).ToList();
            _lightColors = Enumerable.Repeat(Colors.White, MaxWorlds).ToList();
            _diffuseIntensities = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _specularIntensities = Enumerable.Repeat(0.5, MaxWorlds).ToList();
            _shininesses = Enumerable.Repeat(20.0, MaxWorlds).ToList();
            _toonEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _toonSteps = Enumerable.Repeat(4, MaxWorlds).ToList();
            _toonSmoothness = Enumerable.Repeat(0.05, MaxWorlds).ToList();
            _rimEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _rimColor = Enumerable.Repeat(Colors.White, MaxWorlds).ToList();
            _rimIntensity = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _rimPower = Enumerable.Repeat(3.0, MaxWorlds).ToList();
            _outlineEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _outlineColor = Enumerable.Repeat(Colors.Black, MaxWorlds).ToList();
            _outlineWidth = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _outlinePower = Enumerable.Repeat(2.0, MaxWorlds).ToList();
            _fogEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _fogColor = Enumerable.Repeat(Colors.Gray, MaxWorlds).ToList();
            _fogStart = Enumerable.Repeat(10.0, MaxWorlds).ToList();
            _fogEnd = Enumerable.Repeat(100.0, MaxWorlds).ToList();
            _fogDensity = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _saturation = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _contrast = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _gamma = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _brightnessPost = Enumerable.Repeat(0.0, MaxWorlds).ToList();
            _vignetteEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _vignetteColor = Enumerable.Repeat(Colors.Black, MaxWorlds).ToList();
            _vignetteIntensity = Enumerable.Repeat(0.5, MaxWorlds).ToList();
            _vignetteRadius = Enumerable.Repeat(0.8, MaxWorlds).ToList();
            _vignetteSoftness = Enumerable.Repeat(0.3, MaxWorlds).ToList();
            _chromAbEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _chromAbIntensity = Enumerable.Repeat(0.005, MaxWorlds).ToList();
            _scanlineEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _scanlineIntensity = Enumerable.Repeat(0.2, MaxWorlds).ToList();
            _scanlineFrequency = Enumerable.Repeat(100.0, MaxWorlds).ToList();
            _monochromeEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _monochromeColor = Enumerable.Repeat(Colors.White, MaxWorlds).ToList();
            _monochromeMix = Enumerable.Repeat(1.0, MaxWorlds).ToList();
            _posterizeEnabled = Enumerable.Repeat(false, MaxWorlds).ToList();
            _posterizeLevels = Enumerable.Repeat(8, MaxWorlds).ToList();

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
using System.Windows.Media;

namespace ObjLoader.Settings
{
    public class LightingSettings : ICloneable
    {
        public bool ShadowEnabled { get; set; } = true;
        public Color AmbientColor { get; set; } = Color.FromRgb(50, 50, 50);
        public Color LightColor { get; set; } = Colors.White;
        public double DiffuseIntensity { get; set; } = 1.0;
        public double SpecularIntensity { get; set; } = 1.0;
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
        public bool ApplyAfterTonemap { get; set; } = false;
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

    public class PbrSettings : ICloneable
    {
        public double Metallic { get; set; } = 0.0;
        public double Roughness { get; set; } = 0.5;
        public double IBLIntensity { get; set; } = 1.0;
        public object Clone() => MemberwiseClone();
    }

    public class SsrSettings : ICloneable
    {
        public bool Enabled { get; set; } = false;
        public double Step { get; set; } = 0.5;
        public double MaxDist { get; set; } = 20.0;
        public double Thickness { get; set; } = 0.5;
        public int MaxSteps { get; set; } = 64;
        public object Clone() => MemberwiseClone();
    }

    public class PcssSettings : ICloneable
    {
        public double LightSize { get; set; } = 1.0;
        public int Quality { get; set; } = 16;
        public object Clone() => MemberwiseClone();
    }
}
using ObjLoader.Plugin;
using System.Numerics;
using System.Windows.Media;

namespace ObjLoader.Api.Light
{
    public readonly struct LightDescriptor
    {
        public bool IsEnabled { get; }
        public LightType Type { get; }
        public Vector3 Position { get; }
        public Color Color { get; }
        public float Intensity { get; }

        public LightDescriptor(bool isEnabled, LightType type, Vector3 position, Color color, float intensity)
        {
            IsEnabled = isEnabled;
            Type = type;
            Position = position;
            Color = color;
            Intensity = intensity;
        }
    }
}
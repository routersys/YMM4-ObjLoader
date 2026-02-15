using System.Windows.Media;

namespace ObjLoader.Rendering.Core
{
    internal struct PartMaterialState
    {
        public double Roughness;
        public double Metallic;
        public Color BaseColor;
        public string? TexturePath;
    }
}

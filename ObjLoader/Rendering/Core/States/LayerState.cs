using ObjLoader.Core.Enums;
using ObjLoader.Plugin;
using ObjLoader.Settings;
using System.Windows.Media;

namespace ObjLoader.Rendering.Core.States
{
    internal readonly record struct LayerState
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public double Scale { get; init; }
        public double Rx { get; init; }
        public double Ry { get; init; }
        public double Rz { get; init; }
        public double Cx { get; init; }
        public double Cy { get; init; }
        public double Cz { get; init; }
        public double Fov { get; init; }
        public double LightX { get; init; }
        public double LightY { get; init; }
        public double LightZ { get; init; }
        public double Diffuse { get; init; }
        public double Specular { get; init; }
        public double Shininess { get; init; }
        public bool IsLightEnabled { get; init; }
        public LightType LightType { get; init; }
        public string FilePath { get; init; }
        public string ShaderFilePath { get; init; }
        public string CacheKey { get; init; }
        public Color BaseColor { get; init; }
        public Color Ambient { get; init; }
        public Color Light { get; init; }
        public ProjectionType Projection { get; init; }
        public CoordinateSystem CoordSystem { get; init; }
        public RenderCullMode CullMode { get; init; }
        public int WorldId { get; init; }
        public bool IsVisible { get; init; }
        public HashSet<int>? VisibleParts { get; init; }
        public string ParentGuid { get; init; }
        public Dictionary<int, PartMaterialState>? PartMaterials { get; init; }
    }
}
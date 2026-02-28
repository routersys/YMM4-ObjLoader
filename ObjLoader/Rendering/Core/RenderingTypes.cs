using System.Windows.Media;
using ObjLoader.Core.Enums;
using ObjLoader.Plugin;
using ObjLoader.Settings;

namespace ObjLoader.Rendering.Core
{
    internal struct LayerState
    {
        public double X, Y, Z, Scale, Rx, Ry, Rz, Cx, Cy, Cz, Fov, LightX, LightY, LightZ, Diffuse, Specular, Shininess;
        public bool IsLightEnabled;
        public LightType LightType;
        public string FilePath, ShaderFilePath, CacheKey;
        public Color BaseColor, Ambient, Light;
        public ProjectionType Projection;
        public CoordinateSystem CoordSystem;
        public RenderCullMode CullMode;
        public int WorldId;
        public bool IsVisible;
        public HashSet<int>? VisibleParts;
        public string ParentGuid;
        public System.Collections.Generic.Dictionary<int, PartMaterialState>? PartMaterials;
    }
}
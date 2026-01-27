using System.Windows.Media;
using ObjLoader.Core;
using ObjLoader.Settings;

namespace ObjLoader.Rendering
{
    internal struct LayerState
    {
        public double X, Y, Z, Scale, Rx, Ry, Rz, Cx, Cy, Cz, Fov, LightX, LightY, LightZ, Diffuse, Specular, Shininess;
        public bool IsLightEnabled;
        public LightType LightType;
        public string FilePath, ShaderFilePath;
        public Color BaseColor, Ambient, Light;
        public ProjectionType Projection;
        public CoordinateSystem CoordSystem;
        public RenderCullMode CullMode;
        public int WorldId;
        public bool IsVisible;
        public HashSet<int>? VisibleParts;
        public string ParentGuid;
    }
}
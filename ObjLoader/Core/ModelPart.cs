using System.Numerics;

namespace ObjLoader.Core
{
    public struct ModelPart
    {
        public string TexturePath;
        public int IndexOffset;
        public int IndexCount;
        public Vector4 BaseColor;
        public float Metallic;
        public float Roughness;
    }
}
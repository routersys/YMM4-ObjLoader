using System.Numerics;
using ObjLoader.Rendering.Mathematics;
namespace ObjLoader.Core.Models
{
    public struct ModelPart
    {
        public string Name;
        public string TexturePath;
        public int IndexOffset;
        public int IndexCount;
        public Vector4 BaseColor;
        public float Metallic;
        public float Roughness;
        public Vector3 Center;
        public CullingBox LocalBoundingBox;
    }
}
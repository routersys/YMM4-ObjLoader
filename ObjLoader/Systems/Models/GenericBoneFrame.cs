using System.Numerics;

namespace ObjLoader.Systems.Models
{
    public class GenericBoneFrame
    {
        public string BoneName { get; set; } = string.Empty;
        public uint FrameNumber { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public byte[] Interpolation { get; set; } = System.Array.Empty<byte>();
    }
}
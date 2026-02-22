using System.Numerics;

namespace ObjLoader.Systems.Models
{
    public class GenericCameraFrame
    {
        public uint FrameNumber { get; set; }
        public float Distance { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public byte[] Interpolation { get; set; } = System.Array.Empty<byte>();
        public uint ViewAngle { get; set; }
        public bool IsOrthographic { get; set; }
    }
}

using System.Numerics;

namespace ObjLoader.Services.Mmd.Parsers
{
    public class VmdData
    {
        public string ModelName { get; set; } = string.Empty;
        public List<VmdBoneFrame> BoneFrames { get; set; } = new List<VmdBoneFrame>();
        public List<VmdMorphFrame> MorphFrames { get; set; } = new List<VmdMorphFrame>();
        public List<VmdCameraFrame> CameraFrames { get; set; } = new List<VmdCameraFrame>();
    }

    public class VmdBoneFrame
    {
        public string BoneName { get; set; } = string.Empty;
        public uint FrameNumber { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public byte[] Interpolation { get; set; } = Array.Empty<byte>();
    }

    public class VmdMorphFrame
    {
        public string MorphName { get; set; } = string.Empty;
        public uint FrameNumber { get; set; }
        public float Weight { get; set; }
    }

    public class VmdCameraFrame
    {
        public uint FrameNumber { get; set; }
        public float Distance { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public byte[] Interpolation { get; set; } = Array.Empty<byte>();
        public uint ViewAngle { get; set; }
        public bool IsOrthographic { get; set; }
    }
}
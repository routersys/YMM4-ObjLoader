using System.Numerics;

namespace ObjLoader.Core.Mmd
{
    public class PmxBone
    {
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public int ParentIndex { get; set; } = -1;
        public int DeformLayer { get; set; }
        public ushort BoneFlags { get; set; }
        public Vector3 Position { get; set; }
        public int ConnectionIndex { get; set; } = -1;
        public Vector3 ConnectionPosition { get; set; }
        public int AdditionalParentIndex { get; set; } = -1;
        public float AdditionalParentRatio { get; set; }
        public Vector3 AxisX { get; set; }
        public Vector3 AxisZ { get; set; }
        public int ExportKey { get; set; }
        public PmxIkData? IkData { get; set; }
    }

    public class PmxIkData
    {
        public int TargetIndex { get; set; } = -1;
        public int LoopCount { get; set; }
        public float LimitAngle { get; set; }
        public List<PmxIkLink> Links { get; set; } = new List<PmxIkLink>();
    }

    public class PmxIkLink
    {
        public int BoneIndex { get; set; } = -1;
        public byte HasLimit { get; set; }
        public Vector3 LimitMin { get; set; }
        public Vector3 LimitMax { get; set; }
    }
}
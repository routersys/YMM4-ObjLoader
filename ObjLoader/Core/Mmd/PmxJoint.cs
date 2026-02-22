using System.Numerics;

namespace ObjLoader.Core.Mmd
{
    public class PmxJoint
    {
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public int RigidBodyIndexA { get; set; }
        public int RigidBodyIndexB { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 TranslationLimitMin { get; set; }
        public Vector3 TranslationLimitMax { get; set; }
        public Vector3 RotationLimitMin { get; set; }
        public Vector3 RotationLimitMax { get; set; }
        public Vector3 SpringTranslation { get; set; }
        public Vector3 SpringRotation { get; set; }
    }
}
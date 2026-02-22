using System.Numerics;

namespace ObjLoader.Systems.Models
{
    public class GenericJoint
    {
        public string Name { get; set; } = string.Empty;
        public int RigidBodyIndexA { get; set; } = -1;
        public int RigidBodyIndexB { get; set; } = -1;
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
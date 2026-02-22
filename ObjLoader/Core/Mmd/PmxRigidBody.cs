using System.Numerics;

namespace ObjLoader.Core.Mmd
{
    public class PmxRigidBody
    {
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public int BoneIndex { get; set; } = -1;
        public byte CollisionGroup { get; set; }
        public ushort CollisionMask { get; set; }
        public byte ShapeType { get; set; }
        public Vector3 ShapeSize { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public float Mass { get; set; }
        public float LinearDamping { get; set; }
        public float AngularDamping { get; set; }
        public float Restitution { get; set; }
        public float Friction { get; set; }
        public byte PhysicsMode { get; set; }
    }
}
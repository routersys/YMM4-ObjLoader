using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Services.Mmd.Physics
{
    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public struct MmdPhysicsState
    {
        [FieldOffset(0)] public Vector3 LinearVelocity;
        [FieldOffset(12)] public float InverseMass;
        [FieldOffset(16)] public Vector3 AngularVelocity;
        [FieldOffset(28)] public float Friction;
        [FieldOffset(32)] public Vector3 Position;
        [FieldOffset(44)] public float Restitution;
        [FieldOffset(48)] public Quaternion Rotation;
        [FieldOffset(64)] public Matrix4x4 InverseWorldInertia;
        [FieldOffset(128)] public Matrix4x4 InverseLocalInertia;
        [FieldOffset(192)] public float LinearDamping;
        [FieldOffset(196)] public float AngularDamping;
        [FieldOffset(200)] public byte PhysicsMode;
        [FieldOffset(202)] public ushort CollisionGroupMask;
        [FieldOffset(204)] public ushort CollisionMask;
        [FieldOffset(206)] public bool IsSleeping;
        [FieldOffset(208)] public float SleepTimer;
        [FieldOffset(212)] public int IslandId;
    }
}
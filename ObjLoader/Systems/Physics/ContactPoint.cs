using System.Numerics;
using System.Runtime.InteropServices;

namespace ObjLoader.Systems.Physics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ContactPoint
    {
        public Vector3 Normal;
        public float Depth;
        public Vector3 RelPosA;
        public Vector3 RelPosB;
        public Vector3 LocalPointA;
        public Vector3 LocalPointB;

        public float AppliedNormalImpulse;
        public float AppliedFrictionImpulse1;
        public float AppliedFrictionImpulse2;
        public Vector3 FrictionDir1;
        public Vector3 FrictionDir2;

        public float NormalMass;
        public float FrictionMass1;
        public float FrictionMass2;

        public int LifeTime;
    }
}
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ObjLoader.Systems.Physics
{
    public static class PhysicsMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Dot3Sse(Vector3 a, Vector3 b)
        {
            if (Sse41.IsSupported)
            {
                var va = Vector128.Create(a.X, a.Y, a.Z, 0f);
                var vb = Vector128.Create(b.X, b.Y, b.Z, 0f);
                return Sse41.DotProduct(va, vb, 0x71).ToScalar();
            }
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 Cross3Sse(Vector3 a, Vector3 b)
        {
            if (Sse.IsSupported)
            {
                var va = Vector128.Create(a.X, a.Y, a.Z, 0f);
                var vb = Vector128.Create(b.X, b.Y, b.Z, 0f);
                var t1 = Sse.Shuffle(va, va, 0b_11_00_10_01);
                var t2 = Sse.Shuffle(vb, vb, 0b_11_01_00_10);
                var t3 = Sse.Shuffle(va, va, 0b_11_01_00_10);
                var t4 = Sse.Shuffle(vb, vb, 0b_11_00_10_01);
                var r = Sse.Subtract(Sse.Multiply(t1, t2), Sse.Multiply(t3, t4));
                return new Vector3(r.GetElement(0), r.GetElement(1), r.GetElement(2));
            }
            return Vector3.Cross(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 TransformInertia(Matrix4x4 localInertiaInverse, Quaternion rotation)
        {
            float ix = localInertiaInverse.M11;
            float iy = localInertiaInverse.M22;
            float iz = localInertiaInverse.M33;

            float qx = rotation.X, qy = rotation.Y, qz = rotation.Z, qw = rotation.W;
            float x2 = qx * qx, y2 = qy * qy, z2 = qz * qz;
            float xy = qx * qy, xz = qx * qz, yz = qy * qz;
            float wx = qw * qx, wy = qw * qy, wz = qw * qz;

            float r00 = 1f - 2f * (y2 + z2), r01 = 2f * (xy + wz), r02 = 2f * (xz - wy);
            float r10 = 2f * (xy - wz), r11 = 1f - 2f * (x2 + z2), r12 = 2f * (yz + wx);
            float r20 = 2f * (xz + wy), r21 = 2f * (yz - wx), r22 = 1f - 2f * (x2 + y2);

            if (Sse41.IsSupported)
            {
                var vRow0 = Vector128.Create(r00, r01, r02, 0f);
                var vRow1 = Vector128.Create(r10, r11, r12, 0f);
                var vRow2 = Vector128.Create(r20, r21, r22, 0f);
                var vD = Vector128.Create(ix, iy, iz, 0f);
                var vDRow0 = Sse.Multiply(vD, vRow0);
                var vDRow1 = Sse.Multiply(vD, vRow1);
                var vDRow2 = Sse.Multiply(vD, vRow2);
                float m11 = Sse41.DotProduct(vRow0, vDRow0, 0x71).ToScalar();
                float m12 = Sse41.DotProduct(vRow0, vDRow1, 0x71).ToScalar();
                float m13 = Sse41.DotProduct(vRow0, vDRow2, 0x71).ToScalar();
                float m22 = Sse41.DotProduct(vRow1, vDRow1, 0x71).ToScalar();
                float m23 = Sse41.DotProduct(vRow1, vDRow2, 0x71).ToScalar();
                float m33 = Sse41.DotProduct(vRow2, vDRow2, 0x71).ToScalar();
                return new Matrix4x4(
                    m11, m12, m13, 0f,
                    m12, m22, m23, 0f,
                    m13, m23, m33, 0f,
                    0f, 0f, 0f, 1f);
            }

            float m11s = r00 * r00 * ix + r01 * r01 * iy + r02 * r02 * iz;
            float m12s = r00 * r10 * ix + r01 * r11 * iy + r02 * r12 * iz;
            float m13s = r00 * r20 * ix + r01 * r21 * iy + r02 * r22 * iz;
            float m22s = r10 * r10 * ix + r11 * r11 * iy + r12 * r12 * iz;
            float m23s = r10 * r20 * ix + r11 * r21 * iy + r12 * r22 * iz;
            float m33s = r20 * r20 * ix + r21 * r21 * iy + r22 * r22 * iz;
            return new Matrix4x4(
                m11s, m12s, m13s, 0f,
                m12s, m22s, m23s, 0f,
                m13s, m23s, m33s, 0f,
                0f, 0f, 0f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 ComputeLocalInertiaInverse(byte shapeType, Vector3 size, float mass)
        {
            if (mass <= 0f) return Matrix4x4.Identity;

            float ix, iy, iz;
            switch (shapeType)
            {
                case 0:
                    {
                        float iSphere = 0.4f * mass * size.X * size.X;
                        ix = iSphere; iy = iSphere; iz = iSphere;
                        break;
                    }
                case 1:
                    {
                        float lx = size.X * 2.0f;
                        float ly = size.Y * 2.0f;
                        float lz = size.Z * 2.0f;
                        float coeff = mass / 12.0f;
                        ix = coeff * (ly * ly + lz * lz);
                        iy = coeff * (lx * lx + lz * lz);
                        iz = coeff * (lx * lx + ly * ly);
                        break;
                    }
                case 2:
                    {
                        float radius = size.X;
                        float height = size.Y;
                        float totalHeight = height + radius * 2.0f;
                        float rr = radius * radius;
                        float hh = totalHeight * totalHeight;
                        float ixy = mass * (3.0f * rr + hh) / 12.0f;
                        float izz = 0.5f * mass * rr;
                        ix = ixy; iy = izz; iz = ixy;
                        break;
                    }
                default:
                    return Matrix4x4.Identity;
            }

            if (ix > 0f) ix = 1f / ix; else ix = 0f;
            if (iy > 0f) iy = 1f / iy; else iy = 0f;
            if (iz > 0f) iz = 1f / iz; else iz = 0f;

            return new Matrix4x4(
                 ix, 0, 0, 0,
                  0, iy, 0, 0,
                  0, 0, iz, 0,
                  0, 0, 0, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeImpulseDenominator(
            float invMassA, Matrix4x4 invInertiaA, Vector3 relPosA,
            float invMassB, Matrix4x4 invInertiaB, Vector3 relPosB,
            Vector3 normal)
        {
            var crossA = Cross3Sse(relPosA, normal);
            var crossB = Cross3Sse(relPosB, normal);
            var invIA_c = Vector3.TransformNormal(crossA, invInertiaA);
            var invIB_c = Vector3.TransformNormal(crossB, invInertiaB);
            return invMassA + invMassB + Dot3Sse(invIA_c, crossA) + Dot3Sse(invIB_c, crossB);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
        {
            var ab = b - a;
            float abLenSq = ab.LengthSquared();
            if (abLenSq < 1e-12f)
            {
                t = 0f;
                return a;
            }
            t = Dot3Sse(p - a, ab) / abLenSq;
            t = Math.Clamp(t, 0f, 1f);
            return a + t * ab;
        }

        public static void ClosestPointsSegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            const float EPSILON = 1e-6f;
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Dot3Sse(d1, d1);
            float e = Dot3Sse(d2, d2);
            float f = Dot3Sse(d2, r);

            float s, t;

            if (a <= EPSILON && e <= EPSILON)
            {
                c1 = p1;
                c2 = p2;
                return;
            }

            if (a <= EPSILON)
            {
                s = 0.0f;
                t = Math.Clamp(f / e, 0.0f, 1.0f);
            }
            else if (e <= EPSILON)
            {
                t = 0.0f;
                float c = Dot3Sse(d1, r);
                s = Math.Clamp(-c / a, 0.0f, 1.0f);
            }
            else
            {
                float b = Dot3Sse(d1, d2);
                float c = Dot3Sse(d1, r);
                float denom = a * e - b * b;

                if (denom > EPSILON)
                {
                    s = Math.Clamp((b * f - c * e) / denom, 0.0f, 1.0f);
                }
                else
                {
                    float t0 = Dot3Sse(p2 - p1, d1) / a;
                    float t1 = Dot3Sse(q2 - p1, d1) / a;
                    if (t0 > t1) { float temp = t0; t0 = t1; t1 = temp; }
                    float sMin = Math.Max(0.0f, t0);
                    float sMax = Math.Min(1.0f, t1);
                    s = sMin <= sMax ? (sMin + sMax) * 0.5f : (sMin > 1.0f ? 1.0f : 0.0f);
                }

                t = (b * s + f) / e;

                if (t < 0.0f)
                {
                    t = 0.0f;
                    s = Math.Clamp(-c / a, 0.0f, 1.0f);
                }
                else if (t > 1.0f)
                {
                    t = 1.0f;
                    s = Math.Clamp((b - c) / a, 0.0f, 1.0f);
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 QuaternionToEuler(Quaternion q)
        {
            float sinX = 2f * (q.W * q.X + q.Y * q.Z);
            float cosX = 1f - 2f * (q.X * q.X + q.Y * q.Y);
            float rx = MathF.Atan2(sinX, cosX);

            float sinY = 2f * (q.W * q.Y - q.Z * q.X);
            float ry = MathF.Abs(sinY) >= 1f ? MathF.CopySign(MathF.PI * 0.5f, sinY) : MathF.Asin(sinY);

            float sinZ = 2f * (q.W * q.Z + q.X * q.Y);
            float cosZ = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
            float rz = MathF.Atan2(sinZ, cosZ);

            if (float.IsNaN(rx) || float.IsInfinity(rx)) rx = 0f;
            if (float.IsNaN(ry) || float.IsInfinity(ry)) ry = 0f;
            if (float.IsNaN(rz) || float.IsInfinity(rz)) rz = 0f;

            return new Vector3(rx, ry, rz);
        }
    }
}
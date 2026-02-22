using System.Numerics;
using System.Runtime.CompilerServices;

namespace ObjLoader.Systems.MathUtils
{
    public static class MathUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BezierEval(float x1, float y1, float x2, float y2, float t)
        {
            if (MathF.Abs(x1 - y1) < 0.001f && MathF.Abs(x2 - y2) < 0.001f)
                return t;

            float ct = t;
            for (int i = 0; i < 8; i++)
            {
                float cx = 3f * x1 * ct * (1f - ct) * (1f - ct)
                         + 3f * x2 * ct * ct * (1f - ct)
                         + ct * ct * ct;
                float dx = cx - t;
                if (MathF.Abs(dx) < 1e-5f) break;
                float deriv = 3f * x1 * (1f - ct) * (1f - ct)
                            + 6f * (x2 - x1) * ct * (1f - ct)
                            + 3f * (1f - x2) * ct * ct;
                if (MathF.Abs(deriv) < 1e-6f) break;
                ct -= dx / deriv;
                ct = Math.Clamp(ct, 0f, 1f);
            }

            return 3f * y1 * ct * (1f - ct) * (1f - ct)
                 + 3f * y2 * ct * ct * (1f - ct)
                 + ct * ct * ct;
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
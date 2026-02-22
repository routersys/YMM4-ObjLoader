using System.Numerics;
using System.Runtime.CompilerServices;
using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Services.Mmd.Animation.Interfaces;
using ObjLoader.Services.Mmd.Parsers;

namespace ObjLoader.Services.Mmd.Animation
{
    public class DefaultMotionApplier : IMotionApplier
    {
        private const double MmdFps = 30.0;

        [ThreadStatic]
        private static ObjVertex[]? _skinBuffer;

        public List<CameraKeyframe> ConvertCameraFrames(VmdData vmdData)
        {
            var keyframes = new List<CameraKeyframe>();

            if (vmdData.CameraFrames.Count == 0)
                return keyframes;

            var sortedFrames = vmdData.CameraFrames.OrderBy(f => f.FrameNumber).ToList();

            foreach (var frame in sortedFrames)
            {
                double time = frame.FrameNumber / MmdFps;

                double distance = frame.Distance;
                double rx = frame.Rotation.X;
                double ry = frame.Rotation.Y;
                double rz = frame.Rotation.Z;

                double cosRx = Math.Cos(rx);
                double sinRx = Math.Sin(rx);
                double cosRy = Math.Cos(ry);
                double sinRy = Math.Sin(ry);

                double camOffX = -distance * sinRy * cosRx;
                double camOffY = distance * sinRx;
                double camOffZ = -distance * cosRy * cosRx;

                double camX = frame.Position.X + camOffX;
                double camY = frame.Position.Y + camOffY;
                double camZ = -(frame.Position.Z + camOffZ);

                double targetX = frame.Position.X;
                double targetY = frame.Position.Y;
                double targetZ = -frame.Position.Z;

                keyframes.Add(new CameraKeyframe
                {
                    Time = time,
                    CamX = camX,
                    CamY = camY,
                    CamZ = camZ,
                    TargetX = targetX,
                    TargetY = targetY,
                    TargetZ = targetZ,
                    Easing = new EasingData()
                });
            }

            return keyframes;
        }

        public double GetDuration(VmdData vmdData)
        {
            uint maxFrame = 0;

            if (vmdData.CameraFrames.Count > 0)
                maxFrame = Math.Max(maxFrame, vmdData.CameraFrames.Max(f => f.FrameNumber));

            if (vmdData.BoneFrames.Count > 0)
                maxFrame = Math.Max(maxFrame, vmdData.BoneFrames.Max(f => f.FrameNumber));

            if (vmdData.MorphFrames.Count > 0)
                maxFrame = Math.Max(maxFrame, vmdData.MorphFrames.Max(f => f.FrameNumber));

            return maxFrame / MmdFps;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public ObjVertex[] ApplySkinning(ObjVertex[] original, VertexBoneWeight[] weights, Matrix4x4[] boneTransforms)
        {
            int count = original.Length;

            if (_skinBuffer == null || _skinBuffer.Length != count)
                _skinBuffer = new ObjVertex[count];

            var result = _skinBuffer;
            int boneCount = boneTransforms.Length;

            for (int i = 0; i < count; i++)
            {
                ref readonly var src = ref original[i];
                ref readonly var bw = ref weights[i];
                Vector3 skinnedPos = Vector3.Zero;
                Vector3 skinnedNormal = Vector3.Zero;
                float totalWeight = 0f;

                int bi0 = bw.BoneIndex0;
                float w0 = bw.Weight0;
                if ((uint)bi0 < (uint)boneCount && w0 > 0f)
                {
                    ref readonly var mat = ref boneTransforms[bi0];
                    skinnedPos += Vector3.Transform(src.Position, mat) * w0;
                    skinnedNormal += Vector3.TransformNormal(src.Normal, mat) * w0;
                    totalWeight += w0;
                }

                int bi1 = bw.BoneIndex1;
                float w1 = bw.Weight1;
                if ((uint)bi1 < (uint)boneCount && w1 > 0f)
                {
                    ref readonly var mat = ref boneTransforms[bi1];
                    skinnedPos += Vector3.Transform(src.Position, mat) * w1;
                    skinnedNormal += Vector3.TransformNormal(src.Normal, mat) * w1;
                    totalWeight += w1;
                }

                int bi2 = bw.BoneIndex2;
                float w2 = bw.Weight2;
                if ((uint)bi2 < (uint)boneCount && w2 > 0f)
                {
                    ref readonly var mat = ref boneTransforms[bi2];
                    skinnedPos += Vector3.Transform(src.Position, mat) * w2;
                    skinnedNormal += Vector3.TransformNormal(src.Normal, mat) * w2;
                    totalWeight += w2;
                }

                int bi3 = bw.BoneIndex3;
                float w3 = bw.Weight3;
                if ((uint)bi3 < (uint)boneCount && w3 > 0f)
                {
                    ref readonly var mat = ref boneTransforms[bi3];
                    skinnedPos += Vector3.Transform(src.Position, mat) * w3;
                    skinnedNormal += Vector3.TransformNormal(src.Normal, mat) * w3;
                    totalWeight += w3;
                }

                float nLenSq = skinnedNormal.LengthSquared();
                if (nLenSq > 1e-12f)
                    skinnedNormal *= 1f / MathF.Sqrt(nLenSq);

                result[i] = new ObjVertex
                {
                    Position = skinnedPos,
                    Normal = skinnedNormal,
                    TexCoord = src.TexCoord,
                    Color = src.Color
                };
            }

            return result;
        }
    }

    public static class VmdMotionApplier
    {
        private static readonly IMotionApplier Applier = new DefaultMotionApplier();

        public static List<CameraKeyframe> ConvertCameraFrames(VmdData vmdData)
        {
            return Applier.ConvertCameraFrames(vmdData);
        }

        public static double GetDuration(VmdData vmdData)
        {
            return Applier.GetDuration(vmdData);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static ObjVertex[] ApplySkinning(ObjVertex[] original, VertexBoneWeight[] weights, Matrix4x4[] boneTransforms)
        {
            return Applier.ApplySkinning(original, weights, boneTransforms);
        }
    }
}
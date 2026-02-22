using System.Numerics;
using System.Runtime.CompilerServices;
using ObjLoader.Services.Mmd.Animation.Interfaces;
using ObjLoader.Services.Mmd.Physics.Interfaces;
using ObjLoader.Systems.MathUtils;
using ObjLoader.Systems.Models;

namespace ObjLoader.Systems.Animation
{
    public class GenericBoneAnimator : IAnimator
    {
        private readonly List<GenericBone> _bones;
        private readonly Dictionary<string, List<GenericBoneFrame>> _framesByBone;
        private readonly Matrix4x4[] _inverseBindPose;
        private readonly IPhysicsEngine? _physics;
        private double _lastTime = double.NaN;
        private bool _physicsInitialized;

        private readonly Matrix4x4[] _localTransforms;
        private readonly Matrix4x4[] _globalTransforms;
        private readonly Matrix4x4[] _result;
        private readonly int[] _evalOrder;

        public GenericBoneAnimator(List<GenericBone> bones, List<GenericBoneFrame> boneFrames, IPhysicsEngine? physics = null)
        {
            _bones = bones ?? new List<GenericBone>();

            int boneCount = _bones.Count;
            _framesByBone = new Dictionary<string, List<GenericBoneFrame>>(boneCount);

            if (boneFrames == null)
            {
                _inverseBindPose = Array.Empty<Matrix4x4>();
                _localTransforms = Array.Empty<Matrix4x4>();
                _globalTransforms = Array.Empty<Matrix4x4>();
                _result = Array.Empty<Matrix4x4>();
                _evalOrder = Array.Empty<int>();
                return;
            }

            foreach (var frame in boneFrames)
            {
                if (!_framesByBone.TryGetValue(frame.BoneName, out var list))
                {
                    list = new List<GenericBoneFrame>();
                    _framesByBone[frame.BoneName] = list;
                }
                list.Add(frame);
            }

            foreach (var kvp in _framesByBone)
            {
                kvp.Value.Sort((a, b) => a.FrameNumber.CompareTo(b.FrameNumber));
            }

            _inverseBindPose = new Matrix4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                _inverseBindPose[i] = Matrix4x4.CreateTranslation(-_bones[i].Position);
            }

            _localTransforms = new Matrix4x4[boneCount];
            _globalTransforms = new Matrix4x4[boneCount];
            _result = new Matrix4x4[boneCount];
            _evalOrder = new int[boneCount];

            int[] state = new int[boneCount];
            int orderIdx = 0;

            void Visit(int b)
            {
                if (state[b] == 2) return;
                if (state[b] == 1) return;
                state[b] = 1;

                int p = _bones[b].ParentIndex;
                if (p >= 0 && p < boneCount)
                {
                    Visit(p);
                }

                state[b] = 2;
                _evalOrder[orderIdx++] = b;
            }

            for (int i = 0; i < boneCount; i++)
            {
                Visit(i);
            }

            _physics = physics;
        }

        public unsafe Matrix4x4[] ComputeBoneTransforms(double timeSeconds)
        {
            int boneCount = _bones.Count;
            if (boneCount == 0) return Array.Empty<Matrix4x4>();

            uint frame = (uint)(timeSeconds * 30.0);
            float subFrame = (float)(timeSeconds * 30.0 - frame);

            for (int i = 0; i < boneCount; i++)
            {
                var bone = _bones[i];
                Vector3 translation = Vector3.Zero;
                Quaternion rotation = Quaternion.Identity;

                if (_framesByBone.TryGetValue(bone.Name, out var frames) && frames.Count > 0)
                {
                    InterpolateFrame(frames, frame, subFrame, out translation, out rotation);
                }

                Vector3 boneOffset;
                if (bone.ParentIndex >= 0 && bone.ParentIndex < boneCount)
                    boneOffset = bone.Position - _bones[bone.ParentIndex].Position;
                else
                    boneOffset = bone.Position;

                _localTransforms[i] =
                    Matrix4x4.CreateFromQuaternion(rotation) *
                    Matrix4x4.CreateTranslation(translation + boneOffset);
            }

            fixed (int* pOrder = _evalOrder)
            fixed (Matrix4x4* pLocal = _localTransforms)
            fixed (Matrix4x4* pGlobal = _globalTransforms)
            {
                for (int k = 0; k < boneCount; k++)
                {
                    int i = pOrder[k];
                    int parent = _bones[i].ParentIndex;
                    if (parent >= 0 && parent < boneCount)
                        pGlobal[i] = pLocal[i] * pGlobal[parent];
                    else
                        pGlobal[i] = pLocal[i];
                }
            }

            if (_physics != null)
            {
                double rawDt = double.IsNaN(_lastTime) ? 0.0 : timeSeconds - _lastTime;

                if (!_physicsInitialized || rawDt < 0.0 || rawDt > 0.5)
                {
                    _lastTime = timeSeconds;
                    _physics.Reset(_globalTransforms);

                    for (int i = 0; i < 30; i++)
                    {
                        _physics.Update(_globalTransforms, 1f / 60f);
                    }

                    _physics.ApplyToGlobalTransforms(_globalTransforms);
                    _physicsInitialized = true;
                }
                else if (rawDt > 0.0)
                {
                    _lastTime = timeSeconds;

                    int steps = (int)Math.Ceiling(rawDt * 60.0);
                    if (steps > 10) steps = 10;

                    float dt = (float)(rawDt / steps);

                    for (int s = 0; s < steps; s++)
                    {
                        _physics.Update(_globalTransforms, dt);
                    }

                    _physics.ApplyToGlobalTransforms(_globalTransforms);
                }
                else
                {
                    _physics.ApplyToGlobalTransforms(_globalTransforms);
                }

                if (_physicsInitialized)
                {
                    fixed (int* pOrder = _evalOrder)
                    fixed (Matrix4x4* pLocal = _localTransforms)
                    fixed (Matrix4x4* pGlobal = _globalTransforms)
                    {
                        for (int k = 0; k < boneCount; k++)
                        {
                            int i = pOrder[k];
                            if (_physics.IsPhysicsBone(i)) continue;

                            int parent = _bones[i].ParentIndex;
                            if (parent >= 0 && parent < boneCount)
                                pGlobal[i] = pLocal[i] * pGlobal[parent];
                            else
                                pGlobal[i] = pLocal[i];
                        }
                    }
                }
            }

            fixed (Matrix4x4* pInv = _inverseBindPose)
            fixed (Matrix4x4* pGlobal = _globalTransforms)
            fixed (Matrix4x4* pResult = _result)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    pResult[i] = pInv[i] * pGlobal[i];
                }
            }

            return _result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InterpolateFrame(List<GenericBoneFrame> frames, uint targetFrame, float subFrame, out Vector3 position, out Quaternion rotation)
        {
            if (frames.Count == 1)
            {
                position = frames[0].Position;
                rotation = frames[0].Rotation;
                return;
            }

            uint effectiveFrame = targetFrame;
            if (effectiveFrame <= frames[0].FrameNumber)
            {
                position = frames[0].Position;
                rotation = frames[0].Rotation;
                return;
            }

            int lastIdx = frames.Count - 1;
            if (effectiveFrame >= frames[lastIdx].FrameNumber)
            {
                position = frames[lastIdx].Position;
                rotation = frames[lastIdx].Rotation;
                return;
            }

            int idx = BinarySearchFrame(frames, effectiveFrame);
            var f0 = frames[idx];
            var f1 = frames[idx + 1];

            float range = f1.FrameNumber - f0.FrameNumber;
            float t = range > 0 ? ((effectiveFrame - f0.FrameNumber) + subFrame) / range : 0f;
            t = Math.Clamp(t, 0f, 1f);

            if (f1.Interpolation != null && f1.Interpolation.Length >= 64)
            {
                float txBez = MathUtility.BezierEval(
                    f1.Interpolation[0] / 127f, f1.Interpolation[4] / 127f,
                    f1.Interpolation[8] / 127f, f1.Interpolation[12] / 127f, t);
                float tyBez = MathUtility.BezierEval(
                    f1.Interpolation[1] / 127f, f1.Interpolation[5] / 127f,
                    f1.Interpolation[9] / 127f, f1.Interpolation[13] / 127f, t);
                float tzBez = MathUtility.BezierEval(
                    f1.Interpolation[2] / 127f, f1.Interpolation[6] / 127f,
                    f1.Interpolation[10] / 127f, f1.Interpolation[14] / 127f, t);
                float trBez = MathUtility.BezierEval(
                    f1.Interpolation[3] / 127f, f1.Interpolation[7] / 127f,
                    f1.Interpolation[11] / 127f, f1.Interpolation[15] / 127f, t);

                position = new Vector3(
                    f0.Position.X + (f1.Position.X - f0.Position.X) * txBez,
                    f0.Position.Y + (f1.Position.Y - f0.Position.Y) * tyBez,
                    f0.Position.Z + (f1.Position.Z - f0.Position.Z) * tzBez);
                rotation = Quaternion.Slerp(f0.Rotation, f1.Rotation, trBez);
            }
            else
            {
                position = Vector3.Lerp(f0.Position, f1.Position, t);
                rotation = Quaternion.Slerp(f0.Rotation, f1.Rotation, t);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BinarySearchFrame(List<GenericBoneFrame> frames, uint targetFrame)
        {
            int lo = 0;
            int hi = frames.Count - 2;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (frames[mid + 1].FrameNumber <= targetFrame)
                    lo = mid + 1;
                else if (frames[mid].FrameNumber > targetFrame)
                    hi = mid - 1;
                else
                    return mid;
            }
            return Math.Clamp(lo, 0, frames.Count - 2);
        }
    }
}
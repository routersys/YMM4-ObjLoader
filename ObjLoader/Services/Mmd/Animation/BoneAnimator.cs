using ObjLoader.Core.Mmd;
using ObjLoader.Services.Mmd.Adapters;
using ObjLoader.Services.Mmd.Animation.Interfaces;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.Services.Mmd.Physics;
using ObjLoader.Services.Mmd.Physics.Interfaces;
using ObjLoader.Systems.Animation;
using System.Numerics;

namespace ObjLoader.Services.Mmd.Animation
{
    public class BoneAnimator : IAnimator
    {
        private readonly GenericBoneAnimator _genericAnimator;

        public BoneAnimator(List<PmxBone> bones, List<VmdBoneFrame> boneFrames,
            List<PmxRigidBody>? rigidBodies = null, List<PmxJoint>? joints = null)
        {
            var genBones = MmdToGenericAdapter.ConvertBones(bones ?? new List<PmxBone>());
            var genFrames = MmdToGenericAdapter.ConvertBoneFrames(boneFrames ?? new List<VmdBoneFrame>());

            IPhysicsEngine? physicsEngine = null;
            if (rigidBodies != null && rigidBodies.Count > 0 && joints != null && joints.Count > 0)
            {
                physicsEngine = new MmdPhysics(bones!, rigidBodies, joints);
            }

            _genericAnimator = new GenericBoneAnimator(genBones, genFrames, physicsEngine);
        }

        public Matrix4x4[] ComputeBoneTransforms(double timeSeconds)
        {
            return _genericAnimator.ComputeBoneTransforms(timeSeconds);
        }
    }
}
using System.Numerics;
using ObjLoader.Core.Mmd;
using ObjLoader.Services.Mmd.Adapters;
using ObjLoader.Services.Mmd.Physics.Interfaces;
using ObjLoader.Systems.Physics;

namespace ObjLoader.Services.Mmd.Physics
{
    public class MmdPhysics : IPhysicsEngine
    {
        private readonly GenericPhysicsEngine _genericPhysics;

        public MmdPhysics(List<PmxBone> bones, List<PmxRigidBody> rigidBodies, List<PmxJoint> joints)
        {
            var genBones = MmdToGenericAdapter.ConvertBones(bones);
            var genRbs = MmdToGenericAdapter.ConvertRigidBodies(rigidBodies);
            var genJoints = MmdToGenericAdapter.ConvertJoints(joints);

            _genericPhysics = new GenericPhysicsEngine(genBones, genRbs, genJoints);
        }

        public void Reset(Matrix4x4[] globalBoneTransforms)
        {
            _genericPhysics.Reset(globalBoneTransforms);
        }

        public void Update(Matrix4x4[] globalBoneTransforms, float deltaTime)
        {
            _genericPhysics.Update(globalBoneTransforms, deltaTime);
        }

        public void ApplyToGlobalTransforms(Matrix4x4[] globalBoneTransforms)
        {
            _genericPhysics.ApplyToGlobalTransforms(globalBoneTransforms);
        }

        public bool IsPhysicsBone(int boneIndex)
        {
            return _genericPhysics.IsPhysicsBone(boneIndex);
        }
    }
}
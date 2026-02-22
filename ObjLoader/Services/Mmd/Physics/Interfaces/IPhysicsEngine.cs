using System.Numerics;

namespace ObjLoader.Services.Mmd.Physics.Interfaces
{
    public interface IPhysicsEngine
    {
        void Reset(Matrix4x4[] globalBoneTransforms);
        void Update(Matrix4x4[] globalBoneTransforms, float deltaTime);
        void ApplyToGlobalTransforms(Matrix4x4[] globalBoneTransforms);
        bool IsPhysicsBone(int boneIndex);
    }
}
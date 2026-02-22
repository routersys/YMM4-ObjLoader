using ObjLoader.Core.Mmd;
using ObjLoader.Services.Mmd.Parsers;
using ObjLoader.Systems.Models;

namespace ObjLoader.Services.Mmd.Adapters
{
    public static class MmdToGenericAdapter
    {
        public static List<GenericBone> ConvertBones(List<PmxBone> source)
        {
            var result = new List<GenericBone>(source.Count);
            foreach (var b in source)
            {
                result.Add(new GenericBone
                {
                    Name = b.Name,
                    ParentIndex = b.ParentIndex,
                    Position = b.Position
                });
            }
            return result;
        }

        public static List<GenericBoneFrame> ConvertBoneFrames(List<VmdBoneFrame> source)
        {
            var result = new List<GenericBoneFrame>(source.Count);
            foreach (var f in source)
            {
                result.Add(new GenericBoneFrame
                {
                    BoneName = f.BoneName,
                    FrameNumber = f.FrameNumber,
                    Position = f.Position,
                    Rotation = f.Rotation,
                    Interpolation = f.Interpolation
                });
            }
            return result;
        }

        public static List<GenericRigidBody> ConvertRigidBodies(List<PmxRigidBody> source)
        {
            var result = new List<GenericRigidBody>(source.Count);
            foreach (var rb in source)
            {
                result.Add(new GenericRigidBody
                {
                    Name = rb.Name,
                    BoneIndex = rb.BoneIndex,
                    CollisionGroup = rb.CollisionGroup,
                    CollisionMask = rb.CollisionMask,
                    ShapeType = rb.ShapeType,
                    ShapeSize = rb.ShapeSize,
                    Position = rb.Position,
                    Rotation = rb.Rotation,
                    Mass = rb.Mass,
                    LinearDamping = rb.LinearDamping,
                    AngularDamping = rb.AngularDamping,
                    Restitution = rb.Restitution,
                    Friction = rb.Friction,
                    PhysicsMode = rb.PhysicsMode
                });
            }
            return result;
        }

        public static List<GenericJoint> ConvertJoints(List<PmxJoint> source)
        {
            var result = new List<GenericJoint>(source.Count);
            foreach (var j in source)
            {
                result.Add(new GenericJoint
                {
                    Name = j.Name,
                    RigidBodyIndexA = j.RigidBodyIndexA,
                    RigidBodyIndexB = j.RigidBodyIndexB,
                    Position = j.Position,
                    Rotation = j.Rotation,
                    TranslationLimitMin = j.TranslationLimitMin,
                    TranslationLimitMax = j.TranslationLimitMax,
                    RotationLimitMin = j.RotationLimitMin,
                    RotationLimitMax = j.RotationLimitMax,
                    SpringTranslation = j.SpringTranslation,
                    SpringRotation = j.SpringRotation
                });
            }
            return result;
        }
    }
}
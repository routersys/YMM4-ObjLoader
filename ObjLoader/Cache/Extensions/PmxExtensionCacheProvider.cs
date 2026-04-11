using ObjLoader.Attributes;
using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using System.IO;
using System.Numerics;

namespace ObjLoader.Cache.Extensions
{
    [ExtensionCacheProvider]
    public class PmxExtensionCacheProvider : IExtensionCacheProvider
    {
        public string ProviderId => "PMX";

        public bool HasExtensionData(ObjModel model)
        {
            return model.Bones.Count > 0 || model.RigidBodies.Count > 0 || model.Joints.Count > 0 || model.Morphs.Count > 0 || model.DisplayFrames.Count > 0 || (model.BoneWeights != null && model.BoneWeights.Length > 0);
        }

        public unsafe void WriteExtensionData(BinaryWriter bw, ObjModel model)
        {
            bw.Write(model.Name ?? string.Empty);
            bw.Write(model.NameEn ?? string.Empty);
            bw.Write(model.Comment ?? string.Empty);
            bw.Write(model.CommentEn ?? string.Empty);

            bw.Write(model.Bones.Count);
            foreach (var b in model.Bones)
            {
                bw.Write(b.Name ?? string.Empty);
                bw.Write(b.NameEn ?? string.Empty);
                bw.Write(b.ParentIndex);
                bw.Write(b.Position.X);
                bw.Write(b.Position.Y);
                bw.Write(b.Position.Z);
                bw.Write(b.DeformLayer);
                bw.Write(b.BoneFlags);
                bw.Write(b.ConnectionIndex);
                bw.Write(b.ConnectionPosition.X);
                bw.Write(b.ConnectionPosition.Y);
                bw.Write(b.ConnectionPosition.Z);
                bw.Write(b.AdditionalParentIndex);
                bw.Write(b.AdditionalParentRatio);
                bw.Write(b.AxisX.X);
                bw.Write(b.AxisX.Y);
                bw.Write(b.AxisX.Z);
                bw.Write(b.AxisZ.X);
                bw.Write(b.AxisZ.Y);
                bw.Write(b.AxisZ.Z);
                bw.Write(b.ExportKey);

                if (b.IkData != null)
                {
                    bw.Write(true);
                    bw.Write(b.IkData.TargetIndex);
                    bw.Write(b.IkData.LoopCount);
                    bw.Write(b.IkData.LimitAngle);
                    bw.Write(b.IkData.Links.Count);
                    foreach (var link in b.IkData.Links)
                    {
                        bw.Write(link.BoneIndex);
                        bw.Write(link.HasLimit);
                        bw.Write(link.LimitMin.X);
                        bw.Write(link.LimitMin.Y);
                        bw.Write(link.LimitMin.Z);
                        bw.Write(link.LimitMax.X);
                        bw.Write(link.LimitMax.Y);
                        bw.Write(link.LimitMax.Z);
                    }
                }
                else
                {
                    bw.Write(false);
                }
            }

            bool hasWeights = model.BoneWeights != null && model.BoneWeights.Length > 0;
            bw.Write(hasWeights);
            if (hasWeights)
            {
                bw.Write(model.BoneWeights!.Length);
                fixed (VertexBoneWeight* pW = model.BoneWeights)
                {
                    var span = new ReadOnlySpan<byte>(pW, model.BoneWeights.Length * sizeof(VertexBoneWeight));
                    bw.Write(span);
                }
            }

            bw.Write(model.RigidBodies.Count);
            foreach (var r in model.RigidBodies)
            {
                bw.Write(r.Name ?? string.Empty);
                bw.Write(r.NameEn ?? string.Empty);
                bw.Write(r.BoneIndex);
                bw.Write(r.CollisionGroup);
                bw.Write(r.CollisionMask);
                bw.Write(r.ShapeType);
                bw.Write(r.ShapeSize.X);
                bw.Write(r.ShapeSize.Y);
                bw.Write(r.ShapeSize.Z);
                bw.Write(r.Position.X);
                bw.Write(r.Position.Y);
                bw.Write(r.Position.Z);
                bw.Write(r.Rotation.X);
                bw.Write(r.Rotation.Y);
                bw.Write(r.Rotation.Z);
                bw.Write(r.Mass);
                bw.Write(r.LinearDamping);
                bw.Write(r.AngularDamping);
                bw.Write(r.Restitution);
                bw.Write(r.Friction);
                bw.Write(r.PhysicsMode);
            }

            bw.Write(model.Joints.Count);
            foreach (var j in model.Joints)
            {
                bw.Write(j.Name ?? string.Empty);
                bw.Write(j.NameEn ?? string.Empty);
                bw.Write(j.RigidBodyIndexA);
                bw.Write(j.RigidBodyIndexB);
                bw.Write(j.Position.X);
                bw.Write(j.Position.Y);
                bw.Write(j.Position.Z);
                bw.Write(j.Rotation.X);
                bw.Write(j.Rotation.Y);
                bw.Write(j.Rotation.Z);
                bw.Write(j.TranslationLimitMin.X);
                bw.Write(j.TranslationLimitMin.Y);
                bw.Write(j.TranslationLimitMin.Z);
                bw.Write(j.TranslationLimitMax.X);
                bw.Write(j.TranslationLimitMax.Y);
                bw.Write(j.TranslationLimitMax.Z);
                bw.Write(j.RotationLimitMin.X);
                bw.Write(j.RotationLimitMin.Y);
                bw.Write(j.RotationLimitMin.Z);
                bw.Write(j.RotationLimitMax.X);
                bw.Write(j.RotationLimitMax.Y);
                bw.Write(j.RotationLimitMax.Z);
                bw.Write(j.SpringTranslation.X);
                bw.Write(j.SpringTranslation.Y);
                bw.Write(j.SpringTranslation.Z);
                bw.Write(j.SpringRotation.X);
                bw.Write(j.SpringRotation.Y);
                bw.Write(j.SpringRotation.Z);
            }

            if (model.Morphs != null)
            {
                bw.Write(model.Morphs.Count);
                foreach (var m in model.Morphs)
                {
                    bw.Write(m.Name ?? string.Empty);
                    bw.Write(m.NameEn ?? string.Empty);
                    bw.Write(m.Panel);
                    bw.Write(m.MorphType);
                    bw.Write(m.Offsets.Length);
                    bw.Write(m.Offsets);
                }
            }
            else
            {
                bw.Write(0);
            }

            if (model.DisplayFrames != null)
            {
                bw.Write(model.DisplayFrames.Count);
                foreach (var df in model.DisplayFrames)
                {
                    bw.Write(df.Name ?? string.Empty);
                    bw.Write(df.NameEn ?? string.Empty);
                    bw.Write(df.SpecialFlag);
                    bw.Write(df.Elements.Count);
                    foreach (var e in df.Elements)
                    {
                        bw.Write(e.ElementType);
                        bw.Write(e.Index);
                    }
                }
            }
            else
            {
                bw.Write(0);
            }
        }

        public unsafe void ReadExtensionData(BinaryReader br, ObjModel model)
        {
            model.Name = br.ReadString();
            model.NameEn = br.ReadString();
            model.Comment = br.ReadString();
            model.CommentEn = br.ReadString();

            int boneCount = br.ReadInt32();
            if (boneCount < 0) return;
            model.Bones = new List<PmxBone>(boneCount);
            for (int i = 0; i < boneCount; i++)
            {
                var b = new PmxBone
                {
                    Name = br.ReadString(),
                    NameEn = br.ReadString(),
                    ParentIndex = br.ReadInt32(),
                    Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    DeformLayer = br.ReadInt32(),
                    BoneFlags = br.ReadUInt16(),
                    ConnectionIndex = br.ReadInt32(),
                    ConnectionPosition = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    AdditionalParentIndex = br.ReadInt32(),
                    AdditionalParentRatio = br.ReadSingle(),
                    AxisX = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    AxisZ = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    ExportKey = br.ReadInt32()
                };

                bool hasIk = br.ReadBoolean();
                if (hasIk)
                {
                    b.IkData = new PmxIkData
                    {
                        TargetIndex = br.ReadInt32(),
                        LoopCount = br.ReadInt32(),
                        LimitAngle = br.ReadSingle()
                    };
                    int linkCount = br.ReadInt32();
                    for (int j = 0; j < linkCount; j++)
                    {
                        b.IkData.Links.Add(new PmxIkLink
                        {
                            BoneIndex = br.ReadInt32(),
                            HasLimit = br.ReadByte(),
                            LimitMin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                            LimitMax = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                        });
                    }
                }

                model.Bones.Add(b);
            }

            bool hasWeights = br.ReadBoolean();
            if (hasWeights)
            {
                int weightCount = br.ReadInt32();
                if (weightCount < 0) return;
                model.BoneWeights = GC.AllocateUninitializedArray<VertexBoneWeight>(weightCount, true);
                fixed (VertexBoneWeight* pW = model.BoneWeights)
                {
                    var span = new Span<byte>(pW, weightCount * sizeof(VertexBoneWeight));
                    int totalRead = 0;
                    while (totalRead < span.Length)
                    {
                        int read = br.BaseStream.Read(span.Slice(totalRead));
                        if (read == 0) break;
                        totalRead += read;
                    }
                }
            }

            int rbCount = br.ReadInt32();
            if (rbCount < 0) return;
            model.RigidBodies = new List<PmxRigidBody>(rbCount);
            for (int i = 0; i < rbCount; i++)
            {
                model.RigidBodies.Add(new PmxRigidBody
                {
                    Name = br.ReadString(),
                    NameEn = br.ReadString(),
                    BoneIndex = br.ReadInt32(),
                    CollisionGroup = br.ReadByte(),
                    CollisionMask = br.ReadUInt16(),
                    ShapeType = br.ReadByte(),
                    ShapeSize = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    Rotation = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    Mass = br.ReadSingle(),
                    LinearDamping = br.ReadSingle(),
                    AngularDamping = br.ReadSingle(),
                    Restitution = br.ReadSingle(),
                    Friction = br.ReadSingle(),
                    PhysicsMode = br.ReadByte()
                });
            }

            int jCount = br.ReadInt32();
            if (jCount < 0) return;
            model.Joints = new List<PmxJoint>(jCount);
            for (int i = 0; i < jCount; i++)
            {
                model.Joints.Add(new PmxJoint
                {
                    Name = br.ReadString(),
                    NameEn = br.ReadString(),
                    RigidBodyIndexA = br.ReadInt32(),
                    RigidBodyIndexB = br.ReadInt32(),
                    Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    Rotation = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    TranslationLimitMin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    TranslationLimitMax = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    RotationLimitMin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    RotationLimitMax = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    SpringTranslation = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                    SpringRotation = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())
                });
            }

            if (br.BaseStream.Position < br.BaseStream.Length)
            {
                int mCount = br.ReadInt32();
                if (mCount < 0) return;
                model.Morphs = new List<PmxMorph>(mCount);
                for (int i = 0; i < mCount; i++)
                {
                    string mName = br.ReadString();
                    string mNameEn = br.ReadString();
                    byte panel = br.ReadByte();
                    byte morphType = br.ReadByte();
                    int offsetLen = br.ReadInt32();
                    byte[] offsets = br.ReadBytes(offsetLen);

                    model.Morphs.Add(new PmxMorph
                    {
                        Name = mName,
                        NameEn = mNameEn,
                        Panel = panel,
                        MorphType = morphType,
                        Offsets = offsets
                    });
                }

                if (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int dfCount = br.ReadInt32();
                    if (dfCount < 0) return;
                    model.DisplayFrames = new List<PmxDisplayFrame>(dfCount);
                    for (int i = 0; i < dfCount; i++)
                    {
                        string dfName = br.ReadString();
                        string dfNameEn = br.ReadString();
                        byte sFlag = br.ReadByte();
                        int eCount = br.ReadInt32();
                        var elements = new List<PmxDisplayElement>();
                        for (int j = 0; j < eCount; j++)
                        {
                            elements.Add(new PmxDisplayElement
                            {
                                ElementType = br.ReadByte(),
                                Index = br.ReadInt32()
                            });
                        }

                        model.DisplayFrames.Add(new PmxDisplayFrame
                        {
                            Name = dfName,
                            NameEn = dfNameEn,
                            SpecialFlag = sFlag,
                            Elements = elements
                        });
                    }
                }
            }
        }
    }
}
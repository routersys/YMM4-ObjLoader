using ObjLoader.Attributes;
using ObjLoader.Core.Interfaces;
using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using System.IO;
using System.Numerics;
using System.Text;

namespace ObjLoader.Parsers
{
    [ModelParser(2, ".pmx")]
    public class PmxParser : IModelParser
    {
        public bool CanParse(string extension) => extension == ".pmx";

        public unsafe ObjModel Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            var magic = br.ReadBytes(4);
            if (Encoding.ASCII.GetString(magic) != "PMX ") return new ObjModel();

            float ver = br.ReadSingle();
            byte globalCount = br.ReadByte();
            var globals = br.ReadBytes(globalCount);

            if (globals.Length < 8)
            {
                var newGlobals = new byte[8];
                Array.Copy(globals, newGlobals, globals.Length);
                globals = newGlobals;
            }

            Encoding encoding = globals[0] == 0 ? Encoding.Unicode : Encoding.UTF8;
            int addUvCount = globals[1];
            int vertexIdxSize = globals[2];
            int textureIdxSize = globals[3];
            int materialIdxSize = globals[4];
            int boneIdxSize = globals[5];
            int morphIdxSize = globals[6];
            int rigidIdxSize = globals[7];

            int len = br.ReadInt32();
            string name = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

            len = br.ReadInt32();
            string nameEn = encoding.GetString(br.ReadBytes(len));

            len = br.ReadInt32();
            string comment = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

            len = br.ReadInt32();
            string commentEn = encoding.GetString(br.ReadBytes(len));

            int vCount = br.ReadInt32();
            var vertices = GC.AllocateUninitializedArray<ObjVertex>(vCount, true);
            var boneWeights = new VertexBoneWeight[vCount];

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 n = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector2 uv = new Vector2(br.ReadSingle(), br.ReadSingle());

                if (addUvCount > 0)
                {
                    int skip = addUvCount * 16;
                    for (int k = 0; k < skip; k++) br.ReadByte();
                }

                byte weightType = br.ReadByte();
                var bw = new VertexBoneWeight();

                switch (weightType)
                {
                    case 0:
                        bw.BoneIndex0 = ReadBoneIndex(br, boneIdxSize);
                        bw.Weight0 = 1.0f;
                        bw.BoneIndex1 = -1; bw.BoneIndex2 = -1; bw.BoneIndex3 = -1;
                        break;
                    case 1:
                        bw.BoneIndex0 = ReadBoneIndex(br, boneIdxSize);
                        bw.BoneIndex1 = ReadBoneIndex(br, boneIdxSize);
                        bw.Weight0 = br.ReadSingle();
                        bw.Weight1 = 1.0f - bw.Weight0;
                        bw.BoneIndex2 = -1; bw.BoneIndex3 = -1;
                        break;
                    case 2:
                    case 4:
                        bw.BoneIndex0 = ReadBoneIndex(br, boneIdxSize);
                        bw.BoneIndex1 = ReadBoneIndex(br, boneIdxSize);
                        bw.BoneIndex2 = ReadBoneIndex(br, boneIdxSize);
                        bw.BoneIndex3 = ReadBoneIndex(br, boneIdxSize);
                        bw.Weight0 = br.ReadSingle();
                        bw.Weight1 = br.ReadSingle();
                        bw.Weight2 = br.ReadSingle();
                        bw.Weight3 = br.ReadSingle();
                        break;
                    case 3:
                        bw.BoneIndex0 = ReadBoneIndex(br, boneIdxSize);
                        bw.BoneIndex1 = ReadBoneIndex(br, boneIdxSize);
                        bw.Weight0 = br.ReadSingle();
                        bw.Weight1 = 1.0f - bw.Weight0;
                        bw.BoneIndex2 = -1; bw.BoneIndex3 = -1;
                        for (int k = 0; k < 36; k++) br.ReadByte();
                        break;
                }

                boneWeights[i] = bw;

                float edge = br.ReadSingle();
                vertices[i] = new ObjVertex { Position = p, Normal = n, TexCoord = uv };
            }


            int iCount = br.ReadInt32();
            var indices = GC.AllocateUninitializedArray<int>(iCount, true);

            if (vertexIdxSize == 1)
            {
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadByte();
            }
            else if (vertexIdxSize == 2)
            {
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadUInt16();
            }
            else
            {
                for (int i = 0; i < iCount; i++) indices[i] = br.ReadInt32();
            }

            int tCount = br.ReadInt32();
            var texturePaths = new string[tCount];
            for (int i = 0; i < tCount; i++)
            {
                len = br.ReadInt32();
                var bytes = br.ReadBytes(len);
                string tPath = encoding.GetString(bytes);
                if (tPath.Contains("*")) tPath = "";
                else
                {
                    tPath = tPath.Replace('\\', Path.DirectorySeparatorChar);
                    if (!Path.IsPathRooted(tPath))
                        tPath = Path.Combine(Path.GetDirectoryName(path) ?? "", tPath);
                }
                texturePaths[i] = tPath;
            }

            int mCount = br.ReadInt32();
            var parts = new List<ModelPart>(mCount);
            int indexOffset = 0;

            for (int i = 0; i < mCount; i++)
            {
                len = br.ReadInt32();
                var mNameBytes = br.ReadBytes(len);
                string mName = encoding.GetString(mNameBytes).Trim().Replace("\0", "");
                len = br.ReadInt32();
                var mNameEn = br.ReadBytes(len);

                Vector4 diff = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                Vector3 spec = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float specPow = br.ReadSingle();
                Vector3 amb = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                byte drawMode = br.ReadByte();
                Vector4 edgeCol = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                float edgeSize = br.ReadSingle();

                int texIdx = -1;
                if (textureIdxSize == 1) texIdx = br.ReadSByte();
                else if (textureIdxSize == 2) texIdx = br.ReadInt16();
                else texIdx = br.ReadInt32();

                int sphereIdx = -1;
                if (textureIdxSize == 1) sphereIdx = br.ReadSByte();
                else if (textureIdxSize == 2) sphereIdx = br.ReadInt16();
                else sphereIdx = br.ReadInt32();

                byte sphereMode = br.ReadByte();
                byte sharedToon = br.ReadByte();

                if (sharedToon == 0)
                {
                    if (textureIdxSize == 1) br.ReadSByte();
                    else if (textureIdxSize == 2) br.ReadInt16();
                    else br.ReadInt32();
                }
                else
                {
                    br.ReadByte();
                }

                len = br.ReadInt32();
                var memo = br.ReadBytes(len);

                int faceCount = br.ReadInt32();

                string texPath = "";
                if (texIdx >= 0 && texIdx < tCount) texPath = texturePaths[texIdx];

                Vector3 partMin = new Vector3(float.MaxValue);
                Vector3 partMax = new Vector3(float.MinValue);

                if (indexOffset + faceCount <= indices.Length)
                {
                    for (int k = 0; k < faceCount; k++)
                    {
                        int vIdx = indices[indexOffset + k];
                        if (vIdx >= 0 && vIdx < vertices.Length)
                        {
                            var p = vertices[vIdx].Position;
                            partMin = Vector3.Min(partMin, p);
                            partMax = Vector3.Max(partMax, p);
                        }
                    }
                }

                parts.Add(new ModelPart
                {
                    Name = mName,
                    TexturePath = texPath,
                    IndexOffset = indexOffset,
                    IndexCount = faceCount,
                    BaseColor = diff,
                    Center = faceCount > 0 ? (partMin + partMax) * 0.5f : Vector3.Zero
                });

                indexOffset += faceCount;
            }

            ModelHelper.CalculateBounds(vertices, out Vector3 c, out float s);

            var bones = new List<PmxBone>();
            if (fs.Position < fs.Length)
            {
                try
                {
                    int boneCount = br.ReadInt32();
                    for (int i = 0; i < boneCount; i++)
                    {
                        len = br.ReadInt32();
                        string boneName = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

                        len = br.ReadInt32();
                        string boneNameEn = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

                        Vector3 bonePos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                        int parentIdx = -1;
                        if (boneIdxSize == 1) parentIdx = br.ReadSByte();
                        else if (boneIdxSize == 2) parentIdx = br.ReadInt16();
                        else parentIdx = br.ReadInt32();

                        int deformLayer = br.ReadInt32();

                        ushort boneFlags = br.ReadUInt16();

                        var pmxBone = new PmxBone
                        {
                            Name = boneName,
                            NameEn = boneNameEn,
                            ParentIndex = parentIdx,
                            Position = bonePos,
                            DeformLayer = deformLayer,
                            BoneFlags = boneFlags
                        };

                        if ((boneFlags & 0x0001) != 0)
                        {
                            if (boneIdxSize == 1) pmxBone.ConnectionIndex = br.ReadSByte();
                            else if (boneIdxSize == 2) pmxBone.ConnectionIndex = br.ReadInt16();
                            else pmxBone.ConnectionIndex = br.ReadInt32();
                        }
                        else
                        {
                            pmxBone.ConnectionPosition = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        }

                        if ((boneFlags & 0x0100) != 0 || (boneFlags & 0x0200) != 0)
                        {
                            if (boneIdxSize == 1) pmxBone.AdditionalParentIndex = br.ReadSByte();
                            else if (boneIdxSize == 2) pmxBone.AdditionalParentIndex = br.ReadInt16();
                            else pmxBone.AdditionalParentIndex = br.ReadInt32();
                            pmxBone.AdditionalParentRatio = br.ReadSingle();
                        }

                        if ((boneFlags & 0x0400) != 0)
                        {
                            pmxBone.AxisX = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        }

                        if ((boneFlags & 0x0800) != 0)
                        {
                            pmxBone.AxisX = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            pmxBone.AxisZ = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        }

                        if ((boneFlags & 0x2000) != 0)
                        {
                            pmxBone.ExportKey = br.ReadInt32();
                        }

                        if ((boneFlags & 0x0020) != 0)
                        {
                            pmxBone.IkData = new PmxIkData();
                            if (boneIdxSize == 1) pmxBone.IkData.TargetIndex = br.ReadSByte();
                            else if (boneIdxSize == 2) pmxBone.IkData.TargetIndex = br.ReadInt16();
                            else pmxBone.IkData.TargetIndex = br.ReadInt32();
                            pmxBone.IkData.LoopCount = br.ReadInt32();
                            pmxBone.IkData.LimitAngle = br.ReadSingle();
                            int ikLinkCount = br.ReadInt32();
                            for (int j = 0; j < ikLinkCount; j++)
                            {
                                var link = new PmxIkLink();
                                if (boneIdxSize == 1) link.BoneIndex = br.ReadSByte();
                                else if (boneIdxSize == 2) link.BoneIndex = br.ReadInt16();
                                else link.BoneIndex = br.ReadInt32();
                                link.HasLimit = br.ReadByte();
                                if (link.HasLimit != 0)
                                {
                                    link.LimitMin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                    link.LimitMax = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                                }
                                pmxBone.IkData.Links.Add(link);
                            }
                        }

                        bones.Add(pmxBone);
                    }
                }
                catch
                {
                }
            }

            var rigidBodies = new List<PmxRigidBody>();
            var joints = new List<PmxJoint>();
            var morphs = new List<PmxMorph>();
            var displayFrames = new List<PmxDisplayFrame>();

            if (fs.Position < fs.Length)
            {
                try
                {
                    morphs = ReadMorphSection(br, encoding, vertexIdxSize, boneIdxSize, materialIdxSize, morphIdxSize, rigidIdxSize);
                    displayFrames = ReadDisplayFrameSection(br, encoding, boneIdxSize, morphIdxSize);

                    int rbCount = br.ReadInt32();
                    for (int i = 0; i < rbCount; i++)
                    {
                        len = br.ReadInt32();
                        string rbName = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");
                        len = br.ReadInt32();
                        string rbNameEn = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

                        int rbBoneIdx = ReadBoneIndex(br, boneIdxSize);
                        byte group = br.ReadByte();
                        ushort mask = br.ReadUInt16();
                        byte shape = br.ReadByte();
                        Vector3 size = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        Vector3 pos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        Vector3 rot = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        float mass = br.ReadSingle();
                        float linearDamp = br.ReadSingle();
                        float angularDamp = br.ReadSingle();
                        float restitution = br.ReadSingle();
                        float friction = br.ReadSingle();
                        byte mode = br.ReadByte();

                        rigidBodies.Add(new PmxRigidBody
                        {
                            Name = rbName,
                            NameEn = rbNameEn,
                            BoneIndex = rbBoneIdx,
                            CollisionGroup = group,
                            CollisionMask = mask,
                            ShapeType = shape,
                            ShapeSize = size,
                            Position = pos,
                            Rotation = rot,
                            Mass = mass,
                            LinearDamping = linearDamp,
                            AngularDamping = angularDamp,
                            Restitution = restitution,
                            Friction = friction,
                            PhysicsMode = mode
                        });
                    }

                    if (fs.Position < fs.Length)
                    {
                        int jCount = br.ReadInt32();
                        for (int i = 0; i < jCount; i++)
                        {
                            len = br.ReadInt32();
                            string jName = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");
                            len = br.ReadInt32();
                            string jNameEn = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

                            byte jType = br.ReadByte();
                            int rbIdxA = ReadRigidBodyIndex(br, rigidIdxSize);
                            int rbIdxB = ReadRigidBodyIndex(br, rigidIdxSize);
                            Vector3 jPos = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 jRot = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 tMin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 tMax = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 rMin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 rMax = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 sTrans = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                            Vector3 sRot = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

                            joints.Add(new PmxJoint
                            {
                                Name = jName,
                                NameEn = jNameEn,
                                RigidBodyIndexA = rbIdxA,
                                RigidBodyIndexB = rbIdxB,
                                Position = jPos,
                                Rotation = jRot,
                                TranslationLimitMin = tMin,
                                TranslationLimitMax = tMax,
                                RotationLimitMin = rMin,
                                RotationLimitMax = rMax,
                                SpringTranslation = sTrans,
                                SpringRotation = sRot
                            });
                        }
                    }
                }
                catch
                {
                }
            }

            return new ObjModel { Vertices = vertices, Indices = indices, Parts = parts, ModelCenter = c, ModelScale = s, Name = name, NameEn = nameEn, Comment = comment, CommentEn = commentEn, Bones = bones, BoneWeights = boneWeights, Morphs = morphs, DisplayFrames = displayFrames, RigidBodies = rigidBodies, Joints = joints };
        }

        private static int ReadBoneIndex(BinaryReader br, int size)
        {
            if (size == 1) return br.ReadSByte();
            if (size == 2) return br.ReadInt16();
            return br.ReadInt32();
        }

        private static int ReadRigidBodyIndex(BinaryReader br, int size)
        {
            if (size == 1) return br.ReadSByte();
            if (size == 2) return br.ReadInt16();
            return br.ReadInt32();
        }

        private static void SkipIndex(BinaryReader br, int size)
        {
            if (size == 1) br.ReadSByte();
            else if (size == 2) br.ReadInt16();
            else br.ReadInt32();
        }

        private static List<PmxMorph> ReadMorphSection(BinaryReader br, Encoding encoding, int vertexIdxSize, int boneIdxSize, int materialIdxSize, int morphIdxSize, int rigidIdxSize)
        {
            var morphs = new List<PmxMorph>();
            int morphCount = br.ReadInt32();
            for (int i = 0; i < morphCount; i++)
            {
                int len = br.ReadInt32();
                string morphName = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");
                len = br.ReadInt32();
                string morphNameEn = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

                byte panel = br.ReadByte();
                byte morphType = br.ReadByte();
                int offsetCount = br.ReadInt32();

                long startPos = br.BaseStream.Position;

                for (int j = 0; j < offsetCount; j++)
                {
                    switch (morphType)
                    {
                        case 0:
                            SkipIndex(br, morphIdxSize);
                            br.ReadBytes(4);
                            break;
                        case 1:
                            SkipIndex(br, vertexIdxSize);
                            br.ReadBytes(12);
                            break;
                        case 2:
                            SkipIndex(br, boneIdxSize);
                            br.ReadBytes(28);
                            break;
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            SkipIndex(br, vertexIdxSize);
                            br.ReadBytes(16);
                            break;
                        case 8:
                            SkipIndex(br, materialIdxSize);
                            br.ReadBytes(113);
                            break;
                        case 9:
                            SkipIndex(br, morphIdxSize);
                            br.ReadBytes(4);
                            break;
                        case 10:
                            SkipIndex(br, rigidIdxSize);
                            br.ReadBytes(25);
                            break;
                    }
                }

                long sizeBytes = br.BaseStream.Position - startPos;
                br.BaseStream.Position = startPos;
                byte[] offsets = br.ReadBytes((int)sizeBytes);

                morphs.Add(new PmxMorph
                {
                    Name = morphName,
                    NameEn = morphNameEn,
                    Panel = panel,
                    MorphType = morphType,
                    Offsets = offsets
                });
            }
            return morphs;
        }

        private static List<PmxDisplayFrame> ReadDisplayFrameSection(BinaryReader br, Encoding encoding, int boneIdxSize, int morphIdxSize)
        {
            var frames = new List<PmxDisplayFrame>();
            int frameCount = br.ReadInt32();
            for (int i = 0; i < frameCount; i++)
            {
                int len = br.ReadInt32();
                string frameName = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");
                len = br.ReadInt32();
                string frameNameEn = encoding.GetString(br.ReadBytes(len)).Trim().Replace("\0", "");

                byte specialFlag = br.ReadByte();
                int elementCount = br.ReadInt32();

                var elements = new List<PmxDisplayElement>();
                for (int j = 0; j < elementCount; j++)
                {
                    byte elementType = br.ReadByte();
                    int index = -1;
                    if (elementType == 0)
                        index = ReadBoneIndex(br, boneIdxSize);
                    else
                        index = ReadBoneIndex(br, morphIdxSize);

                    elements.Add(new PmxDisplayElement { ElementType = elementType, Index = index });
                }

                frames.Add(new PmxDisplayFrame
                {
                    Name = frameName,
                    NameEn = frameNameEn,
                    SpecialFlag = specialFlag,
                    Elements = elements
                });
            }
            return frames;
        }
    }
}
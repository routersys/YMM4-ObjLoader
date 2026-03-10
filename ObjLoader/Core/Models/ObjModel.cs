using ObjLoader.Core.Mmd;
using System.Numerics;
using ObjLoader.Rendering.Mathematics;

namespace ObjLoader.Core.Models
{
    public class ObjModel
    {
        public ObjVertex[] Vertices { get; set; } = Array.Empty<ObjVertex>();
        public int[] Indices { get; set; } = Array.Empty<int>();
        public List<ModelPart> Parts { get; set; } = new List<ModelPart>();
        public Vector3 ModelCenter { get; set; }
        public float ModelScale { get; set; } = 1.0f;
        public string Name { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string CommentEn { get; set; } = string.Empty;
        public List<PmxBone> Bones { get; set; } = new List<PmxBone>();
        public VertexBoneWeight[]? BoneWeights { get; set; }
        public List<PmxMorph> Morphs { get; set; } = new List<PmxMorph>();
        public List<PmxDisplayFrame> DisplayFrames { get; set; } = new List<PmxDisplayFrame>();
        public List<PmxRigidBody> RigidBodies { get; set; } = new List<PmxRigidBody>();
        public List<PmxJoint> Joints { get; set; } = new List<PmxJoint>();
        public Task? ExtensionLoadTask { get; set; }
        public CullingBox LocalBoundingBox { get; set; }
    }
}
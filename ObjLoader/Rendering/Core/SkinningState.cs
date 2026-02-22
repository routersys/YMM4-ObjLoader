using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Rendering.Processors;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Core
{
    internal class SkinningState
    {
        public string FilePath { get; set; } = string.Empty;
        public ObjVertex[] OriginalVertices { get; set; } = [];
        public VertexBoneWeight[] BoneWeights { get; set; } = [];
        public ID3D11Buffer? DynamicVB { get; set; }
        public GpuSkinningProcessor? GpuProcessor { get; set; }
        public bool UseGpuSkinning { get; set; }
    }
}
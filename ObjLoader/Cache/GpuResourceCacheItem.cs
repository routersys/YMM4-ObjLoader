using System.Numerics;
using Vortice.Direct3D11;
using ObjLoader.Core;

namespace ObjLoader.Cache
{
    internal class GpuResourceCacheItem : IDisposable
    {
        public ID3D11Device Device { get; }
        public ID3D11Buffer VertexBuffer { get; }
        public ID3D11Buffer IndexBuffer { get; }
        public int IndexCount { get; }
        public ModelPart[] Parts { get; }
        public ID3D11ShaderResourceView?[] PartTextures { get; }
        public Vector3 ModelCenter { get; }
        public float ModelScale { get; }

        public GpuResourceCacheItem(
            ID3D11Device device,
            ID3D11Buffer vb,
            ID3D11Buffer ib,
            int indexCount,
            ModelPart[] parts,
            ID3D11ShaderResourceView?[] textures,
            Vector3 center,
            float scale)
        {
            Device = device;
            VertexBuffer = vb;
            IndexBuffer = ib;
            IndexCount = indexCount;
            Parts = parts;
            PartTextures = textures;
            ModelCenter = center;
            ModelScale = scale;
        }

        public void Dispose()
        {
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            if (PartTextures != null)
            {
                foreach (var tex in PartTextures)
                {
                    tex?.Dispose();
                }
            }
        }
    }
}
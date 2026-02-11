using System.Numerics;
using Vortice.Direct3D11;
using ObjLoader.Core;

namespace ObjLoader.Cache
{
    internal sealed class GpuResourceCacheItem : IDisposable
    {
        private int _disposed;
        private ID3D11ShaderResourceView?[]? _partTextures;

        public ID3D11Device Device { get; }
        public ID3D11Buffer VertexBuffer { get; }
        public ID3D11Buffer IndexBuffer { get; }
        public int IndexCount { get; }
        public ModelPart[] Parts { get; }
        public ID3D11ShaderResourceView?[] PartTextures => _partTextures!;
        public Vector3 ModelCenter { get; }
        public float ModelScale { get; }
        public long EstimatedGpuBytes { get; }

        public GpuResourceCacheItem(
            ID3D11Device device,
            ID3D11Buffer vb,
            ID3D11Buffer ib,
            int indexCount,
            ModelPart[] parts,
            ID3D11ShaderResourceView?[] textures,
            Vector3 center,
            float scale,
            long estimatedGpuBytes = 0)
        {
            Device = device ?? throw new ArgumentNullException(nameof(device));
            VertexBuffer = vb ?? throw new ArgumentNullException(nameof(vb));
            IndexBuffer = ib ?? throw new ArgumentNullException(nameof(ib));
            IndexCount = indexCount;
            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            _partTextures = textures ?? throw new ArgumentNullException(nameof(textures));
            ModelCenter = center;
            ModelScale = scale;
            EstimatedGpuBytes = estimatedGpuBytes;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            SafeDispose(VertexBuffer);
            SafeDispose(IndexBuffer);

            var textures = _partTextures;
            _partTextures = null;
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    SafeDispose(textures[i]);
                    textures[i] = null;
                }
            }

            GC.SuppressFinalize(this);
        }

        private static void SafeDispose(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        ~GpuResourceCacheItem()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            SafeDispose(VertexBuffer);
            SafeDispose(IndexBuffer);

            var textures = _partTextures;
            _partTextures = null;
            if (textures != null)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    SafeDispose(textures[i]);
                    textures[i] = null;
                }
            }
        }
    }
}
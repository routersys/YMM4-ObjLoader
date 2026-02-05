using System.Numerics;
using Vortice.Direct3D11;
using ObjLoader.Core;

namespace ObjLoader.Cache
{
    internal sealed class GpuResourceCacheItem : IDisposable
    {
        private bool _disposed;
        private readonly object _disposeLock = new object();

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
            Device = device ?? throw new ArgumentNullException(nameof(device));
            VertexBuffer = vb ?? throw new ArgumentNullException(nameof(vb));
            IndexBuffer = ib ?? throw new ArgumentNullException(nameof(ib));
            IndexCount = indexCount;
            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            PartTextures = textures ?? throw new ArgumentNullException(nameof(textures));
            ModelCenter = center;
            ModelScale = scale;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;

                if (disposing)
                {
                    SafeDispose(VertexBuffer);
                    SafeDispose(IndexBuffer);

                    if (PartTextures != null)
                    {
                        foreach (var tex in PartTextures)
                        {
                            SafeDispose(tex);
                        }
                    }
                }
            }
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
            Dispose(false);
        }
    }
}
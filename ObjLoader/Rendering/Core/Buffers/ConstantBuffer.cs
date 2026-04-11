using System.Runtime.InteropServices;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Core.Buffers
{
    internal sealed class ConstantBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly ID3D11Device _device;
        private ID3D11Buffer? _buffer;
        private bool _isDisposed;

        public ID3D11Buffer Buffer => _buffer ?? throw new ObjectDisposedException(nameof(ConstantBuffer<T>));

        public ConstantBuffer(ID3D11Device device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            CreateBuffer();
        }

        private void CreateBuffer()
        {
            int size = Marshal.SizeOf<T>();
            int alignedSize = (size + 15) & ~15;

            var cbDesc = new BufferDescription(
                alignedSize,
                BindFlags.ConstantBuffer,
                ResourceUsage.Dynamic,
                CpuAccessFlags.Write);

            _buffer = _device.CreateBuffer(cbDesc);
        }

        public void Update(ID3D11DeviceContext context, ref T data)
        {
            if (_isDisposed || _buffer == null) return;

            MappedSubresource mapped;
            context.Map(_buffer, 0, MapMode.WriteDiscard, MapFlags.None, out mapped);

            unsafe
            {
                System.Runtime.CompilerServices.Unsafe.Copy(mapped.DataPointer.ToPointer(), ref data);
            }

            context.Unmap(_buffer, 0);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _buffer?.Dispose();
            _buffer = null;
        }
    }
}
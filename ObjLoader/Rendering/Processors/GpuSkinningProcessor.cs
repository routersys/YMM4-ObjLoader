using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ObjLoader.Core.Mmd;
using ObjLoader.Core.Models;
using ObjLoader.Rendering.Shaders;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Rendering.Processors
{
    public sealed class GpuSkinningProcessor : IDisposable
    {
        private const int ThreadGroupSize = 256;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct GpuBoneWeight
        {
            public int BoneIndex0;
            public int BoneIndex1;
            public int BoneIndex2;
            public int BoneIndex3;
            public float Weight0;
            public float Weight1;
            public float Weight2;
            public float Weight3;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct SkinningConstants
        {
            public uint VertexCount;
            public uint BoneCount;
            public uint Pad0;
            public uint Pad1;
        }

        private ID3D11ComputeShader? _computeShader;
        private ID3D11Buffer? _boneBuffer;
        private ID3D11ShaderResourceView? _boneSrv;
        private ID3D11Buffer? _weightBuffer;
        private ID3D11ShaderResourceView? _weightSrv;
        private ID3D11Buffer? _inputVertexBuffer;
        private ID3D11ShaderResourceView? _inputVertexSrv;
        private ID3D11Buffer? _outputVertexBuffer;
        private ID3D11UnorderedAccessView? _outputUav;
        private ID3D11Buffer? _resultBuffer;
        private ID3D11Buffer? _constantBuffer;
        private int _lastVertexCount;
        private int _lastWeightCount;
        private int _lastWeightHash;
        private int _boneBufferCapacity;
        private bool _shaderCompiled;
        private bool _disposed;

        private static readonly ID3D11ShaderResourceView[] _nullSrvArray3 = new ID3D11ShaderResourceView[3];

        public bool IsAvailable => _shaderCompiled;

        public void Initialize(ID3D11Device device)
        {
            if (_shaderCompiled || _disposed) return;

            try
            {
                var byteCode = ShaderStore.GetGpuSkinningByteCode();
                _computeShader = device.CreateComputeShader(byteCode);

                _constantBuffer = device.CreateBuffer(new BufferDescription(
                    16, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));

                _shaderCompiled = true;
            }
            catch
            {
                _shaderCompiled = false;
            }
        }

        public unsafe ID3D11Buffer? ApplySkinning(
            ID3D11Device device, ID3D11DeviceContext context,
            ObjVertex[] originalVertices, VertexBoneWeight[] weights, Matrix4x4[] boneTransforms)
        {
            if (!_shaderCompiled || _disposed) return null;

            int vertexCount = originalVertices.Length;
            int boneCount = boneTransforms.Length;
            int vertexStride = Unsafe.SizeOf<ObjVertex>();
            int weightStride = Unsafe.SizeOf<GpuBoneWeight>();

            EnsureVertexBuffers(device, vertexCount, vertexStride);
            EnsureWeightBuffer(device, vertexCount, weightStride);
            EnsureBoneBuffer(device, boneCount);

            var mapped = context.Map(_boneBuffer!, MapMode.WriteDiscard);
            fixed (Matrix4x4* pBones = boneTransforms)
            {
                Buffer.MemoryCopy(pBones, (void*)mapped.DataPointer, _boneBufferCapacity * 64, boneCount * 64);
            }
            context.Unmap(_boneBuffer!, 0);

            mapped = context.Map(_inputVertexBuffer!, MapMode.WriteDiscard);
            fixed (ObjVertex* pVerts = originalVertices)
            {
                Buffer.MemoryCopy(pVerts, (void*)mapped.DataPointer, vertexCount * vertexStride, vertexCount * vertexStride);
            }
            context.Unmap(_inputVertexBuffer!, 0);

            int currentHash = originalVertices.GetHashCode();
            if (_lastWeightHash != currentHash || _lastWeightCount != vertexCount)
            {
                mapped = context.Map(_weightBuffer!, MapMode.WriteDiscard);
                fixed (VertexBoneWeight* pSrc = weights)
                {
                    GpuBoneWeight* pDst = (GpuBoneWeight*)mapped.DataPointer;
                    for (int i = 0; i < vertexCount; i++)
                    {
                        pDst[i].BoneIndex0 = pSrc[i].BoneIndex0;
                        pDst[i].BoneIndex1 = pSrc[i].BoneIndex1;
                        pDst[i].BoneIndex2 = pSrc[i].BoneIndex2;
                        pDst[i].BoneIndex3 = pSrc[i].BoneIndex3;
                        pDst[i].Weight0 = pSrc[i].Weight0;
                        pDst[i].Weight1 = pSrc[i].Weight1;
                        pDst[i].Weight2 = pSrc[i].Weight2;
                        pDst[i].Weight3 = pSrc[i].Weight3;
                    }
                }
                context.Unmap(_weightBuffer!, 0);
                _lastWeightCount = vertexCount;
                _lastWeightHash = currentHash;
            }

            mapped = context.Map(_constantBuffer!, MapMode.WriteDiscard);
            var constants = new SkinningConstants { VertexCount = (uint)vertexCount, BoneCount = (uint)boneCount };
            Unsafe.Copy(mapped.DataPointer.ToPointer(), ref constants);
            context.Unmap(_constantBuffer!, 0);

            context.CSSetShader(_computeShader);
            context.CSSetConstantBuffer(0, _constantBuffer);
            context.CSSetShaderResources(0, new ID3D11ShaderResourceView[] { _inputVertexSrv!, _weightSrv!, _boneSrv! });
            context.CSSetUnorderedAccessView(0, _outputUav);

            int groups = (vertexCount + ThreadGroupSize - 1) / ThreadGroupSize;
            context.Dispatch(groups, 1, 1);

            context.CSSetShader(null);
            context.CSSetUnorderedAccessView(0, null);
            context.CSSetShaderResources(0, _nullSrvArray3);

            context.CopyResource(_resultBuffer!, _outputVertexBuffer!);

            return _resultBuffer;
        }

        private void EnsureBoneBuffer(ID3D11Device device, int boneCount)
        {
            if (_boneBuffer != null && _boneBufferCapacity >= boneCount) return;

            _boneSrv?.Dispose();
            _boneBuffer?.Dispose();

            _boneBufferCapacity = Math.Max(256, boneCount);

            _boneBuffer = device.CreateBuffer(new BufferDescription(
                _boneBufferCapacity * 64, BindFlags.ShaderResource, ResourceUsage.Dynamic, CpuAccessFlags.Write,
                ResourceOptionFlags.BufferStructured, 64));

            _boneSrv = device.CreateShaderResourceView(_boneBuffer, new ShaderResourceViewDescription
            {
                Format = Format.Unknown,
                ViewDimension = Vortice.Direct3D.ShaderResourceViewDimension.Buffer,
                Buffer = new BufferShaderResourceView { FirstElement = 0, NumElements = _boneBufferCapacity }
            });
        }

        private void EnsureVertexBuffers(ID3D11Device device, int vertexCount, int vertexStride)
        {
            if (_inputVertexBuffer != null && _lastVertexCount == vertexCount) return;

            _inputVertexSrv?.Dispose();
            _inputVertexBuffer?.Dispose();
            _outputUav?.Dispose();
            _outputVertexBuffer?.Dispose();
            _resultBuffer?.Dispose();

            _inputVertexBuffer = device.CreateBuffer(new BufferDescription(
                vertexCount * vertexStride, BindFlags.ShaderResource, ResourceUsage.Dynamic, CpuAccessFlags.Write,
                ResourceOptionFlags.BufferStructured, vertexStride));

            _inputVertexSrv = device.CreateShaderResourceView(_inputVertexBuffer, new ShaderResourceViewDescription
            {
                Format = Format.Unknown,
                ViewDimension = Vortice.Direct3D.ShaderResourceViewDimension.Buffer,
                Buffer = new BufferShaderResourceView { FirstElement = 0, NumElements = vertexCount }
            });

            _outputVertexBuffer = device.CreateBuffer(new BufferDescription(
                vertexCount * vertexStride, BindFlags.UnorderedAccess, ResourceUsage.Default, CpuAccessFlags.None,
                ResourceOptionFlags.BufferStructured, vertexStride));

            _outputUav = device.CreateUnorderedAccessView(_outputVertexBuffer, new UnorderedAccessViewDescription
            {
                Format = Format.Unknown,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Buffer = new BufferUnorderedAccessView { FirstElement = 0, NumElements = vertexCount }
            });

            _resultBuffer = device.CreateBuffer(new BufferDescription(
                vertexCount * vertexStride, BindFlags.VertexBuffer, ResourceUsage.Default, CpuAccessFlags.None));

            _lastVertexCount = vertexCount;
        }

        private void EnsureWeightBuffer(ID3D11Device device, int vertexCount, int weightStride)
        {
            if (_weightBuffer != null && _lastWeightCount == vertexCount) return;

            _weightSrv?.Dispose();
            _weightBuffer?.Dispose();

            _weightBuffer = device.CreateBuffer(new BufferDescription(
                vertexCount * weightStride, BindFlags.ShaderResource, ResourceUsage.Dynamic, CpuAccessFlags.Write,
                ResourceOptionFlags.BufferStructured, weightStride));

            _weightSrv = device.CreateShaderResourceView(_weightBuffer, new ShaderResourceViewDescription
            {
                Format = Format.Unknown,
                ViewDimension = Vortice.Direct3D.ShaderResourceViewDimension.Buffer,
                Buffer = new BufferShaderResourceView { FirstElement = 0, NumElements = vertexCount }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _constantBuffer?.Dispose();
            _resultBuffer?.Dispose();
            _outputUav?.Dispose();
            _outputVertexBuffer?.Dispose();
            _inputVertexSrv?.Dispose();
            _inputVertexBuffer?.Dispose();
            _weightSrv?.Dispose();
            _weightBuffer?.Dispose();
            _boneSrv?.Dispose();
            _boneBuffer?.Dispose();
            _computeShader?.Dispose();
        }
    }
}
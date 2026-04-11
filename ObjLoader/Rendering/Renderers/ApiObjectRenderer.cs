using ObjLoader.Api.Draw;
using ObjLoader.Core.Models;
using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Rendering.Managers.Interfaces;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ObjLoader.Rendering.Renderers
{
    internal sealed class ApiObjectRenderer : IDisposable
    {
        private readonly D3DResources _resources;
        private readonly ID3D11Buffer _billboardVb;
        private readonly ID3D11Buffer _billboardIb;
        private readonly ConstantBuffer<CBPerFrame> _cbPerFrame;
        private readonly ConstantBuffer<CBPerObject> _cbPerObject;
        private readonly ConstantBuffer<CBPerMaterial> _cbPerMaterialCore;
        private readonly ConstantBuffer<CBSceneEffects> _cbSceneEffects;
        private readonly ConstantBuffer<CBPostEffects> _cbPostEffects;

        private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
        private readonly int[] _strideArray = new int[1];
        private readonly int[] _offsetArray = new int[1] { 0 };
        private readonly ID3D11ShaderResourceView[] _srvSlot0 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11Buffer[] _cbPerFrameArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerMaterialArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbSceneEffectsArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPostEffectsArray = new ID3D11Buffer[1];
        private readonly ID3D11ShaderResourceView[] _nullSrv1 = new ID3D11ShaderResourceView[1];

        private static readonly Matrix4x4[] _emptyLightViewProjs = new Matrix4x4[D3DResources.CascadeCount];
        private static readonly float[] _emptyCascadeSplits = new float[4];
        private static readonly Color4 _clearBlend = new Color4(0, 0, 0, 0);

        private bool _isDisposed;

        public ApiObjectRenderer(ID3D11Device device, D3DResources resources)
        {
            _resources = resources;

            float hw = 0.5f, hh = 0.5f;
            var bVerts = new ObjVertex[]
            {
                new ObjVertex { Position = new Vector3(-hw,  hh, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(0,0) },
                new ObjVertex { Position = new Vector3( hw,  hh, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(1,0) },
                new ObjVertex { Position = new Vector3(-hw, -hh, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(0,1) },
                new ObjVertex { Position = new Vector3( hw, -hh, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(1,1) }
            };
            var bInds = new int[] { 0, 1, 2, 2, 1, 3 };

            var vDesc = new BufferDescription(Unsafe.SizeOf<ObjVertex>() * bVerts.Length, BindFlags.VertexBuffer, ResourceUsage.Immutable);
            var iDesc = new BufferDescription(sizeof(int) * bInds.Length, BindFlags.IndexBuffer, ResourceUsage.Immutable);

            unsafe
            {
                fixed (ObjVertex* pV = bVerts) _billboardVb = device.CreateBuffer(vDesc, new SubresourceData(pV));
                fixed (int* pI = bInds) _billboardIb = device.CreateBuffer(iDesc, new SubresourceData(pI));
            }

            _cbPerFrame = new ConstantBuffer<CBPerFrame>(device);
            _cbPerObject = new ConstantBuffer<CBPerObject>(device);
            _cbPerMaterialCore = new ConstantBuffer<CBPerMaterial>(device);
            _cbSceneEffects = new ConstantBuffer<CBSceneEffects>(device);
            _cbPostEffects = new ConstantBuffer<CBPostEffects>(device);
        }

        private void BindMaterialBuffers(ID3D11DeviceContext context, int worldId, Vector4 baseColor, bool lightEnabled, float diffuse, float shininess, float roughness, float metallic)
        {
            ConstantBufferFactory.CreatePerMaterial(worldId, baseColor, lightEnabled, diffuse, shininess, roughness, metallic, out var cbCore, out var cbScene, out var cbPost);
            _cbPerMaterialCore.Update(context, ref cbCore);
            _cbSceneEffects.Update(context, ref cbScene);
            _cbPostEffects.Update(context, ref cbPost);
            _cbPerMaterialArray[0] = _cbPerMaterialCore.Buffer;
            _cbSceneEffectsArray[0] = _cbSceneEffects.Buffer;
            _cbPostEffectsArray[0] = _cbPostEffects.Buffer;
            context.PSSetConstantBuffers(RenderingConstants.CbSlotPerMaterial, 1, _cbPerMaterialArray);
            context.PSSetConstantBuffers(RenderingConstants.CbSlotSceneEffects, 1, _cbSceneEffectsArray);
            context.PSSetConstantBuffers(RenderingConstants.CbSlotPostEffects, 1, _cbPostEffectsArray);
        }

        public void RenderApiObjects(ID3D11DeviceContext context, ISceneDrawManager drawManager, Matrix4x4 viewProj, Vector4 camPos, Matrix4x4[]? lightViewProjs, float[]? cascadeSplits, bool shadowValid, int activeWorldId, bool bindEnvironment)
        {
            if (_isDisposed) return;
            var externalObjects = (List<ExternalObjectHandle>)drawManager.GetExternalObjects();
            if (externalObjects.Count == 0) return;

            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(_resources.PixelShader);
            context.IASetInputLayout(_resources.InputLayout);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            var lightPos = new Vector4(0, 10, 0, 1.0f);
            var amb = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
            var lCol = new Vector4(1, 1, 1, 1);

            CBPerFrame cbFrame = ConstantBufferFactory.CreatePerFrameForScene(
                viewProj, camPos, lightPos, amb, lCol,
                lightViewProjs ?? _emptyLightViewProjs, cascadeSplits ?? _emptyCascadeSplits,
                0, shadowValid, activeWorldId, 0, bindEnvironment);

            _cbPerFrame.Update(context, ref cbFrame);
            _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            context.VSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, _cbPerFrameArray);
            context.PSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, _cbPerFrameArray);

            for (int i = 0; i < externalObjects.Count; i++)
            {
                var obj = externalObjects[i];
                if (!obj.IsVisible || string.IsNullOrEmpty(obj.Descriptor.FilePath)) continue;

                string cacheKey = "export:" + obj.Descriptor.FilePath;
                if (!ObjLoader.Cache.Gpu.GpuResourceCache.Instance.TryGetValue(cacheKey, out var resource) || resource == null || resource.VertexBuffer == null || resource.IndexBuffer == null)
                {
                    continue;
                }

                var world = obj.CurrentTransform.ToMatrix();
                var wvp = world * viewProj;
                CBPerObject cbObject = new CBPerObject { WorldViewProj = Matrix4x4.Transpose(wvp), World = Matrix4x4.Transpose(world) };
                _cbPerObject.Update(context, ref cbObject);
                _cbPerObjectArray[0] = _cbPerObject.Buffer;
                context.VSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, _cbPerObjectArray);

                _vbArray[0] = resource.VertexBuffer;
                _strideArray[0] = Unsafe.SizeOf<ObjVertex>();
                context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                for (int p = 0; p < resource.Parts.Length; p++)
                {
                    var part = resource.Parts[p];
                    var texView = resource.PartTextures[p];

                    _srvSlot0[0] = texView ?? _resources.WhiteTextureView!;
                    context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                    BindMaterialBuffers(context, 0, texView != null ? Vector4.One : part.BaseColor, true, 1.0f, 32.0f, 0.5f, 0.0f);

                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }
            context.PSSetShaderResources(0, 1, _nullSrv1);
        }

        public void RenderBillboards(ID3D11DeviceContext context, ISceneDrawManager drawManager, Matrix4x4 viewProj, Vector4 camPos, Matrix4x4[]? lightViewProjs, float[]? cascadeSplits, bool shadowValid, int activeWorldId, bool bindEnvironment)
        {
            if (_isDisposed) return;
            var billboards = (List<(Api.Core.SceneObjectId Id, BillboardDescriptor Desc)>)drawManager.GetBillboards();
            if (billboards.Count == 0) return;

            context.OMSetDepthStencilState(_resources.DepthStencilState);
            context.OMSetBlendState(_resources.BillboardBlendState, _clearBlend, -1);

            context.IASetInputLayout(_resources.InputLayout);
            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(_resources.PixelShader);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            int stride = Unsafe.SizeOf<ObjVertex>();
            _vbArray[0] = _billboardVb;
            _strideArray[0] = stride;
            context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
            context.IASetIndexBuffer(_billboardIb, Format.R32_UInt, 0);

            var lightPos = new Vector4(0, 10, 0, 1.0f);
            var amb = new Vector4(1, 1, 1, 1);
            var lCol = new Vector4(1, 1, 1, 1);

            CBPerFrame cbFrame = ConstantBufferFactory.CreatePerFrameForScene(
                viewProj, camPos, lightPos, amb, lCol,
                lightViewProjs ?? _emptyLightViewProjs, cascadeSplits ?? _emptyCascadeSplits,
                0, shadowValid, activeWorldId, 0, bindEnvironment);

            _cbPerFrame.Update(context, ref cbFrame);
            _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            context.VSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, _cbPerFrameArray);
            context.PSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, _cbPerFrameArray);

            Vector3 camPos3 = new Vector3(camPos.X, camPos.Y, camPos.Z);

            for (int i = 0; i < billboards.Count; i++)
            {
                var b = billboards[i];
                if (b.Desc.Opacity <= 0) continue;
                var srv = drawManager.GetBillboardSrv(b.Id);
                if (srv == null) continue;

                Matrix4x4 world;
                var radX = b.Desc.Rotation.X * (float)(Math.PI / 180.0);
                var radY = b.Desc.Rotation.Y * (float)(Math.PI / 180.0);
                var radZ = b.Desc.Rotation.Z * (float)(Math.PI / 180.0);
                var rotMatrix = Matrix4x4.CreateFromYawPitchRoll(radY, radX, radZ);

                if (b.Desc.FaceCamera)
                {
                    world = Matrix4x4.CreateScale(b.Desc.Size.X, b.Desc.Size.Y, 1.0f) * rotMatrix * Matrix4x4.CreateBillboard(b.Desc.WorldPosition, camPos3, Vector3.UnitY, Vector3.UnitZ);
                }
                else
                {
                    world = Matrix4x4.CreateScale(b.Desc.Size.X, b.Desc.Size.Y, 1.0f) * rotMatrix * Matrix4x4.CreateTranslation(b.Desc.WorldPosition);
                }

                var wvp = world * viewProj;
                CBPerObject cbObject = new CBPerObject { WorldViewProj = Matrix4x4.Transpose(wvp), World = Matrix4x4.Transpose(world) };
                _cbPerObject.Update(context, ref cbObject);
                _cbPerObjectArray[0] = _cbPerObject.Buffer;
                context.VSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, _cbPerObjectArray);

                _srvSlot0[0] = srv;
                context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                var pCol = b.Desc.BlendColor * b.Desc.Opacity;
                BindMaterialBuffers(context, 0, pCol, false, 1.0f, 32.0f, 0.5f, 0.0f);

                context.DrawIndexed(6, 0, 0);
            }

            context.PSSetShaderResources(0, 1, _nullSrv1);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _billboardVb?.Dispose();
            _billboardIb?.Dispose();
            _cbPerFrame.Dispose();
            _cbPerObject.Dispose();
            _cbPerMaterialCore.Dispose();
            _cbSceneEffects.Dispose();
            _cbPostEffects.Dispose();
        }
    }
}
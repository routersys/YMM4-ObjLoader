using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Plugin;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace ObjLoader.Rendering
{
    internal class ShadowRenderer
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly D3DResources _resources;

        private readonly ID3D11Buffer[] _cbArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
        private readonly int[] _strideArray = new int[1];
        private readonly int[] _offsetArray = new int[] { 0 };

        private readonly ID3D11ShaderResourceView[] _nullSrv1 = new ID3D11ShaderResourceView[1];

        public ShadowRenderer(IGraphicsDevicesAndContext devices, D3DResources resources)
        {
            _devices = devices;
            _resources = resources;
        }

        public void Render(
            List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> layers,
            Matrix4x4[] lightViewProjs,
            int activeWorldId,
            Dictionary<string, LayerState> layerStates)
        {
            if (_resources.ShadowMapDSVs == null || _resources.ShadowMapDSVs.Length == 0) return;
            if (_resources.ConstantBuffer == null) return;

            var context = _devices.D3D.Device.ImmediateContext;

            context.PSSetShaderResources(1, 1, _nullSrv1);

            context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);

            context.RSSetState(_resources.ShadowRasterizerState);
            context.OMSetDepthStencilState(_resources.DepthStencilState);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(_resources.InputLayout);
            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(null);

            if (_resources.ConstantBuffer != null)
            {
                _cbArray[0] = _resources.ConstantBuffer;
            }

            int cascadeCount = Math.Min(D3DResources.CascadeCount, _resources.ShadowMapDSVs.Length);
            int resolution = _resources.CurrentShadowMapSize;

            for (int cascade = 0; cascade < cascadeCount; cascade++)
            {
                var dsv = _resources.ShadowMapDSVs[cascade];
                if (dsv == null) continue;

                context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), dsv);
                context.ClearDepthStencilView(dsv, DepthStencilClearFlags.Depth, 1.0f, 0);
                context.RSSetViewport(0, 0, resolution, resolution);

                var lightViewProj = lightViewProjs[cascade];

                foreach (var item in layers)
                {
                    var state = item.State;
                    var resource = item.Resource;

                    if (state.WorldId != activeWorldId) continue;

                    Matrix4x4 hierarchyMatrix = RenderUtils.GetLayerTransform(state);
                    var currentGuid = state.ParentGuid;
                    int depth = 0;
                    while (!string.IsNullOrEmpty(currentGuid) && layerStates.TryGetValue(currentGuid, out var parentState))
                    {
                        hierarchyMatrix *= RenderUtils.GetLayerTransform(parentState);
                        currentGuid = parentState.ParentGuid;
                        depth++;
                        if (depth > 100) break;
                    }

                    var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);
                    var world = normalize * hierarchyMatrix;
                    var wvp = world * lightViewProj;

                    int stride = Unsafe.SizeOf<ObjVertex>();
                    _vbArray[0] = resource.VertexBuffer;
                    _strideArray[0] = stride;
                    context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                    context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                    ConstantBufferData cbData = new ConstantBufferData
                    {
                        WorldViewProj = Matrix4x4.Transpose(wvp),
                        World = Matrix4x4.Transpose(world)
                    };

                    MappedSubresource mapped;
                    context.Map(_resources.ConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None, out mapped);
                    unsafe
                    {
                        Unsafe.Copy(mapped.DataPointer.ToPointer(), ref cbData);
                    }
                    context.Unmap(_resources.ConstantBuffer, 0);

                    context.VSSetConstantBuffers(0, _cbArray);

                    for (int p = 0; p < resource.Parts.Length; p++)
                    {
                        if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(p)) continue;

                        var part = resource.Parts[p];
                        if (part.BaseColor.W >= 0.99f)
                        {
                            context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                        }
                    }
                }
            }

            context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);

            context.Flush();
        }
    }
}
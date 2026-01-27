using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Settings;
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

        public ShadowRenderer(IGraphicsDevicesAndContext devices, D3DResources resources)
        {
            _devices = devices;
            _resources = resources;
        }

        public void Render(List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> layers, Matrix4x4[] lightViewProjs, int activeWorldId, Dictionary<string, LayerState> layerStates)
        {
            if (_resources.ShadowMapDSVs == null || _resources.ConstantBuffer == null) return;

            var context = _devices.D3D.Device.ImmediateContext;
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(null!);
            context.IASetInputLayout(_resources.InputLayout);
            context.RSSetState(_resources.ShadowRasterizerState);

            var size = PluginSettings.Instance.ShadowResolution;
            context.RSSetViewport(0, 0, size, size);

            for (int cascadeIdx = 0; cascadeIdx < D3DResources.CascadeCount; cascadeIdx++)
            {
                var dsv = _resources.ShadowMapDSVs[cascadeIdx];
                if (dsv == null) continue;

                context.ClearDepthStencilView(dsv, DepthStencilClearFlags.Depth, 1.0f, 0);
                context.OMSetRenderTargets((ID3D11RenderTargetView?)null!, dsv);

                var viewProj = lightViewProjs[cascadeIdx];

                foreach (var item in layers)
                {
                    if (item.State.WorldId != activeWorldId) continue;

                    var resource = item.Resource;
                    Matrix4x4 hierarchyMatrix = RenderUtils.GetLayerTransform(item.State);
                    var currentGuid = item.State.ParentGuid;
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

                    ConstantBufferData cbData = new ConstantBufferData();
                    cbData.WorldViewProj = Matrix4x4.Transpose(world * viewProj);
                    cbData.World = Matrix4x4.Transpose(world);

                    MappedSubresource mapped;
                    context.Map(_resources.ConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None, out mapped);
                    unsafe
                    {
                        Unsafe.Copy(mapped.DataPointer.ToPointer(), ref cbData);
                    }
                    context.Unmap(_resources.ConstantBuffer, 0);

                    context.VSSetConstantBuffers(0, new[] { _resources.ConstantBuffer });

                    int stride = Unsafe.SizeOf<ObjVertex>();
                    context.IASetVertexBuffers(0, 1, new[] { resource.VertexBuffer }, new[] { stride }, new[] { 0 });
                    context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                    for (int i = 0; i < resource.Parts.Length; i++)
                    {
                        if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;
                        var part = resource.Parts[i];
                        if (part.BaseColor.W >= 0.99f)
                        {
                            context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                        }
                    }
                }
            }
        }
    }
}
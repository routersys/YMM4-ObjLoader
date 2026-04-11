using ObjLoader.Core.Models;
using ObjLoader.Rendering.Core;
using ObjLoader.Settings;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Services.Rendering.Passes;

internal sealed class OpaqueRenderPass : IRenderPass
{
    private readonly ID3D11ShaderResourceView[] _nullSrv = new ID3D11ShaderResourceView[1];
    private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
    private readonly int[] _strideArray = new int[1];
    private readonly int[] _offsetArray = new int[] { 0 };

    public void Render(in RenderPassContext context)
    {
        if (context.IsWireframe) return;

        var settings = PluginSettings.Instance;

        context.DeviceContext.VSSetShader(context.Resources.VertexShader);
        context.DeviceContext.PSSetShader(context.Resources.PixelShader);
        context.DeviceContext.PSSetSamplers(RenderingConstants.SlotStandardSampler, context.SamplerArray);

        if (context.RenderShadowMap && context.ShadowSrvArray[0] != null)
        {
            context.DeviceContext.PSSetShaderResources(RenderingConstants.SlotShadowMap, context.ShadowSrvArray);
            context.DeviceContext.PSSetSamplers(RenderingConstants.SlotShadowSampler, context.ShadowSamplerArray);
        }
        else
        {
            context.DeviceContext.PSSetShaderResources(RenderingConstants.SlotShadowMap, _nullSrv);
        }

        context.DeviceContext.IASetInputLayout(context.Resources.InputLayout);
        context.DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        int lastLayerIndex = -1;
        for (int i = 0; i < context.OpaqueParts.Count; i++)
        {
            var partsInfo = context.OpaqueParts[i];
            int layerIndex = partsInfo.LayerIndex;
            int partIndex = partsInfo.PartIndex;
            var layer = context.Layers[layerIndex];
            var modelResource = layer.Resource;

            if (layerIndex != lastLayerIndex)
            {
                int stride = Unsafe.SizeOf<ObjVertex>();
                _vbArray[0] = layer.OverrideVB ?? modelResource.VertexBuffer!;
                _strideArray[0] = stride;
                context.DeviceContext.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.DeviceContext.IASetIndexBuffer(modelResource.IndexBuffer, Format.R32_UInt, 0);
                lastLayerIndex = layerIndex;
            }

            PartDrawHelper.DrawPart(
                context,
                layer,
                modelResource,
                partIndex,
                context.LayerWorlds[layerIndex],
                context.LayerWvps[layerIndex]);
        }
    }
}
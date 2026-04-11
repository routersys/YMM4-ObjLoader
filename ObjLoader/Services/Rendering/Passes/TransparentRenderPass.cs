using ObjLoader.Core.Models;
using System.Runtime.CompilerServices;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Services.Rendering.Passes;

internal sealed class TransparentRenderPass : IRenderPass
{
    private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
    private readonly int[] _strideArray = new int[1];
    private readonly int[] _offsetArray = new int[] { 0 };

    public void Render(in RenderPassContext context)
    {
        if (context.TransparentParts.Count == 0) return;

        if (context.TransparentParts.Count > 0)
        {
            context.TransparentParts.Sort();
        }

        context.DeviceContext.OMSetDepthStencilState(context.Resources.DepthStencilStateNoWrite);
        if (!context.IsWireframe)
        {
            context.DeviceContext.RSSetState(context.Resources.CullNoneRasterizerState);
        }

        int lastLayerIndex = -1;
        for (int i = 0; i < context.TransparentParts.Count; i++)
        {
            var tp = context.TransparentParts[i];
            var layer = context.Layers[tp.LayerIndex];
            if (layer.VisibleParts != null && !layer.VisibleParts.Contains(tp.PartIndex)) continue;

            var resource = layer.Resource;

            if (tp.LayerIndex != lastLayerIndex)
            {
                int stride = Unsafe.SizeOf<ObjVertex>();
                _vbArray[0] = layer.OverrideVB ?? resource.VertexBuffer!;
                _strideArray[0] = stride;
                context.DeviceContext.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.DeviceContext.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);
                lastLayerIndex = tp.LayerIndex;
            }

            PartDrawHelper.DrawPart(
                context,
                layer,
                resource,
                tp.PartIndex,
                context.LayerWorlds[tp.LayerIndex],
                context.LayerWvps[tp.LayerIndex]);
        }

        context.DeviceContext.OMSetDepthStencilState(context.Resources.DepthStencilState);
        if (!context.IsWireframe)
        {
            context.DeviceContext.RSSetState(context.Resources.RasterizerState);
        }
    }
}
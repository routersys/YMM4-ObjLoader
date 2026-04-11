using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Core.Buffers;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Services.Rendering.Passes;

internal sealed class GridRenderPass : IRenderPass
{
    private readonly ID3D11Buffer[] _gridVbArray = new ID3D11Buffer[1];
    private readonly int[] _gridStrideArray = new int[] { 12 };
    private readonly int[] _gridOffsetArray = new int[] { 0 };

    public void Render(in RenderPassContext context)
    {
        if (!context.IsGridVisible || context.GridVertexBuffer == null) return;

        _gridVbArray[0] = context.GridVertexBuffer;

        context.DeviceContext.IASetInputLayout(context.Resources.GridInputLayout);
        context.DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.DeviceContext.IASetVertexBuffers(0, 1, _gridVbArray, _gridStrideArray, _gridOffsetArray);
        context.DeviceContext.VSSetShader(context.Resources.GridVertexShader);
        context.DeviceContext.PSSetShader(context.Resources.GridPixelShader);
        context.DeviceContext.OMSetBlendState(context.Resources.GridBlendState, new Color4(0, 0, 0, 0), -1);

        Matrix4x4 gridWorld = Matrix4x4.Identity;
        if (!context.IsInfiniteGrid)
        {
            float finiteScale = (float)(context.GridScale * RenderingConstants.GridScaleBase / RenderingConstants.GridSize);
            if (finiteScale < 0.001f) finiteScale = 0.001f;
            gridWorld = Matrix4x4.CreateScale(finiteScale);
        }

        CBPerFrame cbFrameObj = new CBPerFrame
        {
            ViewProj = Matrix4x4.Transpose(context.View * context.Proj),
            CameraPos = new System.Numerics.Vector4(context.CamPos, 1),
            GridColor = context.GridColor,
            GridAxisColor = context.AxisColor,
            EnvironmentParam = new System.Numerics.Vector4(0, 0, 0, context.IsInfiniteGrid ? 1.0f : 0.0f)
        };

        CBPerObject cbObjectObj = new CBPerObject
        {
            WorldViewProj = Matrix4x4.Transpose(gridWorld * context.View * context.Proj),
            World = Matrix4x4.Transpose(gridWorld)
        };

        CBPerMaterial cbCoreObj = default;
        CBSceneEffects cbSceneObj = default;
        CBPostEffects cbPostObj = default;

        context.CbPerFrame.Update(context.DeviceContext, ref cbFrameObj);
        context.CbPerObject.Update(context.DeviceContext, ref cbObjectObj);
        context.CbPerMaterialCore.Update(context.DeviceContext, ref cbCoreObj);
        context.CbSceneEffects.Update(context.DeviceContext, ref cbSceneObj);
        context.CbPostEffects.Update(context.DeviceContext, ref cbPostObj);

        context.CbPerFrameArray[0] = context.CbPerFrame.Buffer;
        context.CbPerObjectArray[0] = context.CbPerObject.Buffer;
        context.CbPerMaterialArray[0] = context.CbPerMaterialCore.Buffer;
        context.CbSceneEffectsArray[0] = context.CbSceneEffects.Buffer;
        context.CbPostEffectsArray[0] = context.CbPostEffects.Buffer;

        context.DeviceContext.VSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, context.CbPerFrameArray);
        context.DeviceContext.PSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, context.CbPerFrameArray);
        context.DeviceContext.VSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, context.CbPerObjectArray);
        context.DeviceContext.PSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, context.CbPerObjectArray);
        context.DeviceContext.PSSetConstantBuffers(RenderingConstants.CbSlotPerMaterial, 1, context.CbPerMaterialArray);
        context.DeviceContext.PSSetConstantBuffers(RenderingConstants.CbSlotSceneEffects, 1, context.CbSceneEffectsArray);
        context.DeviceContext.PSSetConstantBuffers(RenderingConstants.CbSlotPostEffects, 1, context.CbPostEffectsArray);

        context.DeviceContext.Draw(6, 0);

        context.DeviceContext.OMSetBlendState(context.Resources.BlendState, new Color4(0, 0, 0, 0), -1);
    }
}
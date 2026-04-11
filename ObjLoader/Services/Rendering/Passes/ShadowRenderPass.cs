using ObjLoader.Core.Models;
using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Settings;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Services.Rendering.Passes;

internal sealed class ShadowRenderPass : IRenderPass
{
    private readonly ID3D11ShaderResourceView[] _nullSrv = new ID3D11ShaderResourceView[1];
    private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
    private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
    private readonly int[] _strideArray = new int[1];
    private readonly int[] _offsetArray = new int[] { 0 };

    private static readonly ID3D11RenderTargetView[] _emptyRtvs = [];
    private static readonly Vortice.Mathematics.Color4 _clearBlend = new Vortice.Mathematics.Color4(0, 0, 0, 0);

    public void Render(in RenderPassContext context)
    {
        var settings = PluginSettings.Instance;

        if (!context.EnableShadow || !settings.ShadowMappingEnabled)
        {
            context.RenderShadowMap = false;
            context.LightViewProj = Matrix4x4.Identity;
            return;
        }

        bool useCascaded = settings.CascadedShadowsEnabled;
        if (settings.ShadowResolution != context.Resources.CurrentShadowMapSize || context.Resources.IsCascaded != useCascaded)
        {
            context.Resources.EnsureShadowMapSize(settings.ShadowResolution, useCascaded);
        }

        if (context.Resources.ShadowMapDSVs == null || context.Resources.ShadowMapDSVs.Length == 0)
        {
            context.RenderShadowMap = false;
            context.LightViewProj = Matrix4x4.Identity;
            return;
        }

        LayerRenderData? shadowCasterLayer = null;
        Matrix4x4 shadowCasterWorld = Matrix4x4.Identity;

        for (int i = 0; i < context.Layers.Count; i++)
        {
            var l = context.Layers[i];
            if (l.LightEnabled && (l.LightType == 1 || l.LightType == 2))
            {
                shadowCasterLayer = l;
                shadowCasterWorld = context.LayerWorlds[i];
                break;
            }
        }

        if (shadowCasterLayer == null || !settings.GetShadowEnabled(shadowCasterLayer.Value.WorldId))
        {
            context.RenderShadowMap = false;
            context.LightViewProj = Matrix4x4.Identity;
            return;
        }

        if (context.Resources.ShadowMapSRV != null) context.ShadowSrvArray[0] = context.Resources.ShadowMapSRV;

        var rawLightPos = new System.Numerics.Vector3((float)shadowCasterLayer.Value.LightX, (float)shadowCasterLayer.Value.LightY, (float)shadowCasterLayer.Value.LightZ);
        System.Numerics.Vector3 lightPosVec;

        if (shadowCasterLayer.Value.LightType == 2)
            lightPosVec = System.Numerics.Vector3.TransformNormal(rawLightPos, shadowCasterWorld);
        else
            lightPosVec = System.Numerics.Vector3.Transform(rawLightPos, shadowCasterWorld);

        Matrix4x4 lightView;
        Matrix4x4 lightProj;
        float shadowRange = (float)settings.SunLightShadowRange;

        if (shadowCasterLayer.Value.LightType == 2)
        {
            var lightDir = System.Numerics.Vector3.Normalize(lightPosVec);
            if (lightDir.LengthSquared() < 0.0001f) lightDir = System.Numerics.Vector3.UnitY;

            var targetPos = System.Numerics.Vector3.Zero;
            var camPosShadow = targetPos + lightDir * shadowRange * 0.5f;

            lightView = Matrix4x4.CreateLookAt(camPosShadow, targetPos, System.Numerics.Vector3.UnitY);
            lightProj = Matrix4x4.CreateOrthographic(shadowRange, shadowRange, 1.0f, shadowRange * 2.0f);
        }
        else
        {
            var targetPos = System.Numerics.Vector3.Zero;
            lightView = Matrix4x4.CreateLookAt(lightPosVec, targetPos, System.Numerics.Vector3.UnitY);
            lightProj = Matrix4x4.CreatePerspectiveFieldOfView((float)(60.0 * Math.PI / 180.0), 1.0f, 1.0f, RenderingConstants.SpotLightFarPlanePreview);
        }

        context.LightViewProj = lightView * lightProj;
        context.RenderShadowMap = true;

        context.DeviceContext.PSSetShaderResources(RenderingConstants.SlotShadowMap, _nullSrv);

        context.DeviceContext.OMSetRenderTargets(0, _emptyRtvs, context.Resources.ShadowMapDSVs[0]);
        context.DeviceContext.ClearDepthStencilView(context.Resources.ShadowMapDSVs[0], DepthStencilClearFlags.Depth, 1.0f, 0);
        context.DeviceContext.RSSetState(context.Resources.ShadowRasterizerState);
        context.DeviceContext.RSSetViewport(0, 0, settings.ShadowResolution, settings.ShadowResolution);
        context.DeviceContext.VSSetShader(context.Resources.VertexShader);
        context.DeviceContext.PSSetShader(null);
        context.DeviceContext.IASetInputLayout(context.Resources.InputLayout);
        context.DeviceContext.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        for (int i = 0; i < context.Layers.Count; i++)
        {
            var layer = context.Layers[i];
            if (!layer.LightEnabled) continue;

            var modelResource = layer.Resource;
            var world = context.LayerWorlds[i];
            var wvpShadow = world * context.LightViewProj;

            int stride = Unsafe.SizeOf<ObjVertex>();
            _vbArray[0] = layer.OverrideVB ?? modelResource.VertexBuffer!;
            _strideArray[0] = stride;
            context.DeviceContext.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
            context.DeviceContext.IASetIndexBuffer(modelResource.IndexBuffer, Format.R32_UInt, 0);

            CBPerObject cbShadow = new CBPerObject
            {
                WorldViewProj = Matrix4x4.Transpose(wvpShadow),
                World = Matrix4x4.Transpose(world)
            };

            context.CbPerObject.Update(context.DeviceContext, ref cbShadow);
            _cbPerObjectArray[0] = context.CbPerObject.Buffer;
            context.DeviceContext.VSSetConstantBuffers(1, 1, _cbPerObjectArray);

            for (int p = 0; p < modelResource.Parts.Length; p++)
            {
                if (layer.VisibleParts != null && !layer.VisibleParts.Contains(p)) continue;
                var part = modelResource.Parts[p];
                if (part.BaseColor.W >= 0.99f)
                {
                    context.DeviceContext.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }
        }

        context.DeviceContext.OMSetRenderTargets(context.MainRtv, context.MainDsv);
        context.DeviceContext.RSSetState(context.IsWireframe ? context.Resources.WireframeRasterizerState : context.Resources.RasterizerState);
        context.DeviceContext.OMSetDepthStencilState(context.Resources.DepthStencilState, 0);
        context.DeviceContext.OMSetBlendState(context.Resources.BlendState, _clearBlend, -1);
        context.DeviceContext.RSSetViewport(0, 0, context.ViewportWidth, context.ViewportHeight);
        context.DeviceContext.Flush();
    }
}
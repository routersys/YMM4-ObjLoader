using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Models;
using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Settings;
using System.Numerics;
using Vortice.Direct3D11;

namespace ObjLoader.Services.Rendering.Passes;

internal static class PartDrawHelper
{
    private static readonly ID3D11ShaderResourceView[] _texArray = new ID3D11ShaderResourceView[1];

    public static void DrawPart(
        in RenderPassContext passContext,
        LayerRenderData layer,
        GpuResourceCacheItem resource,
        int partIndex,
        Matrix4x4 world,
        Matrix4x4 wvp)
    {
        var context = passContext.DeviceContext;
        var settings = PluginSettings.Instance;
        var part = resource.Parts[partIndex];
        var texView = resource.PartTextures[partIndex];

        ID3D11ShaderResourceView? activeTexView = texView;
        PartMaterialData? material = null;

        if (layer.Data != null && layer.Data.PartMaterials != null)
        {
            layer.Data.PartMaterials.TryGetValue(partIndex, out material);
        }

        if (material != null && !string.IsNullOrEmpty(material.TexturePath) && passContext.DynamicTextures.TryGetValue(material.TexturePath, out var dynTex))
        {
            activeTexView = dynTex;
        }

        _texArray[0] = activeTexView ?? passContext.Resources.WhiteTextureView!;
        context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, _texArray);

        var rawLightPos = new Vector3((float)layer.LightX, (float)layer.LightY, (float)layer.LightZ);
        Vector3 finalLightPos;
        if (layer.LightType == 2)
            finalLightPos = Vector3.TransformNormal(rawLightPos, world);
        else
            finalLightPos = Vector3.Transform(rawLightPos, world);

        int wId = layer.WorldId;
        float roughness = (float)(material?.Roughness ?? settings.GetRoughness(wId));
        float metallic = (float)(material?.Metallic ?? settings.GetMetallic(wId));

        CBPerFrame cbFrameObj = ConstantBufferFactory.CreatePerFrameForPreview(
            passContext.View * passContext.Proj, passContext.CamPos, finalLightPos, wId, passContext.GridColor, passContext.AxisColor, passContext.LightViewProj, layer.LightType, passContext.EnableShadow);

        CBPerObject cbObjectObj = new CBPerObject
        {
            WorldViewProj = Matrix4x4.Transpose(wvp),
            World = Matrix4x4.Transpose(world)
        };

        ConstantBufferFactory.CreatePerMaterial(
            wId,
            material != null ? new Vector4(material.BaseColor.R / 255.0f, material.BaseColor.G / 255.0f, material.BaseColor.B / 255.0f, material.BaseColor.A / 255.0f) : part.BaseColor,
            layer.LightEnabled,
            (float)settings.GetDiffuseIntensity(wId),
            (float)settings.GetShininess(wId),
            roughness,
            metallic,
            out var cbCore,
            out var cbScene,
            out var cbPost);

        passContext.CbPerFrame.Update(context, ref cbFrameObj);
        passContext.CbPerObject.Update(context, ref cbObjectObj);
        passContext.CbPerMaterialCore.Update(context, ref cbCore);
        passContext.CbSceneEffects.Update(context, ref cbScene);
        passContext.CbPostEffects.Update(context, ref cbPost);

        passContext.CbPerFrameArray[0] = passContext.CbPerFrame.Buffer;
        passContext.CbPerObjectArray[0] = passContext.CbPerObject.Buffer;
        passContext.CbPerMaterialArray[0] = passContext.CbPerMaterialCore.Buffer;
        passContext.CbSceneEffectsArray[0] = passContext.CbSceneEffects.Buffer;
        passContext.CbPostEffectsArray[0] = passContext.CbPostEffects.Buffer;

        context.VSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, passContext.CbPerFrameArray);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotPerFrame, 1, passContext.CbPerFrameArray);
        context.VSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, passContext.CbPerObjectArray);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotPerObject, 1, passContext.CbPerObjectArray);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotPerMaterial, 1, passContext.CbPerMaterialArray);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotSceneEffects, 1, passContext.CbSceneEffectsArray);
        context.PSSetConstantBuffers(RenderingConstants.CbSlotPostEffects, 1, passContext.CbPostEffectsArray);

        context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
    }
}
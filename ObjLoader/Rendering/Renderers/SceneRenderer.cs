using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Rendering.Utilities;
using ObjLoader.Settings;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace ObjLoader.Rendering.Renderers
{
    internal class SceneRenderer
    {
        private const int MaxHierarchyDepth = 100;

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;
        private readonly CustomShaderManager _shaderManager;

        private readonly ID3D11Buffer[] _cbArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
        private readonly int[] _strideArray = new int[1];
        private readonly int[] _offsetArray = new int[] { 0 };
        private readonly ID3D11SamplerState[] _samplerArray = new ID3D11SamplerState[1];
        private readonly ID3D11SamplerState[] _shadowSamplerArray = new ID3D11SamplerState[1];

        private readonly ID3D11ShaderResourceView[] _nullSrv1 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _nullSrv4 = new ID3D11ShaderResourceView[4];
        private readonly ID3D11ShaderResourceView[] _srvSlot0 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot1 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot2 = new ID3D11ShaderResourceView[1];

        private readonly List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> _singleLayerBuffer = new(1);

        public SceneRenderer(IGraphicsDevicesAndContext devices, D3DResources resources, RenderTargetManager renderTargets, CustomShaderManager shaderManager)
        {
            _devices = devices;
            _resources = resources;
            _renderTargets = renderTargets;
            _shaderManager = shaderManager;
        }

        public void Render(
            List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> layers,
            Dictionary<string, LayerState> layerStates,
            ObjLoaderParameter parameter,
            int width, int height,
            double camX, double camY, double camZ,
            double targetX, double targetY, double targetZ,
            Matrix4x4[] lightViewProjs, float[] cascadeSplits,
            bool shadowValid, int activeWorldId, bool updateEnvironmentMap)
        {
            if (_resources.ConstantBuffer == null || _renderTargets.RenderTargetView == null) return;

            var context = _devices.D3D.Device.ImmediateContext;

            ClearAllResourceBindings(context);

            context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);
            context.ClearRenderTargetView(_renderTargets.RenderTargetView, new Color4(0, 0, 0, 0));
            if (_renderTargets.DepthStencilView != null)
            {
                context.ClearDepthStencilView(_renderTargets.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
            }

            float aspect = (float)width / height;
            Vector3 cameraPosition = new Vector3((float)camX, (float)camY, (float)camZ);
            var target = new Vector3((float)targetX, (float)targetY, (float)targetZ);
            Matrix4x4 mainView, mainProj;

            var activeLayerTuple = layers.FirstOrDefault(x => x.State.WorldId == activeWorldId);
            var fov = activeLayerTuple.Data != null ? activeLayerTuple.State.Fov : 45.0f;
            var projectionType = activeLayerTuple.Data != null ? activeLayerTuple.State.Projection : ProjectionType.Perspective;

            if (projectionType == ProjectionType.Parallel)
            {
                if (cameraPosition == target) cameraPosition.Z -= 2.0f;
                mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                float orthoSize = 2.0f;
                mainProj = Matrix4x4.CreateOrthographic(orthoSize * aspect, orthoSize, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
            }
            else
            {
                if (cameraPosition == target) cameraPosition.Z -= 2.5f;
                mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                float radFov = (float)(Math.Max(1, Math.Min(RenderingConstants.DefaultFovLimit, fov)) * Math.PI / 180.0);
                mainProj = Matrix4x4.CreatePerspectiveFieldOfView(radFov, aspect, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
            }

            if (updateEnvironmentMap)
            {
                var targets = new[]
                {
                    new Vector3(-1, 0, 0), new Vector3(1, 0, 0),
                    new Vector3(0, 1, 0), new Vector3(0, -1, 0),
                    new Vector3(0, 0, 1), new Vector3(0, 0, -1)
                };
                var ups = new[]
                {
                    new Vector3(0, 1, 0), new Vector3(0, 1, 0),
                    new Vector3(0, 0, -1), new Vector3(0, 0, 1),
                    new Vector3(0, 1, 0), new Vector3(0, 1, 0)
                };

                ups[2] = new Vector3(0, 0, -1);
                ups[3] = new Vector3(0, 0, 1);

                Vector3 captureCenter = Vector3.Zero;
                if (layers.Count > 0)
                {
                    Vector3 minBounds = new Vector3(float.MaxValue);
                    Vector3 maxBounds = new Vector3(float.MinValue);

                    foreach (var item in layers)
                    {
                        Matrix4x4 hierarchyMatrix = RenderUtils.GetLayerTransform(item.State);
                        var currentGuid = item.State.ParentGuid;
                        int depth = 0;
                        while (!string.IsNullOrEmpty(currentGuid) && layerStates.TryGetValue(currentGuid, out var parentState))
                        {
                            hierarchyMatrix *= RenderUtils.GetLayerTransform(parentState);
                            currentGuid = parentState.ParentGuid;
                            depth++;
                            if (depth > MaxHierarchyDepth) break;
                        }

                        var normalize = Matrix4x4.CreateTranslation(-item.Resource.ModelCenter) * Matrix4x4.CreateScale(item.Resource.ModelScale);
                        var world = normalize * hierarchyMatrix;
                        Vector3 pos = world.Translation;

                        minBounds = Vector3.Min(minBounds, pos);
                        maxBounds = Vector3.Max(maxBounds, pos);
                    }

                    captureCenter = (minBounds + maxBounds) * 0.5f;
                }

                context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);

                for (int face = 0; face < RenderingConstants.EnvironmentMapFaceCount; face++)
                {
                    context.OMSetRenderTargets(_resources.EnvironmentRTVs[face], _resources.EnvironmentDSV);
                    context.ClearRenderTargetView(_resources.EnvironmentRTVs[face], new Color4(0, 0, 0, 0));
                    context.ClearDepthStencilView(_resources.EnvironmentDSV, DepthStencilClearFlags.Depth, 1.0f, 0);

                    var view = Matrix4x4.CreateLookAt(captureCenter, captureCenter + targets[face], ups[face]);

                    if (face == 3)
                    {
                        view *= Matrix4x4.CreateScale(-1, 1, -1);
                    }
                    else
                    {
                        view *= Matrix4x4.CreateScale(1, 1, -1);
                    }

                    var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);

                    RenderScene(context, layers, layerStates, parameter, view, proj, captureCenter.X, captureCenter.Y, captureCenter.Z, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, RenderingConstants.EnvironmentMapSize, RenderingConstants.EnvironmentMapSize, false, _resources.CullNoneRasterizerState, _resources.DepthStencilState);
                }

                context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
                context.GenerateMips(_resources.EnvironmentSRV);
            }

            context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);

            for (int i = 0; i < layers.Count; i++)
            {
                _singleLayerBuffer.Clear();
                _singleLayerBuffer.Add(layers[i]);
                RenderScene(context, _singleLayerBuffer, layerStates, parameter, mainView, mainProj, camX, camY, camZ, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, width, height, true, null, null);
            }

            context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);

            ClearAllResourceBindings(context);

            context.Flush();
        }

        private void ClearAllResourceBindings(ID3D11DeviceContext context)
        {
            context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);

            context.PSSetShaderResources(0, 4, _nullSrv4);
            context.VSSetShaderResources(0, 4, _nullSrv4);
        }

        private void RenderScene(
            ID3D11DeviceContext context,
            IEnumerable<(LayerData Data, GpuResourceCacheItem Resource, LayerState State)> layers,
            Dictionary<string, LayerState> layerStates,
            ObjLoaderParameter parameter,
            Matrix4x4 view,
            Matrix4x4 proj,
            double camX, double camY, double camZ,
            Matrix4x4[] lightViewProjs,
            float[] cascadeSplits,
            bool shadowValid,
            int activeWorldId,
            int width, int height,
            bool bindEnvironment,
            ID3D11RasterizerState? rasterizerState = null,
            ID3D11DepthStencilState? depthStencilState = null)
        {
            context.RSSetState(rasterizerState ?? _resources.RasterizerState);
            context.OMSetDepthStencilState(depthStencilState ?? _resources.DepthStencilState);
            context.OMSetBlendState(_resources.BlendState, new Color4(0, 0, 0, 0), -1);
            context.RSSetViewport(0, 0, width, height);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            if (_resources.ConstantBuffer != null)
            {
                _cbArray[0] = _resources.ConstantBuffer;
            }

            if (_resources.SamplerState != null)
            {
                _samplerArray[0] = _resources.SamplerState;
            }

            if (_resources.ShadowSampler != null)
            {
                _shadowSamplerArray[0] = _resources.ShadowSampler;
            }

            foreach (var item in layers)
            {
                var state = item.State;
                var resource = item.Resource;
                var settings = PluginSettings.Instance;

                _shaderManager.Update(state.ShaderFilePath, parameter);

                var vs = _shaderManager.VertexShader ?? _resources.VertexShader;
                var ps = _shaderManager.PixelShader ?? _resources.PixelShader;
                var layout = _shaderManager.VertexShader != null ? _shaderManager.InputLayout : _resources.InputLayout;

                if (vs == null || ps == null || layout == null) continue;

                context.IASetInputLayout(layout);
                context.VSSetShader(vs);
                context.PSSetShader(ps);
                context.PSSetSamplers(RenderingConstants.SlotStandardSampler, _samplerArray);

                if (shadowValid && state.WorldId == activeWorldId && _resources.ShadowMapSRV != null)
                {
                    _srvSlot1[0] = _resources.ShadowMapSRV;
                    context.PSSetShaderResources(RenderingConstants.SlotShadowMap, 1, _srvSlot1);
                    context.PSSetSamplers(RenderingConstants.SlotShadowSampler, _shadowSamplerArray);
                }
                else
                {
                    context.PSSetShaderResources(RenderingConstants.SlotShadowMap, 1, _nullSrv1);
                }

                if (bindEnvironment && _resources.EnvironmentSRV != null)
                {
                    _srvSlot2[0] = _resources.EnvironmentSRV;
                    context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _srvSlot2);
                }
                else
                {
                    context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);
                }

                Matrix4x4 hierarchyMatrix = RenderUtils.GetLayerTransform(state);
                var currentGuid = state.ParentGuid;
                int depth = 0;
                while (!string.IsNullOrEmpty(currentGuid) && layerStates.TryGetValue(currentGuid, out var parentState))
                {
                    hierarchyMatrix *= RenderUtils.GetLayerTransform(parentState);
                    currentGuid = parentState.ParentGuid;
                    depth++;
                    if (depth > MaxHierarchyDepth) break;
                }

                var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);
                var world = normalize * hierarchyMatrix;

                var viewProj = view * proj;
                var wvp = world * viewProj;
                var lightPos = new Vector4((float)state.LightX, (float)state.LightY, (float)state.LightZ, 1.0f);
                var amb = new Vector4(state.Ambient.ScR, state.Ambient.ScG, state.Ambient.ScB, state.Ambient.ScA);
                var lCol = new Vector4(state.Light.ScR, state.Light.ScG, state.Light.ScB, state.Light.ScA);
                var camPos = new Vector4((float)camX, (float)camY, (float)camZ, 1.0f);

                Matrix4x4.Invert(viewProj, out var inverseViewProj);

                int stride = Unsafe.SizeOf<ObjVertex>();
                _vbArray[0] = resource.VertexBuffer;
                _strideArray[0] = stride;
                context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                int wId = state.WorldId;

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;

                    var part = resource.Parts[i];
                    var texView = resource.PartTextures[i];
                    bool hasTexture = texView != null;

                    _srvSlot0[0] = hasTexture ? texView! : _resources.WhiteTextureView!;
                    context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                    PartMaterialData? material = null;
                    if (item.Data.PartMaterials != null)
                    {
                        item.Data.PartMaterials.TryGetValue(i, out material);
                    }

                    float roughness = (float)(material?.Roughness ?? settings.GetRoughness(wId));
                    float metallic = (float)(material?.Metallic ?? settings.GetMetallic(wId));
                    var resolvedBaseColor = material != null ? RenderUtils.ToVec4(material.BaseColor) : part.BaseColor;

                    var uiColorVec = hasTexture ? Vector4.One : new Vector4(state.BaseColor.ScR, state.BaseColor.ScG, state.BaseColor.ScB, state.BaseColor.ScA);
                    var partColor = resolvedBaseColor * uiColorVec;

                    ConstantBufferData cbData = new ConstantBufferData
                    {
                        WorldViewProj = Matrix4x4.Transpose(wvp),
                        World = Matrix4x4.Transpose(world),
                        LightPos = lightPos,
                        BaseColor = partColor,
                        AmbientColor = amb,
                        LightColor = lCol,
                        CameraPos = camPos,
                        LightEnabled = state.IsLightEnabled ? 1.0f : 0.0f,
                        DiffuseIntensity = (float)state.Diffuse,
                        SpecularIntensity = (float)settings.GetSpecularIntensity(wId),
                        Shininess = (float)state.Shininess,

                        ToonParams = new System.Numerics.Vector4(settings.GetToonEnabled(wId) ? 1 : 0, settings.GetToonSteps(wId), (float)settings.GetToonSmoothness(wId), 0),
                        RimParams = new System.Numerics.Vector4(settings.GetRimEnabled(wId) ? 1 : 0, (float)settings.GetRimIntensity(wId), (float)settings.GetRimPower(wId), 0),
                        RimColor = RenderUtils.ToVec4(settings.GetRimColor(wId)),
                        OutlineParams = new System.Numerics.Vector4(settings.GetOutlineEnabled(wId) ? 1 : 0, (float)settings.GetOutlineWidth(wId), (float)settings.GetOutlinePower(wId), 0),
                        OutlineColor = RenderUtils.ToVec4(settings.GetOutlineColor(wId)),
                        FogParams = new System.Numerics.Vector4(settings.GetFogEnabled(wId) ? 1 : 0, (float)settings.GetFogStart(wId), (float)settings.GetFogEnd(wId), (float)settings.GetFogDensity(wId)),
                        FogColor = RenderUtils.ToVec4(settings.GetFogColor(wId)),
                        ColorCorrParams = new System.Numerics.Vector4((float)settings.GetSaturation(wId), (float)settings.GetContrast(wId), (float)settings.GetGamma(wId), (float)settings.GetBrightnessPost(wId)),
                        VignetteParams = new System.Numerics.Vector4(settings.GetVignetteEnabled(wId) ? 1 : 0, (float)settings.GetVignetteIntensity(wId), (float)settings.GetVignetteRadius(wId), (float)settings.GetVignetteSoftness(wId)),
                        VignetteColor = RenderUtils.ToVec4(settings.GetVignetteColor(wId)),
                        ScanlineParams = new System.Numerics.Vector4(settings.GetScanlineEnabled(wId) ? 1 : 0, (float)settings.GetScanlineIntensity(wId), (float)settings.GetScanlineFrequency(wId), settings.GetScanlinePost(wId) ? 1 : 0),
                        ChromAbParams = new System.Numerics.Vector4(settings.GetChromAbEnabled(wId) ? 1 : 0, (float)settings.GetChromAbIntensity(wId), 0, 0),
                        MonoParams = new System.Numerics.Vector4(settings.GetMonochromeEnabled(wId) ? 1 : 0, (float)settings.GetMonochromeMix(wId), 0, 0),
                        MonoColor = RenderUtils.ToVec4(settings.GetMonochromeColor(wId)),
                        PosterizeParams = new System.Numerics.Vector4(settings.GetPosterizeEnabled(wId) ? 1 : 0, settings.GetPosterizeLevels(wId), 0, 0),
                        LightTypeParams = new System.Numerics.Vector4((float)state.LightType, 0, 0, 0),

                        LightViewProj0 = Matrix4x4.Transpose(lightViewProjs[0]),
                        LightViewProj1 = Matrix4x4.Transpose(lightViewProjs[1]),
                        LightViewProj2 = Matrix4x4.Transpose(lightViewProjs[2]),
                        ShadowParams = new Vector4(
                            (shadowValid && state.WorldId == activeWorldId) ? 1.0f : 0.0f,
                            (float)settings.ShadowBias,
                            (float)settings.ShadowStrength,
                            (float)settings.ShadowResolution),
                        CascadeSplits = new Vector4(cascadeSplits[0], cascadeSplits[1], cascadeSplits[2], cascadeSplits[3]),
                        EnvironmentParam = bindEnvironment ? new Vector4(1, 0, 0, 0) : new Vector4(0, 0, 0, 0),

                        PbrParams = new Vector4(metallic, roughness, 1.0f, 0),
                        IblParams = new Vector4((float)settings.GetIBLIntensity(wId), 6.0f, 0, 0),
                        SsrParams = new Vector4(settings.GetSSREnabled(wId) ? 1 : 0, (float)settings.GetSSRStep(wId), (float)settings.GetSSRMaxDist(wId), (float)settings.GetSSRMaxSteps(wId)),
                        ViewProj = Matrix4x4.Transpose(viewProj),
                        InverseViewProj = Matrix4x4.Transpose(inverseViewProj),
                        PcssParams = new Vector4((float)settings.GetPcssLightSize(wId), 0.5f, (float)settings.GetPcssQuality(wId), (float)settings.GetPcssQuality(wId)),
                        SsrParams2 = new Vector4((float)settings.GetSSRMaxSteps(wId), (float)settings.GetSSRThickness(wId), 0, 0)
                    };

                    if (_resources.ConstantBuffer != null)
                    {
                        MappedSubresource mapped;
                        context.Map(_resources.ConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None, out mapped);
                        unsafe
                        {
                            Unsafe.Copy(mapped.DataPointer.ToPointer(), ref cbData);
                        }
                        context.Unmap(_resources.ConstantBuffer, 0);

                        context.VSSetConstantBuffers(0, _cbArray);
                        context.PSSetConstantBuffers(0, _cbArray);
                    }

                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }

            context.PSSetShaderResources(0, 1, _nullSrv1);
        }
    }
}
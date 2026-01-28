using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Plugin;
using ObjLoader.Settings;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Linq;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace ObjLoader.Rendering
{
    internal class SceneRenderer
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;
        private readonly CustomShaderManager _shaderManager;

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
            bool shadowValid, int activeWorldId)
        {
            if (_resources.ConstantBuffer == null || _renderTargets.RenderTargetView == null) return;

            var context = _devices.D3D.Device.ImmediateContext;

            context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);
            context.ClearRenderTargetView(_renderTargets.RenderTargetView, new Color4(0, 0, 0, 0));
            context.ClearDepthStencilView(_renderTargets.DepthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);

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
                mainProj = Matrix4x4.CreateOrthographic(orthoSize * aspect, orthoSize, 0.1f, 1000.0f);
            }
            else
            {
                if (cameraPosition == target) cameraPosition.Z -= 2.5f;
                mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                float radFov = (float)(Math.Max(1, Math.Min(179, fov)) * Math.PI / 180.0);
                mainProj = Matrix4x4.CreatePerspectiveFieldOfView(radFov, aspect, 0.1f, 1000.0f);
            }

            var targets = new[]
            {
                new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
                new Vector3(0, 1, 0), new Vector3(0, -1, 0),
                new Vector3(0, 0, 1), new Vector3(0, 0, -1)
            };
            var ups = new[]
            {
                new Vector3(0, 1, 0), new Vector3(0, 1, 0),
                new Vector3(0, 0, -1), new Vector3(0, 0, 1),
                new Vector3(0, 1, 0), new Vector3(0, 1, 0)
            };

            for (int i = 0; i < layers.Count; i++)
            {
                var currentLayer = layers[i];
                var state = currentLayer.State;
                var resource = currentLayer.Resource;

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
                var captureCenter = world.Translation;

                var envLayers = layers.Where((_, idx) => idx != i);

                for (int face = 0; face < 6; face++)
                {
                    context.OMSetRenderTargets(_resources.EnvironmentRTVs[face], _resources.EnvironmentDSV);
                    context.ClearRenderTargetView(_resources.EnvironmentRTVs[face], new Color4(0, 0, 0, 0));
                    context.ClearDepthStencilView(_resources.EnvironmentDSV, DepthStencilClearFlags.Depth, 1.0f, 0);

                    var view = Matrix4x4.CreateLookAt(captureCenter, captureCenter + targets[face], ups[face]);
                    var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, 0.1f, 1000.0f);

                    RenderScene(context, new[] { currentLayer }, layerStates, parameter, view, proj, captureCenter.X, captureCenter.Y, captureCenter.Z, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, 512, 512, false, _resources.CullNoneRasterizerState, _resources.DepthStencilStateNoWrite);

                    RenderScene(context, envLayers, layerStates, parameter, view, proj, captureCenter.X, captureCenter.Y, captureCenter.Z, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, 512, 512, false);
                }

                context.OMSetRenderTargets((ID3D11RenderTargetView?)null!, (ID3D11DepthStencilView?)null);
                context.GenerateMips(_resources.EnvironmentSRV);

                context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);

                RenderScene(context, new[] { currentLayer }, layerStates, parameter, mainView, mainProj, camX, camY, camZ, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, width, height, true);
            }

            context.OMSetRenderTargets((ID3D11RenderTargetView?)null!, (ID3D11DepthStencilView?)null);
            context.Flush();
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
                context.PSSetSamplers(0, new[] { _resources.SamplerState });

                if (shadowValid && state.WorldId == activeWorldId)
                {
                    context.PSSetShaderResources(1, new[] { _resources.ShadowMapSRV! });
                    context.PSSetSamplers(1, new[] { _resources.ShadowSampler });
                }
                else
                {
                    context.PSSetShaderResources(1, new ID3D11ShaderResourceView[] { null! });
                }

                if (bindEnvironment)
                {
                    context.PSSetShaderResources(2, new[] { _resources.EnvironmentSRV });
                }
                else
                {
                    context.PSSetShaderResources(2, new ID3D11ShaderResourceView[] { null! });
                }

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

                var viewProj = view * proj;
                var wvp = world * viewProj;
                var lightPos = new Vector4((float)state.LightX, (float)state.LightY, (float)state.LightZ, 1.0f);
                var amb = new Vector4(state.Ambient.ScR, state.Ambient.ScG, state.Ambient.ScB, state.Ambient.ScA);
                var lCol = new Vector4(state.Light.ScR, state.Light.ScG, state.Light.ScB, state.Light.ScA);
                var camPos = new Vector4((float)camX, (float)camY, (float)camZ, 1.0f);

                Matrix4x4.Invert(viewProj, out var inverseViewProj);

                int stride = Unsafe.SizeOf<ObjVertex>();
                context.IASetVertexBuffers(0, 1, new[] { resource.VertexBuffer }, new[] { stride }, new[] { 0 });
                context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                int wId = state.WorldId;

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;

                    var part = resource.Parts[i];
                    var texView = resource.PartTextures[i];
                    bool hasTexture = texView != null;

                    ID3D11ShaderResourceView viewResource = hasTexture ? texView! : _resources.WhiteTextureView!;
                    context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { viewResource });

                    var uiColorVec = hasTexture ? Vector4.One : new Vector4(state.BaseColor.ScR, state.BaseColor.ScG, state.BaseColor.ScB, state.BaseColor.ScA);
                    var partColor = part.BaseColor * uiColorVec;

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

                        PbrParams = new Vector4((float)settings.GetMetallic(wId), (float)settings.GetRoughness(wId), 1.0f, 0),
                        IblParams = new Vector4((float)settings.GetIBLIntensity(wId), 6.0f, 0, 0),
                        SsrParams = new Vector4(settings.GetSSREnabled(wId) ? 1 : 0, (float)settings.GetSSRStep(wId), (float)settings.GetSSRMaxDist(wId), (float)settings.GetSSRThickness(wId)),
                        ViewProj = Matrix4x4.Transpose(viewProj),
                        InverseViewProj = Matrix4x4.Transpose(inverseViewProj),
                        PcssParams = new Vector4((float)settings.GetPcssLightSize(wId), 0.5f, (float)settings.GetPcssQuality(wId), (float)settings.GetPcssQuality(wId))
                    };

                    MappedSubresource mapped;
                    context.Map(_resources.ConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None, out mapped);
                    unsafe
                    {
                        Unsafe.Copy(mapped.DataPointer.ToPointer(), ref cbData);
                    }
                    context.Unmap(_resources.ConstantBuffer, 0);

                    context.VSSetConstantBuffers(0, new[] { _resources.ConstantBuffer });
                    context.PSSetConstantBuffers(0, new[] { _resources.ConstantBuffer });

                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }
        }
    }
}
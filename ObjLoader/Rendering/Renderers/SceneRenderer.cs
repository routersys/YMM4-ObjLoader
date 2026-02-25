using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Enums;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
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

namespace ObjLoader.Rendering.Renderers
{
    internal class SceneRenderer : IDisposable
    {
        private const int MaxHierarchyDepth = 100;
        private bool _isDisposed;

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;
        private readonly CustomShaderManager _shaderManager;

        private ConstantBuffer<CBPerFrame>? _cbPerFrame;
        private ConstantBuffer<CBPerObject>? _cbPerObject;
        private ConstantBuffer<CBPerMaterial>? _cbPerMaterial;

        private readonly ID3D11Buffer[] _cbPerFrameArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerMaterialArray = new ID3D11Buffer[1];

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

        private readonly List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> _singleLayerBuffer = new(1);

        private Vector3[] _layerWorldCenters = Array.Empty<Vector3>();
        private bool[] _useLocalCenter = Array.Empty<bool>();

        public SceneRenderer(IGraphicsDevicesAndContext devices, D3DResources resources, RenderTargetManager renderTargets, CustomShaderManager shaderManager)
        {
            _devices = devices;
            _resources = resources;
            _renderTargets = renderTargets;
            _shaderManager = shaderManager;

            _cbPerFrame = new ConstantBuffer<CBPerFrame>(_devices.D3D.Device);
            _cbPerObject = new ConstantBuffer<CBPerObject>(_devices.D3D.Device);
            _cbPerMaterial = new ConstantBuffer<CBPerMaterial>(_devices.D3D.Device);
        }

        public void Render(
            List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> layers,
            Dictionary<string, LayerState> layerStates,
            ObjLoaderParameter parameter,
            int width, int height,
            double camX, double camY, double camZ,
            double targetX, double targetY, double targetZ,
            Matrix4x4[] lightViewProjs, float[] cascadeSplits,
            bool shadowValid, int activeWorldId, bool updateEnvironmentMap,
            IReadOnlyDictionary<string, ID3D11ShaderResourceView>? dynamicTextureCache = null)
        {
            if (_renderTargets.RenderTargetView == null) return;

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
                if (cameraPosition == target) cameraPosition.Z -= RenderingConstants.CameraFallbackOffsetParallel;
                mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                mainProj = Matrix4x4.CreateOrthographic(RenderingConstants.DefaultOrthoSize * aspect, RenderingConstants.DefaultOrthoSize, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
            }
            else
            {
                if (cameraPosition == target) cameraPosition.Z -= RenderingConstants.CameraFallbackOffsetPerspective;
                mainView = Matrix4x4.CreateLookAt(cameraPosition, target, Vector3.UnitY);
                float radFov = (float)(Math.Max(1, Math.Min(RenderingConstants.DefaultFovLimit, fov)) * Math.PI / 180.0);
                mainProj = Matrix4x4.CreatePerspectiveFieldOfView(radFov, aspect, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
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

            if (_layerWorldCenters.Length < layers.Count)
            {
                int newSize = layers.Count * 2;
                _layerWorldCenters = new Vector3[newSize];
                _useLocalCenter = new bool[newSize];
            }

            Vector3 globalCenter = Vector3.Zero;

            if (layers.Count > 0)
            {
                Vector3 minBounds = new Vector3(float.MaxValue);
                Vector3 maxBounds = new Vector3(float.MinValue);

                for (int i = 0; i < layers.Count; i++)
                {
                    var item = layers[i];
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

                    _layerWorldCenters[i] = pos;
                    minBounds = Vector3.Min(minBounds, pos);
                    maxBounds = Vector3.Max(maxBounds, pos);

                    _useLocalCenter[i] = !string.IsNullOrEmpty(item.State.ParentGuid);
                }

                globalCenter = (minBounds + maxBounds) * 0.5f;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (updateEnvironmentMap && _resources.EnvironmentRTVs != null && _resources.EnvironmentDSV != null)
                {
                    Vector3 captureCenter = _useLocalCenter[i] ? _layerWorldCenters[i] : globalCenter;

                    context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);

                    for (int face = 0; face < RenderingConstants.EnvironmentMapFaceCount; face++)
                    {
                        context.OMSetRenderTargets(_resources.EnvironmentRTVs[face], _resources.EnvironmentDSV);
                        context.ClearRenderTargetView(_resources.EnvironmentRTVs[face], new Color4(0, 0, 0, 0));
                        context.ClearDepthStencilView(_resources.EnvironmentDSV, DepthStencilClearFlags.Depth, 1.0f, 0);

                        var view = Matrix4x4.CreateLookAt(captureCenter, captureCenter + targets[face], ups[face]);
                        view *= Matrix4x4.CreateScale(-1, 1, 1);
                        var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);

                        RenderScene(context, layers, layerStates, parameter, view, proj, captureCenter.X, captureCenter.Y, captureCenter.Z, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, RenderingConstants.EnvironmentMapSize, RenderingConstants.EnvironmentMapSize, false, _resources.CullNoneRasterizerState, _resources.DepthStencilState, dynamicTextureCache, i);
                    }

                    context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
                    context.GenerateMips(_resources.EnvironmentSRV);
                }

                context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);

                _singleLayerBuffer.Clear();
                _singleLayerBuffer.Add(layers[i]);
                RenderScene(context, _singleLayerBuffer, layerStates, parameter, mainView, mainProj, camX, camY, camZ, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, width, height, true, null, null, dynamicTextureCache);
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
            List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> layers,
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
            ID3D11DepthStencilState? depthStencilState = null,
            IReadOnlyDictionary<string, ID3D11ShaderResourceView>? dynamicTextureCache = null,
            int skipIndex = -1)
        {
            context.RSSetState(rasterizerState ?? _resources.RasterizerState);
            context.OMSetDepthStencilState(depthStencilState ?? _resources.DepthStencilState);
            context.OMSetBlendState(_resources.BlendState, new Color4(0, 0, 0, 0), -1);
            context.RSSetViewport(0, 0, width, height);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            if (_cbPerFrame == null || _cbPerObject == null || _cbPerMaterial == null) return;

            _samplerArray[0] = _resources.SamplerState;

            if (_resources.ShadowSampler != null)
            {
                _shadowSamplerArray[0] = _resources.ShadowSampler;
            }

            var viewProj = view * proj;
            var camPos = new Vector4((float)camX, (float)camY, (float)camZ, 1.0f);

            for (int li = 0; li < layers.Count; li++)
            {
                if (li == skipIndex) continue;
                var item = layers[li];
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
                var wvp = world * viewProj;

                var lightPos = new Vector4((float)state.LightX, (float)state.LightY, (float)state.LightZ, 1.0f);
                var amb = new Vector4(state.Ambient.ScR, state.Ambient.ScG, state.Ambient.ScB, state.Ambient.ScA);
                var lCol = new Vector4(state.Light.ScR, state.Light.ScG, state.Light.ScB, state.Light.ScA);

                CBPerFrame cbFrame = ConstantBufferFactory.CreatePerFrameForScene(
                    viewProj, camPos, lightPos, amb, lCol,
                    lightViewProjs, cascadeSplits,
                    (int)state.LightType, shadowValid, activeWorldId, state.WorldId, bindEnvironment);

                CBPerObject cbObject = new CBPerObject
                {
                    WorldViewProj = Matrix4x4.Transpose(wvp),
                    World = Matrix4x4.Transpose(world)
                };

                _cbPerFrame.Update(context, ref cbFrame);
                _cbPerObject.Update(context, ref cbObject);

                _cbPerFrameArray[0] = _cbPerFrame.Buffer;
                _cbPerObjectArray[0] = _cbPerObject.Buffer;

                context.VSSetConstantBuffers(0, 1, _cbPerFrameArray);
                context.PSSetConstantBuffers(0, 1, _cbPerFrameArray);

                context.VSSetConstantBuffers(1, 1, _cbPerObjectArray);
                context.PSSetConstantBuffers(1, 1, _cbPerObjectArray);

                int stride = Unsafe.SizeOf<ObjVertex>();
                _vbArray[0] = item.OverrideVB ?? resource.VertexBuffer;
                _strideArray[0] = stride;
                context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                int wId = state.WorldId;

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;

                    var part = resource.Parts[i];
                    var texView = resource.PartTextures[i];

                    PartMaterialData? material = null;
                    if (item.Data.PartMaterials != null)
                    {
                        item.Data.PartMaterials.TryGetValue(i, out material);
                    }

                    ID3D11ShaderResourceView? activeTexView = texView;
                    if (material != null && !string.IsNullOrEmpty(material.TexturePath) && dynamicTextureCache != null && dynamicTextureCache.TryGetValue(material.TexturePath, out var dynTex))
                    {
                        activeTexView = dynTex;
                    }

                    bool hasTexture = activeTexView != null;

                    _srvSlot0[0] = hasTexture ? activeTexView! : _resources.WhiteTextureView!;
                    context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                    float roughness = (float)(material?.Roughness ?? settings.GetRoughness(wId));
                    float metallic = (float)(material?.Metallic ?? settings.GetMetallic(wId));
                    var resolvedBaseColor = material != null ? RenderUtils.ToVec4(material.BaseColor) : part.BaseColor;

                    var uiColorVec = hasTexture ? Vector4.One : new Vector4(state.BaseColor.ScR, state.BaseColor.ScG, state.BaseColor.ScB, state.BaseColor.ScA);
                    var partColor = resolvedBaseColor * uiColorVec;

                    CBPerMaterial cbMaterial = ConstantBufferFactory.CreatePerMaterial(
                        wId,
                        partColor,
                        state.IsLightEnabled,
                        (float)state.Diffuse,
                        (float)state.Shininess,
                        roughness,
                        metallic);

                    _cbPerMaterial.Update(context, ref cbMaterial);
                    _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;
                    context.PSSetConstantBuffers(2, 1, _cbPerMaterialArray);

                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }

            context.PSSetShaderResources(0, 1, _nullSrv1);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cbPerFrame?.Dispose();
            _cbPerObject?.Dispose();
            _cbPerMaterial?.Dispose();
        }
    }
}
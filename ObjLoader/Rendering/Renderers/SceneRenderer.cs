using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Enums;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
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
    internal sealed class SceneRenderer : IDisposable
    {
        private const int MaxHierarchyDepth = 100;
        private bool _isDisposed;

        private readonly IGraphicsDevicesAndContext _devices;
        private readonly D3DResources _resources;
        private readonly RenderTargetManager _renderTargets;
        private readonly CustomShaderManager _shaderManager;
        private readonly ISceneDrawManager _sceneDrawManager;

        private ConstantBuffer<CBPerFrame>? _cbPerFrame;
        private ConstantBuffer<CBPerObject>? _cbPerObject;
        private ConstantBuffer<CBPerMaterial>? _cbPerMaterial;

        private readonly ID3D11Buffer[] _cbPerFrameArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerMaterialArray = new ID3D11Buffer[1];

        private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
        private readonly int[] _strideArray = new int[1];
        private readonly int[] _offsetArray = [0];
        private readonly ID3D11SamplerState[] _samplerArray = new ID3D11SamplerState[1];
        private readonly ID3D11SamplerState[] _shadowSamplerArray = new ID3D11SamplerState[1];

        private readonly ID3D11ShaderResourceView[] _nullSrv1 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _nullSrv4 = new ID3D11ShaderResourceView[4];
        private readonly ID3D11ShaderResourceView[] _srvSlot0 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot1 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot2 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot3 = new ID3D11ShaderResourceView[1];

        private readonly List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> _singleLayerBuffer = new(1);

        private static readonly Vector3[] _envFaceTargets =
        [
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, -1, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        ];

        private static readonly Vector3[] _envFaceUps =
        [
            new Vector3(0, 1, 0), new Vector3(0, 1, 0),
            new Vector3(0, 0, -1), new Vector3(0, 0, 1),
            new Vector3(0, 1, 0), new Vector3(0, 1, 0)
        ];


        private ID3D11Buffer? _billboardVb;
        private ID3D11Buffer? _billboardIb;

        public SceneRenderer(
            IGraphicsDevicesAndContext devices,
            D3DResources resources,
            RenderTargetManager renderTargets,
            CustomShaderManager shaderManager,
            ISceneDrawManager sceneDrawManager)
        {
            _devices = devices;
            _resources = resources;
            _renderTargets = renderTargets;
            _shaderManager = shaderManager;
            _sceneDrawManager = sceneDrawManager;

            _cbPerFrame = new ConstantBuffer<CBPerFrame>(_devices.D3D.Device);
            _cbPerObject = new ConstantBuffer<CBPerObject>(_devices.D3D.Device);
            _cbPerMaterial = new ConstantBuffer<CBPerMaterial>(_devices.D3D.Device);

            var verts = new ObjVertex[]
            {
                new ObjVertex { Position = new Vector3(-0.5f, 0.5f, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(0,0) },
                new ObjVertex { Position = new Vector3(0.5f, 0.5f, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(1,0) },
                new ObjVertex { Position = new Vector3(-0.5f, -0.5f, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(0,1) },
                new ObjVertex { Position = new Vector3(0.5f, -0.5f, 0), Normal = new Vector3(0,0,-1), TexCoord = new Vector2(1,1) }
            };
            var indices = new int[] { 0, 1, 2, 2, 1, 3 };

            unsafe
            {
                fixed (ObjVertex* pVerts = verts)
                {
                    _billboardVb = _devices.D3D.Device.CreateBuffer(new BufferDescription(verts.Length * Unsafe.SizeOf<ObjVertex>(), BindFlags.VertexBuffer, ResourceUsage.Immutable), new SubresourceData(pVerts));
                }
                fixed (int* pIndices = indices)
                {
                    _billboardIb = _devices.D3D.Device.CreateBuffer(new BufferDescription(indices.Length * sizeof(int), BindFlags.IndexBuffer, ResourceUsage.Immutable), new SubresourceData(pIndices));
                }
            }
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
                context.ClearDepthStencilView(_renderTargets.DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
            }

            float aspect = (float)width / height;
            Vector3 cameraPosition = new Vector3((float)camX, (float)camY, (float)camZ);
            var target = new Vector3((float)targetX, (float)targetY, (float)targetZ);
            Matrix4x4 mainView, mainProj;

            float fov = 45.0f;
            ProjectionType projectionType = ProjectionType.Perspective;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].State.WorldId == activeWorldId)
                {
                    fov = (float)layers[i].State.Fov;
                    projectionType = layers[i].State.Projection;
                    break;
                }
            }

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

            var mainViewProj = mainView * mainProj;

            var layerWorldCenters = System.Buffers.ArrayPool<Vector3>.Shared.Rent(Math.Max(1, layers.Count));
            var useLocalCenter = System.Buffers.ArrayPool<bool>.Shared.Rent(Math.Max(1, layers.Count));

            try
            {
                Vector3 globalCenter = Vector3.Zero;

                if (layers.Count > 0)
                {
                    Vector3 minBounds = new Vector3(float.MaxValue);
                    Vector3 maxBounds = new Vector3(float.MinValue);

                    for (int i = 0; i < layers.Count; i++)
                    {
                        var item = layers[i];
                        var hierarchyMatrix = BuildHierarchyMatrix(item.State, layerStates);
                        var normalize = Matrix4x4.CreateTranslation(-item.Resource.ModelCenter) * Matrix4x4.CreateScale(item.Resource.ModelScale);
                        var world = normalize * hierarchyMatrix;

                        layerWorldCenters[i] = world.Translation;
                        useLocalCenter[i] = !string.IsNullOrEmpty(item.State.ParentGuid);

                        minBounds = Vector3.Min(minBounds, world.Translation);
                        maxBounds = Vector3.Max(maxBounds, world.Translation);
                    }

                    globalCenter = (minBounds + maxBounds) * 0.5f;
                }

                if ((layers.Count > 0 || _sceneDrawManager.GetExternalObjects().Count > 0) && _renderTargets.DepthStencilView != null)
                {
                    DepthPrePass(context, layers, layerStates, mainViewProj, width, height);
                    context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
                    _renderTargets.CopyDepthBuffer(context);
                    context.ClearDepthStencilView(_renderTargets.DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                }

                for (int i = 0; i < layers.Count; i++)
                {
                    if (updateEnvironmentMap && _resources.EnvironmentRTVs != null && _resources.EnvironmentDSV != null)
                    {
                        Vector3 captureCenter = useLocalCenter[i] ? layerWorldCenters[i] : globalCenter;

                        context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);

                        for (int face = 0; face < RenderingConstants.EnvironmentMapFaceCount; face++)
                        {
                            context.OMSetRenderTargets(_resources.EnvironmentRTVs[face], _resources.EnvironmentDSV);
                            context.ClearRenderTargetView(_resources.EnvironmentRTVs[face], new Color4(0, 0, 0, 0));
                            context.ClearDepthStencilView(_resources.EnvironmentDSV, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

                            var view = Matrix4x4.CreateLookAt(captureCenter, captureCenter + _envFaceTargets[face], _envFaceUps[face]);
                            view *= Matrix4x4.CreateScale(-1, 1, 1);
                            var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);

                            RenderScene(context, layers, layerStates, parameter, view, proj, captureCenter.X, captureCenter.Y, captureCenter.Z, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, RenderingConstants.EnvironmentMapSize, RenderingConstants.EnvironmentMapSize, false, _resources.CullNoneRasterizerState, _resources.DepthStencilState, dynamicTextureCache, i, null);

                            var envViewProj = view * proj;
                            var camPosEnv = new Vector4(captureCenter.X, captureCenter.Y, captureCenter.Z, 1.0f);
                            RenderApiObjects(context, envViewProj, camPosEnv, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, false);
                            RenderBillboards(context, view, proj, camPosEnv, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, false);
                        }

                        context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
                        context.GenerateMips(_resources.EnvironmentSRV);
                    }

                    context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);

                    _singleLayerBuffer.Clear();
                    _singleLayerBuffer.Add(layers[i]);
                    RenderScene(context, _singleLayerBuffer, layerStates, parameter, mainView, mainProj, camX, camY, camZ, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, width, height, true, null, null, dynamicTextureCache, -1, _renderTargets.DepthCopySRV);
                }

                context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);
                var camPosRender = new Vector4((float)camX, (float)camY, (float)camZ, 1.0f);
                RenderApiObjects(context, mainViewProj, camPosRender, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, true);
                RenderBillboards(context, mainView, mainProj, camPosRender, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, true);

                context.VSSetShader(null);
                context.PSSetShader(null);
                context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);
                ClearAllResourceBindings(context);
            }
            finally
            {
                System.Buffers.ArrayPool<Vector3>.Shared.Return(layerWorldCenters);
                System.Buffers.ArrayPool<bool>.Shared.Return(useLocalCenter);
            }
        }

        private void ClearAllResourceBindings(ID3D11DeviceContext context)
        {
            context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
            context.PSSetShaderResources(0, 4, _nullSrv4);
            context.VSSetShaderResources(0, 4, _nullSrv4);
        }

        private static Matrix4x4 BuildHierarchyMatrix(LayerState state, Dictionary<string, LayerState> layerStates)
        {
            var matrix = RenderUtils.GetLayerTransform(state);
            var currentGuid = state.ParentGuid;
            int depth = 0;

            while (!string.IsNullOrEmpty(currentGuid) && layerStates.TryGetValue(currentGuid, out var parentState))
            {
                matrix *= RenderUtils.GetLayerTransform(parentState);
                currentGuid = parentState.ParentGuid;
                depth++;
                if (depth > MaxHierarchyDepth) break;
            }

            return matrix;
        }

        private void DepthPrePass(
            ID3D11DeviceContext context,
            List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> layers,
            Dictionary<string, LayerState> layerStates,
            Matrix4x4 viewProj,
            int width,
            int height)
        {
            if (_cbPerObject == null) return;

            context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), _renderTargets.DepthStencilView);
            context.RSSetState(_resources.RasterizerState);
            context.OMSetDepthStencilState(_resources.DepthStencilState);
            context.RSSetViewport(0, 0, width, height);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(_resources.InputLayout);
            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(null);

            for (int li = 0; li < layers.Count; li++)
            {
                var item = layers[li];
                var state = item.State;
                var resource = item.Resource;

                var hierarchyMatrix = BuildHierarchyMatrix(state, layerStates);
                var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);
                var world = normalize * hierarchyMatrix;
                var wvp = world * viewProj;

                var cbObject = new CBPerObject
                {
                    WorldViewProj = Matrix4x4.Transpose(wvp),
                    World = Matrix4x4.Transpose(world)
                };

                _cbPerObject.Update(context, ref cbObject);
                _cbPerObjectArray[0] = _cbPerObject.Buffer;
                context.VSSetConstantBuffers(1, 1, _cbPerObjectArray);

                int stride = Unsafe.SizeOf<ObjVertex>();
                _vbArray[0] = item.OverrideVB ?? resource.VertexBuffer;
                _strideArray[0] = stride;
                context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;
                    var part = resource.Parts[i];
                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }
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
            int skipIndex = -1,
            ID3D11ShaderResourceView? depthSrv = null)
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

            if (depthSrv != null)
            {
                _srvSlot3[0] = depthSrv;
                context.PSSetShaderResources(RenderingConstants.SlotDepthMap, 1, _srvSlot3);
            }
            else
            {
                context.PSSetShaderResources(RenderingConstants.SlotDepthMap, 1, _nullSrv1);
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

                var hierarchyMatrix = BuildHierarchyMatrix(state, layerStates);
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

        private void RenderApiObjects(
            ID3D11DeviceContext context,
            Matrix4x4 viewProj,
            Vector4 camPos,
            Matrix4x4[] lightViewProjs,
            float[] cascadeSplits,
            bool shadowValid,
            int activeWorldId,
            bool bindEnvironment)
        {
            var externalObjects = (List<Api.Draw.ExternalObjectHandle>)_sceneDrawManager.GetExternalObjects();
            if (externalObjects.Count == 0 || _cbPerFrame == null || _cbPerObject == null || _cbPerMaterial == null) return;

            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(_resources.PixelShader);
            context.IASetInputLayout(_resources.InputLayout);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            var lightPos = new Vector4(0, 10, 0, 1.0f);
            var amb = new Vector4(0.2f, 0.2f, 0.2f, 1.0f);
            var lCol = new Vector4(1, 1, 1, 1);

            CBPerFrame cbFrame = ConstantBufferFactory.CreatePerFrameForScene(
                viewProj, camPos, lightPos, amb, lCol,
                lightViewProjs, cascadeSplits,
                0, shadowValid, activeWorldId, 0, bindEnvironment);
            _cbPerFrame.Update(context, ref cbFrame);
            _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            context.VSSetConstantBuffers(0, 1, _cbPerFrameArray);
            context.PSSetConstantBuffers(0, 1, _cbPerFrameArray);

            for (int i = 0; i < externalObjects.Count; i++)
            {
                var obj = externalObjects[i];
                if (!obj.IsVisible || string.IsNullOrEmpty(obj.Descriptor.FilePath)) continue;
                
                string cacheKey = "export:" + obj.Descriptor.FilePath;
                if (!ObjLoader.Cache.Gpu.GpuResourceCache.Instance.TryGetValue(cacheKey, out var resource) || resource == null || resource.VertexBuffer == null || resource.IndexBuffer == null)
                {
                    continue;
                }

                var world = obj.CurrentTransform.ToMatrix();

                var wvp = world * viewProj;
                CBPerObject cbObject = new CBPerObject { WorldViewProj = Matrix4x4.Transpose(wvp), World = Matrix4x4.Transpose(world) };
                _cbPerObject.Update(context, ref cbObject);
                _cbPerObjectArray[0] = _cbPerObject.Buffer;
                context.VSSetConstantBuffers(1, 1, _cbPerObjectArray);

                _vbArray[0] = resource.VertexBuffer;
                _strideArray[0] = System.Runtime.CompilerServices.Unsafe.SizeOf<ObjLoader.Core.Models.ObjVertex>();
                context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                context.IASetIndexBuffer(resource.IndexBuffer, Vortice.DXGI.Format.R32_UInt, 0);

                for (int p = 0; p < resource.Parts.Length; p++)
                {
                    var part = resource.Parts[p];
                    var texView = resource.PartTextures[p];

                    _srvSlot0[0] = texView != null ? texView : _resources.WhiteTextureView!;
                    context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                    CBPerMaterial cbMaterial = ConstantBufferFactory.CreatePerMaterial(
                        0, 
                        texView != null ? Vector4.One : part.BaseColor, 
                        true, 1.0f, 32.0f, 0.5f, 0.0f);
                    
                    _cbPerMaterial.Update(context, ref cbMaterial);
                    _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;
                    context.PSSetConstantBuffers(2, 1, _cbPerMaterialArray);

                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }
            }
        }

        private void RenderBillboards(
            ID3D11DeviceContext context,
            Matrix4x4 view,
            Matrix4x4 proj,
            Vector4 camPos,
            Matrix4x4[] lightViewProjs,
            float[] cascadeSplits,
            bool shadowValid,
            int activeWorldId,
            bool bindEnvironment)
        {
            context.OMSetDepthStencilState(_resources.DepthStencilState);
            var billboards = (List<(Api.Core.SceneObjectId Id, Api.Draw.BillboardDescriptor Desc)>)_sceneDrawManager.GetBillboards();
            if (billboards.Count == 0 || _billboardVb == null || _billboardIb == null || _cbPerFrame == null || _cbPerObject == null || _cbPerMaterial == null) return;

            context.OMSetBlendState(_resources.BillboardBlendState, new Color4(0, 0, 0, 0), -1);

            context.IASetInputLayout(_resources.InputLayout);
            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(_resources.PixelShader);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            int stride = Unsafe.SizeOf<ObjVertex>();
            _vbArray[0] = _billboardVb;
            _strideArray[0] = stride;
            context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
            context.IASetIndexBuffer(_billboardIb, Format.R32_UInt, 0);

            var viewProj = view * proj;

            var lightPos = new Vector4(0, 10, 0, 1.0f);
            var amb = new Vector4(1, 1, 1, 1);
            var lCol = new Vector4(1, 1, 1, 1);

            CBPerFrame cbFrame = ConstantBufferFactory.CreatePerFrameForScene(
                viewProj, camPos, lightPos, amb, lCol,
                lightViewProjs, cascadeSplits,
                0, shadowValid, activeWorldId, 0, bindEnvironment);

            _cbPerFrame.Update(context, ref cbFrame);
            _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            context.VSSetConstantBuffers(0, 1, _cbPerFrameArray);
            context.PSSetConstantBuffers(0, 1, _cbPerFrameArray);

            Vector3 camPos3 = new Vector3(camPos.X, camPos.Y, camPos.Z);

            for (int i = 0; i < billboards.Count; i++)
            {
                var b = billboards[i];
                if (b.Desc.Opacity <= 0) continue;
                var srv = _sceneDrawManager.GetBillboardSrv(b.Id);
                if (srv == null) continue;

                Matrix4x4 world;
                if (b.Desc.FaceCamera)
                {
                    world = Matrix4x4.CreateScale(b.Desc.Size.X, b.Desc.Size.Y, 1.0f) * Matrix4x4.CreateBillboard(b.Desc.WorldPosition, camPos3, Vector3.UnitY, Vector3.UnitZ);
                }
                else
                {
                    world = Matrix4x4.CreateScale(b.Desc.Size.X, b.Desc.Size.Y, 1.0f) * Matrix4x4.CreateTranslation(b.Desc.WorldPosition);
                }

                var wvp = world * viewProj;

                CBPerObject cbObject = new CBPerObject
                {
                    WorldViewProj = Matrix4x4.Transpose(wvp),
                    World = Matrix4x4.Transpose(world)
                };
                _cbPerObject.Update(context, ref cbObject);
                _cbPerObjectArray[0] = _cbPerObject.Buffer;
                context.VSSetConstantBuffers(1, 1, _cbPerObjectArray);

                CBPerMaterial cbMaterial = ConstantBufferFactory.CreatePerMaterial(
                    b.Desc.WorldId,
                    new Vector4(1, 1, 1, b.Desc.Opacity),
                    false, 1.0f, 0.0f, 1.0f, 0.0f);
                _cbPerMaterial.Update(context, ref cbMaterial);
                _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;
                context.PSSetConstantBuffers(2, 1, _cbPerMaterialArray);

                _srvSlot0[0] = srv;
                context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                context.DrawIndexed(6, 0, 0);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cbPerFrame?.Dispose();
            _cbPerObject?.Dispose();
            _cbPerMaterial?.Dispose();
            _billboardVb?.Dispose();
            _billboardIb?.Dispose();
        }
    }
}
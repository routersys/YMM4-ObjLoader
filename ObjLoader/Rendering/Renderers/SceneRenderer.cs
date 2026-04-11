using ObjLoader.Cache.Gpu;
using ObjLoader.Core.Enums;
using ObjLoader.Core.Models;
using ObjLoader.Core.Timeline;
using ObjLoader.Plugin;
using ObjLoader.Rendering.Core;
using ObjLoader.Rendering.Core.Buffers;
using ObjLoader.Rendering.Core.Resources;
using ObjLoader.Rendering.Core.States;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Rendering.Mathematics;
using ObjLoader.Rendering.Shaders.Interfaces;
using ObjLoader.Rendering.Utilities;
using ObjLoader.Services.Rendering.Spatial;
using ObjLoader.Settings;
using System.Buffers;
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
        private readonly IShaderCache _shaderCache;
        private readonly SceneDrawManager _sceneDrawManager;
        private readonly List<int> _tempMainVisibleIndices = new List<int>(1024);
        private readonly List<int> _tempEnvVisibleIndices = new List<int>(1024);
        private readonly HashSet<int> _mainVisibleSet = new HashSet<int>();
        private readonly List<int> _singleVisibleIndices = [0];

        private ConstantBuffer<CBPerFrame>? _cbPerFrame;
        private ConstantBuffer<CBPerObject>? _cbPerObject;
        private ConstantBuffer<CBPerMaterial>? _cbPerMaterialCore;
        private ConstantBuffer<CBSceneEffects>? _cbSceneEffects;
        private ConstantBuffer<CBPostEffects>? _cbPostEffects;
        private ConstantBuffer<CustomShaderCbuf>? _cbCustomShader;
        private ApiObjectRenderer? _apiObjectRenderer;

        private readonly Octree _octree = new Octree();

        private readonly ID3D11Buffer[] _cbPerFrameArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerMaterialArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbSceneEffectsArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPostEffectsArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbCustomShaderArray = new ID3D11Buffer[1];

        private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];
        private readonly int[] _strideArray = new int[1];
        private readonly int[] _offsetArray = { 0 };

        private readonly ID3D11SamplerState[] _samplerArray = new ID3D11SamplerState[1];
        private readonly ID3D11SamplerState[] _shadowSamplerArray = new ID3D11SamplerState[1];

        private readonly ID3D11ShaderResourceView[] _nullSrv1 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _nullSrv4 = new ID3D11ShaderResourceView[4];
        private readonly ID3D11ShaderResourceView[] _srvSlot0 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot1 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot2 = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _srvSlot3 = new ID3D11ShaderResourceView[1];

        private readonly List<(LayerData Data, GpuResourceCacheItem Resource, LayerState State, ID3D11Buffer? OverrideVB)> _singleLayerBuffer = new(1);

        private static readonly ID3D11RenderTargetView[] _emptyRtvs = [];
        private static readonly Color4 _clearBlend = new Color4(0, 0, 0, 0);

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

        public SceneRenderer(
            IGraphicsDevicesAndContext devices,
            D3DResources resources,
            RenderTargetManager renderTargets,
            IShaderCache shaderCache,
            ISceneDrawManager sceneDrawManager)
        {
            _devices = devices;
            _resources = resources;
            _renderTargets = renderTargets;
            _shaderCache = shaderCache;
            _sceneDrawManager = (SceneDrawManager)sceneDrawManager;

            _cbPerFrame = new ConstantBuffer<CBPerFrame>(_devices.D3D.Device);
            _cbPerObject = new ConstantBuffer<CBPerObject>(_devices.D3D.Device);
            _cbPerMaterialCore = new ConstantBuffer<CBPerMaterial>(_devices.D3D.Device);
            _cbSceneEffects = new ConstantBuffer<CBSceneEffects>(_devices.D3D.Device);
            _cbPostEffects = new ConstantBuffer<CBPostEffects>(_devices.D3D.Device);
            _cbCustomShader = new ConstantBuffer<CustomShaderCbuf>(_devices.D3D.Device);
            _apiObjectRenderer = new ApiObjectRenderer(_devices.D3D.Device, _resources);
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
            context.ClearRenderTargetView(_renderTargets.RenderTargetView, _clearBlend);
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

            var layerWorldCenters = ArrayPool<Vector3>.Shared.Rent(Math.Max(1, layers.Count));
            var useLocalCenter = ArrayPool<bool>.Shared.Rent(Math.Max(1, layers.Count));

            try
            {
                Vector3 globalCenter = Vector3.Zero;

                int layerCount = layers.Count;
                CullingBox[] itemBounds = ArrayPool<CullingBox>.Shared.Rent(Math.Max(1, layerCount));
                bool[] disableCulling = ArrayPool<bool>.Shared.Rent(Math.Max(1, layerCount));
                Matrix4x4[] worldMatrices = ArrayPool<Matrix4x4>.Shared.Rent(Math.Max(1, layerCount));

                try
                {
                    if (layerCount > 0)
                    {
                        Vector3 minBounds = new Vector3(float.MaxValue);
                        Vector3 maxBounds = new Vector3(-float.MaxValue);
                        CullingBox rootBounds = new CullingBox();

                        for (int i = 0; i < layerCount; i++)
                        {
                            var item = layers[i];
                            var hierarchyMatrix = BuildHierarchyMatrix(item.State, layerStates);
                            var normalize = Matrix4x4.CreateTranslation(-item.Resource.ModelCenter) * Matrix4x4.CreateScale(item.Resource.ModelScale);
                            var world = normalize * hierarchyMatrix;

                            worldMatrices[i] = world;
                            layerWorldCenters[i] = world.Translation;
                            useLocalCenter[i] = !string.IsNullOrEmpty(item.State.ParentGuid);

                            minBounds = Vector3.Min(minBounds, world.Translation);
                            maxBounds = Vector3.Max(maxBounds, world.Translation);

                            bool isDynamic = item.OverrideVB != null;
                            CullingBox transformedBox = CullingBox.Transform(item.Resource.LocalBoundingBox, world);
                            itemBounds[i] = transformedBox;
                            disableCulling[i] = isDynamic;

                            rootBounds.Expand(transformedBox.Min);
                            rootBounds.Expand(transformedBox.Max);
                        }

                        globalCenter = (minBounds + maxBounds) * 0.5f;

                        _tempMainVisibleIndices.Clear();
                        _octree.Build(rootBounds, itemBounds, disableCulling, layerCount);

                        var mainFrustum = new Frustum(mainViewProj);
                        _octree.GetVisibleItems(mainFrustum, _tempMainVisibleIndices, layerCount);

                        _mainVisibleSet.Clear();
                        for (int vi = 0; vi < _tempMainVisibleIndices.Count; vi++)
                        {
                            _mainVisibleSet.Add(_tempMainVisibleIndices[vi]);
                        }
                    }
                    else
                    {
                        _tempMainVisibleIndices.Clear();
                        _mainVisibleSet.Clear();
                    }

                    if ((layerCount > 0 || _sceneDrawManager.GetExternalObjects().Count > 0) && _renderTargets.DepthStencilView != null)
                    {
                        DepthPrePass(context, layers, worldMatrices, layerCount, mainViewProj, width, height, _tempMainVisibleIndices);
                        context.OMSetRenderTargets(0, _emptyRtvs, null);
                        _renderTargets.CopyDepthBuffer(context);
                        context.ClearDepthStencilView(_renderTargets.DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                    }

                    for (int i = 0; i < layerCount; i++)
                    {
                        if (!_mainVisibleSet.Contains(i)) continue;

                        if (updateEnvironmentMap && _resources.EnvironmentRTVs != null && _resources.EnvironmentDSV != null)
                        {
                            Vector3 captureCenter = useLocalCenter[i] ? layerWorldCenters[i] : globalCenter;

                            context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);

                            for (int face = 0; face < RenderingConstants.EnvironmentMapFaceCount; face++)
                            {
                                context.OMSetRenderTargets(_resources.EnvironmentRTVs[face], _resources.EnvironmentDSV);
                                context.ClearRenderTargetView(_resources.EnvironmentRTVs[face], _clearBlend);
                                context.ClearDepthStencilView(_resources.EnvironmentDSV, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

                                var view = Matrix4x4.CreateLookAt(captureCenter, captureCenter + _envFaceTargets[face], _envFaceUps[face]);
                                view *= Matrix4x4.CreateScale(-1, 1, 1);
                                var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 2.0), 1.0f, RenderingConstants.DefaultNearPlane, RenderingConstants.DefaultFarPlane);
                                var envViewProj = view * proj;
                                var envFrustum = new Frustum(envViewProj);

                                _tempEnvVisibleIndices.Clear();
                                _octree.GetVisibleItems(envFrustum, _tempEnvVisibleIndices, layerCount);

                                RenderScene(context, layers, layerStates, parameter, view, proj, captureCenter.X, captureCenter.Y, captureCenter.Z, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, RenderingConstants.EnvironmentMapSize, RenderingConstants.EnvironmentMapSize, false, _resources.CullNoneRasterizerState, _resources.DepthStencilState, dynamicTextureCache, i, null, _tempEnvVisibleIndices, worldMatrices);

                                var camPosEnv = new Vector4(captureCenter.X, captureCenter.Y, captureCenter.Z, 1.0f);
                                _apiObjectRenderer?.RenderApiObjects(context, _sceneDrawManager, envViewProj, camPosEnv, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, false);
                                _apiObjectRenderer?.RenderBillboards(context, _sceneDrawManager, envViewProj, camPosEnv, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, false);
                            }

                            context.OMSetRenderTargets(0, _emptyRtvs, null);
                            context.GenerateMips(_resources.EnvironmentSRV);
                        }

                        context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);

                        _singleLayerBuffer.Clear();
                        _singleLayerBuffer.Add(layers[i]);
                        _singleVisibleIndices[0] = 0;
                        RenderScene(context, _singleLayerBuffer, layerStates, parameter, mainView, mainProj, camX, camY, camZ, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, width, height, true, null, null, dynamicTextureCache, -1, _renderTargets.DepthCopySRV, _singleVisibleIndices, worldMatrices, i);
                    }
                }
                finally
                {
                    _octree.Clear();
                    ArrayPool<CullingBox>.Shared.Return(itemBounds);
                    ArrayPool<bool>.Shared.Return(disableCulling);
                    ArrayPool<Matrix4x4>.Shared.Return(worldMatrices);
                }

                context.OMSetRenderTargets(_renderTargets.RenderTargetView, _renderTargets.DepthStencilView);
                var camPosRender = new Vector4((float)camX, (float)camY, (float)camZ, 1.0f);
                _apiObjectRenderer?.RenderApiObjects(context, _sceneDrawManager, mainViewProj, camPosRender, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, true);
                _apiObjectRenderer?.RenderBillboards(context, _sceneDrawManager, mainViewProj, camPosRender, lightViewProjs, cascadeSplits, shadowValid, activeWorldId, true);

                context.VSSetShader(null);
                context.PSSetShader(null);
                context.PSSetShaderResources(RenderingConstants.SlotEnvironmentMap, 1, _nullSrv1);
                ClearAllResourceBindings(context);
            }
            finally
            {
                ArrayPool<Vector3>.Shared.Return(layerWorldCenters);
                ArrayPool<bool>.Shared.Return(useLocalCenter);
            }
        }

        private void ClearAllResourceBindings(ID3D11DeviceContext context)
        {
            context.OMSetRenderTargets(0, _emptyRtvs, null);
            context.PSSetShaderResources(0, 4, _nullSrv4);
            context.VSSetShaderResources(0, 4, _nullSrv4);
            context.RSSetState(null);
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
            Matrix4x4[] worldMatrices,
            int layerCount,
            Matrix4x4 viewProj,
            int width,
            int height,
            List<int>? visibleIndices = null)
        {
            if (_cbPerObject == null) return;

            context.OMSetRenderTargets(0, _emptyRtvs, _renderTargets.DepthStencilView);
            context.RSSetState(_resources.RasterizerState);
            context.OMSetDepthStencilState(_resources.DepthStencilState);
            context.RSSetViewport(0, 0, width, height);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            context.IASetInputLayout(_resources.InputLayout);
            context.VSSetShader(_resources.VertexShader);
            context.PSSetShader(null);

            int count = visibleIndices?.Count ?? layerCount;
            var mainFrustum = new Frustum(viewProj);
            for (int idx = 0; idx < count; idx++)
            {
                int li = visibleIndices != null ? visibleIndices[idx] : idx;
                var item = layers[li];
                var resource = item.Resource;
                var world = worldMatrices[li];
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

                bool isAnimated = item.OverrideVB != null;

                for (int i = 0; i < resource.Parts.Length; i++)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) continue;
                    var part = resource.Parts[i];
                    if (part.BaseColor.W < 1.0f) continue;

                    if (!isAnimated)
                    {
                        var pBox = CullingBox.Transform(part.LocalBoundingBox, world);
                        if (!mainFrustum.Intersects(pBox)) continue;
                    }

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
            ID3D11ShaderResourceView? depthSrv = null,
            List<int>? visibleIndices = null,
            Matrix4x4[]? precomputedWorlds = null,
            int precomputedWorldsOffset = 0)
        {
            context.RSSetState(rasterizerState ?? _resources.RasterizerState);
            context.OMSetDepthStencilState(depthStencilState ?? _resources.DepthStencilState);
            context.OMSetBlendState(_resources.BlendState, _clearBlend, -1);
            context.RSSetViewport(0, 0, width, height);
            context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            if (_cbPerFrame == null || _cbPerObject == null || _cbPerMaterialCore == null || _cbSceneEffects == null || _cbPostEffects == null) return;

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
            var mainFrustum = new Frustum(viewProj);

            int count = visibleIndices?.Count ?? layers.Count;
            for (int idx = 0; idx < count; idx++)
            {
                int li = visibleIndices != null ? visibleIndices[idx] : idx;
                if (li == skipIndex) continue;
                var item = layers[li];
                var state = item.State;
                var resource = item.Resource;
                var settings = PluginSettings.Instance;

                var shaders = _shaderCache.Resolve(state.ShaderFilePath);

                var vs = shaders?.VertexShader ?? _resources.VertexShader;
                var ps = shaders?.PixelShader ?? _resources.PixelShader;
                var layout = shaders?.InputLayout ?? _resources.InputLayout;

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

                Matrix4x4 world;
                if (precomputedWorlds != null)
                {
                    world = precomputedWorlds[precomputedWorldsOffset + li];
                }
                else
                {
                    var hierarchyMatrix = BuildHierarchyMatrix(state, layerStates);
                    var normalize = Matrix4x4.CreateTranslation(-resource.ModelCenter) * Matrix4x4.CreateScale(resource.ModelScale);
                    world = normalize * hierarchyMatrix;
                }

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
                bool isAnimated = item.OverrideVB != null;
                int opaqueEnd = resource.OpaquePartCount;
                int partTotal = resource.Parts.Length;

                void DrawPart(int i)
                {
                    if (item.Data.VisibleParts != null && !item.Data.VisibleParts.Contains(i)) return;

                    var part = resource.Parts[i];

                    if (!isAnimated)
                    {
                        var pBox = CullingBox.Transform(part.LocalBoundingBox, world);
                        if (!mainFrustum.Intersects(pBox)) return;
                    }

                    var texView = resource.PartTextures[i];

                    PartMaterialData? material = null;
                    item.Data.PartMaterials?.TryGetValue(i, out material);

                    ID3D11ShaderResourceView? activeTexView = texView;
                    if (material != null && !string.IsNullOrEmpty(material.TexturePath) && dynamicTextureCache != null && dynamicTextureCache.TryGetValue(material.TexturePath, out var dynTex))
                        activeTexView = dynTex;

                    bool hasTexture = activeTexView != null;
                    _srvSlot0[0] = hasTexture ? activeTexView! : _resources.WhiteTextureView!;
                    context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, 1, _srvSlot0);

                    float roughness = (float)(material?.Roughness ?? settings.GetRoughness(wId));
                    float metallic = (float)(material?.Metallic ?? settings.GetMetallic(wId));
                    var resolvedBaseColor = material != null ? RenderUtils.ToVec4(material.BaseColor) : part.BaseColor;
                    var uiColorVec = hasTexture ? Vector4.One : new Vector4(state.BaseColor.ScR, state.BaseColor.ScG, state.BaseColor.ScB, state.BaseColor.ScA);
                    var partColor = resolvedBaseColor * uiColorVec;

                    if (shaders?.VertexShader != null && _cbCustomShader != null)
                    {
                        var cbCustom = new CustomShaderCbuf
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
                            SpecularIntensity = 1.0f,
                            Shininess = (float)state.Shininess,
                            GridColor = Vector4.Zero,
                            GridAxisColor = Vector4.Zero
                        };
                        _cbCustomShader.Update(context, ref cbCustom);
                        _cbCustomShaderArray[0] = _cbCustomShader.Buffer;
                        context.VSSetConstantBuffers(0, 1, _cbCustomShaderArray);
                        context.PSSetConstantBuffers(0, 1, _cbCustomShaderArray);
                    }

                    ConstantBufferFactory.CreatePerMaterial(wId, partColor, state.IsLightEnabled, (float)state.Diffuse, (float)state.Shininess, roughness, metallic, out var cbCore, out var cbScene, out var cbPost);

                    _cbPerMaterialCore.Update(context, ref cbCore);
                    _cbSceneEffects.Update(context, ref cbScene);
                    _cbPostEffects.Update(context, ref cbPost);
                    _cbPerMaterialArray[0] = _cbPerMaterialCore.Buffer;
                    _cbSceneEffectsArray[0] = _cbSceneEffects.Buffer;
                    _cbPostEffectsArray[0] = _cbPostEffects.Buffer;
                    context.PSSetConstantBuffers(RenderingConstants.CbSlotPerMaterial, 1, _cbPerMaterialArray);
                    context.PSSetConstantBuffers(RenderingConstants.CbSlotSceneEffects, 1, _cbSceneEffectsArray);
                    context.PSSetConstantBuffers(RenderingConstants.CbSlotPostEffects, 1, _cbPostEffectsArray);

                    context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }

                for (int i = 0; i < opaqueEnd; i++) DrawPart(i);
                for (int i = opaqueEnd; i < partTotal; i++) DrawPart(i);
            }

            context.PSSetShaderResources(0, 1, _nullSrv1);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _apiObjectRenderer?.Dispose();
            _cbPerFrame?.Dispose();
            _cbPerObject?.Dispose();
            _cbPerMaterialCore?.Dispose();
            _cbSceneEffects?.Dispose();
            _cbPostEffects?.Dispose();
            _cbCustomShader?.Dispose();
        }
    }
}
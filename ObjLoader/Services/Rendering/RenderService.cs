using ObjLoader.Infrastructure;
using ObjLoader.Rendering.Core;
using ObjLoader.Settings;
using ObjLoader.Services.Textures;
using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Managers.Interfaces;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using D3D11MapFlags = Vortice.Direct3D11.MapFlags;
using Matrix4x4 = System.Numerics.Matrix4x4;
using ObjLoader.Core.Models;
using ObjLoader.Cache.Gpu;
using ObjLoader.Utilities.Logging;
using System.Collections.Generic;
using System;

namespace ObjLoader.Services.Rendering
{
    internal class RenderService : IDisposable
    {
        private const int MaxLayerArrayCapacity = 1024;

        private readonly string _instanceId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly object _bitmapLock = new object();

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private ID3D11Texture2D? _renderTarget;
        private ID3D11RenderTargetView? _rtv;
        private ID3D11Texture2D? _depthStencil;
        private ID3D11DepthStencilView? _dsv;
        private ID3D11Texture2D? _stagingTexture;
        private ID3D11Texture2D? _resolveTexture;
        private D3DResources? _d3dResources;
        private ID3D11Buffer? _gridVertexBuffer;
        private int _viewportWidth;
        private int _viewportHeight;

        private readonly ID3D11ShaderResourceView[] _nullSrv = new ID3D11ShaderResourceView[1];
        private readonly ID3D11ShaderResourceView[] _nullSrv4 = new ID3D11ShaderResourceView[4];
        private readonly ID3D11Buffer[] _cbPerFrameArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerObjectArray = new ID3D11Buffer[1];
        private readonly ID3D11Buffer[] _cbPerMaterialArray = new ID3D11Buffer[1];

        private ConstantBuffer<CBPerFrame>? _cbPerFrame;
        private ConstantBuffer<CBPerObject>? _cbPerObject;
        private ConstantBuffer<CBPerMaterial>? _cbPerMaterial;

        private readonly ID3D11ShaderResourceView[] _texArray = new ID3D11ShaderResourceView[1];
        private readonly ID3D11SamplerState[] _samplerArray = new ID3D11SamplerState[1];
        private readonly ID3D11ShaderResourceView[] _shadowSrvArray = new ID3D11ShaderResourceView[1];
        private readonly ID3D11SamplerState[] _shadowSamplerArray = new ID3D11SamplerState[1];
        private readonly ID3D11Buffer[] _gridVbArray = new ID3D11Buffer[1];
        private readonly int[] _gridStrideArray = new int[] { 12 };
        private readonly int[] _gridOffsetArray = new int[] { 0 };
        private readonly int[] _strideArray = new int[1];
        private readonly int[] _offsetArray = new int[] { 0 };
        private readonly ID3D11Buffer[] _vbArray = new ID3D11Buffer[1];

        private readonly List<(int LayerIndex, int PartIndex)> _opaquePartList = new();
        private readonly List<TransparentPart> _transparentParts = new List<TransparentPart>();
        private readonly HashSet<string> _usedPathsBuffer = new HashSet<string>();

        private Matrix4x4[]? _cachedLayerWorlds;
        private Matrix4x4[]? _cachedLayerWvps;
        private int _cachedLayerCapacity;

        private byte[]? _stagingBuffer;
        private int _stagingBufferSize;

        private readonly ITextureService _textureService;
        private readonly IDynamicTextureManager _dynamicTextureManager;

        private bool _isDisposed = false;

        public RenderService()
        {
            _textureService = new TextureService();
            _dynamicTextureManager = new DynamicTextureManager(_textureService);
        }

        private struct TransparentPart : IComparable<TransparentPart>
        {
            public int LayerIndex;
            public int PartIndex;
            public float DistanceSq;

            public int CompareTo(TransparentPart other)
            {
                return other.DistanceSq.CompareTo(DistanceSq);
            }
        }

        public WriteableBitmap? SceneImage { get; private set; }
        public D3DResources? Resources => _d3dResources;
        public ID3D11Device? Device => _device;

        private string TrackingKey(string resourceName) => $"RenderSvc:{_instanceId}:{resourceName}";

        public void Initialize()
        {
            var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, new[] { FeatureLevel.Level_11_0 }, out _device, out _context);
            if (result.Failure || _device == null) return;
            _d3dResources = new D3DResources(_device!);

            float gSize = RenderingConstants.GridSize;
            float[] gridVerts = {
                -gSize, 0, gSize, -gSize, 0, -gSize, gSize, 0, gSize,
                gSize, 0, gSize, -gSize, 0, -gSize, gSize, 0, -gSize
            };
            int gridBufferSize = gridVerts.Length * 4;
            var vDesc = new BufferDescription(gridBufferSize, BindFlags.VertexBuffer, ResourceUsage.Immutable);
            unsafe
            {
                fixed (float* p = gridVerts) _gridVertexBuffer = _device.CreateBuffer(vDesc, new SubresourceData(p));
            }

            ResourceTracker.Instance.Register(TrackingKey("GridVertexBuffer"), "ID3D11Buffer:Grid", _gridVertexBuffer, gridBufferSize);

            _cbPerFrame = new ConstantBuffer<CBPerFrame>(_device);
            _cbPerObject = new ConstantBuffer<CBPerObject>(_device);
            _cbPerMaterial = new ConstantBuffer<CBPerMaterial>(_device);

            _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            _cbPerObjectArray[0] = _cbPerObject.Buffer;
            _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;

            _samplerArray[0] = _d3dResources.SamplerState;
            _shadowSamplerArray[0] = _d3dResources.ShadowSampler;
            _gridVbArray[0] = _gridVertexBuffer;
        }

        public void Resize(int width, int height)
        {
            if (width < 1 || height < 1 || _device == null) return;

            var settings = PluginSettings.Instance;
            int scaleFactor = 1;
            int sampleCount = 4;

            switch (settings.RenderQuality)
            {
                case RenderQuality.High:
                    scaleFactor = 2;
                    sampleCount = 8;
                    break;
                case RenderQuality.Standard:
                    scaleFactor = 1;
                    sampleCount = 4;
                    break;
                case RenderQuality.Low:
                    scaleFactor = 1;
                    sampleCount = 1;
                    break;
            }

            int targetWidth = width * scaleFactor;
            int targetHeight = height * scaleFactor;

            ID3D11Texture2D? newRenderTarget = null;
            ID3D11RenderTargetView? newRtv = null;
            ID3D11Texture2D? newDepthStencil = null;
            ID3D11DepthStencilView? newDsv = null;
            ID3D11Texture2D? newResolveTexture = null;
            ID3D11Texture2D? newStagingTexture = null;

            try
            {
                var texDesc = new Texture2DDescription
                {
                    Width = targetWidth,
                    Height = targetHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(sampleCount, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget,
                    CPUAccessFlags = CpuAccessFlags.None
                };
                newRenderTarget = _device.CreateTexture2D(texDesc);
                newRtv = _device.CreateRenderTargetView(newRenderTarget);

                var depthDesc = new Texture2DDescription
                {
                    Width = targetWidth,
                    Height = targetHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.D24_UNorm_S8_UInt,
                    SampleDescription = new SampleDescription(sampleCount, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.DepthStencil,
                    CPUAccessFlags = CpuAccessFlags.None
                };
                newDepthStencil = _device.CreateTexture2D(depthDesc);
                newDsv = _device.CreateDepthStencilView(newDepthStencil);

                var resolveDesc = texDesc;
                resolveDesc.SampleDescription = new SampleDescription(1, 0);
                newResolveTexture = _device.CreateTexture2D(resolveDesc);

                var stagingDesc = resolveDesc;
                stagingDesc.Usage = ResourceUsage.Staging;
                stagingDesc.BindFlags = BindFlags.None;
                stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
                newStagingTexture = _device.CreateTexture2D(stagingDesc);
            }
            catch
            {
                newRtv?.Dispose();
                newRenderTarget?.Dispose();
                newDsv?.Dispose();
                newDepthStencil?.Dispose();
                newResolveTexture?.Dispose();
                newStagingTexture?.Dispose();
                throw;
            }

            UnregisterResizeResources();

            _rtv?.Dispose();
            _renderTarget?.Dispose();
            _dsv?.Dispose();
            _depthStencil?.Dispose();
            _stagingTexture?.Dispose();
            _resolveTexture?.Dispose();

            _renderTarget = newRenderTarget;
            _rtv = newRtv;
            _depthStencil = newDepthStencil;
            _dsv = newDsv;
            _resolveTexture = newResolveTexture;
            _stagingTexture = newStagingTexture;

            _viewportWidth = targetWidth;
            _viewportHeight = targetHeight;

            long renderTargetBytes = (long)targetWidth * targetHeight * 4 * sampleCount;
            ResourceTracker.Instance.Register(TrackingKey("RenderTarget"), "ID3D11Texture2D:RT", _renderTarget, renderTargetBytes);

            long depthStencilBytes = (long)targetWidth * targetHeight * 4 * sampleCount;
            ResourceTracker.Instance.Register(TrackingKey("DepthStencil"), "ID3D11Texture2D:DS", _depthStencil, depthStencilBytes);

            long resolveBytes = (long)targetWidth * targetHeight * 4;
            ResourceTracker.Instance.Register(TrackingKey("ResolveTexture"), "ID3D11Texture2D:Resolve", _resolveTexture, resolveBytes);

            long stagingBytes = (long)targetWidth * targetHeight * 4;
            ResourceTracker.Instance.Register(TrackingKey("StagingTexture"), "ID3D11Texture2D:Staging", _stagingTexture, stagingBytes);

            int requiredBufferSize = targetWidth * targetHeight * 4;
            if (_stagingBuffer == null || _stagingBufferSize < requiredBufferSize)
            {
                _stagingBuffer = new byte[requiredBufferSize];
                _stagingBufferSize = requiredBufferSize;
            }

            lock (_bitmapLock)
            {
                SceneImage = new WriteableBitmap(targetWidth, targetHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
            }
        }

        private void UnregisterResizeResources()
        {
            ResourceTracker.Instance.Unregister(TrackingKey("RenderTarget"));
            ResourceTracker.Instance.Unregister(TrackingKey("DepthStencil"));
            ResourceTracker.Instance.Unregister(TrackingKey("ResolveTexture"));
            ResourceTracker.Instance.Unregister(TrackingKey("StagingTexture"));
        }

        private void EnsureLayerArrayCapacity(int count)
        {
            if (_cachedLayerCapacity >= count && _cachedLayerWorlds != null && _cachedLayerWvps != null)
            {
                if (_cachedLayerCapacity > MaxLayerArrayCapacity && count <= MaxLayerArrayCapacity)
                {
                    int shrunkCapacity = Math.Max(count, 8);
                    _cachedLayerWorlds = new Matrix4x4[shrunkCapacity];
                    _cachedLayerWvps = new Matrix4x4[shrunkCapacity];
                    _cachedLayerCapacity = shrunkCapacity;
                }
                return;
            }
            int newCapacity = Math.Max(count, 8);
            _cachedLayerWorlds = new Matrix4x4[newCapacity];
            _cachedLayerWvps = new Matrix4x4[newCapacity];
            _cachedLayerCapacity = newCapacity;
        }

        public void Render(
            List<LayerRenderData> layers,
            System.Numerics.Matrix4x4 view,
            System.Numerics.Matrix4x4 proj,
            System.Numerics.Vector3 camPos,
            System.Windows.Media.Color themeColor,
            bool isWireframe,
            bool isGridVisible,
            bool isInfiniteGrid,
            double gridScale,
            bool isInteracting,
            bool enableShadow = true)
        {
            lock (ObjLoaderSource.SharedRenderLock)
            {
                if (_isDisposed) return;
                if (_device == null || _context == null || _rtv == null || _d3dResources == null || SceneImage == null || _stagingTexture == null) return;

                if (IsDeviceLost()) return;

                try
                {
                    RenderInternal(layers, view, proj, camPos, themeColor, isWireframe, isGridVisible, isInfiniteGrid, gridScale, isInteracting, enableShadow);
                }
                catch (SharpGen.Runtime.SharpGenException ex) when (ex.HResult == unchecked((int)0x887A0005) || ex.HResult == unchecked((int)0x887A0006))
                {
                    Logger<RenderService>.Instance.Error("Device lost during render", ex);
                }
            }
            UpdateBitmapFromStagingBuffer();
        }

        private bool IsDeviceLost()
        {
            if (_device == null) return true;
            try
            {
                var reason = _device.DeviceRemovedReason;
                return reason.Failure;
            }
            catch (Exception ex)
            {
                Logger<RenderService>.Instance.Error("Failed to check device status", ex);
                return true;
            }
        }

        private void RenderInternal(
            List<LayerRenderData> layers,
            System.Numerics.Matrix4x4 view,
            System.Numerics.Matrix4x4 proj,
            System.Numerics.Vector3 camPos,
            System.Windows.Media.Color themeColor,
            bool isWireframe,
            bool isGridVisible,
            bool isInfiniteGrid,
            double gridScale,
            bool isInteracting,
            bool enableShadow)
        {
            if (_context == null || _d3dResources == null || _rtv == null || _dsv == null ||
                _stagingTexture == null || _resolveTexture == null || _renderTarget == null || SceneImage == null) return;

            var settings = PluginSettings.Instance;

            if (_cbPerFrameArray[0] == null && _cbPerFrame != null) _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            if (_cbPerObjectArray[0] == null && _cbPerObject != null) _cbPerObjectArray[0] = _cbPerObject.Buffer;
            if (_cbPerMaterialArray[0] == null && _cbPerMaterial != null) _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;

            if (_samplerArray[0] == null) _samplerArray[0] = _d3dResources.SamplerState;
            if (_shadowSamplerArray[0] == null) _shadowSamplerArray[0] = _d3dResources.ShadowSampler;

            _context.PSSetShaderResources(RenderingConstants.SlotShadowMap, _nullSrv);

            _usedPathsBuffer.Clear();
            foreach (var layer in layers)
            {
                if (layer.Data?.PartMaterials != null)
                {
                    foreach (var pm in layer.Data.PartMaterials.Values)
                    {
                        if (!string.IsNullOrEmpty(pm.TexturePath))
                        {
                            _usedPathsBuffer.Add(pm.TexturePath!);
                        }
                    }
                }
            }
            if (_device != null)
            {
                _dynamicTextureManager.Prepare(_usedPathsBuffer, _device);
            }

            ComputeLayerTransforms(layers, view, proj);

            var (renderShadowMap, lightViewProj) = RenderShadowPass(layers, enableShadow);

            var (gridColor, axisColor) = PrepareRenderTargets(themeColor, isWireframe);

            ClassifyPartsByOpacity(layers, camPos);

            var dynamicTextures = _dynamicTextureManager.Textures;

            RenderOpaqueParts(layers, gridColor, axisColor, isInteracting, lightViewProj, enableShadow, renderShadowMap, dynamicTextures, view, proj, camPos, false);

            RenderTransparentParts(layers, gridColor, axisColor, isInteracting, lightViewProj, enableShadow, isWireframe, dynamicTextures, view, proj, camPos, false);

            RenderGridIfVisible(isGridVisible, isInfiniteGrid, gridScale, view, proj, camPos, gridColor, axisColor);

            CopyToStagingBuffer();
        }

        private void ComputeLayerTransforms(List<LayerRenderData> layers, Matrix4x4 view, Matrix4x4 proj)
        {
            var settings = PluginSettings.Instance;

            if (settings.ShadowMappingEnabled)
            {
                bool useCascaded = settings.CascadedShadowsEnabled;
                if (settings.ShadowResolution != _d3dResources!.CurrentShadowMapSize || _d3dResources.IsCascaded != useCascaded)
                {
                    _d3dResources.EnsureShadowMapSize(settings.ShadowResolution, useCascaded);
                }

                if (_d3dResources.ShadowMapSRV != null)
                {
                    _shadowSrvArray[0] = _d3dResources.ShadowMapSRV;
                }
            }

            EnsureLayerArrayCapacity(layers.Count);
            var layerWorlds = _cachedLayerWorlds!;
            var layerWvps = _cachedLayerWvps!;

            Matrix4x4 axisConversion = Matrix4x4.Identity;
            switch (settings.CoordinateSystem)
            {
                case CoordinateSystem.RightHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)); break;
                case CoordinateSystem.LeftHandedYUp: axisConversion = Matrix4x4.CreateScale(1, 1, -1); break;
                case CoordinateSystem.LeftHandedZUp: axisConversion = Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * Matrix4x4.CreateScale(1, 1, -1); break;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var modelResource = layer.Resource;

                Matrix4x4 world;
                if (layer.WorldMatrixOverride.HasValue)
                {
                    world = layer.WorldMatrixOverride.Value;
                }
                else
                {
                    var normalize = Matrix4x4.CreateTranslation(-modelResource.ModelCenter) * Matrix4x4.CreateScale(modelResource.ModelScale);
                    normalize *= Matrix4x4.CreateTranslation(0, (float)layer.HeightOffset, 0);

                    float scale = (float)(layer.Scale / 100.0);
                    float rx = (float)(layer.Rx * Math.PI / 180.0);
                    float ry = (float)(layer.Ry * Math.PI / 180.0);
                    float rz = (float)(layer.Rz * Math.PI / 180.0);
                    float tx = (float)layer.X;
                    float ty = (float)layer.Y;
                    float tz = (float)layer.Z;

                    var placement = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationZ(rz) * Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateTranslation(tx, ty, tz);

                    world = normalize * axisConversion * placement;
                }

                layerWorlds[i] = world;
                layerWvps[i] = world * view * proj;
            }
        }

        private (bool renderShadowMap, Matrix4x4 lightViewProj) RenderShadowPass(List<LayerRenderData> layers, bool enableShadow)
        {
            var settings = PluginSettings.Instance;
            bool renderShadowMap = false;
            Matrix4x4 lightViewProj = Matrix4x4.Identity;

            if (!enableShadow || !settings.ShadowMappingEnabled || _context == null) return (false, lightViewProj);

            bool useCascaded = settings.CascadedShadowsEnabled;
            if (settings.ShadowResolution != _d3dResources!.CurrentShadowMapSize || _d3dResources.IsCascaded != useCascaded)
            {
                _d3dResources.EnsureShadowMapSize(settings.ShadowResolution, useCascaded);
            }

            if (_d3dResources.ShadowMapDSVs == null || _d3dResources.ShadowMapDSVs.Length == 0)
                return (false, lightViewProj);

            LayerRenderData shadowCasterLayer = default;
            bool foundCaster = false;
            Matrix4x4 shadowCasterWorld = Matrix4x4.Identity;

            for (int i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if (l.LightEnabled && (l.LightType == 1 || l.LightType == 2))
                {
                    shadowCasterLayer = l;
                    shadowCasterWorld = _cachedLayerWorlds![i];
                    foundCaster = true;
                    break;
                }
            }

            if (!foundCaster) return (false, lightViewProj);
            if (!settings.GetShadowEnabled(shadowCasterLayer.WorldId)) return (false, lightViewProj);

            renderShadowMap = true;
            if (_d3dResources.ShadowMapSRV != null) _shadowSrvArray[0] = _d3dResources.ShadowMapSRV;

            var rawLightPos = new System.Numerics.Vector3((float)shadowCasterLayer.LightX, (float)shadowCasterLayer.LightY, (float)shadowCasterLayer.LightZ);
            System.Numerics.Vector3 lightPosVec;

            if (shadowCasterLayer.LightType == 2)
                lightPosVec = System.Numerics.Vector3.TransformNormal(rawLightPos, shadowCasterWorld);
            else
                lightPosVec = System.Numerics.Vector3.Transform(rawLightPos, shadowCasterWorld);

            Matrix4x4 lightView;
            Matrix4x4 lightProj;
            float shadowRange = (float)settings.SunLightShadowRange;

            if (shadowCasterLayer.LightType == 2)
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

            lightViewProj = lightView * lightProj;

            _context!.PSSetShaderResources(RenderingConstants.SlotShadowMap, _nullSrv);

            _context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), _d3dResources.ShadowMapDSVs[0]);
            _context.ClearDepthStencilView(_d3dResources.ShadowMapDSVs[0], DepthStencilClearFlags.Depth, 1.0f, 0);
            _context.RSSetState(_d3dResources.ShadowRasterizerState);
            _context.RSSetViewport(0, 0, settings.ShadowResolution, settings.ShadowResolution);
            _context.VSSetShader(_d3dResources.VertexShader);
            _context.PSSetShader(null);
            _context.IASetInputLayout(_d3dResources.InputLayout);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.LightEnabled) continue;
                if (layer.LightType == 0 && !foundCaster) continue;

                var modelResource = layer.Resource;
                var world = _cachedLayerWorlds![i];
                var wvpShadow = world * lightViewProj;

                int stride = Unsafe.SizeOf<ObjVertex>();
                _vbArray[0] = layer.OverrideVB ?? modelResource.VertexBuffer;
                _strideArray[0] = stride;
                _context!.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                _context.IASetIndexBuffer(modelResource.IndexBuffer, Format.R32_UInt, 0);

                CBPerObject cbShadow = new CBPerObject
                {
                    WorldViewProj = Matrix4x4.Transpose(wvpShadow),
                    World = Matrix4x4.Transpose(world)
                };

                if (_context != null && _cbPerObject != null)
                {
                    _cbPerObject.Update(_context, ref cbShadow);
                    _cbPerObjectArray[0] = _cbPerObject.Buffer;
                    _context.VSSetConstantBuffers(1, 1, _cbPerObjectArray);
                }

                for (int p = 0; p < modelResource.Parts.Length; p++)
                {
                    if (layer.VisibleParts != null && !layer.VisibleParts.Contains(p)) continue;
                    var part = modelResource.Parts[p];
                    if (part.BaseColor.W >= 0.99f)
                    {
                        _context!.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                    }
                }
            }

            _context!.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);
            _context.Flush();

            return (renderShadowMap, lightViewProj);
        }

        private (System.Numerics.Vector4 gridColor, System.Numerics.Vector4 axisColor) PrepareRenderTargets(System.Windows.Media.Color themeColor, bool isWireframe)
        {
            double brightness = themeColor.R * 0.299 + themeColor.G * 0.587 + themeColor.B * 0.114;
            Color4 clearColor;
            System.Numerics.Vector4 gridColor;
            System.Numerics.Vector4 axisColor;

            if (brightness < 20)
            {
                clearColor = RenderingConstants.ClearColorDark;
                gridColor = RenderingConstants.GridColorDark;
                axisColor = RenderingConstants.AxisColorDark;
            }
            else if (brightness < 128)
            {
                clearColor = RenderingConstants.ClearColorMedium;
                gridColor = RenderingConstants.GridColorMedium;
                axisColor = RenderingConstants.AxisColorMedium;
            }
            else
            {
                clearColor = RenderingConstants.ClearColorLight;
                gridColor = RenderingConstants.GridColorLight;
                axisColor = RenderingConstants.AxisColorLight;
            }

            _context!.OMSetRenderTargets(_rtv!, _dsv);
            _context.ClearRenderTargetView(_rtv!, clearColor);
            _context.ClearDepthStencilView(_dsv!, DepthStencilClearFlags.Depth, 1.0f, 0);

            _context.RSSetState(isWireframe ? _d3dResources!.WireframeRasterizerState : _d3dResources!.RasterizerState);
            _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
            _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
            _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);

            return (gridColor, axisColor);
        }

        private void ClassifyPartsByOpacity(List<LayerRenderData> layers, System.Numerics.Vector3 camPos)
        {
            _opaquePartList.Clear();
            _transparentParts.Clear();

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var modelResource = layer.Resource;
                var world = _cachedLayerWorlds![i];

                for (int p = 0; p < modelResource.Parts.Length; p++)
                {
                    if (layer.VisibleParts != null && !layer.VisibleParts.Contains(p)) continue;

                    var part = modelResource.Parts[p];
                    if (part.BaseColor.W < 0.99f)
                    {
                        var center = System.Numerics.Vector3.Transform(part.Center, world);
                        float distSq = System.Numerics.Vector3.DistanceSquared(camPos, center);
                        _transparentParts.Add(new TransparentPart { LayerIndex = i, PartIndex = p, DistanceSq = distSq });
                    }
                    else
                    {
                        _opaquePartList.Add((i, p));
                    }
                }
            }
        }

        private void RenderOpaqueParts(
            List<LayerRenderData> layers,
            System.Numerics.Vector4 gridColor,
            System.Numerics.Vector4 axisColor,
            bool isInteracting,
            Matrix4x4 lightViewProj,
            bool enableShadow,
            bool renderShadowMap,
            IReadOnlyDictionary<string, ID3D11ShaderResourceView> dynamicTextures,
            Matrix4x4 view,
            Matrix4x4 proj,
            System.Numerics.Vector3 camPos,
            bool bindEnvironment)
        {
            _context!.VSSetShader(_d3dResources!.VertexShader);
            _context.PSSetShader(_d3dResources.PixelShader);
            _context.PSSetSamplers(RenderingConstants.SlotStandardSampler, _samplerArray);
            if (renderShadowMap && _shadowSrvArray[0] != null)
            {
                _context.PSSetShaderResources(RenderingConstants.SlotShadowMap, _shadowSrvArray);
                _context.PSSetSamplers(RenderingConstants.SlotShadowSampler, _shadowSamplerArray);
            }
            else
            {
                _context.PSSetShaderResources(RenderingConstants.SlotShadowMap, _nullSrv);
            }

            _context.IASetInputLayout(_d3dResources.InputLayout);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            int lastLayerIndex = -1;
            foreach (var (layerIndex, partIndex) in _opaquePartList)
            {
                var layer = layers[layerIndex];
                var modelResource = layer.Resource;

                if (layerIndex != lastLayerIndex)
                {
                    int stride = Unsafe.SizeOf<ObjVertex>();
                    _vbArray[0] = layer.OverrideVB ?? modelResource.VertexBuffer;
                    _strideArray[0] = stride;
                    _context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                    _context.IASetIndexBuffer(modelResource.IndexBuffer, Format.R32_UInt, 0);
                    lastLayerIndex = layerIndex;
                }
                DrawPart(layer, modelResource, partIndex, _cachedLayerWorlds![layerIndex], _cachedLayerWvps![layerIndex], layer.WorldId, gridColor, axisColor, isInteracting, lightViewProj, enableShadow, dynamicTextures, view, proj, camPos, bindEnvironment);
            }
        }

        private void RenderTransparentParts(
            List<LayerRenderData> layers,
            System.Numerics.Vector4 gridColor,
            System.Numerics.Vector4 axisColor,
            bool isInteracting,
            Matrix4x4 lightViewProj,
            bool enableShadow,
            bool isWireframe,
            IReadOnlyDictionary<string, ID3D11ShaderResourceView> dynamicTextures,
            Matrix4x4 view,
            Matrix4x4 proj,
            System.Numerics.Vector3 camPos,
            bool bindEnvironment)
        {
            if (_transparentParts.Count == 0) return;

            _transparentParts.Sort();

            _context!.OMSetDepthStencilState(_d3dResources!.DepthStencilStateNoWrite);
            if (!isWireframe)
            {
                _context.RSSetState(_d3dResources.CullNoneRasterizerState);
            }

            int lastLayerIndex = -1;
            foreach (var tp in _transparentParts)
            {
                var layer = layers[tp.LayerIndex];
                if (layer.VisibleParts != null && !layer.VisibleParts.Contains(tp.PartIndex)) continue;

                var resource = layer.Resource;

                if (tp.LayerIndex != lastLayerIndex)
                {
                    int stride = Unsafe.SizeOf<ObjVertex>();
                    _vbArray[0] = layer.OverrideVB ?? resource.VertexBuffer;
                    _strideArray[0] = stride;
                    _context.IASetVertexBuffers(0, 1, _vbArray, _strideArray, _offsetArray);
                    _context.IASetIndexBuffer(resource.IndexBuffer, Format.R32_UInt, 0);
                    lastLayerIndex = tp.LayerIndex;
                }
                DrawPart(layer, resource, tp.PartIndex, _cachedLayerWorlds![tp.LayerIndex], _cachedLayerWvps![tp.LayerIndex], layer.WorldId, gridColor, axisColor, isInteracting, lightViewProj, enableShadow, dynamicTextures, view, proj, camPos, bindEnvironment);
            }
            _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
            if (!isWireframe)
            {
                _context.RSSetState(_d3dResources.RasterizerState);
            }
        }

        private void RenderGridIfVisible(
            bool isGridVisible,
            bool isInfiniteGrid,
            double gridScale,
            Matrix4x4 view,
            Matrix4x4 proj,
            System.Numerics.Vector3 camPos,
            System.Numerics.Vector4 gridColor,
            System.Numerics.Vector4 axisColor)
        {
            if (!isGridVisible || _gridVertexBuffer == null) return;

            _context!.IASetInputLayout(_d3dResources!.GridInputLayout);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            _context.IASetVertexBuffers(0, 1, _gridVbArray, _gridStrideArray, _gridOffsetArray);
            _context.VSSetShader(_d3dResources.GridVertexShader);
            _context.PSSetShader(_d3dResources.GridPixelShader);
            _context.OMSetBlendState(_d3dResources.GridBlendState, new Color4(0, 0, 0, 0), -1);

            Matrix4x4 gridWorld = Matrix4x4.Identity;
            if (!isInfiniteGrid)
            {
                float finiteScale = (float)(gridScale * RenderingConstants.GridScaleBase / RenderingConstants.GridSize);
                if (finiteScale < 0.001f) finiteScale = 0.001f;
                gridWorld = Matrix4x4.CreateScale(finiteScale);
            }

            CBPerFrame cbFrame = new CBPerFrame
            {
                ViewProj = Matrix4x4.Transpose(view * proj),
                CameraPos = new System.Numerics.Vector4(camPos, 1),
                GridColor = gridColor,
                GridAxisColor = axisColor,
                EnvironmentParam = new System.Numerics.Vector4(0, 0, 0, isInfiniteGrid ? 1.0f : 0.0f)
            };

            CBPerObject cbObject = new CBPerObject
            {
                WorldViewProj = Matrix4x4.Transpose(gridWorld * view * proj),
                World = Matrix4x4.Transpose(gridWorld)
            };

            CBPerMaterial cbMaterial = default;

            UpdateConstantBuffers(ref cbFrame, ref cbObject, ref cbMaterial);
            _context.Draw(6, 0);

            _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
        }

        private void CopyToStagingBuffer()
        {
            ClearAllResourceBindings();

            _context!.ResolveSubresource(_resolveTexture, 0, _renderTarget, 0, Format.B8G8R8A8_UNorm);
            _context.CopyResource(_stagingTexture, _resolveTexture);
            _context.Flush();

            var map = _context.Map(_stagingTexture!, 0, MapMode.Read, D3D11MapFlags.None);
            try
            {
                int rowBytes = _viewportWidth * 4;
                if (_stagingBuffer != null && _stagingBuffer.Length >= _viewportHeight * rowBytes)
                {
                    unsafe
                    {
                        var srcPtr = (byte*)map.DataPointer;
                        fixed (byte* dstBase = _stagingBuffer)
                        {
                            for (int r = 0; r < _viewportHeight; r++)
                            {
                                Buffer.MemoryCopy(srcPtr + (r * map.RowPitch), dstBase + (r * rowBytes), rowBytes, rowBytes);
                            }
                        }
                    }
                }
            }
            finally
            {
                _context.Unmap(_stagingTexture, 0);
            }
        }

        private void UpdateBitmapFromStagingBuffer()
        {
            lock (_bitmapLock)
            {
                var sceneImage = SceneImage;
                if (sceneImage == null || _stagingBuffer == null) return;

                int rowBytes = _viewportWidth * 4;
                bool locked = false;
                try
                {
                    sceneImage.Lock();
                    locked = true;
                    unsafe
                    {
                        var dstPtr = (byte*)sceneImage.BackBuffer;
                        fixed (byte* srcBase = _stagingBuffer)
                        {
                            for (int r = 0; r < _viewportHeight; r++)
                            {
                                Buffer.MemoryCopy(srcBase + (r * rowBytes), dstPtr + (r * sceneImage.BackBufferStride), sceneImage.BackBufferStride, rowBytes);
                            }
                        }
                    }
                    sceneImage.AddDirtyRect(new Int32Rect(0, 0, _viewportWidth, _viewportHeight));
                }
                finally
                {
                    if (locked)
                    {
                        sceneImage.Unlock();
                    }
                }
            }
        }

        private void ClearAllResourceBindings()
        {
            if (_context == null) return;

            _context.OMSetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), null);

            _context.PSSetShaderResources(0, _nullSrv4);
            _context.VSSetShaderResources(0, _nullSrv4);
        }

        private void DrawPart(LayerRenderData layer, GpuResourceCacheItem resource, int partIndex, Matrix4x4 world, Matrix4x4 wvp, int wId, System.Numerics.Vector4 gridColor, System.Numerics.Vector4 axisColor, bool isInteracting, Matrix4x4 lightViewProj, bool enableShadow, IReadOnlyDictionary<string, ID3D11ShaderResourceView> dynamicTextures, Matrix4x4 view, Matrix4x4 proj, System.Numerics.Vector3 camPos, bool bindEnvironment)
        {
            if (_context == null || _d3dResources == null) return;

            var settings = PluginSettings.Instance;
            var part = resource.Parts[partIndex];
            var texView = resource.PartTextures[partIndex];

            ID3D11ShaderResourceView? activeTexView = texView;

            PartMaterialData? material = null;
            if (layer.Data != null && layer.Data.PartMaterials != null)
            {
                layer.Data.PartMaterials.TryGetValue(partIndex, out material);
            }

            if (material != null && !string.IsNullOrEmpty(material.TexturePath) && dynamicTextures.TryGetValue(material.TexturePath!, out var dynTex))
            {
                activeTexView = dynTex;
            }

            _texArray[0] = activeTexView ?? _d3dResources.WhiteTextureView!;
            _context.PSSetShaderResources(RenderingConstants.SlotStandardTexture, _texArray);

            var rawLightPos = new System.Numerics.Vector3((float)layer.LightX, (float)layer.LightY, (float)layer.LightZ);
            System.Numerics.Vector3 finalLightPos;
            if (layer.LightType == 2)
                finalLightPos = System.Numerics.Vector3.TransformNormal(rawLightPos, world);
            else
                finalLightPos = System.Numerics.Vector3.Transform(rawLightPos, world);

            float roughness = (float)(material?.Roughness ?? settings.GetRoughness(wId));
            float metallic = (float)(material?.Metallic ?? settings.GetMetallic(wId));

            CBPerFrame cbFrame = ConstantBufferFactory.CreatePerFrameForPreview(
                view * proj, camPos, finalLightPos, wId, gridColor, axisColor, lightViewProj, layer.LightType, enableShadow);

            CBPerObject cbObject = new CBPerObject
            {
                WorldViewProj = Matrix4x4.Transpose(wvp),
                World = Matrix4x4.Transpose(world)
            };

            CBPerMaterial cbMaterial = ConstantBufferFactory.CreatePerMaterial(
                wId,
                material != null ? new System.Numerics.Vector4(material.BaseColor.R / 255.0f, material.BaseColor.G / 255.0f, material.BaseColor.B / 255.0f, material.BaseColor.A / 255.0f) : part.BaseColor,
                layer.LightEnabled,
                (float)settings.GetDiffuseIntensity(wId),
                (float)settings.GetShininess(wId),
                roughness,
                metallic);

            UpdateConstantBuffers(ref cbFrame, ref cbObject, ref cbMaterial);

            _context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
        }

        private void UpdateConstantBuffers(ref CBPerFrame cbFrame, ref CBPerObject cbObject, ref CBPerMaterial cbMaterial)
        {
            if (_context == null || _cbPerFrame == null || _cbPerObject == null || _cbPerMaterial == null) return;

            _cbPerFrame.Update(_context, ref cbFrame);
            _cbPerObject.Update(_context, ref cbObject);
            _cbPerMaterial.Update(_context, ref cbMaterial);

            _cbPerFrameArray[0] = _cbPerFrame.Buffer;
            _cbPerObjectArray[0] = _cbPerObject.Buffer;
            _cbPerMaterialArray[0] = _cbPerMaterial.Buffer;

            _context.VSSetConstantBuffers(0, 1, _cbPerFrameArray);
            _context.PSSetConstantBuffers(0, 1, _cbPerFrameArray);

            _context.VSSetConstantBuffers(1, 1, _cbPerObjectArray);
            _context.PSSetConstantBuffers(1, 1, _cbPerObjectArray);

            _context.PSSetConstantBuffers(2, 1, _cbPerMaterialArray);
        }

        public void Dispose()
        {
            lock (ObjLoaderSource.SharedRenderLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                _dynamicTextureManager.Dispose();

                UnregisterResizeResources();
                ResourceTracker.Instance.Unregister(TrackingKey("GridVertexBuffer"));

                _d3dResources?.Dispose();
                _d3dResources = null;
                _rtv?.Dispose(); _rtv = null;
                _renderTarget?.Dispose(); _renderTarget = null;
                _dsv?.Dispose(); _dsv = null;
                _depthStencil?.Dispose(); _depthStencil = null;
                _stagingTexture?.Dispose(); _stagingTexture = null;
                _resolveTexture?.Dispose(); _resolveTexture = null;
                _gridVertexBuffer?.Dispose(); _gridVertexBuffer = null;

                _cbPerFrame?.Dispose(); _cbPerFrame = null;
                _cbPerObject?.Dispose(); _cbPerObject = null;
                _cbPerMaterial?.Dispose(); _cbPerMaterial = null;

                _cachedLayerWorlds = null;
                _cachedLayerWvps = null;
                _cachedLayerCapacity = 0;

                _stagingBuffer = null;
                _stagingBufferSize = 0;

                lock (_bitmapLock)
                {
                    SceneImage = null;
                }

                if (_context != null)
                {
                    _context.ClearState();
                    _context.Flush();
                    _context.Dispose();
                    _context = null;
                }
                _device?.Dispose(); _device = null;
            }
        }
    }
}
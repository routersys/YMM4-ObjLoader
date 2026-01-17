using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Rendering;
using ObjLoader.Settings;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using D3D11MapFlags = Vortice.Direct3D11.MapFlags;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace ObjLoader.Services
{
    internal struct LayerRenderData
    {
        public GpuResourceCacheItem Resource;
        public double X, Y, Z;
        public double Scale;
        public double Rx, Ry, Rz;
        public System.Windows.Media.Color BaseColor;
        public bool LightEnabled;
        public int WorldId;
        public double HeightOffset;
        public HashSet<int>? VisibleParts;
        public Matrix4x4? WorldMatrixOverride;
    }

    internal class RenderService : IDisposable
    {
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

        private readonly List<int> _opaqueParts = new List<int>();
        private readonly List<TransparentPart> _transparentParts = new List<TransparentPart>();
        private readonly PartSorter _partSorter = new PartSorter();

        private class TransparentPart
        {
            public int LayerIndex;
            public int PartIndex;
            public float DistanceSq;
        }

        private class PartSorter : IComparer<TransparentPart>
        {
            public int Compare(TransparentPart? x, TransparentPart? y)
            {
                if (x == null || y == null) return 0;
                return y.DistanceSq.CompareTo(x.DistanceSq);
            }
        }

        public WriteableBitmap? SceneImage { get; private set; }
        public D3DResources? Resources => _d3dResources;
        public ID3D11Device? Device => _device;

        public void Initialize()
        {
            var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, new[] { FeatureLevel.Level_11_0 }, out _device, out _context);
            if (result.Failure || _device == null) return;
            _d3dResources = new D3DResources(_device!);

            float[] gridVerts = {
                -1000, 0, 1000, -1000, 0, -1000, 1000, 0, 1000,
                1000, 0, 1000, -1000, 0, -1000, 1000, 0, -1000
            };
            var vDesc = new BufferDescription(gridVerts.Length * 4, BindFlags.VertexBuffer, ResourceUsage.Immutable);
            unsafe
            {
                fixed (float* p = gridVerts) _gridVertexBuffer = _device.CreateBuffer(vDesc, new SubresourceData(p));
            }
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

            _viewportWidth = targetWidth;
            _viewportHeight = targetHeight;

            _rtv?.Dispose();
            _renderTarget?.Dispose();
            _dsv?.Dispose();
            _depthStencil?.Dispose();
            _stagingTexture?.Dispose();
            _resolveTexture?.Dispose();

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
            _renderTarget = _device.CreateTexture2D(texDesc);
            _rtv = _device.CreateRenderTargetView(_renderTarget);

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
            _depthStencil = _device.CreateTexture2D(depthDesc);
            _dsv = _device.CreateDepthStencilView(_depthStencil);

            var resolveDesc = texDesc;
            resolveDesc.SampleDescription = new SampleDescription(1, 0);
            _resolveTexture = _device.CreateTexture2D(resolveDesc);

            var stagingDesc = resolveDesc;
            stagingDesc.Usage = ResourceUsage.Staging;
            stagingDesc.BindFlags = BindFlags.None;
            stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
            _stagingTexture = _device.CreateTexture2D(stagingDesc);

            SceneImage = new WriteableBitmap(targetWidth, targetHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
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
            bool isInteracting)
        {
            if (_device == null || _context == null || _rtv == null || _d3dResources == null || SceneImage == null || _stagingTexture == null) return;

            double brightness = themeColor.R * 0.299 + themeColor.G * 0.587 + themeColor.B * 0.114;
            Color4 clearColor;
            System.Numerics.Vector4 gridColor;
            System.Numerics.Vector4 axisColor;

            if (brightness < 20)
            {
                clearColor = new Color4(0.0f, 0.0f, 0.0f, 1.0f);
                gridColor = new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f);
                axisColor = new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1.0f);
            }
            else if (brightness < 128)
            {
                clearColor = new Color4(0.13f, 0.13f, 0.13f, 1.0f);
                gridColor = new System.Numerics.Vector4(0.65f, 0.65f, 0.65f, 1.0f);
                axisColor = new System.Numerics.Vector4(0.9f, 0.9f, 0.9f, 1.0f);
            }
            else
            {
                clearColor = new Color4(0.9f, 0.9f, 0.9f, 1.0f);
                gridColor = new System.Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f);
                axisColor = new System.Numerics.Vector4(0.1f, 0.1f, 0.1f, 1.0f);
            }

            _context.OMSetRenderTargets(_rtv, _dsv);
            _context.ClearRenderTargetView(_rtv, clearColor);
            _context.ClearDepthStencilView(_dsv!, DepthStencilClearFlags.Depth, 1.0f, 0);

            _context.RSSetState(isWireframe ? _d3dResources.WireframeRasterizerState : _d3dResources.RasterizerState);
            _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
            _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
            _context.RSSetViewport(0, 0, _viewportWidth, _viewportHeight);

            _transparentParts.Clear();
            var layerWorlds = new Matrix4x4[layers.Count];
            var layerWvps = new Matrix4x4[layers.Count];

            var settings = PluginSettings.Instance;
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

                var wvp = world * view * proj;

                layerWorlds[i] = world;
                layerWvps[i] = wvp;
            }

            _context.VSSetShader(_d3dResources.VertexShader);
            _context.PSSetShader(_d3dResources.PixelShader);
            _context.PSSetSamplers(0, new[] { _d3dResources.SamplerState });
            _context.IASetInputLayout(_d3dResources.InputLayout);
            _context.IASetPrimitiveTopology(isInteracting ? PrimitiveTopology.PointList : PrimitiveTopology.TriangleList);

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var modelResource = layer.Resource;
                var world = layerWorlds[i];
                var wvp = layerWvps[i];
                int wId = layer.WorldId;

                int stride = Unsafe.SizeOf<ObjVertex>();
                _context.IASetVertexBuffers(0, 1, new[] { modelResource.VertexBuffer }, new[] { stride }, new[] { 0 });
                _context.IASetIndexBuffer(modelResource.IndexBuffer, Format.R32_UInt, 0);

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
                        DrawPart(layer, modelResource, p, world, wvp, wId, gridColor, axisColor, isInteracting);
                    }
                }
            }

            if (_transparentParts.Count > 0)
            {
                _transparentParts.Sort(_partSorter);

                _context.OMSetDepthStencilState(_d3dResources.DepthStencilStateNoWrite);
                if (!isWireframe)
                {
                    _context.RSSetState(_d3dResources.CullNoneRasterizerState);
                }

                foreach (var tp in _transparentParts)
                {
                    var layer = layers[tp.LayerIndex];
                    if (layer.VisibleParts != null && !layer.VisibleParts.Contains(tp.PartIndex)) continue;

                    var resource = layer.Resource;
                    DrawPart(layer, resource, tp.PartIndex, layerWorlds[tp.LayerIndex], layerWvps[tp.LayerIndex], layer.WorldId, gridColor, axisColor, isInteracting);
                }

                _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
                if (!isWireframe)
                {
                    _context.RSSetState(_d3dResources.RasterizerState);
                }
            }

            if (isGridVisible && _gridVertexBuffer != null)
            {
                _context.IASetInputLayout(_d3dResources.GridInputLayout);
                _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
                _context.IASetVertexBuffers(0, 1, new[] { _gridVertexBuffer }, new[] { 12 }, new[] { 0 });
                _context.VSSetShader(_d3dResources.GridVertexShader);
                _context.PSSetShader(_d3dResources.GridPixelShader);
                _context.OMSetBlendState(_d3dResources.GridBlendState, new Color4(0, 0, 0, 0), -1);

                Matrix4x4 gridWorld = Matrix4x4.Identity;
                if (!isInfiniteGrid)
                {
                    float finiteScale = (float)(gridScale * 50.0 / 1000.0);
                    if (finiteScale < 0.001f) finiteScale = 0.001f;
                    gridWorld = Matrix4x4.CreateScale(finiteScale);
                }

                ConstantBufferData gridCb = new ConstantBufferData
                {
                    WorldViewProj = Matrix4x4.Transpose(gridWorld * view * proj),
                    World = Matrix4x4.Transpose(gridWorld),
                    CameraPos = new System.Numerics.Vector4(camPos, 1),
                    GridColor = gridColor,
                    GridAxisColor = axisColor
                };
                UpdateConstantBuffer(ref gridCb);
                _context.Draw(6, 0);

                _context.OMSetBlendState(_d3dResources.BlendState, new Color4(0, 0, 0, 0), -1);
            }

            _context.ResolveSubresource(_resolveTexture!, 0, _renderTarget!, 0, Format.B8G8R8A8_UNorm);
            _context.CopyResource(_stagingTexture, _resolveTexture);
            var map = _context.Map(_stagingTexture, 0, MapMode.Read, D3D11MapFlags.None);

            try
            {
                SceneImage.Lock();
                unsafe
                {
                    var srcPtr = (byte*)map.DataPointer;
                    var dstPtr = (byte*)SceneImage.BackBuffer;
                    for (int r = 0; r < _viewportHeight; r++)
                    {
                        Buffer.MemoryCopy(srcPtr + (r * map.RowPitch), dstPtr + (r * SceneImage.BackBufferStride), SceneImage.BackBufferStride, _viewportWidth * 4);
                    }
                }
                SceneImage.AddDirtyRect(new Int32Rect(0, 0, _viewportWidth, _viewportHeight));
                SceneImage.Unlock();
            }
            finally
            {
                _context.Unmap(_stagingTexture, 0);
            }
        }

        private void DrawPart(LayerRenderData layer, GpuResourceCacheItem resource, int partIndex, Matrix4x4 world, Matrix4x4 wvp, int wId, System.Numerics.Vector4 gridColor, System.Numerics.Vector4 axisColor, bool isInteracting)
        {
            if (_context == null || _d3dResources == null) return;

            var settings = PluginSettings.Instance;
            var part = resource.Parts[partIndex];
            var texView = resource.PartTextures[partIndex];
            _context.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { texView != null ? texView! : _d3dResources.WhiteTextureView! });

            System.Numerics.Vector4 ToVec4(System.Windows.Media.Color c) => new System.Numerics.Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);

            ConstantBufferData cbData = new ConstantBufferData
            {
                WorldViewProj = Matrix4x4.Transpose(wvp),
                World = Matrix4x4.Transpose(world),
                LightPos = new System.Numerics.Vector4(1, 1, 1, 0),
                BaseColor = part.BaseColor,
                AmbientColor = ToVec4(settings.GetAmbientColor(wId)),
                LightColor = ToVec4(settings.GetLightColor(wId)),
                CameraPos = new System.Numerics.Vector4(0, 0, 0, 1),
                LightEnabled = layer.LightEnabled ? 1.0f : 0.0f,
                DiffuseIntensity = (float)settings.GetDiffuseIntensity(wId),
                SpecularIntensity = (float)settings.GetSpecularIntensity(wId),
                Shininess = (float)settings.GetShininess(wId),
                GridColor = gridColor,
                GridAxisColor = axisColor,
                ToonParams = new System.Numerics.Vector4(settings.GetToonEnabled(wId) ? 1 : 0, settings.GetToonSteps(wId), (float)settings.GetToonSmoothness(wId), 0),
                RimParams = new System.Numerics.Vector4(settings.GetRimEnabled(wId) ? 1 : 0, (float)settings.GetRimIntensity(wId), (float)settings.GetRimPower(wId), 0),
                RimColor = ToVec4(settings.GetRimColor(wId)),
                OutlineParams = new System.Numerics.Vector4(settings.GetOutlineEnabled(wId) ? 1 : 0, (float)settings.GetOutlineWidth(wId), (float)settings.GetOutlinePower(wId), 0),
                OutlineColor = ToVec4(settings.GetOutlineColor(wId)),
                FogParams = new System.Numerics.Vector4(settings.GetFogEnabled(wId) ? 1 : 0, (float)settings.GetFogStart(wId), (float)settings.GetFogEnd(wId), (float)settings.GetFogDensity(wId)),
                FogColor = ToVec4(settings.GetFogColor(wId)),
                ColorCorrParams = new System.Numerics.Vector4((float)settings.GetSaturation(wId), (float)settings.GetContrast(wId), (float)settings.GetGamma(wId), (float)settings.GetBrightnessPost(wId)),
                VignetteParams = new System.Numerics.Vector4(settings.GetVignetteEnabled(wId) ? 1 : 0, (float)settings.GetVignetteIntensity(wId), (float)settings.GetVignetteRadius(wId), (float)settings.GetVignetteSoftness(wId)),
                VignetteColor = ToVec4(settings.GetVignetteColor(wId)),
                ScanlineParams = new System.Numerics.Vector4(settings.GetScanlineEnabled(wId) ? 1 : 0, (float)settings.GetScanlineIntensity(wId), (float)settings.GetScanlineFrequency(wId), 0),
                ChromAbParams = new System.Numerics.Vector4(settings.GetChromAbEnabled(wId) ? 1 : 0, (float)settings.GetChromAbIntensity(wId), 0, 0),
                MonoParams = new System.Numerics.Vector4(settings.GetMonochromeEnabled(wId) ? 1 : 0, (float)settings.GetMonochromeMix(wId), 0, 0),
                MonoColor = ToVec4(settings.GetMonochromeColor(wId)),
                PosterizeParams = new System.Numerics.Vector4(settings.GetPosterizeEnabled(wId) ? 1 : 0, settings.GetPosterizeLevels(wId), 0, 0)
            };

            UpdateConstantBuffer(ref cbData);

            if (isInteracting)
                _context.DrawIndexed(Math.Max(part.IndexCount / 16, 32), part.IndexOffset, 0);
            else
                _context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
        }

        private void UpdateConstantBuffer(ref ConstantBufferData data)
        {
            if (_context == null || _d3dResources == null) return;
            MappedSubresource mapped;
            _context.Map(_d3dResources.ConstantBuffer, 0, MapMode.WriteDiscard, D3D11MapFlags.None, out mapped);
            unsafe { Unsafe.Copy(mapped.DataPointer.ToPointer(), ref data); }
            _context.Unmap(_d3dResources.ConstantBuffer, 0);
            _context.VSSetConstantBuffers(0, new[] { _d3dResources.ConstantBuffer });
            _context.PSSetConstantBuffers(0, new[] { _d3dResources.ConstantBuffer });
        }

        public void Dispose()
        {
            _d3dResources?.Dispose();
            _d3dResources = null;
            _rtv?.Dispose(); _rtv = null;
            _renderTarget?.Dispose(); _renderTarget = null;
            _dsv?.Dispose(); _dsv = null;
            _depthStencil?.Dispose(); _depthStencil = null;
            _stagingTexture?.Dispose(); _stagingTexture = null;
            _resolveTexture?.Dispose(); _resolveTexture = null;
            _gridVertexBuffer?.Dispose(); _gridVertexBuffer = null;

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
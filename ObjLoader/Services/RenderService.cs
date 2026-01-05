using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Plugin;
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

namespace ObjLoader.Services
{
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
        private readonly List<int> _transparentParts = new List<int>();
        private readonly PartSorter _partSorter = new PartSorter();

        private class PartSorter : IComparer<int>
        {
            public ModelPart[]? Parts;
            public System.Numerics.Matrix4x4 World;
            public System.Numerics.Vector3 CamPos;

            public int Compare(int x, int y)
            {
                if (Parts == null) return 0;
                var centerA = System.Numerics.Vector3.Transform(Parts[x].Center, World);
                var centerB = System.Numerics.Vector3.Transform(Parts[y].Center, World);
                var distA = System.Numerics.Vector3.DistanceSquared(CamPos, centerA);
                var distB = System.Numerics.Vector3.DistanceSquared(CamPos, centerB);
                return distB.CompareTo(distA);
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

            _viewportWidth = width;
            _viewportHeight = height;

            _rtv?.Dispose();
            _renderTarget?.Dispose();
            _dsv?.Dispose();
            _depthStencil?.Dispose();
            _stagingTexture?.Dispose();
            _resolveTexture?.Dispose();

            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(4, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CPUAccessFlags = CpuAccessFlags.None
            };
            _renderTarget = _device.CreateTexture2D(texDesc);
            _rtv = _device.CreateRenderTargetView(_renderTarget);

            var depthDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(4, 0),
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

            SceneImage = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
        }

        public void Render(
            GpuResourceCacheItem? modelResource,
            System.Numerics.Matrix4x4 view,
            System.Numerics.Matrix4x4 proj,
            System.Numerics.Vector3 camPos,
            System.Windows.Media.Color themeColor,
            bool isWireframe,
            bool isGridVisible,
            bool isInfiniteGrid,
            double modelScale,
            double modelHeight,
            ObjLoaderParameter parameter,
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

            if (modelResource != null)
            {
                _context.IASetInputLayout(_d3dResources.InputLayout);
                _context.IASetPrimitiveTopology(isInteracting ? PrimitiveTopology.PointList : PrimitiveTopology.TriangleList);

                int stride = Unsafe.SizeOf<ObjVertex>();
                _context.IASetVertexBuffers(0, 1, new[] { modelResource.VertexBuffer }, new[] { stride }, new[] { 0 });
                _context.IASetIndexBuffer(modelResource.IndexBuffer, Format.R32_UInt, 0);

                _context.VSSetShader(_d3dResources.VertexShader);
                _context.PSSetShader(_d3dResources.PixelShader);
                _context.PSSetSamplers(0, new[] { _d3dResources.SamplerState });

                float heightOffset = (float)(modelHeight / 2.0);
                var normalize = System.Numerics.Matrix4x4.CreateTranslation(-modelResource.ModelCenter) * System.Numerics.Matrix4x4.CreateScale(modelResource.ModelScale);
                normalize *= System.Numerics.Matrix4x4.CreateTranslation(0, heightOffset, 0);

                System.Numerics.Matrix4x4 axisConversion = System.Numerics.Matrix4x4.Identity;
                var settings = PluginSettings.Instance;
                switch (settings.CoordinateSystem)
                {
                    case CoordinateSystem.RightHandedZUp: axisConversion = System.Numerics.Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)); break;
                    case CoordinateSystem.LeftHandedYUp: axisConversion = System.Numerics.Matrix4x4.CreateScale(1, 1, -1); break;
                    case CoordinateSystem.LeftHandedZUp: axisConversion = System.Numerics.Matrix4x4.CreateRotationX((float)(-90 * Math.PI / 180.0)) * System.Numerics.Matrix4x4.CreateScale(1, 1, -1); break;
                }

                float scale = (float)(parameter.Scale.Values[0].Value / 100.0);
                float rx = (float)(parameter.RotationX.Values[0].Value * Math.PI / 180.0);
                float ry = (float)(parameter.RotationY.Values[0].Value * Math.PI / 180.0);
                float rz = (float)(parameter.RotationZ.Values[0].Value * Math.PI / 180.0);
                float tx = (float)parameter.X.Values[0].Value;
                float ty = (float)parameter.Y.Values[0].Value;
                float tz = (float)parameter.Z.Values[0].Value;

                var placement = System.Numerics.Matrix4x4.CreateScale(scale) * System.Numerics.Matrix4x4.CreateRotationZ(rz) * System.Numerics.Matrix4x4.CreateRotationX(rx) * System.Numerics.Matrix4x4.CreateRotationY(ry) * System.Numerics.Matrix4x4.CreateTranslation(tx, ty, tz);
                var world = normalize * axisConversion * placement;
                var wvp = world * view * proj;

                System.Numerics.Vector4 ToVec4(System.Windows.Media.Color c) => new System.Numerics.Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
                int wId = settings.WorldId;

                _opaqueParts.Clear();
                _transparentParts.Clear();
                for (int i = 0; i < modelResource.Parts.Length; i++)
                {
                    if (modelResource.Parts[i].BaseColor.W < 0.99f)
                        _transparentParts.Add(i);
                    else
                        _opaqueParts.Add(i);
                }

                if (_transparentParts.Count > 1)
                {
                    _partSorter.Parts = modelResource.Parts;
                    _partSorter.World = world;
                    _partSorter.CamPos = camPos;
                    _transparentParts.Sort(_partSorter);
                }

                void DrawPart(int i)
                {
                    var part = modelResource.Parts[i];
                    var texView = modelResource.PartTextures[i];
                    _context!.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { texView != null ? texView! : _d3dResources!.WhiteTextureView! });

                    ConstantBufferData cbData = new ConstantBufferData
                    {
                        WorldViewProj = System.Numerics.Matrix4x4.Transpose(wvp),
                        World = System.Numerics.Matrix4x4.Transpose(world),
                        LightPos = new System.Numerics.Vector4(1, 1, 1, 0),
                        BaseColor = part.BaseColor,
                        AmbientColor = ToVec4(settings.GetAmbientColor(wId)),
                        LightColor = ToVec4(settings.GetLightColor(wId)),
                        CameraPos = new System.Numerics.Vector4(camPos, 1),
                        LightEnabled = parameter.IsLightEnabled ? 1.0f : 0.0f,
                        DiffuseIntensity = (float)settings.GetDiffuseIntensity(wId),
                        SpecularIntensity = (float)settings.GetSpecularIntensity(wId),
                        Shininess = (float)settings.GetShininess(wId),
                        GridColor = gridColor,
                        GridAxisColor = axisColor,
                        ToonParams = new System.Numerics.Vector4(settings.ToonEnabled ? 1 : 0, settings.ToonSteps, (float)settings.ToonSmoothness, 0),
                        RimParams = new System.Numerics.Vector4(settings.RimEnabled ? 1 : 0, (float)settings.RimIntensity, (float)settings.RimPower, 0),
                        RimColor = ToVec4(settings.RimColor),
                        OutlineParams = new System.Numerics.Vector4(settings.OutlineEnabled ? 1 : 0, (float)settings.OutlineWidth, (float)settings.OutlinePower, 0),
                        OutlineColor = ToVec4(settings.OutlineColor),
                        FogParams = new System.Numerics.Vector4(settings.FogEnabled ? 1 : 0, (float)settings.FogStart, (float)settings.FogEnd, (float)settings.FogDensity),
                        FogColor = ToVec4(settings.FogColor),
                        ColorCorrParams = new System.Numerics.Vector4((float)settings.Saturation, (float)settings.Contrast, (float)settings.Gamma, (float)settings.BrightnessPost),
                        VignetteParams = new System.Numerics.Vector4(settings.VignetteEnabled ? 1 : 0, (float)settings.VignetteIntensity, (float)settings.VignetteRadius, (float)settings.VignetteSoftness),
                        VignetteColor = ToVec4(settings.VignetteColor),
                        ScanlineParams = new System.Numerics.Vector4(settings.ScanlineEnabled ? 1 : 0, (float)settings.ScanlineIntensity, (float)settings.ScanlineFrequency, 0),
                        ChromAbParams = new System.Numerics.Vector4(settings.ChromAbEnabled ? 1 : 0, (float)settings.ChromAbIntensity, 0, 0),
                        MonoParams = new System.Numerics.Vector4(settings.MonochromeEnabled ? 1 : 0, (float)settings.MonochromeMix, 0, 0),
                        MonoColor = ToVec4(settings.MonochromeColor),
                        PosterizeParams = new System.Numerics.Vector4(settings.PosterizeEnabled ? 1 : 0, settings.PosterizeLevels, 0, 0)
                    };
                    UpdateConstantBuffer(ref cbData);

                    if (isInteracting)
                        _context.DrawIndexed(Math.Max(part.IndexCount / 16, 32), part.IndexOffset, 0);
                    else
                        _context.DrawIndexed(part.IndexCount, part.IndexOffset, 0);
                }

                foreach (var i in _opaqueParts)
                {
                    DrawPart(i);
                }

                if (_transparentParts.Count > 0)
                {
                    _context.OMSetDepthStencilState(_d3dResources.DepthStencilStateNoWrite);
                    if (!isWireframe)
                    {
                        _context.RSSetState(_d3dResources.CullNoneRasterizerState);
                    }

                    foreach (var i in _transparentParts)
                    {
                        DrawPart(i);
                    }

                    _context.OMSetDepthStencilState(_d3dResources.DepthStencilState);
                    if (!isWireframe)
                    {
                        _context.RSSetState(_d3dResources.RasterizerState);
                    }
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

                System.Numerics.Matrix4x4 gridWorld = System.Numerics.Matrix4x4.Identity;
                if (!isInfiniteGrid)
                {
                    float finiteScale = (float)(modelScale * 50.0 / 1000.0);
                    if (finiteScale < 0.001f) finiteScale = 0.001f;
                    gridWorld = System.Numerics.Matrix4x4.CreateScale(finiteScale);
                }

                ConstantBufferData gridCb = new ConstantBufferData
                {
                    WorldViewProj = System.Numerics.Matrix4x4.Transpose(gridWorld * view * proj),
                    World = System.Numerics.Matrix4x4.Transpose(gridWorld),
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
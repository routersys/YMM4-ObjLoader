using ObjLoader.Rendering.Managers;
using ObjLoader.Rendering.Shaders;
using ObjLoader.Settings;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Rendering.Core
{
    internal sealed class D3DResources : IDisposable
    {
        private readonly ID3D11Device _device;
        private readonly DisposeCollector _disposer = new DisposeCollector();
        private readonly ShadowMapManager _shadowMapManager = new ShadowMapManager();
        private readonly EnvironmentMapManager _environmentMapManager = new EnvironmentMapManager();
        private readonly object _stateLock = new object();

        private ID3D11RasterizerState? _rasterizerState;
        private ID3D11RasterizerState? _wireframeRasterizerState;
        private RenderCullMode _currentCullMode;
        private bool _isDisposed;

        public ID3D11VertexShader VertexShader { get; }
        public ID3D11PixelShader PixelShader { get; }
        public ID3D11VertexShader GridVertexShader { get; }
        public ID3D11PixelShader GridPixelShader { get; }
        public ID3D11InputLayout InputLayout { get; }
        public ID3D11InputLayout GridInputLayout { get; }
        public ID3D11Buffer ConstantBuffer { get; }
        public ID3D11DepthStencilState DepthStencilState { get; }
        public ID3D11DepthStencilState DepthStencilStateNoWrite { get; }
        public ID3D11SamplerState SamplerState { get; }
        public ID3D11BlendState BlendState { get; }
        public ID3D11BlendState GridBlendState { get; }
        public ID3D11ShaderResourceView WhiteTextureView { get; }
        public ID3D11Device Device => _device;
        public ID3D11RasterizerState CullNoneRasterizerState { get; }
        public ID3D11SamplerState ShadowSampler { get; }
        public ID3D11RasterizerState ShadowRasterizerState { get; }
        public bool IsDisposed => _isDisposed;

        public ID3D11RasterizerState RasterizerState
        {
            get
            {
                lock (_stateLock)
                {
                    return _rasterizerState!;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    _rasterizerState = value;
                }
            }
        }

        public ID3D11RasterizerState WireframeRasterizerState
        {
            get
            {
                lock (_stateLock)
                {
                    return _wireframeRasterizerState!;
                }
            }
            private set
            {
                lock (_stateLock)
                {
                    _wireframeRasterizerState = value;
                }
            }
        }

        public ID3D11Texture2D? ShadowMapTexture => _shadowMapManager.ShadowMapTexture;
        public ID3D11DepthStencilView[]? ShadowMapDSVs => _shadowMapManager.ShadowMapDSVs;
        public ID3D11ShaderResourceView? ShadowMapSRV => _shadowMapManager.ShadowMapSRV;
        public int CurrentShadowMapSize => _shadowMapManager.CurrentShadowMapSize;
        public bool IsCascaded => _shadowMapManager.IsCascaded;
        public const int CascadeCount = ShadowMapManager.CascadeCount;

        public bool IsEnvironmentMapInitialized => _environmentMapManager.IsInitialized;
        public ID3D11Texture2D? EnvironmentCubeMap => _environmentMapManager.EnvironmentCubeMap;
        public ID3D11ShaderResourceView? EnvironmentSRV => _environmentMapManager.EnvironmentSRV;
        public ID3D11RenderTargetView[]? EnvironmentRTVs => _environmentMapManager.EnvironmentRTVs;
        public ID3D11DepthStencilView? EnvironmentDSV => _environmentMapManager.EnvironmentDSV;

        public unsafe D3DResources(ID3D11Device device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));

            var (vsByteCode, psByteCode, gridVsByte, gridPsByte) = ShaderStore.GetByteCodes();

            VertexShader = device.CreateVertexShader(vsByteCode);
            _disposer.Collect(VertexShader);

            PixelShader = device.CreatePixelShader(psByteCode);
            _disposer.Collect(PixelShader);

            GridVertexShader = device.CreateVertexShader(gridVsByte);
            _disposer.Collect(GridVertexShader);

            GridPixelShader = device.CreatePixelShader(gridPsByte);
            _disposer.Collect(GridPixelShader);

            InputLayout = CreateInputLayout(device, vsByteCode);
            _disposer.Collect(InputLayout);

            GridInputLayout = CreateGridInputLayout(device, gridVsByte);
            _disposer.Collect(GridInputLayout);

            ConstantBuffer = CreateConstantBuffer(device);
            _disposer.Collect(ConstantBuffer);

            CullNoneRasterizerState = CreateCullNoneRasterizerState(device);
            _disposer.Collect(CullNoneRasterizerState);

            _currentCullMode = RenderCullMode.None;
            RasterizerState = CreateRasterizerState(RenderCullMode.None, false);
            if (RasterizerState != CullNoneRasterizerState)
            {
                _disposer.Collect(RasterizerState);
            }

            WireframeRasterizerState = CreateRasterizerState(RenderCullMode.Back, true);
            _disposer.Collect(WireframeRasterizerState);

            DepthStencilState = CreateDepthStencilState(device, true);
            _disposer.Collect(DepthStencilState);

            DepthStencilStateNoWrite = CreateDepthStencilState(device, false);
            _disposer.Collect(DepthStencilStateNoWrite);

            SamplerState = CreateSamplerState(device);
            _disposer.Collect(SamplerState);

            BlendState = CreateBlendState(device);
            _disposer.Collect(BlendState);

            GridBlendState = CreateGridBlendState(device);
            _disposer.Collect(GridBlendState);

            WhiteTextureView = CreateWhiteTexture(device);
            _disposer.Collect(WhiteTextureView);

            ShadowSampler = CreateShadowSampler(device);
            _disposer.Collect(ShadowSampler);

            ShadowRasterizerState = CreateShadowRasterizerState(device);
            _disposer.Collect(ShadowRasterizerState);
        }

        private static ID3D11InputLayout CreateInputLayout(ID3D11Device device, byte[] vsByteCode)
        {
            var inputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0)
            };
            return device.CreateInputLayout(inputElements, vsByteCode);
        }

        private static ID3D11InputLayout CreateGridInputLayout(ID3D11Device device, byte[] gridVsByte)
        {
            var gridElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0)
            };
            return device.CreateInputLayout(gridElements, gridVsByte);
        }

        private static ID3D11Buffer CreateConstantBuffer(ID3D11Device device)
        {
            var cbDesc = new BufferDescription(
                (int)((Marshal.SizeOf<ConstantBufferData>() + 15) / 16) * 16,
                BindFlags.ConstantBuffer,
                ResourceUsage.Dynamic,
                CpuAccessFlags.Write);
            return device.CreateBuffer(cbDesc);
        }

        private static ID3D11RasterizerState CreateCullNoneRasterizerState(ID3D11Device device)
        {
            var rasterDescCullNone = new RasterizerDescription(CullMode.None, FillMode.Solid)
            {
                MultisampleEnable = true,
                AntialiasedLineEnable = true
            };
            return device.CreateRasterizerState(rasterDescCullNone);
        }

        private static ID3D11DepthStencilState CreateDepthStencilState(ID3D11Device device, bool writeEnabled)
        {
            var depthDesc = new DepthStencilDescription(true, writeEnabled ? DepthWriteMask.All : DepthWriteMask.Zero, ComparisonFunction.LessEqual);
            return device.CreateDepthStencilState(depthDesc);
        }

        private static ID3D11SamplerState CreateSamplerState(ID3D11Device device)
        {
            var sampDesc = new SamplerDescription(
                Filter.Anisotropic,
                TextureAddressMode.Wrap,
                TextureAddressMode.Wrap,
                TextureAddressMode.Wrap,
                0, 16,
                ComparisonFunction.Always,
                new Vortice.Mathematics.Color4(0, 0, 0, 0),
                0, float.MaxValue);
            return device.CreateSamplerState(sampDesc);
        }

        private static ID3D11BlendState CreateBlendState(ID3D11Device device)
        {
            var blendDesc = new BlendDescription
            {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false,
            };
            blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = Blend.One,
                DestinationBlend = Blend.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = Blend.One,
                DestinationBlendAlpha = Blend.InverseSourceAlpha,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteEnable.All
            };
            return device.CreateBlendState(blendDesc);
        }

        private static ID3D11BlendState CreateGridBlendState(ID3D11Device device)
        {
            var gridBlendDesc = new BlendDescription
            {
                AlphaToCoverageEnable = false,
                IndependentBlendEnable = false
            };
            gridBlendDesc.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = Blend.SourceAlpha,
                DestinationBlend = Blend.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = Blend.One,
                DestinationBlendAlpha = Blend.Zero,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteEnable.All
            };
            return device.CreateBlendState(gridBlendDesc);
        }

        private static unsafe ID3D11ShaderResourceView CreateWhiteTexture(ID3D11Device device)
        {
            var whitePixel = new byte[] { 255, 255, 255, 255 };
            var texDesc = new Texture2DDescription
            {
                Width = 1,
                Height = 1,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource
            };

            fixed (byte* p = whitePixel)
            {
                using var tex = device.CreateTexture2D(texDesc, new[] { new SubresourceData(p, 4) });
                return device.CreateShaderResourceView(tex);
            }
        }

        private static ID3D11SamplerState CreateShadowSampler(ID3D11Device device)
        {
            var shadowSampDesc = new SamplerDescription(
                Filter.ComparisonMinMagMipLinear,
                TextureAddressMode.Border,
                TextureAddressMode.Border,
                TextureAddressMode.Border,
                0, 0,
                ComparisonFunction.Less,
                new Vortice.Mathematics.Color4(1, 1, 1, 1),
                0, float.MaxValue);
            return device.CreateSamplerState(shadowSampDesc);
        }

        private static ID3D11RasterizerState CreateShadowRasterizerState(ID3D11Device device)
        {
            var shadowRasterDesc = new RasterizerDescription(CullMode.Front, FillMode.Solid)
            {
                DepthBias = 0,
                SlopeScaledDepthBias = 0.0f,
                MultisampleEnable = true,
                AntialiasedLineEnable = true
            };
            return device.CreateRasterizerState(shadowRasterDesc);
        }

        public void EnsureShadowMapSize(int size, bool useCascaded)
        {
            if (_isDisposed) return;
            _shadowMapManager.EnsureShadowMapSize(_device, size, useCascaded);
        }

        public void EnsureEnvironmentMap()
        {
            if (_isDisposed) return;
            if (!_environmentMapManager.IsInitialized)
                _environmentMapManager.Initialize(_device);
        }

        public void UpdateRasterizerState(RenderCullMode mode)
        {
            if (_isDisposed) return;

            lock (_stateLock)
            {
                if (_currentCullMode == mode) return;

                if (_rasterizerState != CullNoneRasterizerState && _rasterizerState != null)
                {
                    _disposer.RemoveAndDispose(ref _rasterizerState);
                }

                _currentCullMode = mode;
                RasterizerState = CreateRasterizerState(mode, false);

                if (RasterizerState != CullNoneRasterizerState)
                {
                    _disposer.Collect(RasterizerState);
                }
            }
        }

        private ID3D11RasterizerState CreateRasterizerState(RenderCullMode mode, bool wireframe)
        {
            if (mode == RenderCullMode.None && !wireframe && CullNoneRasterizerState != null)
            {
                return CullNoneRasterizerState;
            }

            CullMode cull = mode switch
            {
                RenderCullMode.Front => CullMode.Front,
                RenderCullMode.Back => CullMode.Back,
                _ => CullMode.None
            };

            var rasterDesc = new RasterizerDescription(cull, wireframe ? FillMode.Wireframe : FillMode.Solid)
            {
                MultisampleEnable = true,
                AntialiasedLineEnable = true
            };
            return _device.CreateRasterizerState(rasterDesc);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _shadowMapManager.Dispose();
            _environmentMapManager.Dispose();
            _disposer.DisposeAndClear();

            try
            {
                _device.ImmediateContext.ClearState();
                _device.ImmediateContext.Flush();
            }
            catch
            {
            }
        }
    }
}
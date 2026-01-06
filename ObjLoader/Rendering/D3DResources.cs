using ObjLoader.Core;
using ObjLoader.Settings;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Rendering
{
    internal class D3DResources : IDisposable
    {
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
        public ID3D11Device Device { get; }
        public ID3D11RasterizerState CullNoneRasterizerState { get; }

        private ID3D11RasterizerState? _rasterizerState;
        public ID3D11RasterizerState RasterizerState
        {
            get => _rasterizerState!;
            private set => _rasterizerState = value;
        }

        private ID3D11RasterizerState? _wireframeRasterizerState;
        public ID3D11RasterizerState WireframeRasterizerState
        {
            get => _wireframeRasterizerState!;
            private set => _wireframeRasterizerState = value;
        }

        private readonly DisposeCollector _disposer = new DisposeCollector();
        private RenderCullMode _currentCullMode;

        public unsafe D3DResources(ID3D11Device device)
        {
            Device = device;
            var (vsByteCode, psByteCode, gridVsByte, gridPsByte) = ShaderStore.GetByteCodes();

            VertexShader = device.CreateVertexShader(vsByteCode);
            _disposer.Collect(VertexShader);

            PixelShader = device.CreatePixelShader(psByteCode);
            _disposer.Collect(PixelShader);

            GridVertexShader = device.CreateVertexShader(gridVsByte);
            _disposer.Collect(GridVertexShader);

            GridPixelShader = device.CreatePixelShader(gridPsByte);
            _disposer.Collect(GridPixelShader);

            var inputElements = new[] {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0, InputClassification.PerVertexData, 0)
            };

            InputLayout = device.CreateInputLayout(inputElements, vsByteCode);
            _disposer.Collect(InputLayout);

            var gridElements = new[] { new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0) };
            GridInputLayout = device.CreateInputLayout(gridElements, gridVsByte);
            _disposer.Collect(GridInputLayout);

            var cbDesc = new BufferDescription(
                (int)((Marshal.SizeOf<ConstantBufferData>() + 15) / 16) * 16,
                BindFlags.ConstantBuffer,
                ResourceUsage.Dynamic,
                CpuAccessFlags.Write);
            ConstantBuffer = device.CreateBuffer(cbDesc);
            _disposer.Collect(ConstantBuffer);

            var rasterDescCullNone = new RasterizerDescription(CullMode.None, FillMode.Solid)
            {
                MultisampleEnable = true,
                AntialiasedLineEnable = true
            };
            CullNoneRasterizerState = device.CreateRasterizerState(rasterDescCullNone);
            _disposer.Collect(CullNoneRasterizerState);

            _currentCullMode = RenderCullMode.None;
            RasterizerState = CreateRasterizerState(_currentCullMode, false);

            if (RasterizerState != CullNoneRasterizerState)
            {
                _disposer.Collect(RasterizerState);
            }

            WireframeRasterizerState = CreateRasterizerState(RenderCullMode.Back, true);
            _disposer.Collect(WireframeRasterizerState);

            var depthDesc = new DepthStencilDescription(true, DepthWriteMask.All, ComparisonFunction.LessEqual);
            DepthStencilState = device.CreateDepthStencilState(depthDesc);
            _disposer.Collect(DepthStencilState);

            var depthDescNoWrite = new DepthStencilDescription(true, DepthWriteMask.Zero, ComparisonFunction.LessEqual);
            DepthStencilStateNoWrite = device.CreateDepthStencilState(depthDescNoWrite);
            _disposer.Collect(DepthStencilStateNoWrite);

            var sampDesc = new SamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap, 0, 1, ComparisonFunction.Always, new Vortice.Mathematics.Color4(0, 0, 0, 0), 0, float.MaxValue);
            SamplerState = device.CreateSamplerState(sampDesc);
            _disposer.Collect(SamplerState);

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
            BlendState = device.CreateBlendState(blendDesc);
            _disposer.Collect(BlendState);

            var gridBlendDesc = new BlendDescription { AlphaToCoverageEnable = false, IndependentBlendEnable = false };
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
            GridBlendState = device.CreateBlendState(gridBlendDesc);
            _disposer.Collect(GridBlendState);

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
                WhiteTextureView = device.CreateShaderResourceView(tex);
            }
            _disposer.Collect(WhiteTextureView);
        }

        public void UpdateRasterizerState(RenderCullMode mode)
        {
            if (_currentCullMode != mode)
            {
                if (_rasterizerState != CullNoneRasterizerState)
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
            return Device.CreateRasterizerState(rasterDesc);
        }

        public void Dispose()
        {
            _disposer.DisposeAndClear();
        }
    }
}
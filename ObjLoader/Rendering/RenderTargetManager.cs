using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering
{
    internal class RenderTargetManager : IDisposable
    {
        public ID3D11Texture2D? RenderTargetTexture { get; private set; }
        public ID3D11RenderTargetView? RenderTargetView { get; private set; }
        public ID3D11Texture2D? DepthStencilTexture { get; private set; }
        public ID3D11DepthStencilView? DepthStencilView { get; private set; }
        public D2D.ID2D1Bitmap1? SharedBitmap { get; private set; }

        private readonly DisposeCollector _disposer = new DisposeCollector();
        private int _width;
        private int _height;

        public bool EnsureSize(IGraphicsDevicesAndContext devices, int width, int height)
        {
            if (RenderTargetView != null && _width == width && _height == height)
            {
                return false;
            }

            DisposeResources();

            _width = width;
            _height = height;

            var device = devices.D3D.Device;

            var texDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.Shared
            };
            RenderTargetTexture = device.CreateTexture2D(texDesc);
            _disposer.Collect(RenderTargetTexture);

            RenderTargetView = device.CreateRenderTargetView(RenderTargetTexture);
            _disposer.Collect(RenderTargetView);

            var depthTexDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
            DepthStencilTexture = device.CreateTexture2D(depthTexDesc);
            _disposer.Collect(DepthStencilTexture);

            DepthStencilView = device.CreateDepthStencilView(DepthStencilTexture);
            _disposer.Collect(DepthStencilView);

            using var surface = RenderTargetTexture.QueryInterface<IDXGISurface>();
            var bmpProps = new D2D.BitmapProperties1(
                new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96, 96, D2D.BitmapOptions.Target);

            SharedBitmap = devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, bmpProps);
            _disposer.Collect(SharedBitmap);

            return true;
        }

        private void DisposeResources()
        {
            _disposer.DisposeAndClear();
        }

        public void Dispose()
        {
            DisposeResources();
        }
    }
}
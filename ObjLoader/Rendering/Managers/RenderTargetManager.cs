using ObjLoader.Rendering.Managers.Interfaces;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering.Managers
{
    internal sealed class RenderTargetManager : IRenderTargetManager
    {
        private ID3D11Texture2D? _renderTargetTexture;
        private ID3D11RenderTargetView? _renderTargetView;
        private ID3D11Texture2D? _depthStencilTexture;
        private ID3D11DepthStencilView? _depthStencilView;
        private D2D.ID2D1Bitmap1? _sharedBitmap;

        private readonly object _lock = new object();
        private int _width;
        private int _height;
        private bool _disposed;

        public ID3D11Texture2D? RenderTargetTexture => _renderTargetTexture;
        public ID3D11RenderTargetView? RenderTargetView => _renderTargetView;
        public ID3D11Texture2D? DepthStencilTexture => _depthStencilTexture;
        public ID3D11DepthStencilView? DepthStencilView => _depthStencilView;
        public D2D.ID2D1Bitmap1? SharedBitmap => _sharedBitmap;

        public bool EnsureSize(IGraphicsDevicesAndContext devices, int width, int height)
        {
            if (devices == null) throw new ArgumentNullException(nameof(devices));
            if (width < 1 || height < 1) return false;

            lock (_lock)
            {
                if (_disposed) return false;

                if (_renderTargetView != null && _width == width && _height == height)
                {
                    return false;
                }

                DisposeResources();

                _width = width;
                _height = height;

                var device = devices.D3D.Device;

                _renderTargetTexture = CreateRenderTargetTexture(device, width, height);
                _renderTargetView = device.CreateRenderTargetView(_renderTargetTexture);

                _depthStencilTexture = CreateDepthStencilTexture(device, width, height);
                _depthStencilView = device.CreateDepthStencilView(_depthStencilTexture);

                _sharedBitmap = CreateSharedBitmap(devices, _renderTargetTexture);

                return true;
            }
        }

        private static ID3D11Texture2D CreateRenderTargetTexture(ID3D11Device device, int width, int height)
        {
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
                MiscFlags = ResourceOptionFlags.None
            };
            return device.CreateTexture2D(texDesc);
        }

        private static ID3D11Texture2D CreateDepthStencilTexture(ID3D11Device device, int width, int height)
        {
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
            return device.CreateTexture2D(depthTexDesc);
        }

        private static D2D.ID2D1Bitmap1 CreateSharedBitmap(IGraphicsDevicesAndContext devices, ID3D11Texture2D renderTargetTexture)
        {
            using var surface = renderTargetTexture.QueryInterface<IDXGISurface>();
            var bmpProps = new D2D.BitmapProperties1(
                new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96, 96, D2D.BitmapOptions.Target);

            return devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, bmpProps);
        }

        private void DisposeResources()
        {
            SafeDispose(ref _sharedBitmap);
            SafeDispose(ref _depthStencilView);
            SafeDispose(ref _depthStencilTexture);
            SafeDispose(ref _renderTargetView);
            SafeDispose(ref _renderTargetTexture);
        }

        private static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
        {
            var temp = disposable;
            disposable = null;
            if (temp != null)
            {
                try
                {
                    temp.Dispose();
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                DisposeResources();
            }
        }
    }
}
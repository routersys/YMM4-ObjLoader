using System.Buffers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Services.Textures
{
    public sealed class TextureService : ITextureService
    {
        private readonly List<ITextureLoader> _loaders = new List<ITextureLoader>();
        private readonly object _lock = new object();
        private bool _disposed;

        public TextureService()
        {
            RegisterLoader(new TgaTextureLoader());
            RegisterLoader(new StandardTextureLoader());
        }

        public void RegisterLoader(ITextureLoader loader)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TextureService));
                _loaders.Add(loader);
            }
        }

        public BitmapSource Load(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            ITextureLoader? selectedLoader;

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TextureService));

                selectedLoader = _loaders
                    .OrderByDescending(l => l.Priority)
                    .FirstOrDefault(l => l.CanLoad(path));
            }

            if (selectedLoader == null)
            {
                throw new NotSupportedException($"No suitable loader found for texture: {path}");
            }

            return selectedLoader.Load(path);
        }

        public unsafe (ID3D11ShaderResourceView? Srv, long GpuBytes) CreateShaderResourceView(string path, ID3D11Device device)
        {
            if (string.IsNullOrEmpty(path)) return (null, 0);
            if (device == null) return (null, 0);

            var bitmapSource = Load(path);
            if (bitmapSource.CanFreeze && !bitmapSource.IsFrozen) bitmapSource.Freeze();
            var converted = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            int requiredSize = stride * height;
            byte[] pixels = ArrayPool<byte>.Shared.Rent(requiredSize);
            try
            {
                converted.CopyPixels(pixels, stride, 0);

                var texDesc = new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Immutable,
                    BindFlags = BindFlags.ShaderResource
                };

                fixed (byte* p = pixels)
                {
                    var data = new SubresourceData(p, stride);
                    using var tex = device.CreateTexture2D(texDesc, new[] { data });
                    var srv = device.CreateShaderResourceView(tex);
                    return (srv, (long)width * height * 4);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixels);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                foreach (var loader in _loaders)
                {
                    if (loader is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch
                        {
                        }
                    }
                }

                _loaders.Clear();
            }
        }
    }
}
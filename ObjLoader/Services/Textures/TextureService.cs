using ObjLoader.Services.Textures.Loaders;
using System.Buffers;
using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Services.Textures
{
    public sealed class TextureService : ITextureService
    {
        private static readonly ConcurrentDictionary<string, TextureRawData> s_rawDataCache = new();
        private static readonly ConcurrentDictionary<(nint DevicePtr, string Path), ID3D11Texture2D> s_gpuTextureCache = new();
        private static readonly ConcurrentDictionary<(nint DevicePtr, string Path), long> s_gpuTextureSizes = new();
        private static readonly ConcurrentDictionary<nint, int> s_deviceRefCounts = new();

        private readonly List<ITextureLoader> _loaders = new List<ITextureLoader>();
        private readonly HashSet<nint> _trackedDevices = new();
        private readonly object _lock = new object();
        private bool _disposed;

        public TextureService()
        {
            RegisterLoader(new DdsTextureLoader());
            RegisterLoader(new PsdTextureLoader());
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

            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TextureService));
            }

            var raw = EnsureRawDataCached(path);
            if (raw != null)
            {
                var bmp = BitmapSource.Create(raw.Width, raw.Height, 96, 96, PixelFormats.Bgra32, null, raw.Pixels, raw.Stride);
                if (bmp.CanFreeze) bmp.Freeze();
                return bmp;
            }

            ITextureLoader? loader = FindLoader(path);
            if (loader == null)
            {
                throw new NotSupportedException($"No suitable loader found for texture: {path}");
            }

            var bitmap = loader.Load(path);
            if (bitmap.CanFreeze && !bitmap.IsFrozen) bitmap.Freeze();
            return bitmap;
        }

        public unsafe (ID3D11ShaderResourceView? Srv, long GpuBytes) CreateShaderResourceView(string path, ID3D11Device device)
        {
            if (string.IsNullOrEmpty(path)) return (null, 0);
            if (device == null) return (null, 0);

            lock (_lock)
            {
                if (_disposed) return (null, 0);
            }

            var devicePtr = device.NativePointer;
            TrackDevice(devicePtr);

            var key = (devicePtr, path);

            if (s_gpuTextureCache.TryGetValue(key, out var cachedTex))
            {
                try
                {
                    var srv = device.CreateShaderResourceView(cachedTex);
                    return (srv, 0);
                }
                catch
                {
                    if (s_gpuTextureCache.TryRemove(key, out var stale))
                    {
                        SafeDisposeCom(stale);
                    }
                    s_gpuTextureSizes.TryRemove(key, out _);
                }
            }

            var rawData = EnsureRawDataCached(path);
            if (rawData == null) return (null, 0);

            return CreateAndCacheGpuTexture(key, rawData, device);
        }

        private unsafe (ID3D11ShaderResourceView? Srv, long GpuBytes) CreateAndCacheGpuTexture(
            (nint DevicePtr, string Path) key, TextureRawData rawData, ID3D11Device device)
        {
            int width = rawData.Width;
            int height = rawData.Height;
            int stride = rawData.Stride;
            long gpuBytes = (long)width * height * 4;

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

            fixed (byte* p = rawData.Pixels)
            {
                var data = new SubresourceData(p, stride);
                var tex = device.CreateTexture2D(texDesc, new[] { data });

                if (s_gpuTextureCache.TryAdd(key, tex))
                {
                    s_gpuTextureSizes.TryAdd(key, gpuBytes);
                    var srv = device.CreateShaderResourceView(tex);
                    return (srv, gpuBytes);
                }

                tex.Dispose();

                if (s_gpuTextureCache.TryGetValue(key, out var existing))
                {
                    try
                    {
                        var srv = device.CreateShaderResourceView(existing);
                        return (srv, 0);
                    }
                    catch
                    {
                        if (s_gpuTextureCache.TryRemove(key, out var stale))
                        {
                            SafeDisposeCom(stale);
                        }
                        s_gpuTextureSizes.TryRemove(key, out _);
                    }
                }

                return (null, 0);
            }
        }

        private TextureRawData? EnsureRawDataCached(string path)
        {
            if (s_rawDataCache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            ITextureLoader? loader = FindLoader(path);
            if (loader == null) return null;

            if (loader.CanLoadRaw(path))
            {
                return DecodeAndCacheRaw(path, loader);
            }

            return DecodeAndCacheFromBitmap(path, loader);
        }

        private TextureRawData DecodeAndCacheRaw(string path, ITextureLoader loader)
        {
            if (s_rawDataCache.TryGetValue(path, out var existing))
            {
                return existing;
            }

            using var pooled = loader.LoadRaw(path);
            var persistent = pooled.ToNonPooled();

            if (s_rawDataCache.TryAdd(path, persistent))
            {
                return persistent;
            }

            persistent.Dispose();
            return s_rawDataCache[path];
        }

        private TextureRawData? DecodeAndCacheFromBitmap(string path, ITextureLoader loader)
        {
            if (s_rawDataCache.TryGetValue(path, out var existing))
            {
                return existing;
            }

            BitmapSource bitmapSource;
            try
            {
                bitmapSource = loader.Load(path);
            }
            catch
            {
                return null;
            }

            if (bitmapSource.CanFreeze && !bitmapSource.IsFrozen) bitmapSource.Freeze();
            var converted = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            int requiredSize = stride * height;

            byte[] pooled = ArrayPool<byte>.Shared.Rent(requiredSize);
            try
            {
                converted.CopyPixels(pooled, stride, 0);

                var pixels = new byte[requiredSize];
                Buffer.BlockCopy(pooled, 0, pixels, 0, requiredSize);
                var rawData = new TextureRawData(pixels, width, height);

                if (s_rawDataCache.TryAdd(path, rawData))
                {
                    return rawData;
                }

                rawData.Dispose();
                return s_rawDataCache[path];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pooled);
            }
        }

        private ITextureLoader? FindLoader(string path)
        {
            lock (_lock)
            {
                return _loaders
                    .OrderByDescending(l => l.Priority)
                    .FirstOrDefault(l => l.CanLoad(path));
            }
        }

        private void TrackDevice(nint devicePtr)
        {
            lock (_lock)
            {
                if (_trackedDevices.Add(devicePtr))
                {
                    s_deviceRefCounts.AddOrUpdate(devicePtr, 1, (_, c) => c + 1);
                }
            }
        }

        public static void EvictDevice(nint devicePtr)
        {
            var keysToRemove = s_gpuTextureCache.Keys
                .Where(k => k.DevicePtr == devicePtr)
                .ToArray();

            foreach (var key in keysToRemove)
            {
                if (s_gpuTextureCache.TryRemove(key, out var tex))
                {
                    SafeDisposeCom(tex);
                }
                s_gpuTextureSizes.TryRemove(key, out _);
            }
        }

        public static void EvictPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var keysToRemove = s_gpuTextureCache.Keys
                .Where(k => k.Path == path)
                .ToArray();

            foreach (var key in keysToRemove)
            {
                if (s_gpuTextureCache.TryRemove(key, out var tex))
                {
                    SafeDisposeCom(tex);
                }
                s_gpuTextureSizes.TryRemove(key, out _);
            }

            if (s_rawDataCache.TryRemove(path, out var raw))
            {
                raw.Dispose();
            }
        }

        public static void ClearAllCaches()
        {
            foreach (var entry in s_gpuTextureCache)
            {
                SafeDisposeCom(entry.Value);
            }
            s_gpuTextureCache.Clear();
            s_gpuTextureSizes.Clear();

            foreach (var entry in s_rawDataCache)
            {
                try
                {
                    entry.Value?.Dispose();
                }
                catch
                {
                }
            }
            s_rawDataCache.Clear();
        }

        public void Dispose()
        {
            HashSet<nint> devicesToEvict;

            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                devicesToEvict = new HashSet<nint>(_trackedDevices);
                _trackedDevices.Clear();
            }

            foreach (var devicePtr in devicesToEvict)
            {
                var newCount = s_deviceRefCounts.AddOrUpdate(devicePtr, 0, (_, c) => Math.Max(0, c - 1));
                if (newCount <= 0)
                {
                    s_deviceRefCounts.TryRemove(devicePtr, out _);
                    EvictDevice(devicePtr);
                }
            }

            List<ITextureLoader> loadersCopy;
            lock (_lock)
            {
                loadersCopy = new List<ITextureLoader>(_loaders);
                _loaders.Clear();
            }

            foreach (var loader in loadersCopy)
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
        }

        private static void SafeDisposeCom(IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }
    }
}
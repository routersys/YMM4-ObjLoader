using System.Windows.Media.Imaging;

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
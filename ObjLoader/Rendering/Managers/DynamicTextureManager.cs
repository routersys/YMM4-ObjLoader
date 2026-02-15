using ObjLoader.Rendering.Managers.Interfaces;
using ObjLoader.Services.Textures;
using Vortice.Direct3D11;

namespace ObjLoader.Rendering.Managers
{
    public class DynamicTextureManager : IDynamicTextureManager
    {
        private readonly ITextureService _textureService;
        private readonly Dictionary<string, ID3D11ShaderResourceView> _cache = new Dictionary<string, ID3D11ShaderResourceView>();
        private readonly object _lock = new object();
        private bool _disposed;

        public IReadOnlyDictionary<string, ID3D11ShaderResourceView> Textures
        {
            get
            {
                lock (_lock)
                {
                    return new Dictionary<string, ID3D11ShaderResourceView>(_cache);
                }
            }
        }

        public DynamicTextureManager(ITextureService textureService)
        {
            _textureService = textureService ?? throw new ArgumentNullException(nameof(textureService));
        }

        public void Prepare(IEnumerable<string> usedPaths, ID3D11Device device)
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DynamicTextureManager));

                if (usedPaths == null || device == null)
                {
                    ClearInternal();
                    return;
                }

                var currentPaths = new HashSet<string>(usedPaths);
                var keysToRemove = _cache.Keys.Except(currentPaths).ToList();

                foreach (var key in keysToRemove)
                {
                    if (_cache.TryGetValue(key, out var srv))
                    {
                        srv?.Dispose();
                        _cache.Remove(key);
                    }
                }

                foreach (var path in currentPaths)
                {
                    if (!_cache.ContainsKey(path))
                    {
                        try
                        {
                            var (srv, _) = _textureService.CreateShaderResourceView(path, device);
                            if (srv != null)
                            {
                                _cache[path] = srv;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DynamicTextureManager));
                ClearInternal();
            }
        }

        private void ClearInternal()
        {
            foreach (var srv in _cache.Values)
            {
                srv?.Dispose();
            }
            _cache.Clear();
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                ClearInternal();
            }
        }
    }
}

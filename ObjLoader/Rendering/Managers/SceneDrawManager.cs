using ObjLoader.Api.Draw;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Rendering.Managers
{
    internal sealed class BillboardTextureCache : IDisposable
    {
        public ID3D11Texture2D Texture { get; }
        public ID3D11ShaderResourceView Srv { get; }
        public ID2D1Bitmap1 D2dTarget { get; }
        public int Width { get; }
        public int Height { get; }
        public nint SharedHandle { get; }

        public BillboardTextureCache(ID3D11Device device, ID2D1DeviceContext d2dContext, int width, int height)
        {
            Width = width;
            Height = height;

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
            Texture = device.CreateTexture2D(texDesc);
            Srv = device.CreateShaderResourceView(Texture);

            using var dxgiResource = Texture.QueryInterface<IDXGIResource>();
            SharedHandle = dxgiResource.SharedHandle;

            using var surface = Texture.QueryInterface<IDXGISurface>();
            var bitmapProps = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                96.0f, 96.0f,
                BitmapOptions.Target | BitmapOptions.CannotDraw);
            D2dTarget = d2dContext.CreateBitmapFromDxgiSurface(surface, bitmapProps);
        }

        public void Dispose()
        {
            D2dTarget?.Dispose();
            Srv?.Dispose();
            Texture?.Dispose();
        }
    }

    internal sealed class SceneDrawManager : Interfaces.ISceneDrawManager
    {
        private readonly List<ExternalObjectHandle> _externalObjects = [];
        private readonly List<(Api.Core.SceneObjectId Id, BillboardDescriptor Desc)> _billboards = [];
        private readonly Dictionary<Api.Core.SceneObjectId, BillboardTextureCache> _textureCaches = [];
        private readonly HashSet<Api.Core.SceneObjectId> _currentIds = [];
        private readonly List<Api.Core.SceneObjectId> _keysToRemove = [];
        private readonly IGraphicsDevicesAndContext _devices;
        private bool _isDirty;
        private bool _isDisposed;

        public event EventHandler? Updated;

        public bool IsDirty => _isDirty;
        public long UpdateCount { get; private set; }

        public SceneDrawManager(IGraphicsDevicesAndContext devices)
        {
            _devices = devices;
        }

        public void UpdateFromApi(SceneDrawApi api)
        {
            if (_isDisposed) return;
            _externalObjects.Clear();
            _billboards.Clear();

            _externalObjects.AddRange(api.GetVisibleExternalObjects());

            var currentBillboards = api.GetBillboards();
            _billboards.AddRange(currentBillboards);

            _currentIds.Clear();
            foreach (var b in currentBillboards)
            {
                _currentIds.Add(b.Id);
                if (b.Desc.Image == null) continue;

                try
                {
                    var bounds = _devices.DeviceContext.GetImageLocalBounds(b.Desc.Image);
                    int w = (int)Math.Ceiling(bounds.Right - bounds.Left);
                    int h = (int)Math.Ceiling(bounds.Bottom - bounds.Top);
                    if (w <= 0 || h <= 0) continue;

                    if (_textureCaches.TryGetValue(b.Id, out var cache))
                    {
                        if (cache.Width != w || cache.Height != h)
                        {
                            cache.Dispose();
                            _textureCaches.Remove(b.Id);
                            cache = null;
                        }
                    }

                    if (cache == null)
                    {
                        cache = new BillboardTextureCache(_devices.D3D.Device, _devices.DeviceContext, w, h);
                        _textureCaches[b.Id] = cache;
                    }

                    var d2dContext = _devices.DeviceContext;
                    var oldTarget = d2dContext.Target;
                    var oldTransform = d2dContext.Transform;
                    try
                    {
                        d2dContext.Target = cache.D2dTarget;
                        d2dContext.BeginDraw();
                        d2dContext.Clear(new Color4(0, 0, 0, 0));
                        d2dContext.Transform = System.Numerics.Matrix3x2.CreateTranslation(-bounds.Left, -bounds.Top);
                        d2dContext.DrawImage(b.Desc.Image);
                        d2dContext.EndDraw();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        d2dContext.Target = oldTarget;
                        d2dContext.Transform = oldTransform;
                    }
                }
                catch
                {
                }
            }

            _keysToRemove.Clear();
            foreach (var key in _textureCaches.Keys)
            {
                if (!_currentIds.Contains(key)) _keysToRemove.Add(key);
            }
            foreach (var key in _keysToRemove)
            {
                _textureCaches[key].Dispose();
                _textureCaches.Remove(key);
            }

            _isDirty = true;
            UpdateCount++;
            Updated?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyCollection<ExternalObjectHandle> GetExternalObjects() => _externalObjects;

        public IReadOnlyCollection<(Api.Core.SceneObjectId Id, BillboardDescriptor Desc)> GetBillboards() => _billboards;

        public ID3D11ShaderResourceView? GetBillboardSrv(Api.Core.SceneObjectId id)
        {
            if (_textureCaches.TryGetValue(id, out var cache)) return cache.Srv;
            return null;
        }

        public nint GetBillboardSharedHandle(Api.Core.SceneObjectId id)
        {
            if (_textureCaches.TryGetValue(id, out var cache)) return cache.SharedHandle;
            return IntPtr.Zero;
        }

        public void ClearDirtyFlag()
        {
            _isDirty = false;
        }

        public void Clear()
        {
            _externalObjects.Clear();
            _billboards.Clear();
            foreach (var cache in _textureCaches.Values) cache.Dispose();
            _textureCaches.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Clear();
        }
    }
}
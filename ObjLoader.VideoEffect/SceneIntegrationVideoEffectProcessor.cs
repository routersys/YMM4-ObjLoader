using ObjLoader.Api;
using ObjLoader.Api.Core;
using ObjLoader.Api.Draw;
using ObjLoader.Rendering.Core;
using ObjLoader.VideoEffect;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using AlphaMode = Vortice.DCommon.AlphaMode;

internal sealed class SceneIntegrationVideoEffectProcessor : IVideoEffectProcessor
{
    private sealed record BillboardSnapshot(
        Vector3 WorldPosition,
        Vector2 Size,
        Vector3 Rotation,
        bool FaceCamera,
        float Opacity,
        nint SharedHandle,
        int SharedWidth,
        int SharedHeight);

    private readonly SceneIntegrationVideoEffect _item;
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly nint _cachedDevicePointer;
    private readonly Lock _idLock = new();

    private volatile ID2D1Image? _input;
    private volatile bool _disposed;

    private SceneObjectId? _objectId;
    private volatile ISceneServices? _cachedServices;
    private string? _cachedSceneInstanceId;

    private volatile BillboardSnapshot? _lastSnapshot;

    private ID2D1Image? _lastSizedImage;
    private Vector2 _lastImageSize;

    private volatile ID2D1Bitmap1? _stagingBitmap;
    private volatile ID2D1Bitmap1? _expiredStagingBitmap;
    private int _stagingW;
    private int _stagingH;
    private nint _stagingSharedHandle;
    private int _needsSceneTrigger;

    public ID2D1Image Output => _input ?? throw new NullReferenceException(nameof(_input) + " is null");

    public SceneIntegrationVideoEffectProcessor(SceneIntegrationVideoEffect item, IGraphicsDevicesAndContext devices)
    {
        _item = item;
        _devices = devices;
        _cachedDevicePointer = devices.D3D?.Device?.NativePointer ?? IntPtr.Zero;
        ObjLoaderApi.SceneRegistrationChanged += OnSceneRegistrationChanged;
        item.X.PropertyChanged += OnPropertyChanged;
        item.Y.PropertyChanged += OnPropertyChanged;
        item.Z.PropertyChanged += OnPropertyChanged;
        item.Scale.PropertyChanged += OnPropertyChanged;
        item.ScaleX.PropertyChanged += OnPropertyChanged;
        item.ScaleY.PropertyChanged += OnPropertyChanged;
        item.RotationX.PropertyChanged += OnPropertyChanged;
        item.RotationY.PropertyChanged += OnPropertyChanged;
        item.RotationZ.PropertyChanged += OnPropertyChanged;
        item.Opacity.PropertyChanged += OnPropertyChanged;
        item.PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Interlocked.Exchange(ref _needsSceneTrigger, 1);
        _item.TriggerUpdate();
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        if (_disposed) return effectDescription.DrawDescription with { Opacity = 0 };

        var currentInput = _input;
        if (currentInput == null)
        {
            TryRemoveBillboard();
            return effectDescription.DrawDescription with { Opacity = 0 };
        }

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame <= 0 ? 1 : effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS <= 0 ? 60 : effectDescription.FPS;

        var x = (float)_item.X.GetValue(frame, length, fps);
        var y = (float)_item.Y.GetValue(frame, length, fps);
        var z = (float)_item.Z.GetValue(frame, length, fps);
        var scale = (float)_item.Scale.GetValue(frame, length, fps);
        var scaleX = (float)_item.ScaleX.GetValue(frame, length, fps);
        var scaleY = (float)_item.ScaleY.GetValue(frame, length, fps);
        var rotX = (float)_item.RotationX.GetValue(frame, length, fps);
        var rotY = (float)_item.RotationY.GetValue(frame, length, fps);
        var rotZ = (float)_item.RotationZ.GetValue(frame, length, fps);
        var opacity = (float)_item.Opacity.GetValue(frame, length, fps);

        var baseSize = GetImageSize(currentInput);
        var size = new Vector2(-(baseSize.X * 0.01f * scale * scaleX), baseSize.Y * 0.01f * scale * scaleY);

        bool hasStagingAlready = _stagingBitmap != null && _stagingSharedHandle != IntPtr.Zero;
        bool lockTaken = false;
        if (hasStagingAlready)
        {
            Monitor.TryEnter(ObjLoaderSource.SharedRenderLock, 0, ref lockTaken);
        }
        else
        {
            Monitor.Enter(ObjLoaderSource.SharedRenderLock, ref lockTaken);
        }

        bool stagingWasNewlyCreated = false;
        if (lockTaken)
        {
            try
            {
                stagingWasNewlyCreated = PreRenderToStagingCore(currentInput, baseSize);
            }
            finally
            {
                Monitor.Exit(ObjLoaderSource.SharedRenderLock);
            }
            FlushD3DContext();
        }

        var stagingBitmap = _stagingBitmap;
        if (stagingBitmap == null)
        {
            TryRemoveBillboard();
            return effectDescription.DrawDescription with { Opacity = 0 };
        }

        var descriptor = new BillboardDescriptor
        {
            Image = stagingBitmap,
            SharedHandle = _stagingSharedHandle,
            SharedWidth = _stagingW,
            SharedHeight = _stagingH,
            WorldPosition = new Vector3(x, y, z),
            Size = size,
            Rotation = new Vector3(rotX, rotY, rotZ),
            FaceCamera = _item.FaceCamera,
            Opacity = opacity
        };

        _lastSnapshot = new BillboardSnapshot(
            descriptor.WorldPosition, descriptor.Size, descriptor.Rotation,
            descriptor.FaceCamera, descriptor.Opacity,
            _stagingSharedHandle, _stagingW, _stagingH);

        var services = GetSceneServices();
        if (services?.Draw == null)
            return effectDescription.DrawDescription with { Opacity = 0 };

        bool isNew;
        lock (_idLock)
        {
            isNew = ApplyBillboard(services, descriptor);
        }

        bool paramChanged = Interlocked.Exchange(ref _needsSceneTrigger, 0) != 0;

        ForceRenderCurrentScene();

        if (isNew || paramChanged)
            _item.TriggerUpdate();

        return effectDescription.DrawDescription with { Opacity = 0 };
    }

    private bool PreRenderToStagingCore(ID2D1Image source, Vector2 baseSize)
    {
        int w = Math.Max(1, (int)Math.Ceiling((double)baseSize.X));
        int h = Math.Max(1, (int)Math.Ceiling((double)baseSize.Y));

        DisposeExpiredStaging();

        Vortice.RawRectF bounds = default;
        bool hasBounds = false;
        try
        {
            bounds = _devices.DeviceContext.GetImageLocalBounds(source);
            hasBounds = true;
        }
        catch { }

        if (hasBounds)
        {
            int bw = (int)Math.Ceiling((double)(bounds.Right - bounds.Left));
            int bh = (int)Math.Ceiling((double)(bounds.Bottom - bounds.Top));
            if (bw > 0) w = bw;
            if (bh > 0) h = bh;
        }

        if (w <= 0 || h <= 0) return false;

        var current = _stagingBitmap;
        bool created = false;
        if (current != null && (_stagingW != w || _stagingH != h))
        {
            Interlocked.Exchange(ref _expiredStagingBitmap, current);
            _stagingBitmap = null;
            _stagingSharedHandle = IntPtr.Zero;
            current = null;
        }

        if (current == null)
        {
            try
            {
                current = CreateStagingBitmap(w, h, out nint handle);
                _stagingW = w;
                _stagingH = h;
                _stagingSharedHandle = handle;
                _stagingBitmap = current;
                created = true;
            }
            catch { return false; }
        }

        DrawToStaging(current, source, hasBounds, bounds);
        return created;
    }

    private void DrawToStaging(ID2D1Bitmap1 target, ID2D1Image source, bool hasBounds, Vortice.RawRectF bounds)
    {
        var d2d = _devices.DeviceContext;
        var oldTarget = d2d.Target;
        var oldTransform = d2d.Transform;
        try
        {
            d2d.Target = target;
            d2d.BeginDraw();
            d2d.Clear(null);
            if (hasBounds)
                d2d.Transform = Matrix3x2.CreateTranslation(-bounds.Left, -bounds.Top);
            d2d.DrawImage(source);
            d2d.EndDraw();
        }
        catch
        {
            try { d2d.EndDraw(); } catch { }
        }
        finally
        {
            try { d2d.Target = oldTarget; } catch { try { d2d.Target = null; } catch { } }
            try { d2d.Transform = oldTransform; } catch { }
        }
    }

    private ID2D1Bitmap1 CreateStagingBitmap(int w, int h, out nint sharedHandle)
    {
        var texDesc = new Texture2DDescription
        {
            Width = w,
            Height = h,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.Shared
        };
        using var tex = _devices.D3D.Device.CreateTexture2D(texDesc);
        using var dxgiResource = tex.QueryInterface<IDXGIResource>();
        sharedHandle = dxgiResource.SharedHandle;
        using var surface = tex.QueryInterface<IDXGISurface>();
        var props = new BitmapProperties1(
            new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96.0f, 96.0f,
            BitmapOptions.Target);
        return _devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, props);
    }

    private void FlushD3DContext()
    {
        try { _devices.D3D?.Device?.ImmediateContext?.Flush(); } catch { }
    }

    private void DisposeExpiredStaging()
    {
        Interlocked.Exchange(ref _expiredStagingBitmap, null)?.Dispose();
    }

    private void ForceRenderCurrentScene()
    {
        var id = _cachedSceneInstanceId;
        if (id == null) return;
        try { ObjLoaderApi.ForceRender(id); } catch { }
    }

    private bool ApplyBillboard(ISceneServices services, BillboardDescriptor descriptor)
    {
        if (services.Draw == null) return false;
        try
        {
            if (!_objectId.HasValue)
            {
                _objectId = services.Draw.CreateDynamicBillboard(descriptor);
                return true;
            }
            if (!services.Draw.UpdateBillboard(_objectId.Value, descriptor))
            {
                _objectId = services.Draw.CreateDynamicBillboard(descriptor);
                return true;
            }
            return false;
        }
        catch
        {
            _objectId = null;
            return false;
        }
    }

    private void TryRemoveBillboard()
    {
        SceneObjectId? removedId;
        lock (_idLock)
        {
            removedId = _objectId;
            if (!removedId.HasValue) return;
            _objectId = null;
        }

        ISceneServices? services = null;
        try
        {
            services = GetSceneServices();
            services?.Draw?.RemoveBillboard(removedId.Value);
        }
        catch { }

        try { services?.TriggerUpdate(); } catch { }
    }

    private Vector2 GetImageSize(ID2D1Image image)
    {
        if (ReferenceEquals(image, _lastSizedImage))
            return _lastImageSize;

        var size = new Vector2(100, 100);

        nint nativePtr;
        try { nativePtr = image.NativePointer; }
        catch { return size; }
        if (nativePtr == IntPtr.Zero) return size;

        if (image is ID2D1Bitmap bitmap)
        {
            size = new Vector2(bitmap.Size.Width, bitmap.Size.Height);
        }
        else if (_devices?.DeviceContext != null)
        {
            try
            {
                var bounds = _devices.DeviceContext.GetImageLocalBounds(image);
                size = new Vector2(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            }
            catch
            {
                _lastSizedImage = null;
                _lastImageSize = default;
                return size;
            }
        }

        _lastSizedImage = image;
        _lastImageSize = size;
        return size;
    }

    public void ClearInput()
    {
        _input = null;
        _lastSizedImage = null;
        _lastImageSize = default;
        _stagingSharedHandle = IntPtr.Zero;
        Interlocked.Exchange(ref _stagingBitmap, null)?.Dispose();
        Interlocked.Exchange(ref _expiredStagingBitmap, null)?.Dispose();
        TryRemoveBillboard();
    }

    public void SetInput(ID2D1Image? newInput)
    {
        _input = newInput;
    }

    private BillboardDescriptor BuildDescriptorFromSnapshot(
        BillboardSnapshot? snap,
        ID2D1Bitmap1 stagingBitmap,
        nint stagingHandle)
    {
        if (snap != null)
        {
            var s = snap;
            return new BillboardDescriptor
            {
                Image = stagingBitmap,
                SharedHandle = stagingHandle,
                SharedWidth = s.SharedWidth,
                SharedHeight = s.SharedHeight,
                WorldPosition = s.WorldPosition,
                Size = s.Size,
                Rotation = s.Rotation,
                FaceCamera = s.FaceCamera,
                Opacity = s.Opacity
            };
        }
        return new BillboardDescriptor
        {
            Image = stagingBitmap,
            SharedHandle = stagingHandle,
            SharedWidth = _stagingW,
            SharedHeight = _stagingH
        };
    }

    public void Dispose()
    {
        _disposed = true;
        ObjLoaderApi.SceneRegistrationChanged -= OnSceneRegistrationChanged;
        _item.X.PropertyChanged -= OnPropertyChanged;
        _item.Y.PropertyChanged -= OnPropertyChanged;
        _item.Z.PropertyChanged -= OnPropertyChanged;
        _item.Scale.PropertyChanged -= OnPropertyChanged;
        _item.ScaleX.PropertyChanged -= OnPropertyChanged;
        _item.ScaleY.PropertyChanged -= OnPropertyChanged;
        _item.RotationX.PropertyChanged -= OnPropertyChanged;
        _item.RotationY.PropertyChanged -= OnPropertyChanged;
        _item.RotationZ.PropertyChanged -= OnPropertyChanged;
        _item.Opacity.PropertyChanged -= OnPropertyChanged;
        _item.PropertyChanged -= OnPropertyChanged;
        TryRemoveBillboard();
        _cachedServices = null;
        _cachedSceneInstanceId = null;
        Interlocked.Exchange(ref _stagingBitmap, null)?.Dispose();
        Interlocked.Exchange(ref _expiredStagingBitmap, null)?.Dispose();
    }

    private void OnSceneRegistrationChanged(object? sender, SceneRegistrationChangedEventArgs e)
    {
        if (_disposed) return;

        if (!e.IsRegistered)
        {
            _cachedServices = null;
            _cachedSceneInstanceId = null;
            return;
        }

        _cachedServices = null;
        _cachedSceneInstanceId = null;

        ISceneServices? services = null;
        try
        {
            if (ObjLoaderApi.TryGetScene(e.InstanceId, out var s) && s != null && !s.IsDisposed)
                services = s;
        }
        catch { return; }

        if (services == null || _cachedDevicePointer == IntPtr.Zero || services.ContextPointer != _cachedDevicePointer)
            return;

        var currentInput = _input;
        if (currentInput == null) return;

        var stagingBitmap = _stagingBitmap;
        var stagingHandle = _stagingSharedHandle;

        if (stagingBitmap == null || stagingHandle == IntPtr.Zero)
        {
            _item.TriggerUpdate();
            return;
        }

        var snap = _lastSnapshot;
        var descriptor = BuildDescriptorFromSnapshot(snap, stagingBitmap, stagingHandle);

        bool isNew;
        lock (_idLock)
        {
            isNew = ApplyBillboard(services, descriptor);
        }

        if (isNew)
        {
            try { ObjLoaderApi.ForceRender(e.InstanceId); } catch { }
            _item.TriggerUpdate();
            services.TriggerUpdate();
        }
    }

    private ISceneServices? GetSceneServices()
    {
        var cached = _cachedServices;
        if (cached != null && !cached.IsDisposed && cached.ContextPointer == _cachedDevicePointer)
            return cached;

        _cachedServices = null;
        _cachedSceneInstanceId = null;

        if (_cachedDevicePointer == IntPtr.Zero) return null;

        try
        {
            foreach (var id in ObjLoaderApi.GetActiveSceneIds())
            {
                if (!ObjLoaderApi.TryGetScene(id, out var s) || s == null || s.IsDisposed) continue;
                if (s.ContextPointer != _cachedDevicePointer) continue;
                _cachedServices = s;
                _cachedSceneInstanceId = id;
                return s;
            }
        }
        catch { }

        return null;
    }
}
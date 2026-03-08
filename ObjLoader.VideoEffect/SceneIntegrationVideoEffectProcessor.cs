using ObjLoader.Api;
using ObjLoader.Api.Core;
using ObjLoader.Api.Draw;
using ObjLoader.VideoEffect;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

internal class SceneIntegrationVideoEffectProcessor : IVideoEffectProcessor
{
    private readonly SceneIntegrationVideoEffect item;
    private readonly IGraphicsDevicesAndContext devices;
    private ID2D1Image? input;
    private SceneObjectId? objectId;
    private readonly BillboardDescriptor lastDescriptor = new();
    private readonly object syncLock = new object();
    private ISceneServices? _cachedServices;
    private nint _cachedDevicePointer;

    public ID2D1Image Output => input ?? throw new NullReferenceException(nameof(input) + " is null");

    public SceneIntegrationVideoEffectProcessor(SceneIntegrationVideoEffect item, IGraphicsDevicesAndContext devices)
    {
        this.item = item;
        this.devices = devices;
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
        item.TriggerUpdate();
    }

    private ID2D1Image? lastSizedImage;
    private Vector2 lastImageSize;

    public DrawDescription Update(EffectDescription effectDescription)
    {
        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame <= 0 ? 1 : effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS <= 0 ? 60 : effectDescription.FPS;

        if (input != null)
        {
            var x = (float)item.X.GetValue(frame, length, fps);
            var y = (float)item.Y.GetValue(frame, length, fps);
            var z = (float)item.Z.GetValue(frame, length, fps);
            var scale = (float)item.Scale.GetValue(frame, length, fps);
            var scaleX = (float)item.ScaleX.GetValue(frame, length, fps);
            var scaleY = (float)item.ScaleY.GetValue(frame, length, fps);
            var rotX = (float)item.RotationX.GetValue(frame, length, fps);
            var rotY = (float)item.RotationY.GetValue(frame, length, fps);
            var rotZ = (float)item.RotationZ.GetValue(frame, length, fps);
            var opacity = (float)item.Opacity.GetValue(frame, length, fps);

            var baseSize = GetImageSize(input);
            var size = new Vector2(-(baseSize.X * 0.01f * scale * scaleX), baseSize.Y * 0.01f * scale * scaleY);

            lock (syncLock)
            {
                lastDescriptor.Image = input;
                lastDescriptor.WorldPosition = new Vector3(x, y, z);
                lastDescriptor.Size = size;
                lastDescriptor.Rotation = new Vector3(rotX, rotY, rotZ);
                lastDescriptor.FaceCamera = item.FaceCamera;
                lastDescriptor.Opacity = opacity;

                var services = GetSceneServices();
                if (services?.Draw != null)
                {
                    RegisterOrUpdateBillboard(services);
                }
            }
        }
        else
        {
            lock (syncLock)
            {
                RemoveBillboard();
            }
        }

        return effectDescription.DrawDescription with { Opacity = 0 };
    }

    private void RegisterOrUpdateBillboard(ISceneServices services)
    {
        if (services.Draw == null) return;

        try
        {
            bool isNew = !objectId.HasValue;
            if (isNew)
            {
                objectId = services.Draw.CreateDynamicBillboard(lastDescriptor);
            }
            else
            {
                var currentId = objectId!.Value;
                if (!services.Draw.UpdateBillboard(currentId, lastDescriptor))
                {
                    objectId = services.Draw.CreateDynamicBillboard(lastDescriptor);
                    isNew = true;
                }
            }

            if (isNew)
            {
                item.TriggerUpdate();
                services.TriggerUpdate();
            }
        }
        catch
        {
            objectId = null;
        }
    }

    private void RemoveBillboard()
    {
        if (!objectId.HasValue) return;

        try
        {
            var services = GetSceneServices();
            services?.Draw?.RemoveBillboard(objectId.Value);
            objectId = null;
            services?.TriggerUpdate();
        }
        catch
        {
            objectId = null;
        }
    }

    private Vector2 GetImageSize(ID2D1Image image)
    {
        if (ReferenceEquals(image, lastSizedImage))
        {
            return lastImageSize;
        }

        Vector2 size = new Vector2(100, 100);

        if (image is ID2D1Bitmap bitmap)
        {
            size = new Vector2(bitmap.Size.Width, bitmap.Size.Height);
        }
        else if (devices?.DeviceContext != null)
        {
            try
            {
                var bounds = devices.DeviceContext.GetImageLocalBounds(image);
                size = new Vector2(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            }
            catch
            {
            }
        }

        lastSizedImage = image;
        lastImageSize = size;
        return size;
    }

    public void ClearInput()
    {
        input = null;
        lock (syncLock)
        {
            RemoveBillboard();
        }
    }

    public void SetInput(ID2D1Image? input)
    {
        this.input = input;
        if (input != null)
        {
            lock (syncLock)
            {
                lastDescriptor.Image = input;
                var services = GetSceneServices();
                if (services?.Draw != null)
                {
                    RegisterOrUpdateBillboard(services);
                }
            }
        }
    }

    public void Dispose()
    {
        ObjLoaderApi.SceneRegistrationChanged -= OnSceneRegistrationChanged;
        item.X.PropertyChanged -= OnPropertyChanged;
        item.Y.PropertyChanged -= OnPropertyChanged;
        item.Z.PropertyChanged -= OnPropertyChanged;
        item.Scale.PropertyChanged -= OnPropertyChanged;
        item.ScaleX.PropertyChanged -= OnPropertyChanged;
        item.ScaleY.PropertyChanged -= OnPropertyChanged;
        item.RotationX.PropertyChanged -= OnPropertyChanged;
        item.RotationY.PropertyChanged -= OnPropertyChanged;
        item.RotationZ.PropertyChanged -= OnPropertyChanged;
        item.Opacity.PropertyChanged -= OnPropertyChanged;
        item.PropertyChanged -= OnPropertyChanged;
        lock (syncLock)
        {
            RemoveBillboard();
        }
        _cachedServices = null;
    }

    private void OnSceneRegistrationChanged(object? sender, SceneRegistrationChangedEventArgs e)
    {
        if (e.IsRegistered)
        {
            lock (syncLock)
            {
                _cachedServices = null;
                if (lastDescriptor.Image != null && ObjLoaderApi.TryGetScene(e.InstanceId, out var services) && services != null && !services.IsDisposed)
                {
                    if (_cachedDevicePointer != IntPtr.Zero && services.ContextPointer == _cachedDevicePointer)
                    {
                        RegisterOrUpdateBillboard(services);
                    }
                }
            }
        }
        else
        {
            lock (syncLock)
            {
                _cachedServices = null;
            }
        }
    }

    private ISceneServices? GetSceneServices()
    {
        var cached = _cachedServices;
        if (cached != null && !cached.IsDisposed && cached.ContextPointer == _cachedDevicePointer)
        {
            return cached;
        }

        _cachedServices = null;

        try
        {
            var services = ObjLoaderApi.GetFirstScene();
            if (services != null && _cachedDevicePointer != IntPtr.Zero && services.ContextPointer == _cachedDevicePointer)
            {
                _cachedServices = services;
                return services;
            }
        }
        catch
        {
        }
        return null;
    }
}
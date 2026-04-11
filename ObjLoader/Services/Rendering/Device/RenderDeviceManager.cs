using ObjLoader.Infrastructure;
using ObjLoader.Settings;
using System.Windows.Media.Imaging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Services.Rendering.Device;

internal sealed class RenderDeviceManager : IDisposable
{
    private readonly string _instanceId;

    public ID3D11Device? Device { get; private set; }
    public ID3D11DeviceContext? Context { get; private set; }
    public ID3D11Texture2D? RenderTarget { get; private set; }
    public ID3D11RenderTargetView? Rtv { get; private set; }
    public ID3D11Texture2D? DepthStencil { get; private set; }
    public ID3D11DepthStencilView? Dsv { get; private set; }
    public ID3D11Texture2D? ResolveTexture { get; private set; }
    public ID3D11Texture2D? StagingTexture { get; private set; }
    public int ViewportWidth { get; private set; }
    public int ViewportHeight { get; private set; }

    public RenderDeviceManager(string instanceId)
    {
        _instanceId = instanceId;
    }

    private string TrackingKey(string resourceName) => $"RenderSvc:{_instanceId}:{resourceName}";

    public bool Initialize()
    {
        var result = D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, [FeatureLevel.Level_11_0], out var device, out var context);
        if (result.Failure || device == null) return false;
        Device = device;
        Context = context;
        return true;
    }

    public WriteableBitmap? Resize(int width, int height, ref byte[]? stagingBuffer, ref int stagingBufferSize)
    {
        if (width < 1 || height < 1 || Device == null) return null;

        var settings = PluginSettings.Instance;
        int scaleFactor = 1;
        int sampleCount = 4;

        switch (settings.RenderQuality)
        {
            case RenderQuality.High:
                scaleFactor = 2;
                sampleCount = 8;
                break;
            case RenderQuality.Standard:
                scaleFactor = 1;
                sampleCount = 4;
                break;
            case RenderQuality.Low:
                scaleFactor = 1;
                sampleCount = 1;
                break;
        }

        int targetWidth = width * scaleFactor;
        int targetHeight = height * scaleFactor;

        ID3D11Texture2D? newRenderTarget = null;
        ID3D11RenderTargetView? newRtv = null;
        ID3D11Texture2D? newDepthStencil = null;
        ID3D11DepthStencilView? newDsv = null;
        ID3D11Texture2D? newResolveTexture = null;
        ID3D11Texture2D? newStagingTexture = null;

        try
        {
            var texDesc = new Texture2DDescription
            {
                Width = targetWidth,
                Height = targetHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(sampleCount, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                CPUAccessFlags = CpuAccessFlags.None
            };
            newRenderTarget = Device.CreateTexture2D(texDesc);
            newRtv = Device.CreateRenderTargetView(newRenderTarget);

            var depthDesc = new Texture2DDescription
            {
                Width = targetWidth,
                Height = targetHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(sampleCount, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CPUAccessFlags = CpuAccessFlags.None
            };
            newDepthStencil = Device.CreateTexture2D(depthDesc);
            newDsv = Device.CreateDepthStencilView(newDepthStencil);

            var resolveDesc = texDesc;
            resolveDesc.SampleDescription = new SampleDescription(1, 0);
            newResolveTexture = Device.CreateTexture2D(resolveDesc);

            var stagingDesc = resolveDesc;
            stagingDesc.Usage = ResourceUsage.Staging;
            stagingDesc.BindFlags = BindFlags.None;
            stagingDesc.CPUAccessFlags = CpuAccessFlags.Read;
            newStagingTexture = Device.CreateTexture2D(stagingDesc);
        }
        catch
        {
            newRtv?.Dispose();
            newRenderTarget?.Dispose();
            newDsv?.Dispose();
            newDepthStencil?.Dispose();
            newResolveTexture?.Dispose();
            newStagingTexture?.Dispose();
            throw;
        }

        UnregisterResizeResources();

        Rtv?.Dispose();
        RenderTarget?.Dispose();
        Dsv?.Dispose();
        DepthStencil?.Dispose();
        StagingTexture?.Dispose();
        ResolveTexture?.Dispose();

        RenderTarget = newRenderTarget;
        Rtv = newRtv;
        DepthStencil = newDepthStencil;
        Dsv = newDsv;
        ResolveTexture = newResolveTexture;
        StagingTexture = newStagingTexture;

        ViewportWidth = targetWidth;
        ViewportHeight = targetHeight;

        long renderTargetBytes = (long)targetWidth * targetHeight * 4 * sampleCount;
        ResourceTracker.Instance.Register(TrackingKey("RenderTarget"), "ID3D11Texture2D:RT", RenderTarget, renderTargetBytes);

        long depthStencilBytes = (long)targetWidth * targetHeight * 4 * sampleCount;
        ResourceTracker.Instance.Register(TrackingKey("DepthStencil"), "ID3D11Texture2D:DS", DepthStencil, depthStencilBytes);

        long resolveBytes = (long)targetWidth * targetHeight * 4;
        ResourceTracker.Instance.Register(TrackingKey("ResolveTexture"), "ID3D11Texture2D:Resolve", ResolveTexture, resolveBytes);

        long stagingBytes = (long)targetWidth * targetHeight * 4;
        ResourceTracker.Instance.Register(TrackingKey("StagingTexture"), "ID3D11Texture2D:Staging", StagingTexture, stagingBytes);

        int requiredBufferSize = targetWidth * targetHeight * 4;
        if (stagingBuffer == null || stagingBufferSize < requiredBufferSize)
        {
            stagingBuffer = new byte[requiredBufferSize];
            stagingBufferSize = requiredBufferSize;
        }

        return new WriteableBitmap(targetWidth, targetHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
    }

    private void UnregisterResizeResources()
    {
        ResourceTracker.Instance.Unregister(TrackingKey("RenderTarget"));
        ResourceTracker.Instance.Unregister(TrackingKey("DepthStencil"));
        ResourceTracker.Instance.Unregister(TrackingKey("ResolveTexture"));
        ResourceTracker.Instance.Unregister(TrackingKey("StagingTexture"));
    }

    public void Dispose()
    {
        UnregisterResizeResources();

        Rtv?.Dispose(); Rtv = null;
        RenderTarget?.Dispose(); RenderTarget = null;
        Dsv?.Dispose(); Dsv = null;
        DepthStencil?.Dispose(); DepthStencil = null;
        StagingTexture?.Dispose(); StagingTexture = null;
        ResolveTexture?.Dispose(); ResolveTexture = null;

        if (Context != null)
        {
            try { Context.ClearState(); } catch { }
            try { Context.Flush(); } catch { }
            Context.Dispose();
            Context = null;
        }
        Device?.Dispose(); Device = null;
    }
}
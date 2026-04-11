using ObjLoader.Utilities.Logging;
using System.Windows;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ObjLoader.Services.Rendering.Device;

internal sealed class StagingBufferTransfer
{
    public void CopyToStagingBuffer(
        ID3D11DeviceContext context,
        ID3D11Texture2D resolveTexture,
        ID3D11Texture2D renderTarget,
        ID3D11Texture2D stagingTexture,
        int viewportWidth,
        int viewportHeight,
        byte[] stagingBuffer)
    {
        context.ResolveSubresource(resolveTexture, 0, renderTarget, 0, Format.B8G8R8A8_UNorm);
        context.CopyResource(stagingTexture, resolveTexture);
        context.Flush();

        try
        {
            var map = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                if (map.DataPointer != IntPtr.Zero)
                {
                    int rowBytes = viewportWidth * 4;
                    if (stagingBuffer.Length >= viewportHeight * rowBytes)
                    {
                        unsafe
                        {
                            var srcPtr = (byte*)map.DataPointer;
                            fixed (byte* dstBase = stagingBuffer)
                            {
                                for (int r = 0; r < viewportHeight; r++)
                                {
                                    Buffer.MemoryCopy(srcPtr + (r * map.RowPitch), dstBase + (r * rowBytes), rowBytes, rowBytes);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (map.DataPointer != IntPtr.Zero)
                {
                    context.Unmap(stagingTexture, 0);
                }
            }
        }
        catch (SharpGen.Runtime.SharpGenException ex) when (ex.ResultCode.Code == unchecked((int)0x887A000A))
        {
            Logger<StagingBufferTransfer>.Instance.Error("Failed to map staging texture", ex);
        }
    }

    public void UpdateBitmapFromStagingBuffer(
        WriteableBitmap sceneImage,
        byte[] stagingBuffer,
        int viewportWidth,
        int viewportHeight)
    {
        int rowBytes = viewportWidth * 4;
        bool locked = false;
        try
        {
            sceneImage.Lock();
            locked = true;
            unsafe
            {
                var dstPtr = (byte*)sceneImage.BackBuffer;
                fixed (byte* srcBase = stagingBuffer)
                {
                    for (int r = 0; r < viewportHeight; r++)
                    {
                        Buffer.MemoryCopy(srcBase + (r * rowBytes), dstPtr + (r * sceneImage.BackBufferStride), sceneImage.BackBufferStride, rowBytes);
                    }
                }
            }
            sceneImage.AddDirtyRect(new Int32Rect(0, 0, viewportWidth, viewportHeight));
        }
        finally
        {
            if (locked)
            {
                sceneImage.Unlock();
            }
        }
    }
}
using Vortice.Direct3D11;
using YukkuriMovieMaker.Commons;
using D2D = Vortice.Direct2D1;

namespace ObjLoader.Rendering.Managers.Interfaces
{
    internal interface IRenderTargetManager : IDisposable
    {
        ID3D11Texture2D? RenderTargetTexture { get; }
        ID3D11RenderTargetView? RenderTargetView { get; }
        ID3D11Texture2D? DepthStencilTexture { get; }
        ID3D11DepthStencilView? DepthStencilView { get; }
        D2D.ID2D1Bitmap1? SharedBitmap { get; }
        bool EnsureSize(IGraphicsDevicesAndContext devices, int width, int height);
    }
}
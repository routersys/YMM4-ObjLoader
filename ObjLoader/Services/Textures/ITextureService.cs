using System.Windows.Media.Imaging;
using Vortice.Direct3D11;

namespace ObjLoader.Services.Textures
{
    public interface ITextureService : IDisposable
    {
        BitmapSource Load(string path);
        void RegisterLoader(ITextureLoader loader);
        (ID3D11ShaderResourceView? Srv, long GpuBytes) CreateShaderResourceView(string path, ID3D11Device device);
    }
}
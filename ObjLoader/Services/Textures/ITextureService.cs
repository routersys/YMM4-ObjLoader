using System.Windows.Media.Imaging;

namespace ObjLoader.Services.Textures
{
    public interface ITextureService : IDisposable
    {
        BitmapSource Load(string path);
        void RegisterLoader(ITextureLoader loader);
    }
}
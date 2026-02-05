using System.Windows.Media.Imaging;

namespace ObjLoader.Services.Textures
{
    public interface ITextureLoader
    {
        bool CanLoad(string path);
        BitmapSource Load(string path);
        int Priority { get; }
    }
}
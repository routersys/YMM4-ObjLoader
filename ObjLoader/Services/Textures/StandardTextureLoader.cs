using System.IO;
using System.Windows.Media.Imaging;

namespace ObjLoader.Services.Textures
{
    public class StandardTextureLoader : ITextureLoader
    {
        public int Priority => 0;

        public bool CanLoad(string path)
        {
            return true;
        }

        public BitmapSource Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
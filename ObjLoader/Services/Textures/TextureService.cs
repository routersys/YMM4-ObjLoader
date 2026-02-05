using System.Windows.Media.Imaging;

namespace ObjLoader.Services.Textures
{
    public class TextureService
    {
        private readonly List<ITextureLoader> _loaders = new List<ITextureLoader>();

        public TextureService()
        {
            RegisterLoader(new TgaTextureLoader());
            RegisterLoader(new StandardTextureLoader());
        }

        public void RegisterLoader(ITextureLoader loader)
        {
            _loaders.Add(loader);
        }

        public BitmapSource Load(string path)
        {
            var loader = _loaders
                .OrderByDescending(l => l.Priority)
                .FirstOrDefault(l => l.CanLoad(path));

            if (loader != null)
            {
                try
                {
                    return loader.Load(path);
                }
                catch
                {
                    throw;
                }
            }

            throw new NotSupportedException($"No suitable loader found for texture: {path}");
        }
    }
}
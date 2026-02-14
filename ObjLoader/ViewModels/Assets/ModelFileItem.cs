using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ObjLoader.ViewModels.Assets
{
    public class ModelFileItem
    {
        private readonly string _path;
        private readonly Func<string, byte[]> _loader;
        private WeakReference<ImageSource>? _thumbnailWeakRef;

        public string FileName { get; }
        public string FullPath => _path;

        public bool IsThumbnailEnabled { get; }

        public ImageSource? Thumbnail
        {
            get
            {
                if (_thumbnailWeakRef != null && _thumbnailWeakRef.TryGetTarget(out var cached))
                {
                    return cached;
                }

                try
                {
                    var bytes = _loader(_path);
                    if (bytes.Length > 0)
                    {
                        var image = new BitmapImage();
                        using var ms = new MemoryStream(bytes);
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = ms;
                        image.DecodePixelWidth = 64;
                        image.EndInit();
                        image.Freeze();

                        _thumbnailWeakRef = new WeakReference<ImageSource>(image);
                        return image;
                    }
                }
                catch { }

                return null;
            }
        }

        public ModelFileItem(string fileName, string fullPath, Func<string, byte[]> loader, bool isThumbnailEnabled)
        {
            FileName = fileName;
            _path = fullPath;
            _loader = loader;
            IsThumbnailEnabled = isThumbnailEnabled;
        }
    }
}
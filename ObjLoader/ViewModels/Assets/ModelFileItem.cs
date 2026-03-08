using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Assets
{
    public class ModelFileItem : Bindable
    {
        private readonly string _path;
        private readonly Func<string, byte[]> _loader;
        private byte[]? _thumbnailBytes;
        private bool _isThumbnailEnabled;

        public string FileName { get; }
        public string FullPath => _path;

        public bool IsThumbnailEnabled
        {
            get => _isThumbnailEnabled;
            set
            {
                if (Set(ref _isThumbnailEnabled, value))
                {
                    OnPropertyChanged(nameof(ThumbnailBytes));
                }
            }
        }

        private bool _isThumbnailLoading;

        public byte[]? ThumbnailBytes
        {
            get
            {
                if (!_isThumbnailEnabled)
                {
                    return null;
                }

                if (_thumbnailBytes != null)
                {
                    return _thumbnailBytes;
                }

                if (!_isThumbnailLoading)
                {
                    _isThumbnailLoading = true;
                    LoadThumbnailAsync();
                }

                return null;
            }
        }

        private async void LoadThumbnailAsync()
        {
            try
            {
                var bytes = await Task.Run(() => _loader(_path));
                if (bytes.Length > 0)
                {
                    _thumbnailBytes = bytes;
                    OnPropertyChanged(nameof(ThumbnailBytes));
                }
            }
            catch { }
            finally
            {
                _isThumbnailLoading = false;
            }
        }

        public ModelFileItem(string fileName, string fullPath, Func<string, byte[]> loader, bool isThumbnailEnabled)
        {
            FileName = fileName;
            _path = fullPath;
            _loader = loader;
            _isThumbnailEnabled = isThumbnailEnabled;
        }
    }
}
using ObjLoader.Localization;
using System.IO;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Assets
{
    public class ModelFileItem : Bindable
    {
        public static readonly ModelFileItem Unselected = new(
            () => Texts.ModelFileSelector_NoneSelected,
            string.Empty);

        private readonly string _path;
        private readonly Func<string, byte[]>? _loader;
        private byte[]? _thumbnailBytes;
        private bool _isThumbnailEnabled;
        private bool _isThumbnailLoading;
        private readonly Func<string>? _displayNameResolver;
        private readonly string? _fileName;

        public string FileName => _displayNameResolver is not null ? _displayNameResolver() : _fileName!;
        public string FullPath => _path;
        public bool IsUnselected => ReferenceEquals(this, Unselected);

        public bool IsThumbnailEnabled
        {
            get => _isThumbnailEnabled;
            set
            {
                if (IsUnselected) return;
                if (Set(ref _isThumbnailEnabled, value))
                {
                    OnPropertyChanged(nameof(ThumbnailBytes));
                }
            }
        }

        public byte[]? ThumbnailBytes
        {
            get
            {
                if (!_isThumbnailEnabled || _loader is null)
                    return null;

                if (_thumbnailBytes != null)
                    return _thumbnailBytes;

                if (!_isThumbnailLoading)
                {
                    _isThumbnailLoading = true;
                    LoadThumbnailAsync();
                }

                return null;
            }
        }

        private ModelFileItem(Func<string> displayNameResolver, string fullPath)
        {
            _displayNameResolver = displayNameResolver;
            _path = fullPath;
            _loader = null;
            _isThumbnailEnabled = false;
        }

        public ModelFileItem(string fullPath, Func<string, byte[]> loader, bool isThumbnailEnabled)
        {
            _fileName = Path.GetFileName(fullPath);
            _path = fullPath;
            _loader = loader;
            _isThumbnailEnabled = isThumbnailEnabled;
        }

        private async void LoadThumbnailAsync()
        {
            try
            {
                var bytes = await Task.Run(() => _loader!(_path));
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
    }
}
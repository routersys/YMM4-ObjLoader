using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class LayerItemViewModel : Bindable
    {
        private readonly ObjModelLoader _loader;
        public LayerData Data { get; }

        public string Name => string.IsNullOrEmpty(Data.FilePath) ? Texts.Layer_New : Path.GetFileName(Data.FilePath);

        private ImageSource? _thumbnailSource;
        public ImageSource? ThumbnailSource
        {
            get => _thumbnailSource;
            set => Set(ref _thumbnailSource, value);
        }

        private int _depth;
        public int Depth
        {
            get => _depth;
            set
            {
                if (Set(ref _depth, value))
                {
                    OnPropertyChanged(nameof(IndentMargin));
                    OnPropertyChanged(nameof(LineVisibility));
                }
            }
        }

        public Thickness IndentMargin => new Thickness(Depth * 15, 0, 0, 0);
        public Visibility LineVisibility => Depth > 0 ? Visibility.Visible : Visibility.Collapsed;

        public LayerItemViewModel(LayerData data, ObjModelLoader loader)
        {
            Data = data;
            _loader = loader;
            UpdateThumbnail();
        }

        public void UpdateThumbnail()
        {
            byte[]? bytes = Data.Thumbnail;

            if ((bytes == null || bytes.Length == 0) && !string.IsNullOrEmpty(Data.FilePath) && File.Exists(Data.FilePath))
            {
                bytes = _loader.GetThumbnail(Data.FilePath);
            }

            if (bytes != null && bytes.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(bytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        ThumbnailSource = bitmap;
                    }
                }
                catch
                {
                    ThumbnailSource = null;
                }
            }
            else
            {
                ThumbnailSource = null;
            }
            OnPropertyChanged(nameof(Name));
        }
    }
}
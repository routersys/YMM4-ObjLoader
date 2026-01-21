using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class LayerItemViewModel : Bindable, IDisposable
    {
        private readonly ObjModelLoader _loader;
        private readonly Action _forceUpdateAction;
        public LayerData Data { get; }

        public bool IsChild => !string.IsNullOrEmpty(Data.ParentGuid);

        private int _sequentialIndex;
        public int SequentialIndex
        {
            get => _sequentialIndex;
            set => Set(ref _sequentialIndex, value);
        }

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(Data.Name) && Data.Name != "Layer" && Data.Name != "Default")
                {
                    return Data.Name;
                }

                if (IsChild)
                {
                    return GeneratePartName();
                }

                return string.IsNullOrEmpty(Data.FilePath) ? Texts.Layer_New : Path.GetFileName(Data.FilePath);
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Data.Name = string.Empty;
                }
                else
                {
                    Data.Name = value;
                }
                OnPropertyChanged();
            }
        }

        public string FileName => !string.IsNullOrEmpty(Data.FilePath) ? Path.GetFileName(Data.FilePath) : string.Empty;

        public string OriginalName => Data.Name;

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (Set(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(VisibilityTooltip));
                }
            }
        }

        public bool IsVisible
        {
            get => Data.IsVisible;
            set
            {
                if (Data.IsVisible != value)
                {
                    Data.IsVisible = value;
                    _forceUpdateAction?.Invoke();
                }
            }
        }

        public string VisibilityTooltip => IsVisible ? Texts.Layer_Visibility_Hide : Texts.Layer_Visibility_Show;

        public ActionCommand ToggleVisibilityCommand { get; }
        public ActionCommand StartEditCommand { get; }
        public ActionCommand EndEditCommand { get; }

        private ImageSource? _thumbnailSource;
        public ImageSource? ThumbnailSource
        {
            get => _thumbnailSource;
            set => Set(ref _thumbnailSource, value);
        }

        public ObservableCollection<ImageSource> PartThumbnails { get; } = new ObservableCollection<ImageSource>();

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

        public LayerItemViewModel(LayerData data, ObjModelLoader loader, Action forceUpdateAction)
        {
            Data = data;
            _loader = loader;
            _forceUpdateAction = forceUpdateAction;

            Data.PropertyChanged += Data_PropertyChanged;

            ToggleVisibilityCommand = new ActionCommand(_ => true, _ => IsVisible = !IsVisible);
            StartEditCommand = new ActionCommand(_ => true, _ => IsEditing = true);
            EndEditCommand = new ActionCommand(_ => true, _ => IsEditing = false);
            UpdateThumbnail();
            LoadPartThumbnails();
        }

        private void Data_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LayerData.IsVisible))
            {
                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(VisibilityTooltip));
            }
            else if (e.PropertyName == nameof(LayerData.Name))
            {
                OnPropertyChanged(nameof(Name));
            }
            else if (e.PropertyName == nameof(LayerData.FilePath))
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(FileName));
                UpdateThumbnail();
                LoadPartThumbnails();
            }
            else if (e.PropertyName == nameof(LayerData.VisibleParts))
            {
                OnPropertyChanged(nameof(Name));
                UpdateThumbnail();
                LoadPartThumbnails();
            }
            else if (e.PropertyName == nameof(LayerData.Thumbnail))
            {
                UpdateThumbnail();
            }
        }

        private string GeneratePartName()
        {
            if (Data.VisibleParts == null || Data.VisibleParts.Count == 0)
            {
                return string.Format(Texts.Layer_PartName, "?");
            }

            var sortedParts = Data.VisibleParts.OrderBy(x => x).ToList();
            var partString = string.Join("+", sortedParts);
            return string.Format(Texts.Layer_PartName, partString);
        }

        public void UpdateName()
        {
            OnPropertyChanged(nameof(Name));
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

        private void LoadPartThumbnails()
        {
            PartThumbnails.Clear();
            if (IsChild && Data.VisibleParts != null && Data.VisibleParts.Count > 0 && !string.IsNullOrEmpty(Data.FilePath) && File.Exists(Data.FilePath))
            {
                try
                {
                    var thumbs = _loader.GetPartThumbnails(Data.FilePath, Data.VisibleParts);
                    foreach (var bytes in thumbs)
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            PartThumbnails.Add(bitmap);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public void Dispose()
        {
            Data.PropertyChanged -= Data_PropertyChanged;
        }
    }
}
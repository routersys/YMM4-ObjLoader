using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using Microsoft.Win32;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class ModelFileSelectorViewModel : Bindable, IDisposable
    {
        private readonly ItemProperty _property;
        private readonly ObjLoaderParameter? _parameter;
        private readonly string _filterString;
        private readonly string[] _extensions;
        private readonly ObjModelLoader _loader;
        private bool _isSelecting;
        private int _notificationTrigger;

        public bool IsResetting { get; set; }

        public ObservableCollection<ModelFileItem> Files { get; } = new ObservableCollection<ModelFileItem>();

        public ModelFileItem? SelectedFile
        {
            get => Files.FirstOrDefault(x => x.FullPath.Equals(FilePath, StringComparison.OrdinalIgnoreCase));
            set
            {
                if (_isSelecting || IsResetting || value == null || value.FullPath == FilePath) return;
                FilePath = value.FullPath;
            }
        }

        public string FilePath
        {
            get => _property.GetValue<string>() ?? string.Empty;
            set
            {
                if (FilePath == value) return;
                _property.SetValue(value);

                UpdateView();
            }
        }

        public ICommand SelectFileCommand { get; }

        public ModelFileSelectorViewModel(ItemProperty property, ObjLoaderParameter? parameter, string filterKey, IEnumerable<string> args)
        {
            _property = property;
            _parameter = parameter;

            (_filterString, _extensions) = BuildFilterAndExtensions(filterKey, args);

            _loader = new ObjModelLoader();
            SelectFileCommand = new ActionCommand(_ => true, _ => SelectFile());

            if (_parameter != null)
            {
                PropertyChangedEventManager.AddHandler(_parameter, OnParameterPropertyChanged, nameof(ObjLoaderParameter.FilePath));
            }
            else if (_property is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += OnPropertyPropertyChanged;
            }

            UpdateFileList();
        }

        private (string, string[]) BuildFilterAndExtensions(string firstKey, IEnumerable<string> args)
        {
            var filters = new List<(string Key, List<string> Exts)>();
            var allExtensions = new List<string>();

            var currentKey = firstKey;
            var currentExts = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("."))
                {
                    currentExts.Add(arg);
                    allExtensions.Add(arg.ToLowerInvariant());
                }
                else
                {
                    if (currentExts.Count > 0)
                    {
                        filters.Add((currentKey, currentExts));
                    }
                    currentKey = arg;
                    currentExts = new List<string>();
                }
            }
            if (currentExts.Count > 0)
            {
                filters.Add((currentKey, currentExts));
            }

            var sb = new StringBuilder();
            foreach (var (key, exts) in filters)
            {
                if (sb.Length > 0) sb.Append("|");

                var desc = GetLocalizedText(key);
                var pattern = string.Join(";", exts.Select(e => "*" + e));

                sb.Append($"{desc} ({pattern})|{pattern}");
            }

            if (sb.Length > 0) sb.Append("|");
            sb.Append("All files (*.*)|*.*");

            return (sb.ToString(), allExtensions.ToArray());
        }

        private string GetLocalizedText(string key)
        {
            try
            {
                var prop = typeof(Texts).GetProperty(key, BindingFlags.Static | BindingFlags.Public);
                if (prop != null)
                {
                    return prop.GetValue(null) as string ?? key;
                }
            }
            catch { }
            return key;
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateView();
        }

        private void OnPropertyPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateView();
        }

        private void UpdateView()
        {
            Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(FilePath));
            UpdateFileList();
            Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(SelectedFile));
        }

        private void SelectFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = _filterString,
                FileName = FilePath,
                ClientGuid = new Guid("870C9532-8409-4C45-B4C2-436323631988")
            };

            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
            }
        }

        private void UpdateFileList()
        {
            _isSelecting = true;
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    Files.Clear();
                    if (!string.IsNullOrEmpty(FilePath))
                    {
                        var item = CreateItem(FilePath, true);
                        if (item != null) Files.Add(item);
                    }
                    return;
                }

                var currentFiles = Files.ToDictionary(x => x.FullPath);
                Files.Clear();

                var files = Directory.GetFiles(dir)
                    .Where(f => _extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f);

                foreach (var file in files)
                {
                    var isSelected = file.Equals(FilePath, StringComparison.OrdinalIgnoreCase);
                    var hasCache = File.Exists(file + ".bin");
                    var isThumbnailEnabled = isSelected || hasCache;

                    if (currentFiles.TryGetValue(file, out var existing))
                    {
                        if (existing.IsThumbnailEnabled == isThumbnailEnabled)
                        {
                            Files.Add(existing);
                        }
                        else
                        {
                            var item = CreateItem(file, isSelected);
                            if (item != null) Files.Add(item);
                        }
                    }
                    else
                    {
                        var item = CreateItem(file, isSelected);
                        if (item != null) Files.Add(item);
                    }
                }

                if (!string.IsNullOrEmpty(FilePath) && !Files.Any(x => x.FullPath.Equals(FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    var item = CreateItem(FilePath, true);
                    if (item != null) Files.Add(item);
                }

                OnPropertyChanged(nameof(SelectedFile));
            }
            finally
            {
                _isSelecting = false;
            }
        }

        private ModelFileItem? CreateItem(string path, bool isSelected)
        {
            if (!File.Exists(path)) return null;
            var hasCache = File.Exists(path + ".bin");
            var isThumbnailEnabled = isSelected || hasCache;
            return new ModelFileItem(Path.GetFileName(path), path, isThumbnailEnabled ? _loader.GetThumbnail : _ => Array.Empty<byte>(), isThumbnailEnabled);
        }

        public void Dispose()
        {
            if (_parameter != null)
            {
                PropertyChangedEventManager.RemoveHandler(_parameter, OnParameterPropertyChanged, nameof(ObjLoaderParameter.FilePath));
            }
        }
    }
}
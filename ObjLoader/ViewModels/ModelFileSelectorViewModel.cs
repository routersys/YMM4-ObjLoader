using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ObjLoader.Parsers;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class ModelFileSelectorViewModel : Bindable
    {
        private readonly ItemProperty _property;
        private readonly string _filter;
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

                Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(FilePath));

                UpdateFileList();

                Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(SelectedFile));
            }
        }

        public ICommand SelectFileCommand { get; }

        public ModelFileSelectorViewModel(ItemProperty property, string filter, IEnumerable<string> extensions)
        {
            _property = property;
            _filter = filter;
            _extensions = extensions.Select(e => e.ToLowerInvariant()).ToArray();
            _loader = new ObjModelLoader();
            SelectFileCommand = new ActionCommand(_ => true, _ => SelectFile());
            UpdateFileList();
        }

        private void SelectFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = _filter,
                FileName = FilePath
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

                Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(SelectedFile));
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
    }
}
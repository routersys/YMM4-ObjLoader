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
        private readonly string[] _extensions;
        private readonly ObjModelLoader _loader;
        private bool _isSelecting;
        private int _notificationTrigger;

        public ObservableCollection<ModelFileItem> Files { get; } = new ObservableCollection<ModelFileItem>();

        public ModelFileItem? SelectedFile
        {
            get => Files.FirstOrDefault(x => x.FullPath.Equals(FilePath, StringComparison.OrdinalIgnoreCase));
            set
            {
                if (_isSelecting || value == null || value.FullPath == FilePath) return;
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

        public ModelFileSelectorViewModel(ItemProperty property, string[] extensions)
        {
            _property = property;
            _extensions = extensions;
            _loader = new ObjModelLoader();
            SelectFileCommand = new ActionCommand(_ => true, _ => SelectFile());
            UpdateFileList();
        }

        private void SelectFile()
        {
            var filter = string.Join(";", _extensions.Select(e => "*" + e));
            var dialog = new OpenFileDialog
            {
                Filter = $"3D Model Files|{filter}|All Files|*.*",
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
                        var item = CreateItem(FilePath);
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
                    if (currentFiles.TryGetValue(file, out var existing))
                    {
                        Files.Add(existing);
                    }
                    else
                    {
                        var item = CreateItem(file);
                        if (item != null) Files.Add(item);
                    }
                }

                if (!string.IsNullOrEmpty(FilePath) && !Files.Any(x => x.FullPath.Equals(FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    var item = CreateItem(FilePath);
                    if (item != null) Files.Add(item);
                }

                Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(SelectedFile));
            }
            finally
            {
                _isSelecting = false;
            }
        }

        private ModelFileItem? CreateItem(string path)
        {
            if (!File.Exists(path)) return null;
            return new ModelFileItem(Path.GetFileName(path), path, _loader.GetThumbnail);
        }
    }
}
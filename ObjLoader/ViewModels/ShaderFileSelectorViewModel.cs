using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ObjLoader.Localization;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class ShaderFileSelectorViewModel : Bindable
    {
        private readonly ItemProperty _property;
        private readonly string[] _extensions;
        private bool _isSelecting;
        private int _notificationTrigger;

        public ObservableCollection<ShaderFileItem> Files { get; } = new ObservableCollection<ShaderFileItem>();

        public ShaderFileItem? SelectedFile
        {
            get
            {
                var currentPath = FilePath;

                if (string.IsNullOrEmpty(currentPath))
                {
                    return Files.FirstOrDefault(x => x.IsNone);
                }

                var exactMatch = Files.FirstOrDefault(x => !x.IsNone && string.Equals(x.FullPath, currentPath, StringComparison.OrdinalIgnoreCase));
                if (exactMatch != null) return exactMatch;

                try
                {
                    var normalizedCurrent = Path.GetFullPath(currentPath);
                    return Files.FirstOrDefault(x => !x.IsNone && string.Equals(Path.GetFullPath(x.FullPath), normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                           ?? Files.FirstOrDefault(x => x.IsNone);
                }
                catch
                {
                    return Files.FirstOrDefault(x => x.IsNone);
                }
            }
            set
            {
                if (_isSelecting || value == null) return;

                if (value.IsNone)
                {
                    FilePath = string.Empty;
                }
                else
                {
                    value.Validate();
                    FilePath = value.FullPath;
                }
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

        public ShaderFileSelectorViewModel(ItemProperty property, string[] extensions)
        {
            _property = property;
            _extensions = extensions;
            SelectFileCommand = new ActionCommand(_ => true, _ => SelectFile());

            UpdateFileList();
        }

        private void SelectFile()
        {
            var filter = string.Join(";", _extensions.Select(e => "*" + e));
            var dialog = new OpenFileDialog
            {
                Filter = $"Shader Files|{filter}|All Files|*.*",
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
                var currentFiles = Files.ToDictionary(x => x.FullPath, StringComparer.OrdinalIgnoreCase);
                Files.Clear();

                var noneItem = currentFiles.Values.FirstOrDefault(x => x.IsNone)
                               ?? new ShaderFileItem(Texts.Shader_None, string.Empty, true);
                Files.Add(noneItem);

                var dir = string.Empty;
                if (!string.IsNullOrEmpty(FilePath))
                {
                    try
                    {
                        dir = Path.GetDirectoryName(FilePath);
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir)
                        .Where(f => _extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .OrderBy(f => f);

                    foreach (var file in files)
                    {
                        if (currentFiles.TryGetValue(file, out var existing) && !existing.IsNone)
                        {
                            Files.Add(existing);
                        }
                        else
                        {
                            var item = CreateItem(file);
                            if (item != null) Files.Add(item);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(FilePath))
                {
                    var existingItem = Files.FirstOrDefault(x => !x.IsNone && string.Equals(x.FullPath, FilePath, StringComparison.OrdinalIgnoreCase));

                    if (existingItem == null)
                    {
                        if (currentFiles.TryGetValue(FilePath, out var existing) && !existing.IsNone)
                        {
                            Files.Add(existing);
                            existingItem = existing;
                        }
                        else
                        {
                            var item = CreateItem(FilePath);
                            if (item != null)
                            {
                                Files.Add(item);
                                existingItem = item;
                            }
                        }
                    }
                    existingItem?.Validate();
                }

                Set(ref _notificationTrigger, _notificationTrigger + 1, nameof(SelectedFile));
            }
            finally
            {
                _isSelecting = false;
            }
        }

        private ShaderFileItem? CreateItem(string path)
        {
            if (!File.Exists(path)) return null;
            return new ShaderFileItem(Path.GetFileName(path), path);
        }
    }
}
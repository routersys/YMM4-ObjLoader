using Microsoft.Win32;
using ObjLoader.Localization;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Settings
{
    public class MaterialTextureItemViewModel : MaterialItemViewModel
    {
        private readonly Func<string?> _getter;
        private readonly Action<string?> _setter;
        private readonly IEnumerable<string> _supportedExtensions;

        public string? Value
        {
            get => _getter();
            set
            {
                _setter(value);
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public string? DisplayText => System.IO.Path.GetFileName(Value);

        public ActionCommand SelectFileCommand { get; }

        public MaterialTextureItemViewModel(string labelKey, Func<string?> getter, Action<string?> setter, Action onUpdate, IEnumerable<string> supportedExtensions)
            : base(labelKey, onUpdate)
        {
            _getter = getter;
            _setter = setter;
            _supportedExtensions = supportedExtensions ?? throw new ArgumentNullException(nameof(supportedExtensions));
            SelectFileCommand = new ActionCommand(_ => true, _ => SelectFile());
        }

        private void SelectFile()
        {
            var extList = string.Join(";", _supportedExtensions.Select(e => "*" + e));
            var dialog = new OpenFileDialog
            {
                Filter = $"{Texts.Image_File}|{extList}|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                Value = dialog.FileName;
                OnUpdate();
            }
        }

        public override void Reset()
        {
            Value = null;
            OnUpdate();
        }
    }
}

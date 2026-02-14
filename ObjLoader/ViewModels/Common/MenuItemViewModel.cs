using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Common
{
    public class MenuItemViewModel : Bindable
    {
        private string _header = string.Empty;
        private ICommand? _command;
        private object? _icon;
        private bool _isCheckable;
        private bool _isChecked;
        private bool _isSeparator;
        private string _inputGestureText = string.Empty;
        private PropertyInfo? _checkPropertyInfo;
        private object? _sourceViewModel;
        private bool _isSubmenuOpen;

        public string Header
        {
            get => _header;
            set => Set(ref _header, value);
        }

        public ICommand? Command
        {
            get => _command;
            set => Set(ref _command, value);
        }

        public object? Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        public bool IsCheckable
        {
            get => _isCheckable;
            set => Set(ref _isCheckable, value);
        }

        public bool IsChecked
        {
            get
            {
                if (_checkPropertyInfo != null && _sourceViewModel != null)
                {
                    return (bool)(_checkPropertyInfo.GetValue(_sourceViewModel) ?? false);
                }
                return _isChecked;
            }
            set
            {
                if (_checkPropertyInfo != null && _sourceViewModel != null)
                {
                    _checkPropertyInfo.SetValue(_sourceViewModel, value);
                    OnPropertyChanged();
                }
                else
                {
                    Set(ref _isChecked, value);
                }
            }
        }

        public bool IsSeparator
        {
            get => _isSeparator;
            set => Set(ref _isSeparator, value);
        }

        public string InputGestureText
        {
            get => _inputGestureText;
            set => Set(ref _inputGestureText, value);
        }

        public bool IsSubmenuOpen
        {
            get => _isSubmenuOpen;
            set => Set(ref _isSubmenuOpen, value);
        }

        public ObservableCollection<MenuItemViewModel> Children { get; } = new ObservableCollection<MenuItemViewModel>();

        public void SetCheckProperty(object source, string propertyName)
        {
            _sourceViewModel = source;
            _checkPropertyInfo = source.GetType().GetProperty(propertyName);
        }

        public void UpdateCheckedState()
        {
            OnPropertyChanged(nameof(IsChecked));
        }
    }
}
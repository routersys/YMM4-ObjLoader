using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using ObjLoader.Infrastructure;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class SettingWindowViewModel : Bindable
    {
        private readonly object _target;
        private SettingGroupViewModel? _selectedGroup;
        private string _description = string.Empty;
        private string _backupJson = string.Empty;
        private readonly Dictionary<string, List<SettingItemViewModelBase>> _viewModels = new Dictionary<string, List<SettingItemViewModelBase>>();
        private readonly List<SettingGroupViewModel> _allGroups = new List<SettingGroupViewModel>();

        public ObservableCollection<SettingGroupViewModel> Groups { get; } = new ObservableCollection<SettingGroupViewModel>();
        public ObservableCollection<ButtonSettingViewModel> LeftButtons { get; } = new ObservableCollection<ButtonSettingViewModel>();
        public ObservableCollection<ButtonSettingViewModel> RightButtons { get; } = new ObservableCollection<ButtonSettingViewModel>();

        public SettingGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup != value)
                {
                    if (_selectedGroup != null) _selectedGroup.IsSelected = false;
                    _selectedGroup = value;
                    if (_selectedGroup != null) _selectedGroup.IsSelected = true;
                    OnPropertyChanged(nameof(SelectedGroup));
                    Description = string.Empty;
                }
            }
        }

        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        public SettingWindowViewModel() : this(null) { }

        public SettingWindowViewModel(object? target)
        {
            _target = target ?? this;
            Backup();
            Initialize();
        }

        public void AddGroup(SettingGroupViewModel group)
        {
            _allGroups.Add(group);
        }

        public void RegisterViewModel(string propertyName, SettingItemViewModelBase vm)
        {
            if (!_viewModels.TryGetValue(propertyName, out var list))
            {
                list = new List<SettingItemViewModelBase>();
                _viewModels[propertyName] = list;
            }
            list.Add(vm);
        }

        public void FinalizeGroups()
        {
            var rootGroups = new List<SettingGroupViewModel>();
            var groupDict = _allGroups.ToDictionary(g => g.Id);

            foreach (var group in _allGroups)
            {
                group.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SettingGroupViewModel.IsSelected) && group.IsSelected)
                    {
                        SelectedGroup = group;
                    }
                };

                if (!string.IsNullOrEmpty(group.ParentId) && groupDict.TryGetValue(group.ParentId, out var parent))
                {
                    parent.Children.Add(group);
                }
                else
                {
                    rootGroups.Add(group);
                }

                foreach (var item in group.Items)
                {
                    if (item is PropertySettingViewModel pvm && !string.IsNullOrEmpty(pvm.EnableBy))
                    {
                        if (_viewModels.TryGetValue(pvm.EnableBy, out var masters))
                        {
                            var master = masters.FirstOrDefault();
                            if (master is PropertySettingViewModel masterProp)
                            {
                                pvm.IsDependent = true;
                                void UpdateEnabled()
                                {
                                    if (masterProp.Value is bool b)
                                    {
                                        pvm.IsEnabled = b;
                                    }
                                }
                                masterProp.PropertyChanged += (s, e) =>
                                {
                                    if (e.PropertyName == nameof(PropertySettingViewModel.Value)) UpdateEnabled();
                                };
                                UpdateEnabled();
                            }
                        }
                    }
                }
            }

            foreach (var group in _allGroups)
            {
                var view = CollectionViewSource.GetDefaultView(group.Children);
                if (view != null)
                {
                    view.SortDescriptions.Add(new SortDescription(nameof(SettingGroupViewModel.Order), ListSortDirection.Ascending));
                }
            }

            foreach (var root in rootGroups)
            {
                Groups.Add(root);
            }

            if (Groups.Count > 0) SelectedGroup = Groups[0];
        }

        private void Backup()
        {
            try
            {
                _backupJson = JsonSerializer.Serialize(_target, _target.GetType());
            }
            catch { }
        }

        private void Rollback()
        {
            try
            {
                if (!string.IsNullOrEmpty(_backupJson))
                {
                    var original = JsonSerializer.Deserialize(_backupJson, _target.GetType());
                    if (original != null)
                    {
                        var props = _target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                        foreach (var p in props)
                        {
                            if (p.CanWrite)
                            {
                                p.SetValue(_target, p.GetValue(original));
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void Initialize()
        {
            if (_target is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != null && _viewModels.TryGetValue(e.PropertyName, out var vms))
                    {
                        foreach (var vm in vms)
                        {
                            vm.Refresh();
                        }
                    }
                };
            }

            var typeName = "ObjLoader.Generated.SettingsInitializer";
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetType(typeName);
            if (type != null)
            {
                var method = type.GetMethod("TryInitialize", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (method != null)
                {
                    method.Invoke(null, new object[] { _target, this });
                    return;
                }
            }

            FallbackInitialize();
        }

        private void FallbackInitialize()
        {
        }
    }
}
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

        public ObservableCollection<SettingGroupViewModel> Groups { get; } = new ObservableCollection<SettingGroupViewModel>();
        public ObservableCollection<ButtonSettingViewModel> LeftButtons { get; } = new ObservableCollection<ButtonSettingViewModel>();
        public ObservableCollection<ButtonSettingViewModel> RightButtons { get; } = new ObservableCollection<ButtonSettingViewModel>();

        public SettingGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (Set(ref _selectedGroup, value))
                {
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
                            var method = vm.GetType().GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (method != null)
                            {
                                method.Invoke(vm, new object[] { "Value" });
                                if (vm is ColorSettingViewModel)
                                {
                                    method.Invoke(vm, new object[] { "ColorValue" });
                                }
                            }
                        }
                    }
                };
            }

            var properties = _target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var methods = _target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var groupDict = new Dictionary<string, SettingGroupViewModel>();

            string GetString(Type? resourceType, string name)
            {
                if (resourceType != null && !string.IsNullOrEmpty(name))
                {
                    var prop = resourceType.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null) return prop.GetValue(null) as string ?? name;
                }
                return name;
            }

            foreach (var prop in properties)
            {
                var groupAttr = prop.GetCustomAttribute<SettingGroupAttribute>();
                if (groupAttr != null)
                {
                    if (!groupDict.ContainsKey(groupAttr.Id))
                    {
                        string title = GetString(groupAttr.ResourceType, groupAttr.Title);
                        groupDict[groupAttr.Id] = new SettingGroupViewModel(groupAttr.Id, title, groupAttr.Order, groupAttr.Icon);
                    }
                }

                var itemAttr = prop.GetCustomAttribute<SettingItemAttribute>();
                if (itemAttr != null)
                {
                    if (!groupDict.ContainsKey(itemAttr.GroupId))
                    {
                        groupDict[itemAttr.GroupId] = new SettingGroupViewModel(itemAttr.GroupId, itemAttr.GroupId, 0, "Geometry");
                    }

                    SettingItemViewModelBase? vm = itemAttr switch
                    {
                        TextSettingAttribute ta => new TextSettingViewModel(_target, prop, ta),
                        BoolSettingAttribute ba => new BoolSettingViewModel(_target, prop, ba),
                        RangeSettingAttribute ra => new RangeSettingViewModel(_target, prop, ra),
                        EnumSettingAttribute ea => new EnumSettingViewModel(_target, prop, ea),
                        ColorSettingAttribute ca => new ColorSettingViewModel(_target, prop, ca),
                        FilePathSettingAttribute fa => new FilePathSettingViewModel(_target, prop, fa),
                        _ => null
                    };

                    if (vm != null)
                    {
                        if (!_viewModels.ContainsKey(prop.Name))
                        {
                            _viewModels[prop.Name] = new List<SettingItemViewModelBase>();
                        }
                        _viewModels[prop.Name].Add(vm);

                        vm.Hovered += (s, e) =>
                        {
                            if (s is SettingItemViewModelBase item) Description = item.Description;
                        };
                        groupDict[itemAttr.GroupId].Items.Add(vm);
                    }
                }
            }

            foreach (var method in methods)
            {
                var btnAttr = method.GetCustomAttribute<SettingButtonAttribute>();
                if (btnAttr != null)
                {
                    Action<Window> action = w => { };
                    if (btnAttr.Type == SettingButtonType.OK)
                    {
                        action = w =>
                        {
                            var saveMethod = _target.GetType().GetMethod("Save", Type.EmptyTypes);
                            saveMethod?.Invoke(_target, null);
                            w?.Close();
                        };
                    }
                    else if (btnAttr.Type == SettingButtonType.Cancel)
                    {
                        action = w => { Rollback(); w?.Close(); };
                    }
                    else if (btnAttr.Type == SettingButtonType.Close)
                    {
                        action = w => w?.Close();
                    }

                    var vm = new ButtonSettingViewModel(_target, method, btnAttr, action);
                    vm.Hovered += (s, e) => Description = vm.Description;

                    if (btnAttr.Placement == SettingButtonPlacement.Content)
                    {
                        if (groupDict.TryGetValue(btnAttr.GroupId, out var group))
                        {
                            group.Items.Add(vm);
                        }
                    }
                    else if (btnAttr.Placement == SettingButtonPlacement.BottomLeft)
                    {
                        LeftButtons.Add(vm);
                    }
                    else if (btnAttr.Placement == SettingButtonPlacement.BottomRight)
                    {
                        RightButtons.Add(vm);
                    }
                }
            }

            var sortedGroups = groupDict.Values.ToList();
            sortedGroups.Sort();

            foreach (var group in sortedGroups)
            {
                var view = CollectionViewSource.GetDefaultView(group.Items);
                view.SortDescriptions.Add(new SortDescription(nameof(SettingItemViewModelBase.Order), ListSortDirection.Ascending));
                Groups.Add(group);
            }

            SortButtons(LeftButtons);
            SortButtons(RightButtons);

            SelectedGroup = Groups.FirstOrDefault();
        }

        private void SortButtons(ObservableCollection<ButtonSettingViewModel> collection)
        {
            var list = collection.ToList();
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
            collection.Clear();
            foreach (var item in list) collection.Add(item);
        }
    }
}
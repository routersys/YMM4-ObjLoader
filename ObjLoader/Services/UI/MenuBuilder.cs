using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Shapes;
using System.Windows.Media;
using ObjLoader.Attributes;
using ObjLoader.ViewModels.Common;

namespace ObjLoader.Services.UI
{
    public static class MenuBuilder
    {
        public static ObservableCollection<MenuItemViewModel> Build(object viewModel)
        {
            var result = new ObservableCollection<MenuItemViewModel>();
            var groups = new Dictionary<string, MenuItemViewModel>();
            var methods = viewModel.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var items = new List<(MenuAttribute Attr, MethodInfo Method)>();

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<MenuAttribute>();
                if (attr != null)
                {
                    items.Add((attr, method));
                }
            }

            var properties = viewModel.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<MenuAttribute>();
                if (attr != null && typeof(System.Windows.Input.ICommand).IsAssignableFrom(prop.PropertyType))
                {
                }
            }

            foreach (var (attr, method) in items.OrderBy(x => x.Attr.Order))
            {
                var menuItem = new MenuItemViewModel();

                if (attr.ResourceType != null)
                {
                    var prop = attr.ResourceType.GetProperty(attr.NameKey, BindingFlags.Static | BindingFlags.Public);
                    if (prop != null)
                    {
                        menuItem.Header = prop.GetValue(null) as string ?? attr.NameKey;
                    }
                    else
                    {
                        menuItem.Header = attr.NameKey;
                    }
                }
                else
                {
                    menuItem.Header = attr.NameKey;
                }

                if (!string.IsNullOrEmpty(attr.AcceleratorKey))
                {
                    menuItem.Header = $"{menuItem.Header} (_{attr.AcceleratorKey})";
                }

                if (!string.IsNullOrEmpty(attr.Icon))
                {
                    try
                    {
                        var geom = Geometry.Parse(attr.Icon);
                        var path = new Path { Data = geom, Fill = Brushes.Black, Stretch = Stretch.Uniform, Width = 16, Height = 16 };
                        menuItem.Icon = path;
                    }
                    catch
                    {
                        menuItem.Icon = attr.Icon;
                    }
                }

                menuItem.IsCheckable = attr.IsCheckable;
                menuItem.InputGestureText = attr.InputGestureText;

                if (attr.IsCheckable && !string.IsNullOrEmpty(attr.CheckPropertyName))
                {
                    menuItem.SetCheckProperty(viewModel, attr.CheckPropertyName);
                    if (viewModel is System.ComponentModel.INotifyPropertyChanged npc)
                    {
                        npc.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == attr.CheckPropertyName)
                            {
                                menuItem.UpdateCheckedState();
                            }
                        };
                    }
                }

                if (method.ReturnType == typeof(void))
                {
                }
            }

            var commandProperties = viewModel.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var menuItemsList = new List<(MenuAttribute Attr, MenuItemViewModel VM)>();

            foreach (var prop in commandProperties)
            {
                var attr = prop.GetCustomAttribute<MenuAttribute>();
                if (attr != null && typeof(System.Windows.Input.ICommand).IsAssignableFrom(prop.PropertyType))
                {
                    var command = prop.GetValue(viewModel) as System.Windows.Input.ICommand;
                    var menuItem = new MenuItemViewModel
                    {
                        Command = command,
                        IsCheckable = attr.IsCheckable,
                        InputGestureText = attr.InputGestureText
                    };

                    if (attr.ResourceType != null)
                    {
                        var resProp = attr.ResourceType.GetProperty(attr.NameKey, BindingFlags.Static | BindingFlags.Public);
                        menuItem.Header = resProp?.GetValue(null) as string ?? attr.NameKey;
                    }
                    else
                    {
                        menuItem.Header = attr.NameKey;
                    }

                    if (!string.IsNullOrEmpty(attr.AcceleratorKey))
                    {
                        menuItem.Header = $"{menuItem.Header} (_{attr.AcceleratorKey})";
                    }

                    if (!string.IsNullOrEmpty(attr.Icon))
                    {
                        menuItem.Icon = attr.Icon;
                    }

                    if (attr.IsCheckable && !string.IsNullOrEmpty(attr.CheckPropertyName))
                    {
                        menuItem.SetCheckProperty(viewModel, attr.CheckPropertyName);
                        if (viewModel is System.ComponentModel.INotifyPropertyChanged npc)
                        {
                            npc.PropertyChanged += (s, e) =>
                            {
                                if (e.PropertyName == attr.CheckPropertyName)
                                {
                                    menuItem.UpdateCheckedState();
                                }
                            };
                        }
                    }

                    menuItemsList.Add((attr, menuItem));
                }
            }

            foreach (var (attr, vm) in menuItemsList.OrderBy(x => x.Attr.Order))
            {
                if (!string.IsNullOrEmpty(attr.Group))
                {
                    if (!groups.ContainsKey(attr.Group))
                    {
                        var groupItem = new MenuItemViewModel();
                        if (attr.ResourceType != null && !string.IsNullOrEmpty(attr.GroupNameKey))
                        {
                            var resProp = attr.ResourceType.GetProperty(attr.GroupNameKey, BindingFlags.Static | BindingFlags.Public);
                            groupItem.Header = resProp?.GetValue(null) as string ?? attr.GroupNameKey;
                        }
                        else
                        {
                            groupItem.Header = attr.Group;
                        }
                        groups[attr.Group] = groupItem;
                        result.Add(groupItem);
                    }
                    groups[attr.Group].Children.Add(vm);

                    if (attr.IsSeparatorAfter)
                    {
                        groups[attr.Group].Children.Add(new MenuItemViewModel { IsSeparator = true });
                    }
                }
                else
                {
                    result.Add(vm);
                    if (attr.IsSeparatorAfter)
                    {
                        result.Add(new MenuItemViewModel { IsSeparator = true });
                    }
                }
            }

            return result;
        }
    }
}
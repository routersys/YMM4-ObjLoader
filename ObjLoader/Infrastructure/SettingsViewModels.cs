using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using ObjLoader.Localization;

namespace ObjLoader.Infrastructure
{
    public abstract class SettingItemViewModelBase : Bindable
    {
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
        private bool _isVisible = true;

        public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value); }
        private bool _isEnabled = true;

        public string EnableBy { get; set; } = string.Empty;
        public bool IsGroupHeader { get; set; }
        public bool IsDependent { get => _isDependent; set => Set(ref _isDependent, value); }
        private bool _isDependent;

        public bool IsHovered
        {
            get => _isHovered;
            set
            {
                if (Set(ref _isHovered, value) && value)
                {
                    OnHovered();
                }
            }
        }
        private bool _isHovered;

        public event EventHandler? Hovered;
        protected void OnHovered() => Hovered?.Invoke(this, EventArgs.Empty);

        public abstract void Commit();
        public abstract void LoadFrom(object source);
        public virtual void Refresh() { }

        internal static string GetString(Type? resourceType, string name)
        {
            if (resourceType != null && !string.IsNullOrEmpty(name))
            {
                var prop = resourceType.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null) return prop.GetValue(null) as string ?? name;
            }
            return name;
        }
    }

    public class SettingGroupViewModel : Bindable, IComparable<SettingGroupViewModel>
    {
        public string Id { get; }
        public string Title { get; }
        public string IconData { get; }
        public int Order { get; }
        public string ParentId { get; }

        public ObservableCollection<SettingItemViewModelBase> Items { get; } = new ObservableCollection<SettingItemViewModelBase>();
        public ObservableCollection<SettingItemViewModelBase> HeaderItems { get; } = new ObservableCollection<SettingItemViewModelBase>();
        public ObservableCollection<SettingGroupViewModel> Children { get; } = new ObservableCollection<SettingGroupViewModel>();

        public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }
        private bool _isExpanded = true;

        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
        private bool _isSelected;

        public SettingGroupViewModel(string id, string title, int order, string icon, string parentId, Type? resourceType)
        {
            Id = id;
            Title = SettingItemViewModelBase.GetString(resourceType, title);
            Order = order;
            IconData = icon;
            ParentId = parentId;
        }

        public int CompareTo(SettingGroupViewModel? other)
        {
            if (other == null) return 1;
            int order = Order.CompareTo(other.Order);
            return order == 0 ? string.Compare(Title, other.Title, StringComparison.Ordinal) : order;
        }
    }

    public class PropertySettingViewModel : SettingItemViewModelBase
    {
        protected readonly object Target;
        protected readonly PropertyInfo Property;

        public PropertySettingViewModel(object target, PropertyInfo property, SettingItemAttribute attr)
        {
            Target = target;
            Property = property;
            Label = GetString(attr.ResourceType, attr.Label);
            Description = GetString(attr.ResourceType, attr.Description);
            Order = attr.Order;
            EnableBy = attr.EnableBy;
            IsGroupHeader = attr.IsGroupHeader;

            if (string.IsNullOrEmpty(Description) && attr.ResourceType == null)
            {
                var display = property.GetCustomAttribute<DisplayAttribute>();
                if (display != null)
                {
                    Description = display.GetDescription() ?? string.Empty;
                }
            }
        }

        public virtual object? Value
        {
            get => Property.GetValue(Target);
            set
            {
                var current = Property.GetValue(Target);
                if (!Equals(current, value))
                {
                    Property.SetValue(Target, value);
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        public override void Commit() { }

        public override void LoadFrom(object source)
        {
            try
            {
                var val = Property.GetValue(source);
                Value = val;
            }
            catch { }
        }

        public override void Refresh()
        {
            OnPropertyChanged(nameof(Value));
        }
    }

    public class TextSettingViewModel : PropertySettingViewModel
    {
        public TextSettingViewModel(object target, PropertyInfo property, TextSettingAttribute attr)
            : base(target, property, attr) { }
    }

    public class BoolSettingViewModel : PropertySettingViewModel
    {
        public string TrueLabel { get; }
        public string FalseLabel { get; }
        public BoolSettingViewModel(object target, PropertyInfo property, BoolSettingAttribute attr)
            : base(target, property, attr)
        {
            if (attr.ResourceType != null)
            {
                TrueLabel = GetString(attr.ResourceType, attr.TrueLabel);
                FalseLabel = GetString(attr.ResourceType, attr.FalseLabel);
            }
            else
            {
                TrueLabel = attr.TrueLabel;
                FalseLabel = attr.FalseLabel;
            }
        }
    }

    public class RangeSettingViewModel : PropertySettingViewModel
    {
        public double Min { get; }
        public double Max { get; }
        public double Tick { get; }
        public string Unit { get; }
        public RangeSettingViewModel(object target, PropertyInfo property, RangeSettingAttribute attr)
            : base(target, property, attr)
        {
            Min = attr.Min;
            Max = attr.Max;
            Tick = attr.Tick;
            Unit = attr.Unit;
        }

        public override object? Value
        {
            get => Convert.ToDouble(base.Value);
            set => base.Value = Convert.ChangeType(value, Property.PropertyType);
        }
    }

    public class IntSpinnerSettingViewModel : PropertySettingViewModel
    {
        public int Min { get; }
        public int Max { get; }
        public ICommand UpCommand { get; }
        public ICommand DownCommand { get; }

        public IntSpinnerSettingViewModel(object target, PropertyInfo property, IntSpinnerSettingAttribute attr)
            : base(target, property, attr)
        {
            Min = attr.Min;
            Max = attr.Max;
            UpCommand = new ActionCommand(_ => true, _ => Add(1));
            DownCommand = new ActionCommand(_ => true, _ => Add(-1));
        }

        private void Add(int delta)
        {
            if (Value is int i)
            {
                var next = Math.Clamp(i + delta, Min, Max);
                Value = next;
            }
        }

        public override object? Value
        {
            get => Convert.ToInt32(base.Value);
            set => base.Value = Convert.ChangeType(value, Property.PropertyType);
        }
    }

    public class EnumItem
    {
        public string Label { get; }
        public object Value { get; }
        public EnumItem(string label, object value)
        {
            Label = label;
            Value = value;
        }
    }

    public class EnumSettingViewModel : PropertySettingViewModel
    {
        public List<EnumItem> EnumValues { get; }
        public EnumSettingViewModel(object target, PropertyInfo property, EnumSettingAttribute attr)
            : base(target, property, attr)
        {
            var values = Enum.GetValues(property.PropertyType);
            EnumValues = new List<EnumItem>();
            foreach (var v in values)
            {
                if (v == null) continue;
                var field = property.PropertyType.GetField(v.ToString()!);
                var displayAttr = field?.GetCustomAttribute<DisplayAttribute>();

                string label;
                if (displayAttr != null)
                {
                    label = displayAttr.GetName() ?? v.ToString()!;
                }
                else
                {
                    label = v.ToString()!;
                }
                EnumValues.Add(new EnumItem(label, v));
            }
        }
    }

    public class ColorSettingViewModel : PropertySettingViewModel
    {
        public ColorSettingViewModel(object target, PropertyInfo property, ColorSettingAttribute attr)
            : base(target, property, attr) { }

        public Color ColorValue
        {
            get => (Color)(Value ?? Colors.White);
            set => Value = value;
        }

        public override object? Value
        {
            get => base.Value;
            set
            {
                base.Value = value;
                OnPropertyChanged(nameof(ColorValue));
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            OnPropertyChanged(nameof(ColorValue));
        }
    }

    public class FilePathSettingViewModel : PropertySettingViewModel
    {
        public string Filter { get; }
        public ICommand SelectCommand { get; }

        public FilePathSettingViewModel(object target, PropertyInfo property, FilePathSettingAttribute attr)
            : base(target, property, attr)
        {
            Filter = attr.Filter;
            SelectCommand = new ActionCommand(_ => true, _ => SelectFile());
        }

        private void SelectFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Filter,
                FileName = Value as string ?? ""
            };
            if (dialog.ShowDialog() == true)
            {
                Value = dialog.FileName;
            }
        }
    }

    public class ButtonSettingViewModel : SettingItemViewModelBase
    {
        public ICommand Command { get; }
        public SettingButtonPlacement Placement { get; }

        public ButtonSettingViewModel(object target, MethodInfo method, SettingButtonAttribute attr, Action<Window> action)
        {
            Label = GetString(attr.ResourceType, attr.Label);
            Placement = attr.Placement;
            Order = attr.Order;
            EnableBy = attr.EnableBy;
            Description = "";
            Command = new ActionCommand(
                _ => true,
                param =>
                {
                    method.Invoke(target, null);
                    if (param is Window window)
                    {
                        if (attr.Type == SettingButtonType.OK)
                        {
                            window.DialogResult = true;
                        }
                        else if (attr.Type == SettingButtonType.Cancel)
                        {
                            window.DialogResult = false;
                        }
                        else
                        {
                            action?.Invoke(window);
                        }
                    }
                });
        }

        public override void Commit() { }
        public override void LoadFrom(object source) { }
    }
}
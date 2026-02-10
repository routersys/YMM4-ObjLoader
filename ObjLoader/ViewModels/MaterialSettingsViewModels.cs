using ObjLoader.Attributes;
using ObjLoader.Localization;
using ObjLoader.Settings;
using System.Collections.ObjectModel;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class MaterialSettingsViewModel : Bindable
    {
        public ObservableCollection<MaterialGroupViewModel> Groups { get; } = new();

        public MaterialSettingsViewModel(PartMaterialProperties target, Action onUpdate)
        {
            PartMaterialPropertiesFactory.CreateGroups(this, target, onUpdate);
        }
    }

    public class MaterialGroupViewModel : Bindable
    {
        public string Id { get; }
        public string Title { get; }
        public ObservableCollection<MaterialItemViewModel> Items { get; } = new();
        public ActionCommand ResetGroupCommand { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get
            {
                if (PluginSettings.Instance.MaterialExpanderStates.TryGetValue(Id, out var state))
                    return state;
                return true;
            }
            set
            {
                if (Set(ref _isExpanded, value))
                {
                    PluginSettings.Instance.MaterialExpanderStates[Id] = value;
                    PluginSettings.Instance.Save();
                }
            }
        }

        public MaterialGroupViewModel(string id, string titleKey, Action onUpdate)
        {
            Id = id;
            Title = Texts.ResourceManager.GetString(titleKey) ?? titleKey;
            _isExpanded = IsExpanded;
            ResetGroupCommand = new ActionCommand(_ => true, _ =>
            {
                foreach (var item in Items)
                {
                    item.Reset();
                }
                onUpdate?.Invoke();
            });
        }
    }

    public abstract class MaterialItemViewModel : Bindable
    {
        protected Action OnUpdate { get; }
        public string Label { get; }

        protected MaterialItemViewModel(string labelKey, Action onUpdate)
        {
            OnUpdate = onUpdate;
            Label = Texts.ResourceManager.GetString(labelKey) ?? labelKey;
        }

        public abstract void Reset();
    }

    public class MaterialRangeItemViewModel : MaterialItemViewModel
    {
        private readonly Func<double> _getter;
        private readonly Action<double> _setter;
        private readonly double _defaultValue;
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }

        public double Value
        {
            get => _getter();
            set
            {
                if (Math.Abs(_getter() - value) > double.Epsilon)
                {
                    _setter(value);
                    OnPropertyChanged(nameof(Value));
                    OnUpdate?.Invoke();
                }
            }
        }

        public MaterialRangeItemViewModel(string labelKey, Func<double> getter, Action<double> setter, double min, double max, double step, Action onUpdate)
            : base(labelKey, onUpdate)
        {
            _getter = getter;
            _setter = setter;
            Min = min;
            Max = max;
            Step = step;
            _defaultValue = _getter();
        }

        public override void Reset()
        {
            Value = _defaultValue;
        }
    }

    public class MaterialColorItemViewModel : MaterialItemViewModel
    {
        private readonly Func<Color> _getter;
        private readonly Action<Color> _setter;
        private readonly Color _defaultValue;

        public Color Value
        {
            get => _getter();
            set
            {
                if (_getter() != value)
                {
                    _setter(value);
                    OnPropertyChanged(nameof(Value));
                    OnUpdate?.Invoke();
                }
            }
        }

        public MaterialColorItemViewModel(string labelKey, Func<Color> getter, Action<Color> setter, Action onUpdate)
            : base(labelKey, onUpdate)
        {
            _getter = getter;
            _setter = setter;
            _defaultValue = _getter();
        }

        public override void Reset()
        {
            Value = _defaultValue;
        }
    }

    [MaterialGroup("Standard", nameof(Texts.Material_Group_Standard), 0)]
    public class PartMaterialProperties
    {
        private readonly Action<Action<Core.PartMaterialData>> _updateAction;

        public PartMaterialProperties(Action<Action<Core.PartMaterialData>> updateAction, Core.PartMaterialData currentData, Core.PartMaterialData defaultData)
        {
            _updateAction = updateAction;

            _roughness = currentData.Roughness;
            _metallic = currentData.Metallic;
            _baseColor = currentData.BaseColor;
        }

        private double _roughness;
        [MaterialRange("Standard", nameof(Texts.Material_Roughness), 0.0, 1.0, 0.01, 0)]
        public double Roughness
        {
            get => _roughness;
            set
            {
                _roughness = value;
                _updateAction(m => m.Roughness = value);
            }
        }

        private double _metallic;
        [MaterialRange("Standard", nameof(Texts.Material_Metallic), 0.0, 1.0, 0.01, 1)]
        public double Metallic
        {
            get => _metallic;
            set
            {
                _metallic = value;
                _updateAction(m => m.Metallic = value);
            }
        }

        private Color _baseColor;
        [MaterialColor("Standard", nameof(Texts.Material_BaseColor), 2)]
        public Color BaseColor
        {
            get => _baseColor;
            set
            {
                _baseColor = value;
                _updateAction(m => m.BaseColor = value);
            }
        }
    }
}
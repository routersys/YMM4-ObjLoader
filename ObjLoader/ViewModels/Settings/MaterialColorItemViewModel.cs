using System.Windows.Media;

namespace ObjLoader.ViewModels.Settings
{
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
}

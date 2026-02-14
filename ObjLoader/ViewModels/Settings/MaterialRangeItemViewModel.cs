namespace ObjLoader.ViewModels.Settings
{
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
}

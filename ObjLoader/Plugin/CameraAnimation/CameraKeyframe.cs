using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ObjLoader.Plugin.CameraAnimation
{
    public class CameraKeyframe : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private double _time;
        private EasingData _easing = EasingManager.Presets.FirstOrDefault()?.Clone() ?? new EasingData();
        private double _camX;
        private double _camY;
        private double _camZ;
        private double _targetX;
        private double _targetY;
        private double _targetZ;

        public double Time { get => _time; set => Set(ref _time, value); }
        public EasingData Easing { get => _easing; set => Set(ref _easing, value); }
        public double CamX { get => _camX; set => Set(ref _camX, value); }
        public double CamY { get => _camY; set => Set(ref _camY, value); }
        public double CamZ { get => _camZ; set => Set(ref _camZ, value); }
        public double TargetX { get => _targetX; set => Set(ref _targetX, value); }
        public double TargetY { get => _targetY; set => Set(ref _targetY, value); }
        public double TargetZ { get => _targetZ; set => Set(ref _targetZ, value); }
    }
}
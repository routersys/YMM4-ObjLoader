using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Plugin.CameraAnimation
{
    public class CameraKeyframe : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private double _time;
        private double _camX;
        private double _camY;
        private double _camZ;
        private double _targetX;
        private double _targetY;
        private double _targetZ;
        private ICameraEasing? _easing;

        public double Time { get => _time; set => Set(ref _time, value); }
        public double CamX { get => _camX; set => Set(ref _camX, value); }
        public double CamY { get => _camY; set => Set(ref _camY, value); }
        public double CamZ { get => _camZ; set => Set(ref _camZ, value); }
        public double TargetX { get => _targetX; set => Set(ref _targetX, value); }
        public double TargetY { get => _targetY; set => Set(ref _targetY, value); }
        public double TargetZ { get => _targetZ; set => Set(ref _targetZ, value); }

        [XmlIgnore]
        public ICameraEasing Easing
        {
            get => _easing ??= new BezierCameraEasing();
            internal set => _easing = value;
        }

        [XmlIgnore]
        public BezierAnimation? BezierEasing => (Easing as BezierCameraEasing)?.Animation;

        [XmlArray("EasingPoints")]
        [XmlArrayItem("Point")]
        public List<BezierPointData> EasingPointsData
        {
            get => Easing.GetPoints();
            set => Easing.SetPoints(value);
        }

        public bool EasingIsQuadratic
        {
            get => Easing.IsQuadratic;
            set => Easing.IsQuadratic = value;
        }
    }
}
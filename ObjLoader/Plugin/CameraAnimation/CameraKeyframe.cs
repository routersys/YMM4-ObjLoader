using System.Collections.Immutable;
using System.ComponentModel;
using System.Numerics;
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

        public double Time { get => _time; set => Set(ref _time, value); }
        public double CamX { get => _camX; set => Set(ref _camX, value); }
        public double CamY { get => _camY; set => Set(ref _camY, value); }
        public double CamZ { get => _camZ; set => Set(ref _camZ, value); }
        public double TargetX { get => _targetX; set => Set(ref _targetX, value); }
        public double TargetY { get => _targetY; set => Set(ref _targetY, value); }
        public double TargetZ { get => _targetZ; set => Set(ref _targetZ, value); }

        [XmlIgnore]
        public BezierAnimation Easing { get; } = new BezierAnimation();

        [XmlArray("EasingPoints")]
        [XmlArrayItem("Point")]
        public List<BezierPointData> EasingPointsData
        {
            get => Easing.Points.Select(p => new BezierPointData(p)).ToList();
            set
            {
                if (value == null || value.Count == 0) return;
                var pts = value.Select(p => p.ToAnimationPoint()).ToArray();
                Easing.Points = ImmutableList.Create(new ReadOnlySpan<BezierAnimationPoint>(pts));
            }
        }

        public bool EasingIsQuadratic
        {
            get => Easing.IsQuadratic;
            set => Easing.IsQuadratic = value;
        }
    }

    public class BezierPointData
    {
        public float PX { get; set; }
        public float PY { get; set; }
        public float C1X { get; set; }
        public float C1Y { get; set; }
        public float C2X { get; set; }
        public float C2Y { get; set; }

        public BezierPointData() { }

        public BezierPointData(BezierAnimationPoint p)
        {
            PX = p.Point.X; PY = p.Point.Y;
            C1X = p.ControlPoint1.X; C1Y = p.ControlPoint1.Y;
            C2X = p.ControlPoint2.X; C2Y = p.ControlPoint2.Y;
        }

        public BezierAnimationPoint ToAnimationPoint() =>
            new BezierAnimationPoint(new Vector2(PX, PY), new Vector2(C1X, C1Y), new Vector2(C2X, C2Y));
    }
}
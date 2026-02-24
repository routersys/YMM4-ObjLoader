using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ObjLoader.Services.Camera
{
    internal class CameraLogic
    {
        private double _camX;
        public double CamX { get => _camX; set { _camX = value; Updated?.Invoke(); } }
        private double _camY;
        public double CamY { get => _camY; set { _camY = value; Updated?.Invoke(); } }
        private double _camZ;
        public double CamZ { get => _camZ; set { _camZ = value; Updated?.Invoke(); } }
        private double _targetX;
        public double TargetX { get => _targetX; set { _targetX = value; Updated?.Invoke(); } }
        private double _targetY;
        public double TargetY { get => _targetY; set { _targetY = value; Updated?.Invoke(); } }
        private double _targetZ;
        public double TargetZ { get => _targetZ; set { _targetZ = value; Updated?.Invoke(); } }

        private double _viewCenterX;
        public double ViewCenterX { get => _viewCenterX; set { _viewCenterX = value; Updated?.Invoke(); } }
        private double _viewCenterY;
        public double ViewCenterY { get => _viewCenterY; set { _viewCenterY = value; Updated?.Invoke(); } }
        private double _viewCenterZ;
        public double ViewCenterZ { get => _viewCenterZ; set { _viewCenterZ = value; Updated?.Invoke(); } }

        private double _viewRadius = 15;
        public double ViewRadius { get => _viewRadius; set { _viewRadius = value; Updated?.Invoke(); } }
        private double _viewTheta = 45 * Math.PI / 180;
        public double ViewTheta { get => _viewTheta; set { _viewTheta = value; Updated?.Invoke(); } }
        private double _viewPhi = 45 * Math.PI / 180;
        public double ViewPhi { get => _viewPhi; set { _viewPhi = value; Updated?.Invoke(); } }
        private double _gizmoRadius = 6.0;
        public double GizmoRadius { get => _gizmoRadius; set { _gizmoRadius = value; Updated?.Invoke(); } }

        private bool _isPilotView = false;
        public bool IsPilotView { get => _isPilotView; set { _isPilotView = value; Updated?.Invoke(); } }

        private DispatcherTimer? _animationTimer;
        private double _animTargetTheta, _animTargetPhi;
        private double _animStartTheta, _animStartPhi;
        private double _animProgress;

        public event Action? Updated;

        public void AnimateView(double targetTheta, double targetPhi)
        {
            if (_animationTimer != null) _animationTimer.Stop();
            _animStartTheta = ViewTheta; _animStartPhi = ViewPhi;
            while (targetTheta - _animStartTheta > Math.PI) _animStartTheta += 2 * Math.PI;
            while (targetTheta - _animStartTheta < -Math.PI) _animStartTheta -= 2 * Math.PI;
            _animTargetTheta = targetTheta; _animTargetPhi = targetPhi; _animProgress = 0;
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += (s, e) =>
            {
                _animProgress += 0.08;
                if (_animProgress >= 1.0) { _animProgress = 1.0; _animationTimer.Stop(); _animationTimer = null; }
                double t = 1 - Math.Pow(1 - _animProgress, 3);
                ViewTheta = _animStartTheta + (_animTargetTheta - _animStartTheta) * t;
                ViewPhi = _animStartPhi + (_animTargetPhi - _animStartPhi) * t;
                Updated?.Invoke();
            };
            _animationTimer.Start();
        }

        public void UpdateViewport(PerspectiveCamera camera, PerspectiveCamera gizmoCamera, double modelHeight)
        {
            double yOffset = modelHeight / 2.0;

            if (IsPilotView)
            {
                var camPos = new Point3D(CamX, CamY + yOffset, CamZ);
                var target = new Point3D(TargetX, TargetY + yOffset, TargetZ);
                camera.Position = camPos;
                camera.LookDirection = target - camPos;
            }
            else
            {
                double y = ViewRadius * Math.Cos(ViewPhi);
                double hRadius = ViewRadius * Math.Sin(ViewPhi);
                double x = hRadius * Math.Sin(ViewTheta);
                double z = hRadius * Math.Cos(ViewTheta);

                var target = new Point3D(ViewCenterX, ViewCenterY, ViewCenterZ);
                var pos = new Point3D(x, y, z) + (Vector3D)target + new Vector3D(0, yOffset, 0);

                camera.Position = pos;
                camera.LookDirection = (target + new Vector3D(0, yOffset, 0)) - pos;
            }

            double gy = GizmoRadius * Math.Cos(ViewPhi);
            double ghRadius = GizmoRadius * Math.Sin(ViewPhi);
            double gx = ghRadius * Math.Sin(ViewTheta);
            double gz = ghRadius * Math.Cos(ViewTheta);
            gizmoCamera.Position = new Point3D(gx, gy, gz);
            gizmoCamera.LookDirection = new Point3D(0, 0, 0) - gizmoCamera.Position;
        }

        public void StopAnimation()
        {
            _animationTimer?.Stop();
            _animationTimer = null;
        }
    }
}
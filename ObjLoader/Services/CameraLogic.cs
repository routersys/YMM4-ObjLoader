using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ObjLoader.Services
{
    internal class CameraLogic
    {
        public double CamX { get; set; }
        public double CamY { get; set; }
        public double CamZ { get; set; }
        public double TargetX { get; set; }
        public double TargetY { get; set; }
        public double TargetZ { get; set; }

        public double ViewCenterX { get; set; }
        public double ViewCenterY { get; set; }
        public double ViewCenterZ { get; set; }

        public double ViewRadius { get; set; } = 15;
        public double ViewTheta { get; set; } = 45 * Math.PI / 180;
        public double ViewPhi { get; set; } = 45 * Math.PI / 180;
        public double GizmoRadius { get; set; } = 6.0;

        public bool IsPilotView { get; set; } = false;

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
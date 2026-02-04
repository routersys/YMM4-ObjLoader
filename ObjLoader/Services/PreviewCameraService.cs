using System.Windows;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.Services
{
    public class PreviewCameraService : Bindable
    {
        private Point _lastMousePos;
        private bool _isRotating;
        private bool _isPanning;
        private bool _isInteracting;

        private double _viewRadius = 5.0;
        private double _viewTheta = Math.PI / 4;
        private double _viewPhi = Math.PI / 4;
        private Vector3 _viewTarget = Vector3.Zero;

        private double _viewportWidth = 100;
        private double _viewportHeight = 100;

        public bool IsInteracting
        {
            get => _isInteracting;
            private set => Set(ref _isInteracting, value);
        }

        public void Resize(double width, double height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
        }

        public void Zoom(int delta)
        {
            double scale = delta > 0 ? 0.9 : 1.1;
            _viewRadius *= scale;
            if (_viewRadius < 0.01) _viewRadius = 0.01;
        }

        public void StartInteraction(Point pos, MouseButton button)
        {
            _lastMousePos = pos;
            if (button == MouseButton.Right) _isRotating = true;
            if (button == MouseButton.Middle) _isPanning = true;
            if (_isRotating || _isPanning) IsInteracting = true;
        }

        public bool MoveInteraction(Point pos, bool left, bool middle, bool right)
        {
            if (!_isRotating && !_isPanning) return false;

            var dx = pos.X - _lastMousePos.X;
            var dy = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;
            bool updated = false;

            if (_isRotating && right)
            {
                _viewTheta -= dx * 0.01;
                _viewPhi -= dy * 0.01;

                if (_viewPhi < 0.01) _viewPhi = 0.01;
                if (_viewPhi > Math.PI - 0.01) _viewPhi = Math.PI - 0.01;
                updated = true;
            }
            else if (_isPanning && middle)
            {
                var camDir = GetCameraPosition() - _viewTarget;
                var forward = Vector3.Normalize(new Vector3(camDir.X, 0, camDir.Z));
                var rightDir = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
                var upDir = Vector3.UnitY;

                float sensitivity = (float)(_viewRadius * 0.002);
                _viewTarget -= rightDir * (float)dx * sensitivity;
                _viewTarget += upDir * (float)dy * sensitivity;
                updated = true;
            }
            return updated;
        }

        public void EndInteraction()
        {
            _isRotating = false;
            _isPanning = false;
            IsInteracting = false;
        }

        public void UpdateFocus(Vector3 center, double radius)
        {
            _viewTarget = center;
            _viewRadius = radius;
        }

        public Vector3 GetCameraPosition()
        {
            float x = (float)(_viewRadius * Math.Sin(_viewPhi) * Math.Cos(_viewTheta));
            float z = (float)(_viewRadius * Math.Sin(_viewPhi) * Math.Sin(_viewTheta));
            float y = (float)(_viewRadius * Math.Cos(_viewPhi));
            return _viewTarget + new Vector3(x, y, z);
        }

        public Matrix4x4 GetViewMatrix()
        {
            var camPos = GetCameraPosition();
            return Matrix4x4.CreateLookAt(camPos, _viewTarget, Vector3.UnitY);
        }

        public Matrix4x4 GetProjectionMatrix()
        {
            var aspect = (float)(_viewportWidth / _viewportHeight);
            if (double.IsNaN(aspect) || double.IsInfinity(aspect)) aspect = 1.0f;
            return Matrix4x4.CreatePerspectiveFieldOfView((float)(45 * Math.PI / 180.0), aspect, 0.1f, 10000.0f);
        }
    }
}
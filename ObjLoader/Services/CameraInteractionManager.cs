using System.Windows;
using System.Windows.Media.Media3D;

namespace ObjLoader.Services
{
    public enum GizmoMode { None, X, Y, Z, XY, YZ, ZX, View }

    public class CameraInteractionManager
    {
        private readonly ICameraManipulator _manipulator;

        private Point _lastMousePos;
        private bool _isRotatingView;
        private bool _isPanningView;
        private bool _isDraggingTarget;
        private bool _isSpacePanning;
        private GizmoMode _currentGizmoMode = GizmoMode.None;
        private Geometry3D? _hoveredGeometry;
        private string _hoveredDirectionName = "";

        public string HoveredDirectionName => _hoveredDirectionName;

        public CameraInteractionManager(ICameraManipulator manipulator)
        {
            _manipulator = manipulator;
        }

        public void HandleGizmoMove(object? modelHit, MeshGeometry3D gizmoX, MeshGeometry3D gizmoY, MeshGeometry3D gizmoZ, MeshGeometry3D gizmoXY, MeshGeometry3D gizmoYZ, MeshGeometry3D gizmoZX, MeshGeometry3D camVisual, MeshGeometry3D targetVisual)
        {
            if (modelHit is GeometryModel3D gm && gm.GetValue(FrameworkElement.TagProperty) is string name)
                _hoveredDirectionName = name;
            else
                _hoveredDirectionName = "";

            CheckGizmoHit(modelHit, gizmoX, gizmoY, gizmoZ, gizmoXY, gizmoYZ, gizmoZX, camVisual, targetVisual);
        }

        private void CheckGizmoHit(object? modelHit, MeshGeometry3D gizmoX, MeshGeometry3D gizmoY, MeshGeometry3D gizmoZ, MeshGeometry3D gizmoXY, MeshGeometry3D gizmoYZ, MeshGeometry3D gizmoZX, MeshGeometry3D camVisual, MeshGeometry3D targetVisual)
        {
            _currentGizmoMode = GizmoMode.None;
            _hoveredGeometry = null;
            if (modelHit is GeometryModel3D gm)
            {
                _hoveredGeometry = gm.Geometry;
                if (gm.Geometry == gizmoX) _currentGizmoMode = GizmoMode.X;
                else if (gm.Geometry == gizmoY) _currentGizmoMode = GizmoMode.Y;
                else if (gm.Geometry == gizmoZ) _currentGizmoMode = GizmoMode.Z;
                else if (gm.Geometry == gizmoXY) _currentGizmoMode = GizmoMode.XY;
                else if (gm.Geometry == gizmoYZ) _currentGizmoMode = GizmoMode.YZ;
                else if (gm.Geometry == gizmoZX) _currentGizmoMode = GizmoMode.ZX;
                else if (gm.Geometry == targetVisual || gm.Geometry == camVisual) _currentGizmoMode = GizmoMode.View;
            }
        }

        public void HandleViewCubeClick(object? modelHit, GeometryModel3D[] cubeFaces, GeometryModel3D[] cubeCorners)
        {
            if (modelHit == null) return;
            int faceIdx = Array.IndexOf(cubeFaces, modelHit);
            if (faceIdx >= 0)
            {
                if (faceIdx == 0) _manipulator.AnimateView(Math.PI / 2, Math.PI / 2);
                else if (faceIdx == 1) _manipulator.AnimateView(-Math.PI / 2, Math.PI / 2);
                else if (faceIdx == 2) _manipulator.AnimateView(0, 0.01);
                else if (faceIdx == 3) _manipulator.AnimateView(0, Math.PI - 0.01);
                else if (faceIdx == 4) _manipulator.AnimateView(0, Math.PI / 2);
                else if (faceIdx == 5) _manipulator.AnimateView(Math.PI, Math.PI / 2);
                return;
            }
            int cornerIdx = Array.IndexOf(cubeCorners, modelHit);
            if (cornerIdx >= 0)
            {
                if (cornerIdx == 0) _manipulator.AnimateView(Math.PI / 4, 0.955);
                else if (cornerIdx == 1) _manipulator.AnimateView(-Math.PI / 4, 0.955);
                else if (cornerIdx == 2) _manipulator.AnimateView(3 * Math.PI / 4, 0.955);
                else if (cornerIdx == 3) _manipulator.AnimateView(-3 * Math.PI / 4, 0.955);
                else if (cornerIdx == 4) _manipulator.AnimateView(Math.PI / 4, 2.186);
                else if (cornerIdx == 5) _manipulator.AnimateView(-Math.PI / 4, 2.186);
                else if (cornerIdx == 6) _manipulator.AnimateView(3 * Math.PI / 4, 2.186);
                else if (cornerIdx == 7) _manipulator.AnimateView(-3 * Math.PI / 4, 2.186);
            }
        }

        public void StartPan(Point pos)
        {
            _manipulator.IsTargetFixed = false;
            _isPanningView = true;
            _lastMousePos = pos;
        }

        public void StartRotate(Point pos)
        {
            _isRotatingView = true;
            _lastMousePos = pos;
        }

        public void StartGizmoDrag(Point pos, MeshGeometry3D camVisual, MeshGeometry3D targetVisual)
        {
            _manipulator.RecordUndo();
            if (_currentGizmoMode == GizmoMode.View)
            {
                if (_hoveredGeometry == camVisual) _manipulator.IsTargetFixed = true;
                else if (_hoveredGeometry == targetVisual) _manipulator.IsTargetFixed = false;
            }
            if (_currentGizmoMode != GizmoMode.None)
            {
                _isDraggingTarget = true;
                _lastMousePos = pos;
            }
            else
            {
                _isSpacePanning = true;
                _lastMousePos = pos;
            }
        }

        public void EndDrag()
        {
            _isRotatingView = false;
            _isPanningView = false;
            _isDraggingTarget = false;
            _isSpacePanning = false;
            _currentGizmoMode = GizmoMode.None;
            _manipulator.SyncToParameter();
            _manipulator.UpdateVisuals();
        }

        public void ScrubValue(string axis, double delta, double modelScale)
        {
            _manipulator.RecordUndo();
            double val = delta * modelScale * 0.01;
            if (axis == "X") _manipulator.CamX += val;
            else if (axis == "Y") _manipulator.CamY += val;
            else if (axis == "Z") _manipulator.CamZ += val;
            _manipulator.UpdateVisuals();
        }

        public void Move(Point pos)
        {
            var dx = pos.X - _lastMousePos.X;
            var dy = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            if (_isDraggingTarget && _currentGizmoMode != GizmoMode.None)
            {
                double yOffset = _manipulator.ModelHeight / 2.0;
                Point3D objPos;
                if (_manipulator.IsTargetFixed) objPos = new Point3D(_manipulator.CamX, _manipulator.CamY + yOffset, _manipulator.CamZ);
                else objPos = new Point3D(_manipulator.TargetX, _manipulator.TargetY + yOffset, _manipulator.TargetZ);

                double dist = (_manipulator.Camera.Position - objPos).Length;
                if (dist < 0.001) dist = 0.001;
                double fovRad = _manipulator.Camera.FieldOfView * Math.PI / 180.0;
                double speed = (2.0 * dist * Math.Tan(fovRad / 2.0)) / _manipulator.ViewportHeight;

                double mx = 0, my = 0, mz = 0;
                var camDir = _manipulator.Camera.LookDirection; camDir.Normalize();
                var camRight = Vector3D.CrossProduct(camDir, _manipulator.Camera.UpDirection); camRight.Normalize();
                var camUp = Vector3D.CrossProduct(camRight, camDir); camUp.Normalize();
                var moveVec = camRight * dx * speed + (-camUp) * dy * speed;

                switch (_currentGizmoMode)
                {
                    case GizmoMode.X: mx = moveVec.X; break;
                    case GizmoMode.Y: my = moveVec.Y; break;
                    case GizmoMode.Z: mz = moveVec.Z; break;
                    case GizmoMode.XY: mx = moveVec.X; my = moveVec.Y; break;
                    case GizmoMode.YZ: my = moveVec.Y; mz = moveVec.Z; break;
                    case GizmoMode.ZX: mx = moveVec.X; mz = moveVec.Z; break;
                    case GizmoMode.View: mx = moveVec.X; my = moveVec.Y; mz = moveVec.Z; break;
                }

                if (_manipulator.IsSnapping) { mx = Math.Round(mx / 0.5) * 0.5; my = Math.Round(my / 0.5) * 0.5; mz = Math.Round(mz / 0.5) * 0.5; }
                if (_manipulator.IsTargetFixed) { _manipulator.CamX += mx; _manipulator.CamY += my; _manipulator.CamZ += mz; }
                else { _manipulator.TargetX += mx; _manipulator.TargetY += my; _manipulator.TargetZ += mz; }
                _manipulator.UpdateVisuals();
            }
            else if (_isSpacePanning)
            {
                double dist = _manipulator.ViewRadius;
                double fovRad = _manipulator.Camera.FieldOfView * Math.PI / 180.0;
                double panSpeed = (2.0 * dist * Math.Tan(fovRad / 2.0)) / _manipulator.ViewportHeight;
                var look = _manipulator.Camera.LookDirection; look.Normalize();
                var right = Vector3D.CrossProduct(look, _manipulator.Camera.UpDirection); right.Normalize();
                var up = Vector3D.CrossProduct(right, look); up.Normalize();
                var move = (-right * dx * panSpeed) + (up * dy * panSpeed);
                _manipulator.ViewCenterX += move.X; _manipulator.ViewCenterY += move.Y; _manipulator.ViewCenterZ += move.Z;
                _manipulator.UpdateVisuals();
            }
            else if (_isRotatingView || _isPanningView)
            {
                if (_isPanningView)
                {
                    if (!_manipulator.IsTargetFixed)
                    {
                        double dist = _manipulator.ViewRadius;
                        double fovRad = _manipulator.Camera.FieldOfView * Math.PI / 180.0;
                        double panSpeed = (2.0 * dist * Math.Tan(fovRad / 2.0)) / _manipulator.ViewportHeight;
                        var look = _manipulator.Camera.LookDirection; look.Normalize();
                        var right = Vector3D.CrossProduct(look, _manipulator.Camera.UpDirection); right.Normalize();
                        var up = Vector3D.CrossProduct(right, look); up.Normalize();
                        var move = (-right * dx * panSpeed) + (up * dy * panSpeed);
                        _manipulator.TargetX += move.X; _manipulator.TargetY += move.Y; _manipulator.TargetZ += move.Z;
                        _manipulator.UpdateVisuals();
                    }
                }
                else
                {
                    _manipulator.ViewTheta += dx * 0.01;
                    _manipulator.ViewPhi -= dy * 0.01;
                    if (_manipulator.ViewPhi < 0.01) _manipulator.ViewPhi = 0.01;
                    if (_manipulator.ViewPhi > Math.PI - 0.01) _manipulator.ViewPhi = Math.PI - 0.01;
                    if (_manipulator.IsSnapping) _manipulator.ViewTheta = Math.Round(_manipulator.ViewTheta / (Math.PI / 12)) * (Math.PI / 12);
                    _manipulator.UpdateVisuals();
                }
            }
        }

        public void Zoom(int delta, bool isPilotView, double modelScale)
        {
            if (isPilotView)
            {
                double speed = modelScale * 0.1 * (delta > 0 ? 1 : -1);
                var dir = _manipulator.Camera.LookDirection; dir.Normalize();
                _manipulator.CamX += dir.X * speed; _manipulator.CamY += dir.Y * speed; _manipulator.CamZ += dir.Z * speed;
                _manipulator.UpdateVisuals();
                _manipulator.SyncToParameter();
            }
            else
            {
                _manipulator.ViewRadius -= delta * (modelScale * 0.005);
                if (_manipulator.ViewRadius < modelScale * 0.01) _manipulator.ViewRadius = modelScale * 0.01;
                _manipulator.UpdateVisuals();
            }
        }

        public void MovePilot(double fwd, double right, double up, bool isPilotView, double modelScale)
        {
            if (!isPilotView) return;
            double speed = modelScale * 0.05;
            var look = _manipulator.Camera.LookDirection; look.Normalize();
            var r = Vector3D.CrossProduct(look, _manipulator.Camera.UpDirection); r.Normalize();
            var u = Vector3D.CrossProduct(r, look); u.Normalize();
            var move = look * fwd * speed + r * right * speed + u * up * speed;
            _manipulator.CamX += move.X; _manipulator.CamY += move.Y; _manipulator.CamZ += move.Z;
            _manipulator.TargetX += move.X; _manipulator.TargetY += move.Y; _manipulator.TargetZ += move.Z;
            _manipulator.UpdateVisuals();
        }
    }
}
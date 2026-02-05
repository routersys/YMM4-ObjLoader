using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin.Parameters;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Windows.Media.Media3D;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public enum SelectionMode { Vertex, Edge, Face }

    internal class CenterPointWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader = new ObjModelLoader();
        private LayerData? _targetLayer;
        private MeshGeometry3D? _sourceMesh;
        private Vector3 _modelCenter = Vector3.Zero;
        private double _cameraTheta = Math.PI / 2;
        private double _cameraPhi = Math.PI / 2;
        private double _cameraRadius = 10;

        public double CenterX
        {
            get => _targetLayer?.RotationCenterX ?? 0;
            set
            {
                if (_targetLayer != null)
                {
                    _targetLayer.RotationCenterX = value;
                    OnPropertyChanged();
                    UpdateCenterPointVisual();
                    NotifyLayerChanged();
                }
            }
        }

        public double CenterY
        {
            get => _targetLayer?.RotationCenterY ?? 0;
            set
            {
                if (_targetLayer != null)
                {
                    _targetLayer.RotationCenterY = value;
                    OnPropertyChanged();
                    UpdateCenterPointVisual();
                    NotifyLayerChanged();
                }
            }
        }

        public double CenterZ
        {
            get => _targetLayer?.RotationCenterZ ?? 0;
            set
            {
                if (_targetLayer != null)
                {
                    _targetLayer.RotationCenterZ = value;
                    OnPropertyChanged();
                    UpdateCenterPointVisual();
                    NotifyLayerChanged();
                }
            }
        }

        private MeshGeometry3D? _modelMesh;
        public MeshGeometry3D? ModelMesh { get => _modelMesh; set => Set(ref _modelMesh, value); }

        private Geometry3D? _highlightGeometry;
        public Geometry3D? HighlightGeometry { get => _highlightGeometry; set => Set(ref _highlightGeometry, value); }

        private Geometry3D? _centerPointGeometry;
        public Geometry3D? CenterPointGeometry { get => _centerPointGeometry; set => Set(ref _centerPointGeometry, value); }

        private Point3D _cameraPosition = new Point3D(0, 0, 10);
        public Point3D CameraPosition { get => _cameraPosition; set => Set(ref _cameraPosition, value); }

        private Vector3D _cameraLookDirection = new Vector3D(0, 0, -1);
        public Vector3D CameraLookDirection { get => _cameraLookDirection; set => Set(ref _cameraLookDirection, value); }

        private string _hoveredElementInfo = Texts.State_None;
        public string HoveredElementInfo { get => _hoveredElementInfo; set => Set(ref _hoveredElementInfo, value); }

        private bool _isLocked;
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (Set(ref _isLocked, value))
                {
                    OnPropertyChanged(nameof(LockStatusText));
                }
            }
        }

        public string LockStatusText => IsLocked ? Texts.State_Locked : Texts.State_Scanning;

        private SelectionMode _mode = SelectionMode.Vertex;

        public SelectionMode Mode
        {
            get => _mode;
            set => Set(ref _mode, value);
        }

        private Point3D? _detectedPoint;
        public ActionCommand ApplySelectionCommand { get; }
        public ActionCommand ResetSelectionCommand { get; }
        public ActionCommand ToggleLockCommand { get; }
        public ActionCommand SetModeCommand { get; }

        public CenterPointWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;
            UpdateTargetLayer();

            PropertyChangedEventManager.AddHandler(_parameter, OnParameterPropertyChanged, string.Empty);

            ApplySelectionCommand = new ActionCommand(
                _ => _detectedPoint.HasValue,
                _ => ApplySelection()
            );

            ResetSelectionCommand = new ActionCommand(
                _ => true,
                _ => ResetSelection()
            );

            ToggleLockCommand = new ActionCommand(
                _ => true,
                _ => IsLocked = !IsLocked
            );

            SetModeCommand = new ActionCommand(
                _ => true,
                o =>
                {
                    if (o is SelectionMode mode)
                    {
                        Mode = mode;
                    }
                }
            );

            LoadModel();
            UpdateCenterPointVisual();
        }

        private void LoadModel()
        {
            if (_targetLayer != null && !string.IsNullOrEmpty(_targetLayer.FilePath) && File.Exists(_targetLayer.FilePath))
            {
                try
                {
                    var objModel = _loader.Load(_targetLayer.FilePath);
                    _modelCenter = objModel.ModelCenter;
                    var mesh = new MeshGeometry3D();

                    foreach (var v in objModel.Vertices)
                    {
                        mesh.Positions.Add(new Point3D(v.Position.X, v.Position.Y, v.Position.Z));
                        mesh.Normals.Add(new Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
                    }

                    int partIndex = 0;
                    foreach (var part in objModel.Parts)
                    {
                        if (_targetLayer.VisibleParts == null || _targetLayer.VisibleParts.Contains(partIndex))
                        {
                            for (int i = 0; i < part.IndexCount; i++)
                            {
                                mesh.TriangleIndices.Add(objModel.Indices[part.IndexOffset + i]);
                            }
                        }
                        partIndex++;
                    }

                    _sourceMesh = mesh;
                    ModelMesh = mesh;

                    var bounds = mesh.Bounds;
                    var center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
                    var radius = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));

                    _cameraRadius = radius * 2;
                    _cameraTheta = Math.PI * 1.5;
                    _cameraPhi = Math.PI / 2;
                    UpdateCamera();
                }
                catch
                {
                    ModelMesh = null;
                    _modelCenter = Vector3.Zero;
                }
            }
        }

        private void UpdateCamera()
        {
            var x = _cameraRadius * Math.Sin(_cameraPhi) * Math.Cos(_cameraTheta);
            var z = _cameraRadius * Math.Sin(_cameraPhi) * Math.Sin(_cameraTheta);
            var y = _cameraRadius * Math.Cos(_cameraPhi);

            var cx = _modelCenter.X + x;
            var cy = _modelCenter.Y + y;
            var cz = _modelCenter.Z + z;

            CameraPosition = new Point3D(cx, cy, cz);
            CameraLookDirection = new Vector3D(_modelCenter.X - cx, _modelCenter.Y - cy, _modelCenter.Z - cz);
        }

        public void RotateCamera(double dx, double dy)
        {
            _cameraTheta -= dx * 0.01;
            _cameraPhi -= dy * 0.01;

            if (_cameraPhi < 0.01) _cameraPhi = 0.01;
            if (_cameraPhi > Math.PI - 0.01) _cameraPhi = Math.PI - 0.01;

            UpdateCamera();
        }

        public void ZoomCamera(double delta)
        {
            _cameraRadius -= delta * _cameraRadius * 0.001;
            if (_cameraRadius < 0.1) _cameraRadius = 0.1;
            UpdateCamera();
        }

        public void PanCamera(double dx, double dy)
        {
            var look = CameraLookDirection;
            look.Normalize();
            var right = Vector3D.CrossProduct(look, new Vector3D(0, 1, 0));
            var up = Vector3D.CrossProduct(right, look);

            right.Normalize();
            up.Normalize();

            var move = right * (-dx * _cameraRadius * 0.002) + up * (dy * _cameraRadius * 0.002);
            _modelCenter += new Vector3((float)move.X, (float)move.Y, (float)move.Z);
            UpdateCamera();
        }

        private void UpdateCenterPointVisual()
        {
            var mesh = new MeshGeometry3D();
            AddSphere(mesh, new Point3D(CenterX + _modelCenter.X, CenterY + _modelCenter.Y, CenterZ + _modelCenter.Z), 0.05);
            CenterPointGeometry = mesh;
        }

        public void UpdateHoverState(Point3D hitPoint, int i1, int i2, int i3)
        {
            if (_sourceMesh == null || IsLocked) return;

            Point3D p1 = _sourceMesh.Positions[i1];
            Point3D p2 = _sourceMesh.Positions[i2];
            Point3D p3 = _sourceMesh.Positions[i3];

            Point3D finalPoint = hitPoint;
            string type = "";
            var highlightMesh = new MeshGeometry3D();

            if (Mode == SelectionMode.Vertex)
            {
                type = Texts.Mode_Vertex;
                double d1 = (hitPoint - p1).LengthSquared;
                double d2 = (hitPoint - p2).LengthSquared;
                double d3 = (hitPoint - p3).LengthSquared;

                if (d1 <= d2 && d1 <= d3) finalPoint = p1;
                else if (d2 <= d1 && d2 <= d3) finalPoint = p2;
                else finalPoint = p3;

                AddSphere(highlightMesh, finalPoint, 0.03);
            }
            else if (Mode == SelectionMode.Edge)
            {
                type = Texts.Mode_Edge;
                Point3D e1 = GetClosestPointOnSegment(p1, p2, hitPoint);
                Point3D e2 = GetClosestPointOnSegment(p2, p3, hitPoint);
                Point3D e3 = GetClosestPointOnSegment(p3, p1, hitPoint);

                double d1 = (hitPoint - e1).LengthSquared;
                double d2 = (hitPoint - e2).LengthSquared;
                double d3 = (hitPoint - e3).LengthSquared;

                if (d1 <= d2 && d1 <= d3)
                {
                    finalPoint = e1;
                    AddLine(highlightMesh, p1, p2, 0.015);
                }
                else if (d2 <= d1 && d2 <= d3)
                {
                    finalPoint = e2;
                    AddLine(highlightMesh, p2, p3, 0.015);
                }
                else
                {
                    finalPoint = e3;
                    AddLine(highlightMesh, p3, p1, 0.015);
                }
                AddSphere(highlightMesh, finalPoint, 0.02);
            }
            else
            {
                type = Texts.Mode_Face;
                finalPoint = hitPoint;
                AddTriangle(highlightMesh, p1, p2, p3);
                AddSphere(highlightMesh, finalPoint, 0.02);
            }

            _detectedPoint = finalPoint;
            HighlightGeometry = highlightMesh;
            HoveredElementInfo = $"{type}: ({finalPoint.X:F3}, {finalPoint.Y:F3}, {finalPoint.Z:F3})";
            ApplySelectionCommand.RaiseCanExecuteChanged();
        }

        private Point3D GetClosestPointOnSegment(Point3D p1, Point3D p2, Point3D p)
        {
            Vector3D v = p2 - p1;
            Vector3D w = p - p1;
            double t = Vector3D.DotProduct(w, v) / Vector3D.DotProduct(v, v);
            t = Math.Max(0, Math.Min(1, t));
            return p1 + v * t;
        }

        private void AddSphere(MeshGeometry3D mesh, Point3D center, double radius)
        {
            int div = 8;
            for (int i = 0; i < div; i++)
            {
                for (int j = 0; j < div; j++)
                {
                    double phi1 = Math.PI * i / div;
                    double phi2 = Math.PI * (i + 1) / div;
                    double theta1 = 2 * Math.PI * j / div;
                    double theta2 = 2 * Math.PI * (j + 1) / div;

                    Point3D p1 = GetSpherePoint(center, radius, phi1, theta1);
                    Point3D p2 = GetSpherePoint(center, radius, phi1, theta2);
                    Point3D p3 = GetSpherePoint(center, radius, phi2, theta1);
                    Point3D p4 = GetSpherePoint(center, radius, phi2, theta2);

                    AddTriangle(mesh, p1, p3, p2);
                    AddTriangle(mesh, p2, p3, p4);
                }
            }
        }

        private Point3D GetSpherePoint(Point3D center, double r, double phi, double theta)
        {
            return new Point3D(
                center.X + r * Math.Sin(phi) * Math.Cos(theta),
                center.Y + r * Math.Cos(phi),
                center.Z + r * Math.Sin(phi) * Math.Sin(theta));
        }

        private void AddLine(MeshGeometry3D mesh, Point3D p1, Point3D p2, double thickness)
        {
            AddSphere(mesh, p1, thickness);
            AddSphere(mesh, p2, thickness);
            AddSphere(mesh, (Point3D)(((Vector3D)p1 + (Vector3D)p2) * 0.5), thickness);
        }

        private void AddTriangle(MeshGeometry3D mesh, Point3D p1, Point3D p2, Point3D p3)
        {
            int baseIdx = mesh.Positions.Count;
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);
            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 1);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx);
            mesh.TriangleIndices.Add(baseIdx + 2);
            mesh.TriangleIndices.Add(baseIdx + 1);
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.SelectedLayerIndex) ||
                e.PropertyName == nameof(ObjLoaderParameter.Layers))
            {
                UpdateTargetLayer();
            }
        }

        private void UpdateTargetLayer()
        {
            if (_parameter.SelectedLayerIndex >= 0 && _parameter.SelectedLayerIndex < _parameter.Layers.Count)
            {
                var newLayer = _parameter.Layers[_parameter.SelectedLayerIndex];
                if (_targetLayer != newLayer)
                {
                    _targetLayer = newLayer;
                    if (_targetLayer != null)
                    {
                        PropertyChangedEventManager.AddHandler(_targetLayer, OnLayerPropertyChanged, string.Empty);
                        LoadModel();
                    }
                }
            }
            else
            {
                _targetLayer = null;
                ModelMesh = null;
            }
            RaisePropertiesChanged();
        }

        private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LayerData.RotationCenterX) ||
                e.PropertyName == nameof(LayerData.RotationCenterY) ||
                e.PropertyName == nameof(LayerData.RotationCenterZ))
            {
                RaisePropertiesChanged();
                UpdateCenterPointVisual();
            }
            else if (e.PropertyName == nameof(LayerData.FilePath) || e.PropertyName == nameof(LayerData.VisibleParts))
            {
                LoadModel();
            }
        }

        private void RaisePropertiesChanged()
        {
            OnPropertyChanged(nameof(CenterX));
            OnPropertyChanged(nameof(CenterY));
            OnPropertyChanged(nameof(CenterZ));
        }

        private void ApplySelection()
        {
            if (_detectedPoint.HasValue && _targetLayer != null)
            {
                _targetLayer.RotationCenterX = _detectedPoint.Value.X - _modelCenter.X;
                _targetLayer.RotationCenterY = _detectedPoint.Value.Y - _modelCenter.Y;
                _targetLayer.RotationCenterZ = _detectedPoint.Value.Z - _modelCenter.Z;

                OnPropertyChanged(nameof(CenterX));
                OnPropertyChanged(nameof(CenterY));
                OnPropertyChanged(nameof(CenterZ));

                UpdateCenterPointVisual();
                NotifyLayerChanged();
            }
        }

        private void ResetSelection()
        {
            if (_targetLayer != null)
            {
                _targetLayer.RotationCenterX = 0;
                _targetLayer.RotationCenterY = 0;
                _targetLayer.RotationCenterZ = 0;

                OnPropertyChanged(nameof(CenterX));
                OnPropertyChanged(nameof(CenterY));
                OnPropertyChanged(nameof(CenterZ));

                UpdateCenterPointVisual();
                NotifyLayerChanged();
            }
        }

        private void NotifyLayerChanged()
        {
            _parameter.ForceUpdate();
        }

        public void Dispose()
        {
            PropertyChangedEventManager.RemoveHandler(_parameter, OnParameterPropertyChanged, string.Empty);
            if (_targetLayer != null)
            {
                PropertyChangedEventManager.RemoveHandler(_targetLayer, OnLayerPropertyChanged, string.Empty);
            }
        }
    }
}
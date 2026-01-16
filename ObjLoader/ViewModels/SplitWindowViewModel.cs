using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Localization;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Services;
using ObjLoader.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace ObjLoader.ViewModels
{
    internal class SplitWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;
        private readonly RenderService _renderService;
        private Color _themeColor = Colors.White;

        private GpuResourceCacheItem? _modelResource;
        private ObjModel? _currentModel;
        private double _modelHeight;
        private double _modelScale = 1.0;

        private Point _lastMousePos;
        private bool _isRotating;
        private bool _isPanning;

        private double _viewRadius = 5.0;
        private double _viewTheta = Math.PI / 4;
        private double _viewPhi = Math.PI / 4;
        private Vector3 _viewTarget = Vector3.Zero;

        private int _viewportWidth = 100;
        private int _viewportHeight = 100;

        private PartItem? _selectedPart;

        public WriteableBitmap? SceneImage => _renderService.SceneImage;

        public ObservableCollection<PartItem> Parts { get; } = new ObservableCollection<PartItem>();

        public List<PartItem> SelectedPartItems { get; set; } = new List<PartItem>();

        public PartItem? SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (Set(ref _selectedPart, value))
                {
                    UpdateFocus();
                }
            }
        }

        public ActionCommand AddToLayerCommand { get; }

        public class PartItem : Bindable
        {
            public string Name { get; set; } = "";
            public int Index { get; set; }
            public Vector3 Center { get; set; }
            public double Radius { get; set; }
            public int FaceCount { get; set; }

            public string Detail => string.Format(Texts.SplitWindow_Faces, FaceCount);

            private BitmapSource? _thumbnail;
            public BitmapSource? Thumbnail
            {
                get => _thumbnail;
                set => Set(ref _thumbnail, value);
            }
        }

        public SplitWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;
            _loader = new ObjModelLoader();
            _renderService = new RenderService();

            AddToLayerCommand = new ActionCommand(_ => true, AddToLayer);

            _renderService.Initialize();
            LoadModel();

            _parameter.PropertyChanged += OnParameterPropertyChanged;
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.FilePath))
            {
                LoadModel();
            }
        }

        public void Resize(int width, int height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
            _renderService.Resize(width, height);
            OnPropertyChanged(nameof(SceneImage));
            UpdateVisuals();
        }

        public void UpdateThemeColor(Color color)
        {
            _themeColor = color;
            UpdateVisuals();
        }

        public void Zoom(int delta)
        {
            double scale = delta > 0 ? 0.9 : 1.1;
            _viewRadius *= scale;
            if (_viewRadius < 0.01) _viewRadius = 0.01;
            UpdateVisuals();
        }

        public void StartInteraction(Point pos, MouseButton button)
        {
            _lastMousePos = pos;
            if (button == MouseButton.Right) _isRotating = true;
            if (button == MouseButton.Middle) _isPanning = true;
        }

        public void MoveInteraction(Point pos, bool left, bool middle, bool right)
        {
            if (!_isRotating && !_isPanning) return;

            var dx = pos.X - _lastMousePos.X;
            var dy = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            if (_isRotating && right)
            {
                _viewTheta -= dx * 0.01;
                _viewPhi -= dy * 0.01;

                if (_viewPhi < 0.01) _viewPhi = 0.01;
                if (_viewPhi > Math.PI - 0.01) _viewPhi = Math.PI - 0.01;
                UpdateVisuals();
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
                UpdateVisuals();
            }
        }

        public void EndInteraction()
        {
            _isRotating = false;
            _isPanning = false;
        }

        private void AddToLayer(object? parameter)
        {
            if (_modelResource == null) return;

            var targets = new HashSet<PartItem>();

            if (parameter is PartItem clickedItem)
            {
                targets.Add(clickedItem);
                if (SelectedPartItems.Contains(clickedItem))
                {
                    foreach (var item in SelectedPartItems) targets.Add(item);
                }
            }
            else
            {
                if (SelectedPartItems.Count > 0)
                {
                    foreach (var item in SelectedPartItems) targets.Add(item);
                }
                else if (_selectedPart != null)
                {
                    targets.Add(_selectedPart);
                }
            }

            var targetList = targets.Where(t => t.Index != -1).ToList();
            if (targetList.Count == 0) return;

            LayerData? sourceLayer = null;
            var currentLayerIndex = _parameter.SelectedLayerIndex;
            if (currentLayerIndex >= 0 && currentLayerIndex < _parameter.Layers.Count)
            {
                var layer = _parameter.Layers[currentLayerIndex];
                if (IsLayerContainsAnyTarget(layer, targetList))
                {
                    sourceLayer = layer;
                }
            }

            if (sourceLayer == null)
            {
                foreach (var layer in _parameter.Layers)
                {
                    if (layer.IsVisible && IsLayerContainsAnyTarget(layer, targetList))
                    {
                        sourceLayer = layer;
                        break;
                    }
                }
            }

            if (sourceLayer == null)
            {
                foreach (var layer in _parameter.Layers)
                {
                    if (IsLayerContainsAnyTarget(layer, targetList))
                    {
                        sourceLayer = layer;
                        break;
                    }
                }
            }

            if (sourceLayer == null) return;

            if (sourceLayer.VisibleParts == null)
            {
                sourceLayer.VisibleParts = new HashSet<int>(Enumerable.Range(0, _modelResource.Parts.Length));
            }

            var indicesToMove = new HashSet<int>();
            foreach (var t in targetList)
            {
                if (sourceLayer.VisibleParts.Contains(t.Index))
                {
                    indicesToMove.Add(t.Index);
                }
            }

            if (indicesToMove.Count > 0)
            {
                var newVisibleParts = new HashSet<int>(sourceLayer.VisibleParts);
                foreach (var idx in indicesToMove)
                {
                    newVisibleParts.Remove(idx);
                }
                sourceLayer.VisibleParts = newVisibleParts;

                if (_currentModel != null)
                {
                    sourceLayer.Thumbnail = ThumbnailUtil.CreateThumbnail(_currentModel, 64, 64, 0, -1, sourceLayer.VisibleParts);
                }

                var newLayer = sourceLayer.Clone();
                if (targetList.Count == 1)
                {
                    newLayer.Name = targetList[0].Name;
                }
                else
                {
                    newLayer.Name = $"{targetList[0].Name} + {targetList.Count - 1}";
                }

                newLayer.VisibleParts = indicesToMove;
                newLayer.Guid = Guid.NewGuid().ToString();
                newLayer.ParentGuid = sourceLayer.Guid;

                if (_currentModel != null)
                {
                    newLayer.Thumbnail = ThumbnailUtil.CreateThumbnail(_currentModel, 64, 64, 0, -1, newLayer.VisibleParts);
                }

                int sourceIndex = _parameter.Layers.IndexOf(sourceLayer);
                int insertIndex = sourceIndex + 1;
                while (insertIndex < _parameter.Layers.Count)
                {
                    if (_parameter.Layers[insertIndex].ParentGuid == sourceLayer.Guid)
                    {
                        insertIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                _parameter.Layers.Insert(insertIndex, newLayer);

                _parameter.ForceUpdate();
            }
        }

        private bool IsLayerContainsAnyTarget(LayerData layer, List<PartItem> targets)
        {
            if (layer.VisibleParts == null) return true;

            foreach (var t in targets)
            {
                if (layer.VisibleParts.Contains(t.Index)) return true;
            }
            return false;
        }

        private Vector3 GetCameraPosition()
        {
            float x = (float)(_viewRadius * Math.Sin(_viewPhi) * Math.Cos(_viewTheta));
            float z = (float)(_viewRadius * Math.Sin(_viewPhi) * Math.Sin(_viewTheta));
            float y = (float)(_viewRadius * Math.Cos(_viewPhi));
            return _viewTarget + new Vector3(x, y, z);
        }

        private void UpdateFocus()
        {
            if (_selectedPart == null || _selectedPart.Index == -1)
            {
                _viewTarget = new Vector3(0, (float)(_modelHeight / 2.0), 0);
                _viewRadius = _modelScale * 2.5;
            }
            else
            {
                _viewTarget = _selectedPart.Center;
                _viewRadius = _selectedPart.Radius * 2.5;
            }
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_renderService.SceneImage == null || _modelResource == null) return;

            var camPos = GetCameraPosition();
            var view = Matrix4x4.CreateLookAt(camPos, _viewTarget, Vector3.UnitY);
            var aspect = (float)_viewportWidth / _viewportHeight;
            var proj = Matrix4x4.CreatePerspectiveFieldOfView((float)(45 * Math.PI / 180.0), aspect, 0.1f, 10000.0f);

            HashSet<int>? visibleParts = null;
            if (_selectedPart != null && _selectedPart.Index != -1)
            {
                visibleParts = new HashSet<int> { _selectedPart.Index };
            }

            var layers = new List<LayerRenderData>
            {
                new LayerRenderData
                {
                    Resource = _modelResource,
                    X = 0, Y = 0, Z = 0,
                    Scale = 100,
                    Rx = 0, Ry = 0, Rz = 0,
                    BaseColor = Colors.White,
                    LightEnabled = true,
                    WorldId = 0,
                    HeightOffset = _modelHeight / 2.0,
                    VisibleParts = visibleParts
                }
            };

            _renderService.Render(
                layers,
                view,
                proj,
                camPos,
                _themeColor,
                false,
                true,
                true,
                _modelScale,
                false);
        }

        private unsafe void LoadModel()
        {
            _modelResource?.Dispose();
            _modelResource = null;
            _currentModel = null;

            Parts.Clear();
            var path = _parameter.FilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            var model = _loader.Load(path);
            if (model.Vertices.Length == 0) return;

            _currentModel = model;

            var vDesc = new BufferDescription(model.Vertices.Length * Unsafe.SizeOf<ObjVertex>(), BindFlags.VertexBuffer, ResourceUsage.Immutable);
            ID3D11Buffer vb;
            fixed (ObjVertex* p = model.Vertices) vb = _renderService.Device!.CreateBuffer(vDesc, new SubresourceData(p));

            var iDesc = new BufferDescription(model.Indices.Length * sizeof(int), BindFlags.IndexBuffer, ResourceUsage.Immutable);
            ID3D11Buffer ib;
            fixed (int* p = model.Indices) ib = _renderService.Device.CreateBuffer(iDesc, new SubresourceData(p));

            var parts = model.Parts.ToArray();
            var partTextures = new ID3D11ShaderResourceView?[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (File.Exists(parts[i].TexturePath))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(parts[i].TexturePath);
                        using var ms = new MemoryStream(bytes);
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        var conv = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                        var pixels = new byte[conv.PixelWidth * conv.PixelHeight * 4];
                        conv.CopyPixels(pixels, conv.PixelWidth * 4, 0);
                        var tDesc = new Texture2DDescription { Width = conv.PixelWidth, Height = conv.PixelHeight, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Immutable, BindFlags = BindFlags.ShaderResource };
                        fixed (byte* p = pixels) { using var t = _renderService.Device.CreateTexture2D(tDesc, new[] { new SubresourceData(p, conv.PixelWidth * 4) }); partTextures[i] = _renderService.Device.CreateShaderResourceView(t); }
                    }
                    catch { }
                }
            }

            _modelResource = new GpuResourceCacheItem(_renderService.Device, vb, ib, model.Indices.Length, parts, partTextures, model.ModelCenter, model.ModelScale);

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            double localMinY = double.MaxValue, localMaxY = double.MinValue;

            foreach (var v in model.Vertices)
            {
                double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (y < localMinY) localMinY = y; if (y > localMaxY) localMaxY = y;
                double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }

            _modelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            _modelHeight = localMaxY - localMinY;
            if (_modelScale < 0.1) _modelScale = 1.0;

            Parts.Add(new PartItem { Name = Texts.SplitWindow_All, Index = -1, Center = new Vector3(0, (float)(_modelHeight / 2.0), 0), Radius = _modelScale, FaceCount = model.Indices.Length / 3 });

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var name = string.IsNullOrEmpty(part.Name) ? string.Format(Texts.SplitWindow_PartName, i) : part.Name;

                Vector3 center = Vector3.Zero;
                double radius = 0;

                if (part.IndexCount > 0)
                {
                    double pMinX = double.MaxValue, pMinY = double.MaxValue, pMinZ = double.MaxValue;
                    double pMaxX = double.MinValue, pMaxY = double.MinValue, pMaxZ = double.MinValue;

                    for (int j = 0; j < part.IndexCount; j++)
                    {
                        int idx = model.Indices[part.IndexOffset + j];
                        var v = model.Vertices[idx];
                        double px = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                        double py = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                        double pz = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;

                        if (px < pMinX) pMinX = px; if (px > pMaxX) pMaxX = px;
                        if (py < pMinY) pMinY = py; if (py > pMaxY) pMaxY = py;
                        if (pz < pMinZ) pMinZ = pz; if (pz > pMaxZ) pMaxZ = pz;
                    }

                    center = new Vector3((float)((pMinX + pMaxX) / 2.0), (float)((pMinY + pMaxY) / 2.0) + (float)(_modelHeight / 2.0), (float)((pMinZ + pMaxZ) / 2.0));
                    radius = Math.Max(pMaxX - pMinX, Math.Max(pMaxY - pMinY, pMaxZ - pMinZ));
                }

                Parts.Add(new PartItem { Name = name, Index = i, Center = center, Radius = radius, FaceCount = part.IndexCount / 3 });
            }

            _selectedPart = Parts[0];
            _viewTarget = new Vector3(0, (float)(_modelHeight / 2.0), 0);
            UpdateVisuals();

            Task.Run(() =>
            {
                foreach (var partItem in Parts)
                {
                    int offset = partItem.Index == -1 ? 0 : parts[partItem.Index].IndexOffset;
                    int count = partItem.Index == -1 ? -1 : parts[partItem.Index].IndexCount;

                    var bytes = ThumbnailUtil.CreateThumbnail(model, 64, 64, offset, count);
                    if (bytes != null && bytes.Length > 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            using var ms = new MemoryStream(bytes);
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = ms;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            partItem.Thumbnail = bitmap;
                        }));
                    }
                }
            });
        }

        public void Dispose()
        {
            _parameter.PropertyChanged -= OnParameterPropertyChanged;
            _modelResource?.Dispose();
            _renderService.Dispose();
        }
    }
}
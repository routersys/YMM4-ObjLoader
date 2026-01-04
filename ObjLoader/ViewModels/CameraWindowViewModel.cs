using ObjLoader.Cache;
using ObjLoader.Core;
using ObjLoader.Parsers;
using ObjLoader.Plugin;
using ObjLoader.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Vortice.Direct3D11;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using System.ComponentModel;
using EasingType = ObjLoader.Plugin.EasingType;

namespace ObjLoader.ViewModels
{
    public class CameraWindowViewModel : Bindable, IDisposable
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly ObjModelLoader _loader;
        private readonly RenderService _renderService;
        private readonly CameraLogic _cameraLogic;
        private readonly UndoStack<(double cx, double cy, double cz, double tx, double ty, double tz)> _undoStack;

        private double _camXMin = -10, _camXMax = 10;
        private double _camYMin = -10, _camYMax = 10;
        private double _camZMin = -10, _camZMax = 10;
        private double _targetXMin = -10, _targetXMax = 10;
        private double _targetYMin = -10, _targetYMax = 10;
        private double _targetZMin = -10, _targetZMax = 10;

        private string _camXScaleInfo = "", _camYScaleInfo = "", _camZScaleInfo = "";
        private string _targetXScaleInfo = "", _targetYScaleInfo = "", _targetZScaleInfo = "";

        private double _modelScale = 1.0;
        private double _modelHeight = 1.0;
        private double _aspectRatio = 16.0 / 9.0;
        private int _viewportWidth = 100;
        private int _viewportHeight = 100;

        private bool _isGridVisible = true;
        private bool _isInfiniteGrid = true;
        private bool _isWireframe = false;
        private bool _isSnapping = false;
        private bool _isTargetFixed = true;

        private Point _lastMousePos;
        private bool _isRotatingView;
        private bool _isPanningView;
        private bool _isDraggingTarget;
        private bool _isSpacePanning;
        private string _hoveredDirectionName = "";
        private Geometry3D? _hoveredGeometry;

        private enum GizmoMode { None, X, Y, Z, XY, YZ, ZX, View }
        private GizmoMode _currentGizmoMode = GizmoMode.None;

        private GpuResourceCacheItem? _modelResource;
        private System.Windows.Media.Color _themeColor = System.Windows.Media.Colors.White;

        private double _currentTime = 0;
        private double _maxDuration = 10.0;
        private bool _isPlaying = false;
        private DispatcherTimer _playbackTimer;
        private CameraKeyframe? _selectedKeyframe;
        private bool _isUpdatingAnimation;

        public PerspectiveCamera Camera { get; } = new PerspectiveCamera { FieldOfView = 45, NearPlaneDistance = 0.01, FarPlaneDistance = 100000 };
        public PerspectiveCamera GizmoCamera { get; } = new PerspectiveCamera { FieldOfView = 45, NearPlaneDistance = 0.1, FarPlaneDistance = 100 };

        public MeshGeometry3D CameraVisualGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D TargetVisualGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoXGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoYGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoZGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoXYGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoYZGeometry { get; } = new MeshGeometry3D();
        public MeshGeometry3D GizmoZXGeometry { get; } = new MeshGeometry3D();

        public Model3DGroup ViewCubeModel { get; private set; }
        private GeometryModel3D[] _cubeFaces;
        private GeometryModel3D[] _cubeCorners;

        public WriteableBitmap? SceneImage => _renderService.SceneImage;

        public ObservableCollection<CameraKeyframe> Keyframes { get; }

        public ActionCommand ResetCommand { get; }
        public ActionCommand UndoCommand { get; }
        public ActionCommand RedoCommand { get; }
        public ActionCommand FocusCommand { get; }
        public ActionCommand PlayCommand { get; }
        public ActionCommand PauseCommand { get; }
        public ActionCommand StopCommand { get; }
        public ActionCommand AddKeyframeCommand { get; }
        public ActionCommand RemoveKeyframeCommand { get; }

        public string HoveredDirectionName { get => _hoveredDirectionName; set => Set(ref _hoveredDirectionName, value); }
        public bool IsTargetFree => !_isTargetFixed;
        public bool IsPilotFrameVisible => _cameraLogic.IsPilotView;

        public double CamX { get => _cameraLogic.CamX; set { _cameraLogic.CamX = value; OnPropertyChanged(); UpdateRange(value, ref _camXMin, ref _camXMax, ref _camXScaleInfo, nameof(CamXMin), nameof(CamXMax), nameof(CamXScaleInfo)); if (!_isUpdatingAnimation) UpdateVisuals(); SyncToParameter(); } }
        public double CamY { get => _cameraLogic.CamY; set { _cameraLogic.CamY = value; OnPropertyChanged(); UpdateRange(value, ref _camYMin, ref _camYMax, ref _camYScaleInfo, nameof(CamYMin), nameof(CamYMax), nameof(CamYScaleInfo)); if (!_isUpdatingAnimation) UpdateVisuals(); SyncToParameter(); } }
        public double CamZ { get => _cameraLogic.CamZ; set { _cameraLogic.CamZ = value; OnPropertyChanged(); UpdateRange(value, ref _camZMin, ref _camZMax, ref _camZScaleInfo, nameof(CamZMin), nameof(CamZMax), nameof(CamZScaleInfo)); if (!_isUpdatingAnimation) UpdateVisuals(); SyncToParameter(); } }
        public double TargetX { get => _cameraLogic.TargetX; set { _cameraLogic.TargetX = value; OnPropertyChanged(); UpdateRange(value, ref _targetXMin, ref _targetXMax, ref _targetXScaleInfo, nameof(TargetXMin), nameof(TargetXMax), nameof(TargetXScaleInfo)); if (!_isUpdatingAnimation) UpdateVisuals(); SyncToParameter(); } }
        public double TargetY { get => _cameraLogic.TargetY; set { _cameraLogic.TargetY = value; OnPropertyChanged(); UpdateRange(value, ref _targetYMin, ref _targetYMax, ref _targetYScaleInfo, nameof(TargetYMin), nameof(TargetYMax), nameof(TargetYScaleInfo)); if (!_isUpdatingAnimation) UpdateVisuals(); SyncToParameter(); } }
        public double TargetZ { get => _cameraLogic.TargetZ; set { _cameraLogic.TargetZ = value; OnPropertyChanged(); UpdateRange(value, ref _targetZMin, ref _targetZMax, ref _targetZScaleInfo, nameof(TargetZMin), nameof(TargetZMax), nameof(TargetZScaleInfo)); if (!_isUpdatingAnimation) UpdateVisuals(); SyncToParameter(); } }

        public double CamXMin { get => _camXMin; set => Set(ref _camXMin, value); }
        public double CamXMax { get => _camXMax; set => Set(ref _camXMax, value); }
        public string CamXScaleInfo { get => _camXScaleInfo; set => Set(ref _camXScaleInfo, value); }
        public double CamYMin { get => _camYMin; set => Set(ref _camYMin, value); }
        public double CamYMax { get => _camYMax; set => Set(ref _camYMax, value); }
        public string CamYScaleInfo { get => _camYScaleInfo; set => Set(ref _camYScaleInfo, value); }
        public double CamZMin { get => _camZMin; set => Set(ref _camZMin, value); }
        public double CamZMax { get => _camZMax; set => Set(ref _camZMax, value); }
        public string CamZScaleInfo { get => _camZScaleInfo; set => Set(ref _camZScaleInfo, value); }
        public double TargetXMin { get => _targetXMin; set => Set(ref _targetXMin, value); }
        public double TargetXMax { get => _targetXMax; set => Set(ref _targetXMax, value); }
        public string TargetXScaleInfo { get => _targetXScaleInfo; set => Set(ref _targetXScaleInfo, value); }
        public double TargetYMin { get => _targetYMin; set => Set(ref _targetYMin, value); }
        public double TargetYMax { get => _targetYMax; set => Set(ref _targetYMax, value); }
        public string TargetYScaleInfo { get => _targetYScaleInfo; set => Set(ref _targetYScaleInfo, value); }
        public double TargetZMin { get => _targetZMin; set => Set(ref _targetZMin, value); }
        public double TargetZMax { get => _targetZMax; set => Set(ref _targetZMax, value); }
        public string TargetZScaleInfo { get => _targetZScaleInfo; set => Set(ref _targetZScaleInfo, value); }

        public bool IsGridVisible { get => _isGridVisible; set { Set(ref _isGridVisible, value); UpdateD3DScene(); } }
        public bool IsInfiniteGrid { get => _isInfiniteGrid; set { Set(ref _isInfiniteGrid, value); UpdateD3DScene(); } }
        public bool IsWireframe { get => _isWireframe; set { Set(ref _isWireframe, value); UpdateD3DScene(); } }
        public bool IsPilotView { get => _cameraLogic.IsPilotView; set { _cameraLogic.IsPilotView = value; OnPropertyChanged(); UpdateVisuals(); OnPropertyChanged(nameof(IsPilotFrameVisible)); } }
        public bool IsSnapping { get => _isSnapping; set => Set(ref _isSnapping, value); }
        public bool IsTargetFixed { get => _isTargetFixed; set { if (Set(ref _isTargetFixed, value)) { OnPropertyChanged(nameof(IsTargetFree)); UpdateVisuals(); } } }

        public double CurrentTime
        {
            get => _currentTime;
            set
            {
                if (Set(ref _currentTime, value))
                {
                    UpdateAnimation();
                }
            }
        }

        public double MaxDuration
        {
            get => _maxDuration;
            set => Set(ref _maxDuration, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (Set(ref _isPlaying, value))
                {
                    PlayCommand.RaiseCanExecuteChanged();
                    PauseCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public CameraKeyframe? SelectedKeyframe
        {
            get => _selectedKeyframe;
            set
            {
                Set(ref _selectedKeyframe, value);
                OnPropertyChanged(nameof(IsKeyframeSelected));
                OnPropertyChanged(nameof(SelectedKeyframeEasing));
                RemoveKeyframeCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsKeyframeSelected => SelectedKeyframe != null;

        public EasingType SelectedKeyframeEasing
        {
            get => SelectedKeyframe?.Easing ?? EasingType.Linear;
            set
            {
                if (SelectedKeyframe != null)
                {
                    SelectedKeyframe.Easing = value;
                    OnPropertyChanged();
                    UpdateAnimation();
                }
            }
        }

        public CameraWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;
            _loader = new ObjModelLoader();
            _renderService = new RenderService();
            _cameraLogic = new CameraLogic();
            _undoStack = new UndoStack<(double, double, double, double, double, double)>();

            _cameraLogic.CamX = _parameter.CameraX.Values[0].Value;
            _cameraLogic.CamY = _parameter.CameraY.Values[0].Value;
            _cameraLogic.CamZ = _parameter.CameraZ.Values[0].Value;
            _cameraLogic.TargetX = _parameter.TargetX.Values[0].Value;
            _cameraLogic.TargetY = _parameter.TargetY.Values[0].Value;
            _cameraLogic.TargetZ = _parameter.TargetZ.Values[0].Value;
            _cameraLogic.ViewCenterX = _cameraLogic.TargetX;
            _cameraLogic.ViewCenterY = _cameraLogic.TargetY;
            _cameraLogic.ViewCenterZ = _cameraLogic.TargetZ;

            _cameraLogic.Updated += UpdateVisuals;

            Keyframes = new ObservableCollection<CameraKeyframe>(_parameter.Keyframes);

            double sw = _parameter.ScreenWidth.Values[0].Value;
            double sh = _parameter.ScreenHeight.Values[0].Value;
            if (sh > 0) _aspectRatio = sw / sh;

            ResetCommand = new ActionCommand(_ => true, _ => ResetSceneCamera());
            UndoCommand = new ActionCommand(_ => _undoStack.CanUndo, _ => PerformUndo());
            RedoCommand = new ActionCommand(_ => _undoStack.CanRedo, _ => PerformRedo());
            FocusCommand = new ActionCommand(_ => true, _ => PerformFocus());

            PlayCommand = new ActionCommand(_ => !IsPlaying, _ => StartPlayback());
            PauseCommand = new ActionCommand(_ => IsPlaying, _ => PausePlayback());
            StopCommand = new ActionCommand(_ => true, _ => StopPlayback());
            AddKeyframeCommand = new ActionCommand(_ => true, _ => AddKeyframe());
            RemoveKeyframeCommand = new ActionCommand(_ => IsKeyframeSelected, _ => RemoveKeyframe());

            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _playbackTimer.Tick += PlaybackTick;

            _parameter.PropertyChanged += OnParameterPropertyChanged;
            MaxDuration = _parameter.Duration;
            if (MaxDuration <= 0) MaxDuration = 10.0;

            ViewCubeModel = GizmoBuilder.CreateViewCube(out _cubeFaces, out _cubeCorners);
            _renderService.Initialize();
            LoadModel();
            UpdateVisuals();
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.Duration))
            {
                MaxDuration = _parameter.Duration;
            }
        }

        private void StartPlayback()
        {
            if (_isPlaying) return;
            if (CurrentTime >= MaxDuration) CurrentTime = 0;
            IsPlaying = true;
            _playbackTimer.Start();
        }

        private void PausePlayback()
        {
            IsPlaying = false;
            _playbackTimer.Stop();
        }

        private void StopPlayback()
        {
            IsPlaying = false;
            _playbackTimer.Stop();
            CurrentTime = 0;
        }

        private void PlaybackTick(object? sender, EventArgs e)
        {
            if (!_isPlaying) return;
            double nextTime = CurrentTime + 0.016;
            if (nextTime >= MaxDuration)
            {
                CurrentTime = MaxDuration;
                PausePlayback();
            }
            else
            {
                CurrentTime = nextTime;
            }
        }

        private void AddKeyframe()
        {
            var keyframe = new CameraKeyframe
            {
                Time = CurrentTime,
                CamX = CamX,
                CamY = CamY,
                CamZ = CamZ,
                TargetX = TargetX,
                TargetY = TargetY,
                TargetZ = TargetZ,
                Easing = EasingType.Linear
            };

            var existing = Keyframes.FirstOrDefault(k => Math.Abs(k.Time - CurrentTime) < 0.001);
            if (existing != null)
            {
                Keyframes.Remove(existing);
                _parameter.Keyframes.Remove(existing);
            }

            Keyframes.Add(keyframe);
            _parameter.Keyframes.Add(keyframe);

            var sorted = Keyframes.OrderBy(k => k.Time).ToList();
            Keyframes.Clear();
            _parameter.Keyframes.Clear();
            foreach (var k in sorted)
            {
                Keyframes.Add(k);
                _parameter.Keyframes.Add(k);
            }

            SelectedKeyframe = keyframe;
        }

        private void RemoveKeyframe()
        {
            if (SelectedKeyframe != null)
            {
                Keyframes.Remove(SelectedKeyframe);
                _parameter.Keyframes.Remove(SelectedKeyframe);
                SelectedKeyframe = null;
            }
        }

        private void UpdateAnimation()
        {
            if (Keyframes.Count == 0) return;

            _isUpdatingAnimation = true;
            var state = _parameter.GetCameraState(CurrentTime);
            CamX = state.cx;
            CamY = state.cy;
            CamZ = state.cz;
            TargetX = state.tx;
            TargetY = state.ty;
            TargetZ = state.tz;
            _isUpdatingAnimation = false;
            UpdateVisuals();
        }

        public void UpdateThemeColor(System.Windows.Media.Color color)
        {
            _themeColor = color;
            UpdateD3DScene();
        }

        public void ResizeViewport(int width, int height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
            _renderService.Resize(width, height);
            OnPropertyChanged(nameof(SceneImage));
            UpdateD3DScene();
        }

        private void UpdateVisuals()
        {
            _cameraLogic.UpdateViewport(Camera, GizmoCamera, _modelHeight);
            UpdateSceneCameraVisual();
            UpdateD3DScene();
        }

        private void UpdateD3DScene()
        {
            if (_renderService.SceneImage == null) return;
            var camDir = Camera.LookDirection; camDir.Normalize();
            var camUp = Camera.UpDirection; camUp.Normalize();
            var camPos = Camera.Position;
            var target = camPos + camDir;
            var view = Matrix4x4.CreateLookAt(
                new Vector3((float)camPos.X, (float)camPos.Y, (float)camPos.Z),
                new Vector3((float)target.X, (float)target.Y, (float)target.Z),
                new Vector3((float)camUp.X, (float)camUp.Y, (float)camUp.Z));

            double fovValue = _parameter.Fov.Values[0].Value;
            if (IsPilotView && Camera.FieldOfView != fovValue) Camera.FieldOfView = fovValue;
            else if (!IsPilotView && Camera.FieldOfView != 45) Camera.FieldOfView = 45;

            float hFovRad = (float)(Camera.FieldOfView * Math.PI / 180.0);
            float aspect = (float)_viewportWidth / _viewportHeight;
            float vFovRad = 2.0f * (float)Math.Atan(Math.Tan(hFovRad / 2.0f) / aspect);
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(vFovRad, aspect, 0.1f, 10000.0f);

            bool isInteracting = _isRotatingView || _isPanningView || _isDraggingTarget || _isSpacePanning;

            _renderService.Render(
                _modelResource,
                view,
                proj,
                new Vector3((float)camPos.X, (float)camPos.Y, (float)camPos.Z),
                _themeColor,
                _isWireframe,
                _isGridVisible,
                _isInfiniteGrid,
                _modelScale,
                _modelHeight,
                _parameter,
                isInteracting);
        }

        private void UpdateSceneCameraVisual()
        {
            bool isInteracting = _currentGizmoMode != GizmoMode.None || _isRotatingView || _isPanningView;
            double yOffset = _modelHeight / 2.0;
            var camPos = new Point3D(CamX, CamY + yOffset, CamZ);
            var targetPos = new Point3D(TargetX, TargetY + yOffset, TargetZ);

            GizmoBuilder.BuildGizmos(
                GizmoXGeometry, GizmoYGeometry, GizmoZGeometry,
                GizmoXYGeometry, GizmoYZGeometry, GizmoZXGeometry,
                CameraVisualGeometry, TargetVisualGeometry,
                camPos, targetPos,
                _isTargetFixed, _modelScale, isInteracting,
                _parameter.Fov.Values[0].Value, _aspectRatio,
                IsPilotView
            );

            OnPropertyChanged(nameof(CameraVisualGeometry));
            OnPropertyChanged(nameof(TargetVisualGeometry));
            OnPropertyChanged(nameof(GizmoXGeometry)); OnPropertyChanged(nameof(GizmoYGeometry)); OnPropertyChanged(nameof(GizmoZGeometry));
            OnPropertyChanged(nameof(GizmoXYGeometry)); OnPropertyChanged(nameof(GizmoYZGeometry)); OnPropertyChanged(nameof(GizmoZXGeometry));
        }

        private unsafe void LoadModel()
        {
            var path = _parameter.FilePath;
            if (string.IsNullOrEmpty(path)) return;
            var model = _loader.Load(path);
            if (model.Vertices.Length == 0) return;

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

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (var v in model.Vertices)
            {
                double x = (v.Position.X - model.ModelCenter.X) * model.ModelScale;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                double y = (v.Position.Y - model.ModelCenter.Y) * model.ModelScale;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                double z = (v.Position.Z - model.ModelCenter.Z) * model.ModelScale;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }
            _modelScale = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            _modelHeight = maxY - minY;
            if (_modelScale < 0.1) _modelScale = 1.0;
            _cameraLogic.ViewRadius = _modelScale * 2.5;

            UpdateRange(CamX, ref _camXMin, ref _camXMax, ref _camXScaleInfo, nameof(CamXMin), nameof(CamXMax), nameof(CamXScaleInfo));
            UpdateRange(CamY, ref _camYMin, ref _camYMax, ref _camYScaleInfo, nameof(CamYMin), nameof(CamYMax), nameof(CamYScaleInfo));
            UpdateRange(CamZ, ref _camZMin, ref _camZMax, ref _camZScaleInfo, nameof(CamZMin), nameof(CamZMax), nameof(CamZScaleInfo));
            UpdateRange(TargetX, ref _targetXMin, ref _targetXMax, ref _targetXScaleInfo, nameof(TargetXMin), nameof(TargetXMax), nameof(TargetXScaleInfo));
            UpdateRange(TargetY, ref _targetYMin, ref _targetYMax, ref _targetYScaleInfo, nameof(TargetYMin), nameof(TargetYMax), nameof(TargetYScaleInfo));
            UpdateRange(TargetZ, ref _targetZMin, ref _targetZMax, ref _targetZScaleInfo, nameof(TargetZMin), nameof(TargetZMax), nameof(TargetZScaleInfo));

            UpdateVisuals();
        }

        private void ResetSceneCamera()
        {
            RecordUndo();
            CamX = 0; CamY = 0; CamZ = -_modelScale * 2.0;
            TargetX = 0; TargetY = 0; TargetZ = 0;
            _cameraLogic.ViewCenterX = 0; _cameraLogic.ViewCenterY = 0; _cameraLogic.ViewCenterZ = 0;
            _cameraLogic.ViewRadius = _modelScale * 3.0;
            _cameraLogic.ViewTheta = Math.PI / 4;
            _cameraLogic.ViewPhi = Math.PI / 4;
            _cameraLogic.AnimateView(Math.PI / 4, Math.PI / 4);
        }

        public void HandleGizmoMove(object? modelHit)
        {
            if (modelHit is GeometryModel3D gm && gm.GetValue(FrameworkElement.TagProperty) is string name) HoveredDirectionName = name;
            else HoveredDirectionName = "";
            CheckGizmoHit(modelHit);
        }

        public void HandleViewCubeClick(object? modelHit)
        {
            if (modelHit == null) return;
            int faceIdx = Array.IndexOf(_cubeFaces, modelHit);
            if (faceIdx >= 0)
            {
                if (faceIdx == 0) _cameraLogic.AnimateView(Math.PI / 2, Math.PI / 2);
                else if (faceIdx == 1) _cameraLogic.AnimateView(-Math.PI / 2, Math.PI / 2);
                else if (faceIdx == 2) _cameraLogic.AnimateView(0, 0.01);
                else if (faceIdx == 3) _cameraLogic.AnimateView(0, Math.PI - 0.01);
                else if (faceIdx == 4) _cameraLogic.AnimateView(0, Math.PI / 2);
                else if (faceIdx == 5) _cameraLogic.AnimateView(Math.PI, Math.PI / 2);
                return;
            }
            int cornerIdx = Array.IndexOf(_cubeCorners, modelHit);
            if (cornerIdx >= 0)
            {
                if (cornerIdx == 0) _cameraLogic.AnimateView(Math.PI / 4, 0.955);
                else if (cornerIdx == 1) _cameraLogic.AnimateView(-Math.PI / 4, 0.955);
                else if (cornerIdx == 2) _cameraLogic.AnimateView(3 * Math.PI / 4, 0.955);
                else if (cornerIdx == 3) _cameraLogic.AnimateView(-3 * Math.PI / 4, 0.955);
                else if (cornerIdx == 4) _cameraLogic.AnimateView(Math.PI / 4, 2.186);
                else if (cornerIdx == 5) _cameraLogic.AnimateView(-Math.PI / 4, 2.186);
                else if (cornerIdx == 6) _cameraLogic.AnimateView(3 * Math.PI / 4, 2.186);
                else if (cornerIdx == 7) _cameraLogic.AnimateView(-3 * Math.PI / 4, 2.186);
            }
        }

        private void UpdateRange(double value, ref double min, ref double max, ref string scaleInfo, string minProp, string maxProp, string infoProp)
        {
            double abs = Math.Abs(value);
            double targetMax = 10;
            if (abs >= 50) targetMax = 100;
            else if (abs >= 10) targetMax = 50;
            if (Math.Abs(max - targetMax) > 0.001)
            {
                max = targetMax; min = -targetMax;
                if (targetMax > 10) scaleInfo = $"x{targetMax / 10:0}"; else scaleInfo = "";
                OnPropertyChanged(minProp); OnPropertyChanged(maxProp); OnPropertyChanged(infoProp);
            }
        }

        private void SyncToParameter()
        {
            _parameter.CameraX.CopyFrom(new Animation(CamX, -100000, 100000));
            _parameter.CameraY.CopyFrom(new Animation(CamY, -100000, 100000));
            _parameter.CameraZ.CopyFrom(new Animation(CamZ, -100000, 100000));
            _parameter.TargetX.CopyFrom(new Animation(TargetX, -100000, 100000));
            _parameter.TargetY.CopyFrom(new Animation(TargetY, -100000, 100000));
            _parameter.TargetZ.CopyFrom(new Animation(TargetZ, -100000, 100000));
        }

        public void RecordUndo()
        {
            _undoStack.Push((CamX, CamY, CamZ, TargetX, TargetY, TargetZ));
            UndoCommand.RaiseCanExecuteChanged(); RedoCommand.RaiseCanExecuteChanged();
        }

        public void PerformUndo()
        {
            if (_undoStack.TryUndo((CamX, CamY, CamZ, TargetX, TargetY, TargetZ), out var s))
            {
                CamX = s.cx; CamY = s.cy; CamZ = s.cz;
                TargetX = s.tx; TargetY = s.ty; TargetZ = s.tz;
                SyncToParameter(); UpdateVisuals();
                UndoCommand.RaiseCanExecuteChanged(); RedoCommand.RaiseCanExecuteChanged();
            }
        }

        public void PerformRedo()
        {
            if (_undoStack.TryRedo((CamX, CamY, CamZ, TargetX, TargetY, TargetZ), out var s))
            {
                CamX = s.cx; CamY = s.cy; CamZ = s.cz;
                TargetX = s.tx; TargetY = s.ty; TargetZ = s.tz;
                SyncToParameter(); UpdateVisuals();
                UndoCommand.RaiseCanExecuteChanged(); RedoCommand.RaiseCanExecuteChanged();
            }
        }

        public void PerformFocus()
        {
            if (IsPilotView) return;
            _cameraLogic.ViewRadius = _modelScale * 2.0;
            _cameraLogic.ViewCenterX = TargetX;
            _cameraLogic.ViewCenterY = TargetY;
            _cameraLogic.ViewCenterZ = TargetZ;
            UpdateVisuals();
        }

        public void Zoom(int delta)
        {
            if (IsPlaying) PausePlayback();
            if (IsPilotView)
            {
                double speed = _modelScale * 0.1 * (delta > 0 ? 1 : -1);
                var dir = Camera.LookDirection; dir.Normalize();
                CamX += dir.X * speed; CamY += dir.Y * speed; CamZ += dir.Z * speed;
                UpdateVisuals(); SyncToParameter();
            }
            else
            {
                _cameraLogic.ViewRadius -= delta * (_modelScale * 0.005);
                if (_cameraLogic.ViewRadius < _modelScale * 0.01) _cameraLogic.ViewRadius = _modelScale * 0.01;
                UpdateVisuals();
            }
        }

        public void StartPan(Point pos)
        {
            if (IsPlaying) PausePlayback();
            IsTargetFixed = false;
            _isPanningView = true;
            _lastMousePos = pos;
        }

        public void StartRotate(Point pos)
        {
            if (IsPlaying) PausePlayback();
            _isRotatingView = true;
            _lastMousePos = pos;
        }

        public void CheckGizmoHit(object? modelHit)
        {
            _currentGizmoMode = GizmoMode.None;
            _hoveredGeometry = null;
            if (modelHit is GeometryModel3D gm)
            {
                _hoveredGeometry = gm.Geometry;
                if (gm.Geometry == GizmoXGeometry) _currentGizmoMode = GizmoMode.X;
                else if (gm.Geometry == GizmoYGeometry) _currentGizmoMode = GizmoMode.Y;
                else if (gm.Geometry == GizmoZGeometry) _currentGizmoMode = GizmoMode.Z;
                else if (gm.Geometry == GizmoXYGeometry) _currentGizmoMode = GizmoMode.XY;
                else if (gm.Geometry == GizmoYZGeometry) _currentGizmoMode = GizmoMode.YZ;
                else if (gm.Geometry == GizmoZXGeometry) _currentGizmoMode = GizmoMode.ZX;
                else if (gm.Geometry == TargetVisualGeometry || gm.Geometry == CameraVisualGeometry) _currentGizmoMode = GizmoMode.View;
            }
        }

        public void StartGizmoDrag(Point pos)
        {
            if (IsPlaying) PausePlayback();
            RecordUndo();
            if (_currentGizmoMode == GizmoMode.View)
            {
                if (_hoveredGeometry == CameraVisualGeometry) IsTargetFixed = true;
                else if (_hoveredGeometry == TargetVisualGeometry) IsTargetFixed = false;
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
            SyncToParameter();
            UpdateVisuals();
        }

        public void ScrubValue(string axis, double delta)
        {
            if (IsPlaying) PausePlayback();
            RecordUndo();
            double val = delta * _modelScale * 0.01;
            if (axis == "X") CamX += val;
            else if (axis == "Y") CamY += val;
            else if (axis == "Z") CamZ += val;
            UpdateVisuals();
        }

        public void Move(Point pos)
        {
            var dx = pos.X - _lastMousePos.X;
            var dy = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            if (_isDraggingTarget && _currentGizmoMode != GizmoMode.None)
            {
                double yOffset = _modelHeight / 2.0;
                Point3D objPos;
                if (_isTargetFixed) objPos = new Point3D(CamX, CamY + yOffset, CamZ);
                else objPos = new Point3D(TargetX, TargetY + yOffset, TargetZ);

                double dist = (Camera.Position - objPos).Length;
                if (dist < 0.001) dist = 0.001;
                double fovRad = Camera.FieldOfView * Math.PI / 180.0;
                double speed = (2.0 * dist * Math.Tan(fovRad / 2.0)) / _viewportHeight;

                double mx = 0, my = 0, mz = 0;
                var camDir = Camera.LookDirection; camDir.Normalize();
                var camRight = Vector3D.CrossProduct(camDir, Camera.UpDirection); camRight.Normalize();
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

                if (IsSnapping) { mx = Math.Round(mx / 0.5) * 0.5; my = Math.Round(my / 0.5) * 0.5; mz = Math.Round(mz / 0.5) * 0.5; mz = Math.Round(mz / 0.5) * 0.5; }
                if (_isTargetFixed) { CamX += mx; CamY += my; CamZ += mz; }
                else { TargetX += mx; TargetY += my; TargetZ += mz; }
                UpdateVisuals();
            }
            else if (_isSpacePanning)
            {
                double dist = _cameraLogic.ViewRadius;
                double fovRad = Camera.FieldOfView * Math.PI / 180.0;
                double panSpeed = (2.0 * dist * Math.Tan(fovRad / 2.0)) / _viewportHeight;
                var look = Camera.LookDirection; look.Normalize();
                var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
                var up = Vector3D.CrossProduct(right, look); up.Normalize();
                var move = (-right * dx * panSpeed) + (up * dy * panSpeed);
                _cameraLogic.ViewCenterX += move.X; _cameraLogic.ViewCenterY += move.Y; _cameraLogic.ViewCenterZ += move.Z;
                UpdateVisuals();
            }
            else if (_isRotatingView || _isPanningView)
            {
                if (_isPanningView)
                {
                    if (!_isTargetFixed)
                    {
                        double dist = _cameraLogic.ViewRadius;
                        double fovRad = Camera.FieldOfView * Math.PI / 180.0;
                        double panSpeed = (2.0 * dist * Math.Tan(fovRad / 2.0)) / _viewportHeight;
                        var look = Camera.LookDirection; look.Normalize();
                        var right = Vector3D.CrossProduct(look, Camera.UpDirection); right.Normalize();
                        var up = Vector3D.CrossProduct(right, look); up.Normalize();
                        var move = (-right * dx * panSpeed) + (up * dy * panSpeed);
                        TargetX += move.X; TargetY += move.Y; TargetZ += move.Z;
                        UpdateVisuals();
                    }
                }
                else
                {
                    _cameraLogic.ViewTheta += dx * 0.01;
                    _cameraLogic.ViewPhi -= dy * 0.01;
                    if (_cameraLogic.ViewPhi < 0.01) _cameraLogic.ViewPhi = 0.01;
                    if (_cameraLogic.ViewPhi > Math.PI - 0.01) _cameraLogic.ViewPhi = Math.PI - 0.01;
                    if (IsSnapping) _cameraLogic.ViewTheta = Math.Round(_cameraLogic.ViewTheta / (Math.PI / 12)) * (Math.PI / 12);
                    UpdateVisuals();
                }
            }
        }

        public void MovePilot(double fwd, double right, double up)
        {
            if (IsPlaying) PausePlayback();
            if (!IsPilotView) return;
            double speed = _modelScale * 0.05;
            var look = Camera.LookDirection; look.Normalize();
            var r = Vector3D.CrossProduct(look, Camera.UpDirection); r.Normalize();
            var u = Vector3D.CrossProduct(r, look); u.Normalize();
            var move = look * fwd * speed + r * right * speed + u * up * speed;
            CamX += move.X; CamY += move.Y; CamZ += move.Z;
            TargetX += move.X; TargetY += move.Y; TargetZ += move.Z;
            UpdateVisuals();
        }

        public void Dispose()
        {
            _renderService.Dispose();
            _modelResource?.Dispose();
            _cameraLogic.StopAnimation();
            _playbackTimer.Stop();
            _parameter.PropertyChanged -= OnParameterPropertyChanged;
        }
    }
}
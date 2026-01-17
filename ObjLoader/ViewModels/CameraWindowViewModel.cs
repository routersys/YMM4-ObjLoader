using ObjLoader.Plugin;
using ObjLoader.Services;
using ObjLoader.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class CameraWindowViewModel : Bindable, IDisposable, ICameraManipulator
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly RenderService _renderService;
        private readonly SceneService _sceneService;
        private readonly CameraLogic _cameraLogic;
        private readonly UndoStack<(double cx, double cy, double cz, double tx, double ty, double tz)> _undoStack;
        private readonly CameraAnimationManager _animationManager;
        private readonly CameraInteractionManager _interactionManager;

        private double _camXMin = -10, _camXMax = 10;
        private double _camYMin = -10, _camYMax = 10;
        private double _camZMin = -10, _camZMax = 10;
        private double _targetXMin = -10, _targetXMax = 10;
        private double _targetYMin = -10, _targetYMax = 10;
        private double _targetZMin = -10, _targetZMax = 10;

        private string _camXScaleInfo = "", _camYScaleInfo = "", _camZScaleInfo = "";
        private string _targetXScaleInfo = "", _targetYScaleInfo = "", _targetZScaleInfo = "";

        private double _aspectRatio = 16.0 / 9.0;
        private int _viewportWidth = 100;
        private int _viewportHeight = 100;

        private bool _isGridVisible = true;
        private bool _isInfiniteGrid = true;
        private bool _isWireframe = false;
        private bool _isSnapping = false;
        private bool _isTargetFixed = true;

        private System.Windows.Media.Color _themeColor = System.Windows.Media.Colors.White;

        private double _currentTime = 0;
        private double _maxDuration = 10.0;
        private CameraKeyframe? _selectedKeyframe;
        private bool _isUpdatingAnimation;

        private GeometryModel3D[] _cubeFaces;
        private GeometryModel3D[] _cubeCorners;

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

        public WriteableBitmap? SceneImage => _renderService.SceneImage;

        public ObservableCollection<CameraKeyframe> Keyframes { get; }
        public ObservableCollection<EasingData> EasingPresets => EasingManager.Presets;

        public ActionCommand ResetCommand { get; }
        public ActionCommand UndoCommand { get; }
        public ActionCommand RedoCommand { get; }
        public ActionCommand FocusCommand { get; }
        public ActionCommand PlayCommand { get; }
        public ActionCommand PauseCommand { get; }
        public ActionCommand StopCommand { get; }
        public ActionCommand AddKeyframeCommand { get; }
        public ActionCommand RemoveKeyframeCommand { get; }
        public ActionCommand SavePresetCommand { get; }
        public ActionCommand DeletePresetCommand { get; }

        public string HoveredDirectionName
        {
            get => _interactionManager.HoveredDirectionName;
            set => OnPropertyChanged();
        }

        public bool IsTargetFree => !_isTargetFixed;
        public bool IsPilotFrameVisible => _cameraLogic.IsPilotView;

        public double CamX { get => _cameraLogic.CamX; set { _cameraLogic.CamX = value; OnPropertyChanged(); UpdateRange(value, ref _camXMin, ref _camXMax, ref _camXScaleInfo, nameof(CamXMin), nameof(CamXMax), nameof(CamXScaleInfo)); if (!_isUpdatingAnimation) { UpdateVisuals(); SyncToParameter(); if (SelectedKeyframe != null) SelectedKeyframe.CamX = value; } } }
        public double CamY { get => _cameraLogic.CamY; set { _cameraLogic.CamY = value; OnPropertyChanged(); UpdateRange(value, ref _camYMin, ref _camYMax, ref _camYScaleInfo, nameof(CamYMin), nameof(CamYMax), nameof(CamYScaleInfo)); if (!_isUpdatingAnimation) { UpdateVisuals(); SyncToParameter(); if (SelectedKeyframe != null) SelectedKeyframe.CamY = value; } } }
        public double CamZ { get => _cameraLogic.CamZ; set { _cameraLogic.CamZ = value; OnPropertyChanged(); UpdateRange(value, ref _camZMin, ref _camZMax, ref _camZScaleInfo, nameof(CamZMin), nameof(CamZMax), nameof(CamZScaleInfo)); if (!_isUpdatingAnimation) { UpdateVisuals(); SyncToParameter(); if (SelectedKeyframe != null) SelectedKeyframe.CamZ = value; } } }
        public double TargetX { get => _cameraLogic.TargetX; set { _cameraLogic.TargetX = value; OnPropertyChanged(); UpdateRange(value, ref _targetXMin, ref _targetXMax, ref _targetXScaleInfo, nameof(TargetXMin), nameof(TargetXMax), nameof(TargetXScaleInfo)); if (!_isUpdatingAnimation) { UpdateVisuals(); SyncToParameter(); if (SelectedKeyframe != null) SelectedKeyframe.TargetX = value; } } }
        public double TargetY { get => _cameraLogic.TargetY; set { _cameraLogic.TargetY = value; OnPropertyChanged(); UpdateRange(value, ref _targetYMin, ref _targetYMax, ref _targetYScaleInfo, nameof(TargetYMin), nameof(TargetYMax), nameof(TargetYScaleInfo)); if (!_isUpdatingAnimation) { UpdateVisuals(); SyncToParameter(); if (SelectedKeyframe != null) SelectedKeyframe.TargetY = value; } } }
        public double TargetZ { get => _cameraLogic.TargetZ; set { _cameraLogic.TargetZ = value; OnPropertyChanged(); UpdateRange(value, ref _targetZMin, ref _targetZMax, ref _targetZScaleInfo, nameof(TargetZMin), nameof(TargetZMax), nameof(TargetZScaleInfo)); if (!_isUpdatingAnimation) { UpdateVisuals(); SyncToParameter(); if (SelectedKeyframe != null) SelectedKeyframe.TargetZ = value; } } }

        public double ViewCenterX { get => _cameraLogic.ViewCenterX; set => _cameraLogic.ViewCenterX = value; }
        public double ViewCenterY { get => _cameraLogic.ViewCenterY; set => _cameraLogic.ViewCenterY = value; }
        public double ViewCenterZ { get => _cameraLogic.ViewCenterZ; set => _cameraLogic.ViewCenterZ = value; }
        public double ViewRadius { get => _cameraLogic.ViewRadius; set => _cameraLogic.ViewRadius = value; }
        public double ViewTheta { get => _cameraLogic.ViewTheta; set => _cameraLogic.ViewTheta = value; }
        public double ViewPhi { get => _cameraLogic.ViewPhi; set => _cameraLogic.ViewPhi = value; }
        public double ModelHeight => _sceneService.ModelHeight;
        public int ViewportHeight => _viewportHeight;

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
                    if (SelectedKeyframe != null && Math.Abs(SelectedKeyframe.Time - value) > 0.001)
                    {
                        SelectedKeyframe = null;
                    }
                    UpdateAnimation();
                    PlayCommand.RaiseCanExecuteChanged();
                    PauseCommand.RaiseCanExecuteChanged();
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
            get => _animationManager.IsPlaying;
            set
            {
                if (value) _animationManager.Start();
                else _animationManager.Pause();
                OnPropertyChanged();
                PlayCommand.RaiseCanExecuteChanged();
                PauseCommand.RaiseCanExecuteChanged();
            }
        }

        public CameraKeyframe? SelectedKeyframe
        {
            get => _selectedKeyframe;
            set
            {
                Set(ref _selectedKeyframe, value);
                if (_selectedKeyframe != null)
                {
                    CurrentTime = _selectedKeyframe.Time;
                }
                OnPropertyChanged(nameof(IsKeyframeSelected));
                OnPropertyChanged(nameof(SelectedKeyframeEasing));
                AddKeyframeCommand.RaiseCanExecuteChanged();
                RemoveKeyframeCommand.RaiseCanExecuteChanged();
                SavePresetCommand.RaiseCanExecuteChanged();
                DeletePresetCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsKeyframeSelected => SelectedKeyframe != null;

        public EasingData? SelectedKeyframeEasing
        {
            get => SelectedKeyframe?.Easing;
            set
            {
                if (SelectedKeyframe != null && value != null)
                {
                    SelectedKeyframe.Easing = value.Clone();
                    OnPropertyChanged();
                    UpdateAnimation();
                }
            }
        }

        public CameraWindowViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;
            _renderService = new RenderService();
            _sceneService = new SceneService(_parameter, _renderService);
            _cameraLogic = new CameraLogic();
            _undoStack = new UndoStack<(double, double, double, double, double, double)>();
            _animationManager = new CameraAnimationManager();
            _interactionManager = new CameraInteractionManager(this);

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
            _animationManager.Tick += PlaybackTick;

            Keyframes = new ObservableCollection<CameraKeyframe>(_parameter.Keyframes);

            double sw = _parameter.ScreenWidth.Values[0].Value;
            double sh = _parameter.ScreenHeight.Values[0].Value;
            if (sh > 0) _aspectRatio = sw / sh;

            ResetCommand = new ActionCommand(_ => true, _ => ResetSceneCamera());
            UndoCommand = new ActionCommand(_ => _undoStack.CanUndo, _ => PerformUndo());
            RedoCommand = new ActionCommand(_ => _undoStack.CanRedo, _ => PerformRedo());
            FocusCommand = new ActionCommand(_ => true, _ => PerformFocus());

            PlayCommand = new ActionCommand(_ => !IsPlaying && CurrentTime < MaxDuration, _ => IsPlaying = true);
            PauseCommand = new ActionCommand(_ => IsPlaying, _ => IsPlaying = false);
            StopCommand = new ActionCommand(_ => true, _ => StopPlayback());
            AddKeyframeCommand = new ActionCommand(_ => !IsKeyframeSelected, _ => AddKeyframe());
            RemoveKeyframeCommand = new ActionCommand(_ => IsKeyframeSelected, _ => RemoveKeyframe());
            SavePresetCommand = new ActionCommand(_ => IsKeyframeSelected, _ => SavePreset());
            DeletePresetCommand = new ActionCommand(_ => IsKeyframeSelected && SelectedKeyframeEasing != null && SelectedKeyframeEasing.IsCustom, _ => DeletePreset());

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
            else if (e.PropertyName == nameof(ObjLoaderParameter.CurrentFrame))
            {

            }
        }

        private void StopPlayback()
        {
            _animationManager.Stop();
            OnPropertyChanged(nameof(IsPlaying));
            CurrentTime = 0;
            PlayCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
        }

        private void PlaybackTick(object? sender, EventArgs e)
        {
            double nextTime = CurrentTime + 0.016;
            if (nextTime >= MaxDuration)
            {
                CurrentTime = MaxDuration;
                _animationManager.Pause();
                OnPropertyChanged(nameof(IsPlaying));
                PlayCommand.RaiseCanExecuteChanged();
                PauseCommand.RaiseCanExecuteChanged();
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
                Easing = EasingManager.Presets.FirstOrDefault()?.Clone() ?? new EasingData()
            };

            var existing = Keyframes.FirstOrDefault(k => Math.Abs(k.Time - CurrentTime) < 0.001);
            if (existing != null)
            {
                Keyframes.Remove(existing);
            }

            Keyframes.Add(keyframe);

            var sorted = Keyframes.OrderBy(k => k.Time).ToList();
            Keyframes.Clear();
            foreach (var k in sorted) Keyframes.Add(k);

            SelectedKeyframe = keyframe;
            _parameter.Keyframes = new List<CameraKeyframe>(Keyframes);
        }

        private void RemoveKeyframe()
        {
            if (SelectedKeyframe != null)
            {
                Keyframes.Remove(SelectedKeyframe);
                SelectedKeyframe = null;

                if (Keyframes.Count == 0)
                {
                    CamX = 0;
                    CamY = 0;
                    CamZ = -_sceneService.ModelScale * 2.5;
                    TargetX = 0;
                    TargetY = 0;
                    TargetZ = 0;
                }

                _parameter.Keyframes = new List<CameraKeyframe>(Keyframes);

                UpdateAnimation();
                SyncToParameter();
            }
        }

        private void SavePreset()
        {
            if (SelectedKeyframeEasing == null) return;
            var dialog = new NameDialog();
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultName))
            {
                SelectedKeyframeEasing.Name = dialog.ResultName;
                EasingManager.SavePreset(SelectedKeyframeEasing);
            }
        }

        private void DeletePreset()
        {
            if (SelectedKeyframeEasing != null && SelectedKeyframeEasing.IsCustom)
            {
                EasingManager.DeletePreset(SelectedKeyframeEasing);
                SelectedKeyframeEasing = EasingManager.Presets.FirstOrDefault();
            }
        }

        private void UpdateAnimation()
        {
            if (Keyframes.Count > 0)
            {
                _isUpdatingAnimation = true;
                var state = _parameter.GetCameraState(CurrentTime);
                CamX = state.cx;
                CamY = state.cy;
                CamZ = state.cz;
                TargetX = state.tx;
                TargetY = state.ty;
                TargetZ = state.tz;
                _isUpdatingAnimation = false;
            }

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
            if (height > 0) _aspectRatio = (double)width / height;
            _renderService.Resize(width, height);
            OnPropertyChanged(nameof(SceneImage));
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            _cameraLogic.UpdateViewport(Camera, GizmoCamera, _sceneService.ModelHeight);
            UpdateSceneCameraVisual();
            UpdateD3DScene();
        }

        private void UpdateD3DScene()
        {
            bool isInteracting = !string.IsNullOrEmpty(HoveredDirectionName);
            _sceneService.Render(Camera, CurrentTime, _viewportWidth, _viewportHeight, IsPilotView, _themeColor, _isWireframe, _isGridVisible, _isInfiniteGrid, isInteracting);
        }

        private void UpdateSceneCameraVisual()
        {
            bool isInteracting = !string.IsNullOrEmpty(HoveredDirectionName);
            double yOffset = _sceneService.ModelHeight / 2.0;
            var camPos = new Point3D(CamX, CamY + yOffset, CamZ);
            var targetPos = new Point3D(TargetX, TargetY + yOffset, TargetZ);

            GizmoBuilder.BuildGizmos(
                GizmoXGeometry, GizmoYGeometry, GizmoZGeometry,
                GizmoXYGeometry, GizmoYZGeometry, GizmoZXGeometry,
                CameraVisualGeometry, TargetVisualGeometry,
                camPos, targetPos,
                _isTargetFixed, _sceneService.ModelScale, isInteracting,
                _parameter.Fov.Values[0].Value, _aspectRatio,
                IsPilotView
            );

            OnPropertyChanged(nameof(CameraVisualGeometry));
            OnPropertyChanged(nameof(TargetVisualGeometry));
            OnPropertyChanged(nameof(GizmoXGeometry)); OnPropertyChanged(nameof(GizmoYGeometry)); OnPropertyChanged(nameof(GizmoZGeometry));
            OnPropertyChanged(nameof(GizmoXYGeometry)); OnPropertyChanged(nameof(GizmoYZGeometry)); OnPropertyChanged(nameof(GizmoZXGeometry));
        }

        private void LoadModel()
        {
            _sceneService.LoadModel();
            _cameraLogic.ViewRadius = _sceneService.ModelScale * 2.5;

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
            CamX = 0; CamY = 0; CamZ = -_sceneService.ModelScale * 2.0;
            TargetX = 0; TargetY = 0; TargetZ = 0;
            _cameraLogic.ViewCenterX = 0; _cameraLogic.ViewCenterY = 0; _cameraLogic.ViewCenterZ = 0;
            _cameraLogic.ViewRadius = _sceneService.ModelScale * 3.0;
            _cameraLogic.ViewTheta = Math.PI / 4;
            _cameraLogic.ViewPhi = Math.PI / 4;
            _cameraLogic.AnimateView(Math.PI / 4, Math.PI / 4);
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

        public void SyncToParameter()
        {
            _parameter.SetCameraValues(CamX, CamY, CamZ, TargetX, TargetY, TargetZ);
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
            _cameraLogic.ViewRadius = _sceneService.ModelScale * 2.0;
            _cameraLogic.ViewCenterX = TargetX;
            _cameraLogic.ViewCenterY = TargetY;
            _cameraLogic.ViewCenterZ = TargetZ;
            UpdateVisuals();
        }

        public void AnimateView(double theta, double phi)
        {
            _cameraLogic.AnimateView(theta, phi);
        }

        public void Zoom(int delta)
        {
            _interactionManager.Zoom(delta, IsPilotView, _sceneService.ModelScale);
        }

        public void StartPan(Point pos)
        {
            _interactionManager.StartPan(pos);
        }

        public void StartRotate(Point pos)
        {
            _interactionManager.StartRotate(pos);
        }

        public void HandleGizmoMove(object? modelHit)
        {
            _interactionManager.HandleGizmoMove(modelHit, GizmoXGeometry, GizmoYGeometry, GizmoZGeometry, GizmoXYGeometry, GizmoYZGeometry, GizmoZXGeometry, CameraVisualGeometry, TargetVisualGeometry);
            OnPropertyChanged(nameof(HoveredDirectionName));
        }

        public void CheckGizmoHit(object? modelHit)
        {
            HandleGizmoMove(modelHit);
        }

        public void HandleViewCubeClick(object? modelHit)
        {
            _interactionManager.HandleViewCubeClick(modelHit, _cubeFaces, _cubeCorners);
        }

        public void StartGizmoDrag(Point pos)
        {
            _interactionManager.StartGizmoDrag(pos, CameraVisualGeometry, TargetVisualGeometry);
        }

        public void EndDrag()
        {
            _interactionManager.EndDrag();
        }

        public void ScrubValue(string axis, double delta)
        {
            _interactionManager.ScrubValue(axis, delta, _sceneService.ModelScale);
        }

        public void Move(Point pos)
        {
            _interactionManager.Move(pos);
        }

        public void MovePilot(double fwd, double right, double up)
        {
            _interactionManager.MovePilot(fwd, right, up, IsPilotView, _sceneService.ModelScale);
        }

        public void Dispose()
        {
            _renderService.Dispose();
            _sceneService.Dispose();
            _cameraLogic.StopAnimation();
            _animationManager.Dispose();
            _parameter.PropertyChanged -= OnParameterPropertyChanged;
        }
    }
}
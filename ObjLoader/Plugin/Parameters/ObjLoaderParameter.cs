using ObjLoader.Attributes;
using ObjLoader.Core.Enums;
using ObjLoader.Core.Timeline;
using ObjLoader.Localization;
using ObjLoader.Plugin.CameraAnimation;
using ObjLoader.Rendering.Core;
using ObjLoader.Services.Camera;
using ObjLoader.Services.Layers;
using ObjLoader.Services.Rendering;
using ObjLoader.Settings;
using ObjLoader.Utilities;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace ObjLoader.Plugin
{
    public class ObjLoaderParameter : ShapeParameterBase
    {
        private readonly CameraService _cameraService = new CameraService();
        private readonly ShaderService _shaderService = new ShaderService();
        private readonly ILayerManager _layerManager = new LayerManager();

        private int _updateSuspendCount = 0;
        private Action? _forceUpdateAction;
        private readonly StringBuilder _layerIdBuilder = new StringBuilder();
        private bool _isEnsuringLayers = false;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Setting), Description = nameof(Texts.Setting_Desc), ResourceType = typeof(Texts))]
        [ItemSettingButton(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        [IgnoreDataMember]
        public ObjLoaderParameter Self => this;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.File), Description = nameof(Texts.File_Desc), ResourceType = typeof(Texts))]
        [ModelFileSelector(
            nameof(Texts.Filter_3DModelFiles),
            ".obj", ".pmx", ".pmd", ".stl", ".glb", ".gltf", ".ply", ".3mf", ".dae", ".fbx", ".x", ".3ds", ".dxf", ".ifc", ".lwo", ".lws", ".lxo", ".ac", ".ms3d", ".cob", ".scn", ".bvh", ".mdl", ".md2", ".md3", ".pk3", ".mdc", ".md5mesh", ".smd", ".vta", ".ogex", ".3d", ".b3d", ".q3d", ".q3s", ".nff", ".off", ".raw", ".ter", ".hmp", ".ndo", ".xgl", ".zgl", ".xml", ".ase",
            nameof(Texts.Filter_BlenderDeprecated),
            ".blend"
            )]
        public string FilePath
        {
            get;
            set
            {
                var sanitized = SanitizeModelPath(value);
                if (Set(ref field, sanitized))
                {
                    SyncActiveLayer();
                    EnsureLayers();

                    UpdateLayerSignature();
                    ForceUpdate();
                    OnPropertyChanged(nameof(Layers));
                }
            }
        } = string.Empty;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Shader), Description = nameof(Texts.Shader_Desc), ResourceType = typeof(Texts))]
        [ShaderFileSelector(nameof(Texts.Filter_ShaderFiles), ".hlsl", ".fx", ".shader", ".cg", ".glsl", ".vert", ".frag", ".txt")]
        public string ShaderFilePath
        {
            get;
            set
            {
                var sanitized = SanitizeShaderPath(value);
                Set(ref field, sanitized);
            }
        } = string.Empty;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.BaseColor), Description = nameof(Texts.BaseColor_Desc), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color BaseColor { get; set => Set(ref field, value); } = Colors.White;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Projection), Description = nameof(Texts.Projection_Desc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ProjectionType Projection { get; set => Set(ref field, value); } = ProjectionType.Parallel;

        [Display(GroupName = nameof(Texts.Group_Display), Name = nameof(Texts.ScreenWidth), Description = nameof(Texts.ScreenWidth_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 4096)]
        public Animation ScreenWidth { get; } = new Animation(1920, 1, 8192);

        [Display(GroupName = nameof(Texts.Group_Display), Name = nameof(Texts.ScreenHeight), Description = nameof(Texts.ScreenHeight_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 4096)]
        public Animation ScreenHeight { get; } = new Animation(1080, 1, 8192);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.X), Description = nameof(Texts.X_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Y), Description = nameof(Texts.Y_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Z), Description = nameof(Texts.Z_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Fov), Description = nameof(Texts.Fov_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "°", 1, 179)]
        public Animation Fov { get; } = new Animation(45, 1, 179);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Scale), Description = nameof(Texts.Scale_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 5000)]
        public Animation Scale { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationX), Description = nameof(Texts.RotationX_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationX { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationY), Description = nameof(Texts.RotationY_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationY { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationZ), Description = nameof(Texts.RotationZ_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationZ { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.ResetTrigger), Description = nameof(Texts.ResetTrigger_Desc), ResourceType = typeof(Texts))]
        [Reset3DTransformButton]
        public bool ResetTrigger { get; set => Set(ref field, value); }

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.OpenCameraWindow), Description = nameof(Texts.OpenCameraWindow_Desc), ResourceType = typeof(Texts))]
        [CameraWindowButton]
        public bool IsCameraWindowOpen2 { get; set => Set(ref field, value); }

        public Animation CameraX { get; } = new Animation(0, -100000, 100000);
        public Animation CameraY { get; } = new Animation(0, -100000, 100000);
        public Animation CameraZ { get; } = new Animation(-2.5, -100000, 100000);
        public Animation TargetX { get; } = new Animation(0, -100000, 100000);
        public Animation TargetY { get; } = new Animation(0, -100000, 100000);
        public Animation TargetZ { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.WorldId), Description = nameof(Texts.WorldId_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 0, 19)]
        public Animation WorldId { get; } = new Animation(0, 0, 19);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.IsLightEnabled), Description = nameof(Texts.IsLightEnabled_Desc), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsLightEnabled { get; set => Set(ref field, value); } = false;

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightType), Description = nameof(Texts.LightType_Desc), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public LightType LightType { get; set => Set(ref field, value); } = LightType.Point;

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightX), Description = nameof(Texts.LightX_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation LightX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightY), Description = nameof(Texts.LightY_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation LightY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightZ), Description = nameof(Texts.LightZ_Desc), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation LightZ { get; } = new Animation(-100, -100000, 100000);

        [Display(AutoGenerateField = false)]
        public List<CameraKeyframe> Keyframes
        {
            get;
            set => Set(ref field, value);
        } = new List<CameraKeyframe>();

        public double Duration { get; set => Set(ref field, value); } = 10.0;

        [Display(AutoGenerateField = false)]
        public Animation SettingsVersion { get; } = new Animation(0, 0, 100000000);
        private int _versionCounter = 0;

        [Display(AutoGenerateField = false)]
        public string InstanceId { get; set => Set(ref field, value); } = Guid.NewGuid().ToString("D");

        [Display(AutoGenerateField = false)]
        [IgnoreDataMember]
        public double CurrentFrame
        {
            get;
            set => Set(ref field, value);
        }

        [Display(AutoGenerateField = false)]
        [IgnoreDataMember]
        public int CurrentFPS
        {
            get;
            set => Set(ref field, value);
        } = 60;

        [Display(AutoGenerateField = false)]
        public ObservableCollection<LayerData> Layers => _layerManager.Layers;

        [IgnoreDataMember]
        public bool IsSwitchingLayer => _layerManager.IsSwitchingLayer;

        [Display(AutoGenerateField = false)]
        public int SelectedLayerIndex
        {
            get => _layerManager.SelectedLayerIndex;
            set
            {
                if (_layerManager.SelectedLayerIndex == value) return;
                _layerManager.ChangeLayer(value, this);
                OnPropertyChanged(nameof(SelectedLayerIndex));
            }
        }

        public string LayerIds
        {
            get;
            set
            {
                if (Set(ref field, value))
                {
                    EnsureLayers();
                }
            }
        } = string.Empty;

        public string ActiveLayerGuid { get; set => Set(ref field, value); } = string.Empty;

        public ObjLoaderParameter() : this(null) { }
        public ObjLoaderParameter(SharedDataStore? sharedData) : base(sharedData)
        {
            Layers.CollectionChanged += Layers_CollectionChanged;

            if (sharedData == null)
            {
                BaseColor = Colors.White;
                Projection = ProjectionType.Parallel;
                IsLightEnabled = false;

                _layerManager.Initialize(this);
                UpdateLayerSignature();
            }
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(PluginSettings.Instance, nameof(INotifyPropertyChanged.PropertyChanged), OnPluginSettingsChanged);
        }

        public void BeginUpdate()
        {
            _updateSuspendCount++;
        }

        public void EndUpdate()
        {
            _updateSuspendCount--;
            if (_updateSuspendCount <= 0)
            {
                _updateSuspendCount = 0;
                UpdateLayerSignature();
                ForceUpdate();
            }
        }

        public IDisposable SuspendUpdate(Action? asyncFinalizer = null)
        {
            return new UpdateSuspender(this, asyncFinalizer);
        }

        private readonly struct UpdateSuspender : IDisposable
        {
            private readonly ObjLoaderParameter _parameter;
            private readonly Action? _asyncFinalizer;

            public UpdateSuspender(ObjLoaderParameter parameter, Action? asyncFinalizer)
            {
                _parameter = parameter;
                _asyncFinalizer = asyncFinalizer;
                _parameter.BeginUpdate();
            }

            public void Dispose()
            {
                if (_asyncFinalizer != null)
                {
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher != null)
                    {
                        var p = _parameter;
                        var a = _asyncFinalizer;
                        dispatcher.InvokeAsync(() =>
                        {
                            try { a(); }
                            finally { p.EndUpdate(); }
                        }, DispatcherPriority.ContextIdle);
                        return;
                    }

                    try { _asyncFinalizer(); }
                    finally { _parameter.EndUpdate(); }
                }
                else
                {
                    _parameter.EndUpdate();
                }
            }
        }

        private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateLayerSignature();
            ForceUpdate();
        }

        public void EnsureLayers()
        {
            if (_isEnsuringLayers) return;

            _isEnsuringLayers = true;
            try
            {
                void Action()
                {
                    int count = Layers.Count;
                    _layerManager.EnsureLayers(this);

                    if (Layers.Count != count)
                    {
                        UpdateLayerSignature();
                        OnPropertyChanged(nameof(Layers));
                        BumpVersion();
                        OnPropertyChanged(string.Empty);
                    }
                }

                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.InvokeAsync(Action, DispatcherPriority.Normal);
                }
                else
                {
                    Action();
                }
            }
            finally
            {
                _isEnsuringLayers = false;
            }
        }

        public void SyncActiveLayer()
        {
            if (IsSwitchingLayer) return;
            _layerManager.SaveActiveLayer(this);
        }

        public void ForceUpdate()
        {
            if (_updateSuspendCount > 0) return;

            if (Application.Current?.Dispatcher != null)
            {
                _forceUpdateAction ??= ForceUpdateCore;
                Application.Current.Dispatcher.InvokeAsync(_forceUpdateAction);
            }
        }

        private void ForceUpdateCore()
        {
            BumpVersion();
            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(Layers));
        }

        private void BumpVersion()
        {
            _versionCounter++;
            var values = SettingsVersion.Values;
            if (values != null && values.Count > 0)
            {
                values[0].Value = _versionCounter;
            }
            else
            {
                SettingsVersion.CopyFrom(new Animation(_versionCounter, 0, 100000000));
            }
        }

        public void UpdateLayerSignature()
        {
            if (_updateSuspendCount > 0) return;

            if (Layers != null)
            {
                _layerIdBuilder.Clear();
                for (int i = 0; i < Layers.Count; i++)
                {
                    if (i > 0) _layerIdBuilder.Append(',');
                    _layerIdBuilder.Append(Layers[i].Guid);
                }
                var newIds = _layerIdBuilder.ToString();
                if (LayerIds != newIds)
                {
                    LayerIds = newIds;
                    OnPropertyChanged(nameof(LayerIds));
                }
            }
        }

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new ObjLoaderSource(devices, this);
        }

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc)
        {
            return Enumerable.Empty<string>();
        }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters)
        {
            return Enumerable.Empty<string>();
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            var animatables = new List<IAnimatable>
            {
                ScreenWidth, ScreenHeight, X, Y, Z, Scale, RotationX, RotationY, RotationZ, Fov, LightX, LightY, LightZ, WorldId, CameraX, CameraY, CameraZ, TargetX, TargetY, TargetZ, SettingsVersion
            };

            if (Layers != null)
            {
                foreach (var layer in Layers)
                {
                    animatables.Add(layer.X);
                    animatables.Add(layer.Y);
                    animatables.Add(layer.Z);
                    animatables.Add(layer.Scale);
                    animatables.Add(layer.RotationX);
                    animatables.Add(layer.RotationY);
                    animatables.Add(layer.RotationZ);
                    animatables.Add(layer.Fov);
                    animatables.Add(layer.LightX);
                    animatables.Add(layer.LightY);
                    animatables.Add(layer.LightZ);
                    animatables.Add(layer.WorldId);
                }
            }

            return animatables;
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            using var suspender = SuspendUpdate(() =>
            {
                if (Layers.Count == 0) return;

                var targetLayer = Layers.FirstOrDefault(l => l.Guid == ActiveLayerGuid);
                var targetIndex = targetLayer != null ? Layers.IndexOf(targetLayer) : 0;

                SelectedLayerIndex = -1;
                SelectedLayerIndex = targetIndex;

                OnPropertyChanged(nameof(SelectedLayerIndex));
                ForceUpdate();
            });

            var data = store.Load<ObjLoaderParameterSharedData>();
            if (data != null)
            {
                data.CopyTo(this);

                if (data.Layers != null)
                {
                    var layers = data.Layers.Select(l =>
                    {
                        var clone = l.Clone();
                        clone.Name = l.Name;
                        clone.Guid = l.Guid;
                        return clone;
                    }).ToList();
                    _layerManager.LoadSharedData(layers);
                }
                _layerManager.Initialize(this);
            }

            EnsureLayers();
            UpdateLayerSignature();
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            if (!IsSwitchingLayer)
            {
                _layerManager.SaveActiveLayer(this);
            }
            UpdateLayerSignature();
            var data = new ObjLoaderParameterSharedData(this);
            data.Layers = new List<LayerData>(Layers);
            store.Save(data);
        }

        public (double cx, double cy, double cz, double tx, double ty, double tz) GetCameraState(double time)
        {
            return _cameraService.CalculateCameraState(Keyframes, time);
        }

        public void SetCameraValues(double cx, double cy, double cz, double tx, double ty, double tz)
        {
            CameraX.CopyFrom(new Animation(cx, -100000, 100000));
            CameraY.CopyFrom(new Animation(cy, -100000, 100000));
            CameraZ.CopyFrom(new Animation(cz, -100000, 100000));
            TargetX.CopyFrom(new Animation(tx, -100000, 100000));
            TargetY.CopyFrom(new Animation(ty, -100000, 100000));
            TargetZ.CopyFrom(new Animation(tz, -100000, 100000));

            OnPropertyChanged(nameof(CameraX));
            OnPropertyChanged(nameof(CameraY));
            OnPropertyChanged(nameof(CameraZ));
            OnPropertyChanged(nameof(TargetX));
            OnPropertyChanged(nameof(TargetY));
            OnPropertyChanged(nameof(TargetZ));
        }

        public string GetAdaptedShaderSource()
        {
            return _shaderService.LoadAndAdaptShader(ShaderFilePath);
        }

        private void OnPluginSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            ForceUpdate();
        }

        private static string SanitizeModelPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var result = FileSystemSandbox.Instance.ValidatePath(value);
            if (result.IsAllowed && result.ResolvedPath != null)
            {
                return result.ResolvedPath;
            }

            var basicResult = PathValidator.Validate(value);
            if (basicResult.IsValid && basicResult.NormalizedPath != null)
            {
                return basicResult.NormalizedPath;
            }

            return string.Empty;
        }

        internal ILayerManager GetLayerManager() => _layerManager;

        private static string SanitizeShaderPath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var result = FileSystemSandbox.Instance.ValidatePath(value);
            if (result.IsAllowed && result.ResolvedPath != null)
            {
                return result.ResolvedPath;
            }

            var basicResult = PathValidator.Validate(value);
            if (basicResult.IsValid && basicResult.NormalizedPath != null)
            {
                return basicResult.NormalizedPath;
            }

            return string.Empty;
        }
    }
}
using ObjLoader.Core;
using ObjLoader.Rendering;
using ObjLoader.Attributes;
using ObjLoader.Localization;
using ObjLoader.Services;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using System.ComponentModel;
using System.Windows;
using ObjLoader.Settings;

namespace ObjLoader.Plugin
{
    public class ObjLoaderParameter : ShapeParameterBase
    {
        private readonly CameraService _cameraService = new CameraService();
        private readonly ShaderService _shaderService = new ShaderService();

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Setting), ResourceType = typeof(Texts))]
        [SettingButton(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool IsSettingWindowOpen { get => _isSettingWindowOpen; set => Set(ref _isSettingWindowOpen, value); }
        private bool _isSettingWindowOpen;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.File), Description = nameof(Texts.File_Desc), ResourceType = typeof(Texts))]
        [ModelFileSelector(
            nameof(Texts.Filter_3DModelFiles),
            ".obj", ".pmx", ".stl", ".glb", ".gltf", ".ply", ".3mf", ".dae", ".fbx", ".x", ".3ds", ".dxf", ".ifc", ".lwo", ".lws", ".lxo", ".ac", ".ms3d", ".cob", ".scn", ".bvh", ".mdl", ".md2", ".md3", ".pk3", ".mdc", ".md5mesh", ".smd", ".vta", ".ogex", ".3d", ".b3d", ".q3d", ".q3s", ".nff", ".off", ".raw", ".ter", ".hmp", ".ndo", ".xgl", ".zgl", ".xml", ".ase",
            nameof(Texts.Filter_BlenderDeprecated),
            ".blend"
            )]
        public string FilePath { get => _filePath; set => Set(ref _filePath, value); }
        private string _filePath = string.Empty;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Shader), Description = nameof(Texts.Shader_Desc), ResourceType = typeof(Texts))]
        [ShaderFileSelector(nameof(Texts.Filter_ShaderFiles), ".hlsl", ".fx", ".shader", ".cg", ".glsl", ".vert", ".frag", ".txt")]
        public string ShaderFilePath { get => _shaderFilePath; set => Set(ref _shaderFilePath, value); }
        private string _shaderFilePath = string.Empty;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.BaseColor), ResourceType = typeof(Texts))]
        [ColorPicker]
        public Color BaseColor { get => _baseColor; set => Set(ref _baseColor, value); }
        private Color _baseColor = Colors.White;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Projection), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ProjectionType Projection { get => _projection; set => Set(ref _projection, value); }
        private ProjectionType _projection = ProjectionType.Parallel;

        [Display(GroupName = nameof(Texts.Group_Display), Name = nameof(Texts.ScreenWidth), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 4096)]
        public Animation ScreenWidth { get; } = new Animation(1920, 1, 8192);

        [Display(GroupName = nameof(Texts.Group_Display), Name = nameof(Texts.ScreenHeight), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "px", 1, 4096)]
        public Animation ScreenHeight { get; } = new Animation(1080, 1, 8192);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.X), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Y), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Z), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Fov), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "°", 1, 179)]
        public Animation Fov { get; } = new Animation(45, 1, 179);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.Scale), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 5000)]
        public Animation Scale { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationX), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationX { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationY { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.RotationZ), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationZ { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.ResetTrigger), ResourceType = typeof(Texts))]
        [Reset3DTransformButton]
        public bool ResetTrigger { get => _resetTrigger; set => Set(ref _resetTrigger, value); }
        private bool _resetTrigger;

        [Display(GroupName = nameof(Texts.Group_Placement), Name = nameof(Texts.OpenCameraWindow), ResourceType = typeof(Texts))]
        [CameraWindowButton]
        public bool IsCameraWindowOpen2 { get => _isCameraWindowOpen2; set => Set(ref _isCameraWindowOpen2, value); }
        private bool _isCameraWindowOpen2;

        public Animation CameraX { get; } = new Animation(0, -100000, 100000);
        public Animation CameraY { get; } = new Animation(0, -100000, 100000);
        public Animation CameraZ { get; } = new Animation(-2.5, -100000, 100000);
        public Animation TargetX { get; } = new Animation(0, -100000, 100000);
        public Animation TargetY { get; } = new Animation(0, -100000, 100000);
        public Animation TargetZ { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.WorldId), ResourceType = typeof(Texts))]
        [AnimationSlider("F0", "", 0, 9)]
        public Animation WorldId { get; } = new Animation(0, 0, 9);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.IsLightEnabled), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsLightEnabled { get => _isLightEnabled; set => Set(ref _isLightEnabled, value); }
        private bool _isLightEnabled = false;

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightX), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation LightX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation LightY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Group_Light), Name = nameof(Texts.LightZ), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -1000, 1000)]
        public Animation LightZ { get; } = new Animation(-100, -100000, 100000);

        public List<CameraKeyframe> Keyframes { get; set; } = new List<CameraKeyframe>();

        public double Duration { get => _duration; set => Set(ref _duration, value); }
        private double _duration = 10.0;

        [Display(AutoGenerateField = false)]
        public Animation SettingsVersion { get; } = new Animation(0, 0, 100000000);
        private int _versionCounter = 0;

        public ObjLoaderParameter() : this(null) { }
        public ObjLoaderParameter(SharedDataStore? sharedData) : base(sharedData)
        {
            if (sharedData == null)
            {
                _baseColor = Colors.White;
                _projection = ProjectionType.Parallel;
                _isLightEnabled = false;
                Scale = new Animation(100.0, 0, 100000);
            }
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(PluginSettings.Instance, nameof(INotifyPropertyChanged.PropertyChanged), OnPluginSettingsChanged);
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
            return new[] { ScreenWidth, ScreenHeight, X, Y, Z, Scale, RotationX, RotationY, RotationZ, Fov, LightX, LightY, LightZ, WorldId, CameraX, CameraY, CameraZ, TargetX, TargetY, TargetZ, SettingsVersion };
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            var data = store.Load<ObjLoaderParameterSharedData>();
            if (data is null) return;
            data.CopyTo(this);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new ObjLoaderParameterSharedData(this));
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
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _versionCounter++;
                SettingsVersion.CopyFrom(new Animation(_versionCounter, 0, 100000000));
                OnPropertyChanged(string.Empty);
            });
        }
    }
}
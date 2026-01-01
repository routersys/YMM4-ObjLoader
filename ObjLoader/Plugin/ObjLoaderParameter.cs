using ObjLoader.Core;
using ObjLoader.Rendering;
using ObjLoader.Attributes;
using ObjLoader.Localization;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
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
        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.Setting), ResourceType = typeof(Texts))]
        [SettingButton(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool IsSettingWindowOpen { get => _isSettingWindowOpen; set => Set(ref _isSettingWindowOpen, value); }
        private bool _isSettingWindowOpen;

        [Display(GroupName = nameof(Texts.Group_Model), Name = nameof(Texts.File), Description = nameof(Texts.File_Desc), ResourceType = typeof(Texts))]
        [ModelFileSelector(".obj", ".pmx", ".stl", ".glb", ".ply", ".3mf")]
        public string FilePath { get => _filePath; set => Set(ref _filePath, value); }
        private string _filePath = string.Empty;

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
            return new[] { ScreenWidth, ScreenHeight, X, Y, Z, Scale, RotationX, RotationY, RotationZ, Fov, LightX, LightY, LightZ, WorldId };
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
    }
}
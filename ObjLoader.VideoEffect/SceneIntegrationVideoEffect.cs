using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace ObjLoader.VideoEffect
{
    [VideoEffect(nameof(Label), ["3D"], ["3D"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    public class SceneIntegrationVideoEffect : VideoEffectBase
    {
        public override string Label => Texts.Label;

        #region 変形
        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_X), Description = nameof(Texts.Desc_X), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "m", -100, 100)]
        public Animation X { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_Y), Description = nameof(Texts.Desc_Y), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "m", -100, 100)]
        public Animation Y { get; } = new Animation(0, -1000, 1000);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_Z), Description = nameof(Texts.Desc_Z), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "m", -100, 100)]
        public Animation Z { get; } = new Animation(5, -1000, 1000);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_Scale), Description = nameof(Texts.Desc_Scale), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "倍", 0, 100)]
        public Animation Scale { get; } = new Animation(1, 0, 1000);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_ScaleX), Description = nameof(Texts.Desc_ScaleX), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "倍", 0, 100)]
        public Animation ScaleX { get; } = new Animation(1, 0, 1000);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_ScaleY), Description = nameof(Texts.Desc_ScaleY), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "倍", 0, 100)]
        public Animation ScaleY { get; } = new Animation(1, 0, 1000);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_RotationX), Description = nameof(Texts.Desc_RotationX), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "度", -360, 360)]
        public Animation RotationX { get; } = new Animation(0, -3600, 3600);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_RotationY), Description = nameof(Texts.Desc_RotationY), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "度", -360, 360)]
        public Animation RotationY { get; } = new Animation(0, -3600, 3600);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_RotationZ), Description = nameof(Texts.Desc_RotationZ), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "度", -360, 360)]
        public Animation RotationZ { get; } = new Animation(0, -3600, 3600);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_Opacity), Description = nameof(Texts.Desc_Opacity), ResourceType = typeof(Texts))]
        [AnimationSlider("F2", "倍", 0, 1)]
        public Animation Opacity { get; } = new Animation(1, 0, 1);

        [Display(GroupName = nameof(Texts.Group_Transform), Name = nameof(Texts.Name_FaceCamera), Description = nameof(Texts.Desc_FaceCamera), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool FaceCamera { get => faceCamera; set => Set(ref faceCamera, value); }
        private bool faceCamera = true;
        #endregion

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new SceneIntegrationVideoEffectProcessor(this, devices);
        }

        [Display(AutoGenerateField = false)]
        public int DummyUpdateCounter { get => dummyUpdateCounter; set => Set(ref dummyUpdateCounter, value); }
        private int dummyUpdateCounter;

        private volatile bool _updatePending;

        public void TriggerUpdate()
        {
            if (_updatePending) return;
            _updatePending = true;

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _updatePending = false;
                    DummyUpdateCounter++;
                }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
            else
            {
                _updatePending = false;
            }
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [
            X, Y, Z, Scale, ScaleX, ScaleY, RotationX, RotationY, RotationZ, Opacity
        ];
    }
}
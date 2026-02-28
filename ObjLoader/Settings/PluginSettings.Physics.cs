using ObjLoader.Localization;
using ObjLoader.Attributes;

namespace ObjLoader.Settings
{
    public partial class PluginSettings
    {
        private float _physicsGravity = -98.0f;
        private int _physicsMaxSubSteps = 10;
        private int _physicsSolverIterations = 12;
        private bool _physicsGroundCollision = true;
        private float _physicsGroundY = 0f;
        private float _physicsSleepLinearThreshold = 0.08f;
        private float _physicsSleepAngularThreshold = 0.08f;
        private float _physicsSleepTimeRequired = 0.3f;
        private int _physicsMaxManifolds = 4096;
        private int _physicsParallelNarrowPhaseThreshold = 16;
        private float _physicsWarmStartScale = 0.85f;

        [SettingGroup("Physics", nameof(Texts.Group_Physics), Order = 4, Icon = "M15.2,12C15.2,13.8 13.8,15.2 12,15.2S8.8,13.8 8.8,12S10.2,8.8 12,8.8S15.2,10.2 15.2,12M22,12C22,14.1 21,16 19.4,17.3L20.8,18.7C22.8,16.9 24,14.4 24,11.6V11.4C24,8.6 22.8,6.1 20.8,4.3L19.4,5.7C21,7 22,8.9 22,11V12M12,2C14.1,2 16,3 17.3,4.6L18.7,3.2C16.9,1.2 14.4,0 11.6,0H11.4C8.6,0 6.1,1.2 4.3,3.2L5.7,4.6C7,3 8.9,2 11,2V2M2,12C2,9.9 3,8 4.6,6.7L3.2,5.3C1.2,7.1 0,9.6 0,12.4V12.6C0,15.4 1.2,17.9 3.2,19.7L4.6,18.3C3,17 2,15.1 2,13V12M12,22C9.9,22 8,21 6.7,19.4L5.3,20.8C7.1,22.8 9.6,24 12.4,24H12.6C15.4,24 17.9,22.8 19.7,20.8L18.3,19.4C17,21 15.1,22 13,22V22Z", ResourceType = typeof(Texts))]

        [RangeSetting("Physics", nameof(Texts.PhysicsGravity), -200.0, 200.0, Tick = 1.0, Description = nameof(Texts.PhysicsGravity_Desc), ResourceType = typeof(Texts))]
        public double PhysicsGravity
        {
            get => _physicsGravity;
            set { if (SetProperty(ref _physicsGravity, (float)value)) OnPropertyChanged(nameof(PhysicsGravity)); }
        }

        [IntSpinnerSetting("Physics", nameof(Texts.PhysicsMaxSubSteps), 1, 64, Description = nameof(Texts.PhysicsMaxSubSteps_Desc), ResourceType = typeof(Texts))]
        public int PhysicsMaxSubSteps
        {
            get => _physicsMaxSubSteps;
            set { if (SetProperty(ref _physicsMaxSubSteps, value)) OnPropertyChanged(nameof(PhysicsMaxSubSteps)); }
        }

        [IntSpinnerSetting("Physics", nameof(Texts.PhysicsSolverIterations), 1, 128, Description = nameof(Texts.PhysicsSolverIterations_Desc), ResourceType = typeof(Texts))]
        public int PhysicsSolverIterations
        {
            get => _physicsSolverIterations;
            set { if (SetProperty(ref _physicsSolverIterations, value)) OnPropertyChanged(nameof(PhysicsSolverIterations)); }
        }

        [BoolSetting("Physics", nameof(Texts.PhysicsGroundCollision), Description = nameof(Texts.PhysicsGroundCollision_Desc), ResourceType = typeof(Texts))]
        public bool PhysicsGroundCollision
        {
            get => _physicsGroundCollision;
            set { if (SetProperty(ref _physicsGroundCollision, value)) OnPropertyChanged(nameof(PhysicsGroundCollision)); }
        }

        [RangeSetting("Physics", nameof(Texts.PhysicsGroundY), -100.0, 100.0, Tick = 0.5, EnableBy = nameof(PhysicsGroundCollision), Description = nameof(Texts.PhysicsGroundY_Desc), ResourceType = typeof(Texts))]
        public double PhysicsGroundY
        {
            get => _physicsGroundY;
            set { if (SetProperty(ref _physicsGroundY, (float)value)) OnPropertyChanged(nameof(PhysicsGroundY)); }
        }

        [RangeSetting("Physics", nameof(Texts.PhysicsSleepLinearThreshold), 0.0, 2.0, Tick = 0.01, Description = nameof(Texts.PhysicsSleepLinearThreshold_Desc), ResourceType = typeof(Texts))]
        public double PhysicsSleepLinearThreshold
        {
            get => _physicsSleepLinearThreshold;
            set { if (SetProperty(ref _physicsSleepLinearThreshold, (float)value)) OnPropertyChanged(nameof(PhysicsSleepLinearThreshold)); }
        }

        [RangeSetting("Physics", nameof(Texts.PhysicsSleepAngularThreshold), 0.0, 2.0, Tick = 0.01, Description = nameof(Texts.PhysicsSleepAngularThreshold_Desc), ResourceType = typeof(Texts))]
        public double PhysicsSleepAngularThreshold
        {
            get => _physicsSleepAngularThreshold;
            set { if (SetProperty(ref _physicsSleepAngularThreshold, (float)value)) OnPropertyChanged(nameof(PhysicsSleepAngularThreshold)); }
        }

        [RangeSetting("Physics", nameof(Texts.PhysicsSleepTimeRequired), 0.0, 5.0, Tick = 0.1, Description = nameof(Texts.PhysicsSleepTimeRequired_Desc), ResourceType = typeof(Texts))]
        public double PhysicsSleepTimeRequired
        {
            get => _physicsSleepTimeRequired;
            set { if (SetProperty(ref _physicsSleepTimeRequired, (float)value)) OnPropertyChanged(nameof(PhysicsSleepTimeRequired)); }
        }

        [IntSpinnerSetting("Physics", nameof(Texts.PhysicsMaxManifolds), 1024, 65536, Description = nameof(Texts.PhysicsMaxManifolds_Desc), ResourceType = typeof(Texts))]
        public int PhysicsMaxManifolds
        {
            get => _physicsMaxManifolds;
            set { if (SetProperty(ref _physicsMaxManifolds, value)) OnPropertyChanged(nameof(PhysicsMaxManifolds)); }
        }

        [IntSpinnerSetting("Physics", nameof(Texts.PhysicsParallelNarrowPhaseThreshold), 1, 128, Description = nameof(Texts.PhysicsParallelNarrowPhaseThreshold_Desc), ResourceType = typeof(Texts))]
        public int PhysicsParallelNarrowPhaseThreshold
        {
            get => _physicsParallelNarrowPhaseThreshold;
            set { if (SetProperty(ref _physicsParallelNarrowPhaseThreshold, value)) OnPropertyChanged(nameof(PhysicsParallelNarrowPhaseThreshold)); }
        }

        [RangeSetting("Physics", nameof(Texts.PhysicsWarmStartScale), 0.0, 1.0, Tick = 0.01, Description = nameof(Texts.PhysicsWarmStartScale_Desc), ResourceType = typeof(Texts))]
        public double PhysicsWarmStartScale
        {
            get => _physicsWarmStartScale;
            set { if (SetProperty(ref _physicsWarmStartScale, (float)value)) OnPropertyChanged(nameof(PhysicsWarmStartScale)); }
        }
    }
}

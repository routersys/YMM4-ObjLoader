using ObjLoader.Infrastructure;
using ObjLoader.Localization;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using YukkuriMovieMaker.Plugin;

namespace ObjLoader.Settings
{
    public enum CoordinateSystem
    {
        [Display(Name = nameof(Texts.CoordinateSystem_RightHandedYUp), ResourceType = typeof(Texts))]
        RightHandedYUp,
        [Display(Name = nameof(Texts.CoordinateSystem_RightHandedZUp), ResourceType = typeof(Texts))]
        RightHandedZUp,
        [Display(Name = nameof(Texts.CoordinateSystem_LeftHandedYUp), ResourceType = typeof(Texts))]
        LeftHandedYUp,
        [Display(Name = nameof(Texts.CoordinateSystem_LeftHandedZUp), ResourceType = typeof(Texts))]
        LeftHandedZUp
    }

    public enum RenderCullMode
    {
        [Display(Name = nameof(Texts.CullMode_None), ResourceType = typeof(Texts))]
        None,
        [Display(Name = nameof(Texts.CullMode_Front), ResourceType = typeof(Texts))]
        Front,
        [Display(Name = nameof(Texts.CullMode_Back), ResourceType = typeof(Texts))]
        Back
    }

    public class PluginSettings : SettingsBase<PluginSettings>
    {
        public override string Name => Texts.PluginName;
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => false;
        public override object? SettingView => null;
        public static PluginSettings Instance => Default;

        private CoordinateSystem _coordinateSystem = CoordinateSystem.RightHandedYUp;
        private RenderCullMode _cullMode = RenderCullMode.None;

        private Color _ambientColor = Color.FromRgb(50, 50, 50);
        private Color _lightColor = Colors.White;
        private double _diffuseIntensity = 1.0;
        private double _specularIntensity = 0.5;
        private double _shininess = 20.0;

        public override void Initialize()
        {
        }

        [SettingGroup("Global", nameof(Texts.Group_Global), Order = 0, Icon = "M21,16.5C21,16.88 20.79,17.21 20.47,17.38L12.57,21.82C12.41,21.94 12.21,22 12,22C11.79,22 11.59,21.94 11.43,21.82L3.53,17.38C3.21,17.21 3,16.88 3,16.5V7.5C3,7.12 3.21,6.79 3.53,6.62L11.43,2.18C11.59,2.06 11.79,2 12,2C12.21,2 12.41,2.06 12.57,2.18L20.47,6.62C20.79,6.79 21,7.12 21,7.5V16.5M12,4.15L6.04,7.5L12,10.85L17.96,7.5L12,4.15M5,15.91L11,19.29V12.58L5,9.21V15.91M19,15.91V9.21L13,12.58V19.29L19,15.91Z", ResourceType = typeof(Texts))]
        [EnumSetting("Global", nameof(Texts.CoordinateSystem), Description = nameof(Texts.CoordinateSystem_Desc), ResourceType = typeof(Texts))]
        public CoordinateSystem CoordinateSystem
        {
            get => _coordinateSystem;
            set => SetProperty(ref _coordinateSystem, value);
        }

        [EnumSetting("Global", nameof(Texts.CullMode), Description = nameof(Texts.CullMode_Desc), ResourceType = typeof(Texts))]
        public RenderCullMode CullMode
        {
            get => _cullMode;
            set => SetProperty(ref _cullMode, value);
        }

        [SettingGroup("Lighting", nameof(Texts.Group_Lighting), Order = 1, Icon = "M12,2A7,7 0 0,0 5,9C5,11.38 6.19,13.47 8,14.74V17A1,1 0 0,0 9,18H15A1,1 0 0,0 16,17V14.74C17.81,13.47 19,11.38 19,9A7,7 0 0,0 12,2M9,21A1,1 0 0,0 10,22H14A1,1 0 0,0 15,21V20H9V21Z", ResourceType = typeof(Texts))]
        [ColorSetting("Lighting", nameof(Texts.AmbientColor), Description = nameof(Texts.AmbientColor_Desc), ResourceType = typeof(Texts))]
        public Color AmbientColor
        {
            get => _ambientColor;
            set => SetProperty(ref _ambientColor, value);
        }

        [RangeSetting("Lighting", nameof(Texts.DiffuseIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.DiffuseIntensity_Desc), ResourceType = typeof(Texts))]
        public double DiffuseIntensity
        {
            get => _diffuseIntensity;
            set => SetProperty(ref _diffuseIntensity, value);
        }

        [RangeSetting("Lighting", nameof(Texts.SpecularIntensity), 0, 5, Tick = 0.1, Description = nameof(Texts.SpecularIntensity_Desc), ResourceType = typeof(Texts))]
        public double SpecularIntensity
        {
            get => _specularIntensity;
            set => SetProperty(ref _specularIntensity, value);
        }

        [RangeSetting("Lighting", nameof(Texts.Shininess), 1, 100, Tick = 1, Description = nameof(Texts.Shininess_Desc), ResourceType = typeof(Texts))]
        public double Shininess
        {
            get => _shininess;
            set => SetProperty(ref _shininess, value);
        }

        [SettingGroup("Environment", nameof(Texts.Group_Environment), Order = 2, Icon = "M12,22C17.5,22 22,17.5 22,12C22,6.5 17.5,2 12,2C6.5,2 2,6.5 2,12C2,17.5 6.5,22 12,22M12,18L6,12L12,6L18,12L12,18Z", ResourceType = typeof(Texts))]
        [ColorSetting("Environment", nameof(Texts.LightColor), Description = nameof(Texts.LightColor_Desc), ResourceType = typeof(Texts))]
        public Color LightColor
        {
            get => _lightColor;
            set => SetProperty(ref _lightColor, value);
        }

        [SettingButton(nameof(Texts.ResetDefaults), Placement = SettingButtonPlacement.BottomLeft, Order = 0, ResourceType = typeof(Texts))]
        public void ResetDefaults()
        {
            CoordinateSystem = CoordinateSystem.RightHandedYUp;
            CullMode = RenderCullMode.None;
            AmbientColor = Color.FromRgb(50, 50, 50);
            LightColor = Colors.White;
            DiffuseIntensity = 1.0;
            SpecularIntensity = 0.5;
            Shininess = 20.0;
        }

        [SettingButton(nameof(Texts.OK), Placement = SettingButtonPlacement.BottomRight, Type = SettingButtonType.OK, Order = 100, ResourceType = typeof(Texts))]
        public void OK() { }

        [SettingButton(nameof(Texts.Cancel), Placement = SettingButtonPlacement.BottomRight, Type = SettingButtonType.Cancel, Order = 101, ResourceType = typeof(Texts))]
        public void Cancel() { }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            if (propertyName != null)
            {
                OnPropertyChanged(propertyName);
            }
            return true;
        }
    }
}
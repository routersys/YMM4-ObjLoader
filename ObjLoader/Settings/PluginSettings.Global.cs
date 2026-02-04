using ObjLoader.Infrastructure;
using ObjLoader.Localization;

namespace ObjLoader.Settings
{
    public partial class PluginSettings
    {
        private CoordinateSystem _coordinateSystem = CoordinateSystem.RightHandedYUp;
        private RenderCullMode _cullMode = RenderCullMode.None;
        private RenderQuality _renderQuality = RenderQuality.Standard;

        [SettingGroup("Global", nameof(Texts.Group_Global), Order = 0, Icon = "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,12.5A0.5,0.5 0 0,1 11.5,12A0.5,0.5 0 0,1 12,11.5A0.5,0.5 0 0,1 12.5,12A0.5,0.5 0 0,1 12,12.5M12,7.2C9.9,7.2 8.2,8.9 8.2,11C8.2,14 12,17.5 12,17.5C12,17.5 15.8,14 15.8,11C15.8,8.9 14.1,7.2 12,7.2Z", ResourceType = typeof(Texts))]
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

        [EnumSetting("Global", nameof(Texts.RenderQuality), Description = nameof(Texts.RenderQuality_Desc), ResourceType = typeof(Texts))]
        public RenderQuality RenderQuality
        {
            get => _renderQuality;
            set => SetProperty(ref _renderQuality, value);
        }
    }
}
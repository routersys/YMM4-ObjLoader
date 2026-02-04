using ObjLoader.Infrastructure;
using ObjLoader.Localization;

namespace ObjLoader.Settings
{
    public partial class PluginSettings
    {
        private bool _assimpObj = false;
        private bool _assimpGlb = false;
        private bool _assimpPly = false;
        private bool _assimpStl = false;
        private bool _assimp3mf = false;
        private bool _assimpPmx = false;

        [SettingGroup("Assimp", nameof(Texts.Group_Assimp), Order = 15, Icon = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4Z", ResourceType = typeof(Texts))]
        [BoolSetting("Assimp", nameof(Texts.Assimp_Obj), Description = nameof(Texts.Assimp_Obj_Desc), ResourceType = typeof(Texts))]
        public bool AssimpObj { get => _assimpObj; set => SetProperty(ref _assimpObj, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Glb), Description = nameof(Texts.Assimp_Glb_Desc), ResourceType = typeof(Texts))]
        public bool AssimpGlb { get => _assimpGlb; set => SetProperty(ref _assimpGlb, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Ply), Description = nameof(Texts.Assimp_Ply_Desc), ResourceType = typeof(Texts))]
        public bool AssimpPly { get => _assimpPly; set => SetProperty(ref _assimpPly, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Stl), Description = nameof(Texts.Assimp_Stl_Desc), ResourceType = typeof(Texts))]
        public bool AssimpStl { get => _assimpStl; set => SetProperty(ref _assimpStl, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_3mf), Description = nameof(Texts.Assimp_3mf_Desc), ResourceType = typeof(Texts))]
        public bool Assimp3mf { get => _assimp3mf; set => SetProperty(ref _assimp3mf, value); }

        [BoolSetting("Assimp", nameof(Texts.Assimp_Pmx), Description = nameof(Texts.Assimp_Pmx_Desc), ResourceType = typeof(Texts))]
        public bool AssimpPmx { get => _assimpPmx; set => SetProperty(ref _assimpPmx, value); }
    }
}
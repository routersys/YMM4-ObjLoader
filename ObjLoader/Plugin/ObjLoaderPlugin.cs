using ObjLoader.Localization;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace ObjLoader.Plugin
{
    public class ObjLoaderPlugin : IShapePlugin
    {
        public string Name => Texts.PluginName;

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new ObjLoaderParameter(sharedData);
        }
    }
}
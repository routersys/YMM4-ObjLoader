using Microsoft.Win32;
using System.Windows;
using ObjLoader.Localization;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using ObjLoader.Plugin.Parameters;

namespace ObjLoader.Plugin.Core
{
    public class ObjLoaderPlugin : IShapePlugin
    {
        public ObjLoaderPlugin()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                if (key == null || (int)key.GetValue("Installed", 0) != 1)
                {
                    MessageBox.Show(Texts.VCRedist_Message, Texts.VCRedist_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch
            {
            }
        }

        public string Name => Texts.PluginName;

        public bool IsExoShapeSupported => false;

        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new ObjLoaderParameter(sharedData);
        }
    }
}
using ObjLoader.Settings;
using ObjLoader.Views;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class SettingButtonViewModel : Bindable
    {
        public ActionCommand OpenSettingWindowCommand { get; }

        public SettingButtonViewModel()
        {
            OpenSettingWindowCommand = new ActionCommand(
                _ => true,
                _ => OpenSettingWindow()
            );
        }

        private void OpenSettingWindow()
        {
            var window = new SettingWindow
            {
                DataContext = new SettingWindowViewModel(PluginSettings.Instance)
            };
            window.ShowDialog();
        }
    }
}
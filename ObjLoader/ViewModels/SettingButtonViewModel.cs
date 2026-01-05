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
            var memento = PluginSettings.Instance.CreateMemento();
            var window = new SettingWindow
            {
                DataContext = new SettingWindowViewModel(PluginSettings.Instance)
            };

            if (window.ShowDialog() != true)
            {
                PluginSettings.Instance.RestoreMemento(memento);
            }
        }
    }
}
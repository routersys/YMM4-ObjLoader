using ObjLoader.Plugin.Parameters;
using ObjLoader.Views;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    public class CameraWindowButtonViewModel : Bindable
    {
        private readonly ItemProperty[] _properties;

        public ActionCommand OpenWindowCommand { get; }

        public CameraWindowButtonViewModel(ItemProperty[] properties)
        {
            _properties = properties;
            OpenWindowCommand = new ActionCommand(_ => true, _ => OpenWindow());
        }

        private void OpenWindow()
        {
            var param = _properties.FirstOrDefault()?.PropertyOwner as ObjLoaderParameter;
            if (param != null)
            {
                var vm = new CameraWindowViewModel(param);
                var win = new CameraWindow { DataContext = vm };
                win.Closed += (s, e) => vm.Dispose();
                win.Show();
            }
        }
    }
}
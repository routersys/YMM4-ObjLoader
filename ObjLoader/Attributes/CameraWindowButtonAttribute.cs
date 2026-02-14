using ObjLoader.ViewModels.Camera;
using ObjLoader.Views;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    public class CameraWindowButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new CameraWindowButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is CameraWindowButton button)
            {
                button.DataContext = new CameraWindowButtonViewModel(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is CameraWindowButton button)
            {
                button.DataContext = null;
            }
        }
    }
}
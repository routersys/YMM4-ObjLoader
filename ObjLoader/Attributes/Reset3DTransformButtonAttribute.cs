using ObjLoader.Views;
using ObjLoader.ViewModels;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    internal class Reset3DTransformButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new Reset3DTransformButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is Reset3DTransformButton button)
            {
                button.DataContext = new Reset3DTransformViewModel(itemProperties);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is Reset3DTransformButton button)
            {
                button.DataContext = null;
            }
        }
    }
}
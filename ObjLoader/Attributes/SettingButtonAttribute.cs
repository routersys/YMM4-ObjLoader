using ObjLoader.ViewModels;
using ObjLoader.Views;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    internal class SettingButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new SettingButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is SettingButton button)
            {
                button.DataContext = new SettingButtonViewModel();
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is SettingButton button)
            {
                button.DataContext = null;
            }
        }
    }
}
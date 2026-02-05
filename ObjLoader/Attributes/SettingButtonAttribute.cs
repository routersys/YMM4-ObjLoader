using ObjLoader.Plugin;
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
                var parameter = itemProperties[0].GetValue<ObjLoaderParameter>();
                if (parameter != null)
                {
                    button.DataContext = new SettingButtonViewModel(parameter);
                }
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
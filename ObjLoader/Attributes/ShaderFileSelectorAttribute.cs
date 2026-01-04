using System.Windows;
using ObjLoader.ViewModels;
using ObjLoader.Views;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    public class ShaderFileSelectorAttribute : PropertyEditorAttribute2
    {
        private readonly string[] _extensions;

        public ShaderFileSelectorAttribute(params string[] extensions)
        {
            _extensions = extensions;
        }

        public override FrameworkElement Create()
        {
            return new ShaderFileSelector();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is ShaderFileSelector selector && itemProperties.Length > 0)
            {
                selector.DataContext = new ShaderFileSelectorViewModel(itemProperties[0], _extensions);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is ShaderFileSelector selector)
            {
                selector.DataContext = null;
            }
        }
    }
}
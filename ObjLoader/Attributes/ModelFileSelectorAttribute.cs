using System.Windows;
using ObjLoader.ViewModels;
using ObjLoader.Views;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.Attributes
{
    public class ModelFileSelectorAttribute : PropertyEditorAttribute2
    {
        private readonly string[] _extensions;

        public ModelFileSelectorAttribute(params string[] extensions)
        {
            _extensions = extensions;
        }

        public override FrameworkElement Create()
        {
            return new ModelFileSelector();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is ModelFileSelector selector && itemProperties.Length > 0)
            {
                selector.DataContext = new ModelFileSelectorViewModel(itemProperties[0], _extensions);
            }
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is ModelFileSelector selector)
            {
                selector.DataContext = null;
            }
        }
    }
}